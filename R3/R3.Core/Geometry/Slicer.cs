namespace R3.Geometry
{
	using R3.Core;
	using R3.Math;
	using System.Diagnostics;
	using System.Collections.Generic;
	using System.Linq;
	using System.Numerics;

	//
	// ZZZ - It sure would be nice to clear up the implementation of SlicePolygon (to make it more clear).
	// I didn't comment things very well, and it is difficult to tell what is going on!
	//

	public static class Slicer
	{
		/// <summary>
		/// Slices a polygon by a circle with some thickness.
		/// Input circle may be a line.
		/// </summary>
		/// <remarks>The input polygon might get reversed</remarks>
		public static void SlicePolygon( Polygon p, CircleNE c, Geometry g, double thickness, out List<Polygon> output )
		{
			// Setup the two slicing circles.
			CircleNE c1 = c.Clone(), c2 = c.Clone();
			Mobius m = new Mobius();
			Vector3D pointOnCircle = c.IsLine ? c.P1 : c.Center + new Vector3D( c.Radius, 0 );

			double offset = thickness / 2;
			m.Hyperbolic2( g, c1.CenterNE, pointOnCircle, offset );
			c1.Transform( m );
			m.Hyperbolic2( g, c2.CenterNE, pointOnCircle, -offset );
			c2.Transform( m );
			
			SlicePolygonHelper( p, c1, c2, out output );
		}

		/// <summary>
		/// c should be geodesic (orthogonal to the disk boundary).
		/// (c it is really a Circle, not a CircleNE, but some low-level helpers it uses take in CircleNEs)
		/// </summary>
		public static void OffsetHyperbolicGeodesic( CircleNE c, double thickness, out Circle c1, out Circle c2 )
		{
			Geometry g = Geometry.Hyperbolic;

			Segment seg = null;
			if( c.IsLine )
			{
				Vector3D p1, p2;
				Euclidean2D.IntersectionLineCircle( c.P1, c.P2, new Circle(), out p1, out p2 );
				seg = Segment.Line( p1, p2 );
			}
			else
			{
				// Setup the two slicing circles.
				// These are cuts equidistant from the passed in geodesic.
				Vector3D closestToOrigin = H3Models.Ball.ClosestToOrigin( new Circle3D() { Center = c.Center, Radius = c.Radius, Normal = new Vector3D( 0, 0, 1 ) } );

				Vector3D p1, p2;
				Circle disk = new Circle();
				Euclidean2D.IntersectionCircleCircle( c, disk, out p1, out p2 );
				seg = Segment.Arc( p1, closestToOrigin, p2 );
			}

			c1 = H3Models.Ball.EquidistantOffset( g, seg, thickness / 2 );
			c2 = H3Models.Ball.EquidistantOffset( g, seg, -thickness / 2 );
		}

		/// <summary>
		/// Slicing function used for systolic puzzles.
		/// c should be geodesic (orthogonal to the disk boundary).
		/// (c it is really a Circle, not a CircleNE, but some low-level helpers it uses take in CircleNEs)
		/// </summary>
		public static void SlicePolygonWithHyperbolicGeodesic( Polygon p, CircleNE c, double thickness, out List<Polygon> output )
		{
			OffsetHyperbolicGeodesic( c, thickness, out Circle c1, out Circle c2 );

			CircleNE c1NE = c.Clone(), c2NE = c.Clone();
			c1NE.Center = c1.Center; c2NE.Center = c2.Center;
			c1NE.Radius = c1.Radius; c2NE.Radius = c2.Radius;
			SlicePolygonHelper( p, c1NE, c2NE, out output );
		}

		private static void SlicePolygonHelper( Polygon p, CircleNE c1, CircleNE c2, out List<Polygon> output )
		{
			output = new List<Polygon>();

			// Slice it up.
			List<Polygon> sliced1, sliced2;
			Slicer.SlicePolygon( p, c1, out sliced1 );
			Slicer.SlicePolygon( p, c2, out sliced2 );

			// Keep the ones we want.
			foreach( Polygon newPoly in sliced1 )
			{
				bool outside = !c1.IsPointInsideNE( newPoly.CentroidApprox );
				if( outside )
					output.Add( newPoly );
			}

			foreach( Polygon newPoly in sliced2 )
			{
				bool inside = c2.IsPointInsideNE( newPoly.CentroidApprox );
				if( inside )
					output.Add( newPoly );
			}
		}

		/// <summary>
		/// Slices up a polygon with a circle (or line).
		/// </summary>
		/// <remarks>The input polygon might get reversed</remarks>
		public static bool SlicePolygon( Polygon p, Circle c, out List<Polygon> output )
		{
			return SlicePolygonInternal( p, c, out output );
		}

		/// <summary>
		/// Clip the drawn polygons in a set of tiles, using a circle.
		/// </summary>
		public static void Clip( ref List<Tile> tiles, Circle c, bool keepInside )
		{
			List<Tile> newTiles = new List<Tile>();

			foreach( Tile t in tiles )
			{
				List<Polygon> sliced;
				Slicer.SlicePolygon( t.Drawn, c, out sliced );

				foreach( Polygon p in sliced )
				{
					bool insideCircle = (p.CentroidApprox - c.Center).Abs() < c.Radius;
					if( ( keepInside && insideCircle ) || ( !keepInside && !insideCircle ) )
						newTiles.Add( new Tile( t.Boundary, p, t.Geometry ) );
				}
			}

			tiles = newTiles;
		}

		private class IntersectionPoint
		{
			public Vector3D Location;

			/// <summary>
			/// Index in the sliced polygon. The location will be at the start of the segment with this index.
			/// </summary>
			public int Index;
		}

		private static bool SlicePolygonInternal( Polygon p, Circle c, out List<Polygon> output )
		{
			// Our approach:
			// (1) Find the intersection points, and splice them into the polygon. (splicing in is the main diff from old algorithm.)
			// (2) From each intersection point, walk the polygon.
			// (3) When you are at an intersection point, always turn left, which may involve adding a new segment of the slicing circle.
			// (4) We'll have to check for duplicate polygons in the resulting list, and remove them.

			output = new List<Polygon>();

			// We must be a digon at a minimum.
			if( p.NumSides < 2 )
				return false;

			// XXX - Code assumes well-formed polygon: closed (has connected segments), 
			//		 no repeated vertices.  Assert all this?
			// Code also assumes CCW orientation.
			if( !p.Orientation )
				p.Reverse();

			// Cycle through our segments and splice in all the intersection points.
			Polygon diced = new Polygon();
			List<IntersectionPoint> iPoints = new List<IntersectionPoint>();
			for( int i=0; i<p.NumSides; i++ )
			{
				Segment s = p.Segments[i];
				Vector3D[] intersections = c.GetIntersectionPoints( s );
				if( intersections == null )
					continue;

				switch( intersections.Length )
				{
					case 0:
					{
						diced.Segments.Add( s );
						break;
					}
					case 1:
					{
						// ZZZ - check here to see if it is a tangent iPoint?  Not sure if we need to do this.
						diced.Segments.Add( SplitHelper( s, intersections[0], diced, iPoints ) );
						break;
					}
					case 2:
					{
						// We need to ensure the intersection points are ordered correctly on the segment.
						Vector3D i1 = intersections[0], i2 = intersections[1];
						if( !s.Ordered( i1, i2 ) )
							Utils.SwapPoints( ref i1, ref i2 );

						Segment secondToSplit = SplitHelper( s, i1, diced, iPoints );
						Segment segmentToAdd = SplitHelper( secondToSplit, i2, diced, iPoints );
						diced.Segments.Add( segmentToAdd );
						break;
					}
					default:
						Debug.Assert( false );
						return false;
				}
			}

			// NOTE: We've been careful to avoid adding duplicates to iPoints.

			// Are we done? (no intersections)
			if( 0 == iPoints.Count )
			{
				output.Add( p );
				return true;
			}

			// We don't yet deal with tangengies,
			// but we're going to let this case slip through as unsliced.
			if( 1 == iPoints.Count )
			{
				output.Add( p );
				return true;
			}

			// We don't yet deal with tangencies.
			// We're going to fail on this case, because it could be more problematic.
			if( Utils.Odd( iPoints.Count ) )
			{
				Debug.Assert( false );
				return false;
			}

			if( iPoints.Count > 2 )
			{
				// We may need our intersection points to all be reorded by 1.
				// This is so that when walking from i1 -> i2 along c, we will be moving through the interior of the polygon.
				// ZZZ - This may need to change when hack in SplicedArc is improved.
				int dummy = 0;
				Segment testArc = SmallerSplicedArc( c, iPoints, ref dummy, true, ref dummy );
				Vector3D midpoint = testArc.Midpoint;

				if( !p.IsPointInsideParanoid( midpoint ) )
				{
					IntersectionPoint t = iPoints[0];
					iPoints.RemoveAt( 0 );
					iPoints.Add( t );
				}
			}

			//
			// From each intersection point, walk the polygon.
			//
			int numPairs = iPoints.Count / 2;
			for( int i = 0; i < numPairs; i++ )
			{
				int pair = i;
				output.Add( WalkPolygon( p, diced, c, pair, iPoints, true ) );
				output.Add( WalkPolygon( p, diced, c, pair, iPoints, false ) );
			}

			//
			// Recalc centers.
			//
			foreach( Polygon poly in output )
				poly.Center = poly.CentroidApprox;

			//
			// Remove duplicate polygons.
			//
			output = output.Distinct( new PolygonEqualityComparer() ).ToList();

			return true;
		}

		/// <summary>
		/// Splits segmentToSplit in two based on iLocation.
		/// First new segment will be added to diced, and second new segment will be returned.
		/// If split doesn't happen, segmentToSplit will be returned.
		/// </summary>
		private static Segment SplitHelper( Segment segmentToSplit, Vector3D iLocation, Polygon diced, List<IntersectionPoint> iPoints )
		{
			List<Segment> split;
			if( segmentToSplit.Split( iLocation, out split ) )
			{
				Debug.Assert( split.Count == 2 );
				diced.Segments.Add( split[0] );

				IntersectionPoint iPoint = new IntersectionPoint();
				iPoint.Location = iLocation;
				iPoint.Index = diced.Segments.Count;
				iPoints.Add( iPoint );

				return split[1];
			}
			else
			{
				// We were presumably at an endpoint.
				// Add to iPoints list only if it was the starting endpoint.
				// (This will avoid duplicate entries).
				if( iLocation.Compare( segmentToSplit.P1 ) )
				{
					IntersectionPoint iPoint = new IntersectionPoint();
					iPoint.Location = iLocation;
					iPoint.Index = diced.Segments.Count;
					iPoints.Add( iPoint );
				}

				return segmentToSplit;
			}
		}

		/// <summary>
		/// Helper to walk a polygon, starting from a pair of intersection points.
		/// increment determines the direction we are walking.
		/// NOTE: when walking from i1 -> i2 along c, we should be moving through the interior of the polygon.
		/// </summary>
		private static Polygon WalkPolygon( Polygon parent, Polygon walking, Circle c, int pair, List<IntersectionPoint> iPoints, bool increment )
		{
			Polygon newPoly = new Polygon();

			IntersectionPoint iPoint1, iPoint2;
			GetPairPoints( iPoints, pair, increment, out iPoint1, out iPoint2 );
			Vector3D startLocation = iPoint1.Location;

			int iSeg = 0;
			Segment current = SplicedSeg( parent, c, iPoints, ref pair, increment, ref iSeg );
			newPoly.Segments.Add( current.Clone() );

			while( true )
			{
				// NOTE: Since we don't allow tangent intersections, there will never 
				//		 be multiple spliced arcs added in succession.

				// Add in the next one.
				current = walking.Segments[iSeg];
				newPoly.Segments.Add( current.Clone() );
				iSeg = GetNextSegmentIndex( walking, iSeg );
				if( current.P2.Compare( startLocation ) )
					break;

				// Do we need to splice in at this point?
				Vector3D segEnd = current.P2;
				if( iPoints.Select( p => p.Location ).Contains( segEnd ) )	// ZZZ - Performance
				{
					current = SplicedSeg( parent, c, iPoints, ref pair, increment, ref iSeg );
					newPoly.Segments.Add( current.Clone() );
					if( current.P2.Compare( startLocation ) )
						break;
				}
			}

			return newPoly;
		}

		/// <summary>
		/// Get a pair of intersection points.
		/// </summary>
		private static void GetPairPoints( List<IntersectionPoint> iPoints, int pair, bool increment, 
			out IntersectionPoint iPoint1, out IntersectionPoint iPoint2 )
		{
			int idx1 = pair * 2;
			int idx2 = pair * 2 + 1;
			if( !increment )
				Utils.Swap<int>( ref idx1, ref idx2 );

			iPoint1 = iPoints[idx1];
			iPoint2 = iPoints[idx2];
		}

		/// <summary>
		/// Helper to return the smaller spliced arc.  In the case of a line, this just returns the spliced line.
		/// </summary>
		private static Segment SmallerSplicedArc( Circle c, List<IntersectionPoint> iPoints, ref int pair, bool increment, ref int nextSegIndex )
		{
			IntersectionPoint iPoint1, iPoint2;
			GetPairPoints( iPoints, pair, increment, out iPoint1, out iPoint2 );

			Vector3D p1 = iPoint1.Location;
			Vector3D p2 = iPoint2.Location;
			nextSegIndex = iPoint2.Index;

			Segment newSeg = null;
			if( c.IsLine )
			{
				newSeg = Segment.Line( p1, p2 );
			}
			else
			{
				newSeg = Segment.Arc( p1, p2, c.Center, clockwise: true );
				if( newSeg.Angle > System.Math.PI )
					newSeg.Clockwise = false;
			}

			pair++;
			if( pair == iPoints.Count / 2 )
				pair = 0;

			return newSeg;
		}

		/// <summary>
		/// Helper to return a spliced segment.
		/// </summary>
		private static Segment SplicedSeg( Polygon parent, Circle c, List<IntersectionPoint> iPoints, ref int pair, bool increment, ref int nextSegIndex )
		{
			Segment spliced = SmallerSplicedArc( c, iPoints, ref pair, increment, ref nextSegIndex );
			if( c.IsLine )
				return spliced;

			// This is heuristic, but works quite well.
			if( System.Math.Abs( spliced.Angle ) < System.Math.PI * .75 )
				return spliced;

			// Direction should actually be such that arc is inside the parent polygon,
			// which may not be the case for the segment above.

			// Now check to make sure the arc is indeed inside the parent polygon.
			double testAngle = spliced.Angle / 1000;
			if( spliced.Clockwise )
				testAngle *= -1;

			Vector3D t1 = spliced.P1;
			t1.RotateXY( spliced.Center, testAngle );

			if( !parent.IsPointInsideParanoid( t1 ) )
				spliced.Clockwise = !spliced.Clockwise;

			return spliced;
		}

		private static int GetNextSegmentIndex( Polygon walking, int idx )
		{
			int ret = idx+1;
			if( ret == walking.NumSides )
				ret = 0;
			return ret;
		}
	}
}
