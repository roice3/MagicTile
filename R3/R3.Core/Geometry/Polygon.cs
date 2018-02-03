namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	public enum SegmentType
	{
		Line,
		Arc
	}

	public class Segment : ITransformable
	{
		public SegmentType Type { get; set; }
		public Vector3D P1 { get; set; }
		public Vector3D P2 { get; set; }

		// These only apply to arc segments.
		public Vector3D Center { get; set; }
		public bool Clockwise { get; set; }

		public Segment Clone()
		{
			Segment newSeg = new Segment();
			newSeg.Type = Type;
			newSeg.P1 = P1;
			newSeg.P2 = P2;
			newSeg.Center = Center;
			newSeg.Clockwise = Clockwise;
			return newSeg;
		}

		public static Segment Line( Vector3D start, Vector3D end )
		{
			Segment newSeg = new Segment();
			newSeg.Type = SegmentType.Line;
			newSeg.P1 = start;
			newSeg.P2 = end;
			return newSeg;
		}

		public static Segment Arc( Vector3D start, Vector3D end, Vector3D center, bool clockwise )
		{
			Segment newSeg = new Segment();
			newSeg.Type = SegmentType.Arc;
			newSeg.P1 = start;
			newSeg.P2 = end;
			newSeg.Center = center;
			newSeg.Clockwise = true;
			return newSeg;
		}

		public static Segment Arc( Vector3D start, Vector3D mid, Vector3D end )
		{
			Segment newSeg = new Segment();
			newSeg.Type = SegmentType.Arc;
			newSeg.P1 = start;
			newSeg.P2 = end;

			Circle c = new Circle();
			c.From3Points( start, mid, end );
			newSeg.Center = c.Center;

			// Obtain vectors from center point of circle (as if at the origin)
			Vector3D startOrigin = start - c.Center;
			Vector3D midOrigin = mid - c.Center;
			Vector3D endOrigin = end - c.Center;

			// Calculate the normal vector and angle to traverse.
			// ZZZ - worry about failure of cross product here.
			Vector3D normalVector = startOrigin.Cross( endOrigin );
			newSeg.Clockwise = normalVector.Z < 0;
			double angleToTraverse = startOrigin.AngleTo( endOrigin );

			// The normal vector might need to be reversed and the angleToTraverse adjusted.
			// This happens depending on the location of the midpoint relative to the start and end points.
			double compareAngle = startOrigin.AngleTo( midOrigin ) + midOrigin.AngleTo( endOrigin );
			bool reverse = !Tolerance.Equal( angleToTraverse, compareAngle );
			if( reverse )
				newSeg.Clockwise = !newSeg.Clockwise;

			return newSeg;
		}

		public double Radius
		{
			get
			{
				Debug.Assert( SegmentType.Arc == Type );
				return ( ( P1 - Center ).Abs() );
			}
		}

		public double Angle
		{
			get
			{
				if( SegmentType.Arc != Type )
				{
					Debug.Assert( false );
					return 0;
				}

				Vector3D v1 = P1 - Center;
				Vector3D v2 = P2 - Center;
				return Clockwise ?
					Euclidean2D.AngleToClock( v1, v2 ) :
					Euclidean2D.AngleToCounterClock( v1, v2 );
			}
		}

		public Circle Circle
		{
			get
			{
				Debug.Assert( SegmentType.Arc == Type );

				// Avoiding allocations of new circles,
				// (Memory profiling showed this was responsible
				// for many allocations.)
				if( m_circle != null )
				{
					if( m_circle.Center == this.Center &&
						m_circle.Radius == this.Radius )
						return m_circle;
				}

				m_circle = new Circle();
				m_circle.Center = Center;
				m_circle.Radius = Radius;
				return m_circle;
			}
		}
		private Circle m_circle;

		public double Length
		{
			get
			{
				if( SegmentType.Arc == Type )
				{
					return Radius * Angle;
				}
				else
				{
					return ( P2 - P1 ).Abs();
				}
			}
		}

		public Vector3D Midpoint
		{
			get
			{
				if( SegmentType.Arc == Type )
				{
					double a = Angle / 2;
					Vector3D ret = P1 - Center;
					ret.RotateXY( Clockwise ? -a : a );
					ret += Center;
					return ret;
				}
				else
				{
					return ( P1 + P2 ) / 2;
				}
			}
		}

		public void Reverse() 
		{
			SwapPoints();
			if( SegmentType.Arc == Type )
				Clockwise = !Clockwise;
		}

		/// <summary>
		/// Return the vertices from subdividing ourselves.
		/// </summary>
		public Vector3D[] Subdivide ( int numSegments )
		{
			List<Vector3D> ret = new List<Vector3D>();
			if( numSegments < 1 )
			{
				Debug.Assert( false );
				return ret.ToArray();
			}

			if( Type == SegmentType.Arc )
			{
				Vector3D v = P1 - Center;
				double angle = this.Angle / numSegments;
				for( int i=0; i<numSegments; i++ )
				{
					ret.Add( Center + v );
					v.RotateXY( Clockwise ? -angle : angle );
				}
			}
			else
			{
				Vector3D v = P2 - P1;
				v.Normalize();
				for( int i=0; i<numSegments; i++ )
					ret.Add( P1 + v * i * Length / numSegments );
			}

			// Add in the last point and return.
			ret.Add( P2 );
			return ret.ToArray();
		}

		public void SwapPoints()
		{
			Vector3D t = P1;
			P1 = P2;
			P2 = t;
		}

		public bool IsPointOn( Vector3D test )
		{
			return SegmentType.Arc == Type ?
				PointOnArcSegment( test, this ) : 
				PointOnLineSegment( test, this );
		}

		private static bool PointOnArcSegment( Vector3D p, Segment seg )
		{
			double maxAngle = seg.Angle;
			Vector3D v1 = seg.P1 - seg.Center;
			Vector3D v2 = p - seg.Center;
			Debug.Assert( Tolerance.Equal( v1.Abs(), v2.Abs() ) );
			double angle = seg.Clockwise ?
				Euclidean2D.AngleToClock( v1, v2 ) :
				Euclidean2D.AngleToCounterClock( v1, v2 );

			return Tolerance.LessThanOrEqual( angle, maxAngle );
		}

		private static bool PointOnLineSegment( Vector3D p, Segment seg )
		{
			// This will be so if the point and the segment ends represent
			// the vertices of a degenerate triangle.
			double d1 = ( seg.P2 - seg.P1 ).Abs();
			double d2 = ( p - seg.P1 ).Abs();
			double d3 = ( seg.P2 - p ).Abs();
			return Tolerance.Equal( d1, d2 + d3 );
		}

		public bool Intersects( Segment s )
		{
			Vector3D i1 = Vector3D.DneVector(), i2 = Vector3D.DneVector();
			int numInt = 0;
			if( SegmentType.Arc == Type )
			{
				if( SegmentType.Arc == s.Type )
					numInt = Euclidean2D.IntersectionCircleCircle( Circle, s.Circle, out i1, out i2 );
				else
					numInt = Euclidean2D.IntersectionLineCircle( P1, P2, s.Circle, out i1, out i2 );
			}
			else
			{
				if( SegmentType.Arc == s.Type )
					numInt = Euclidean2D.IntersectionLineCircle( s.P1, s.P2, Circle, out i1, out i2 );
				else
					numInt = Euclidean2D.IntersectionLineLine( P1, P2, s.P1, s.P2, out i1 );
			}

			// -1 can denote conincident segments (I'm not consistent in the impls above :/),
			// and we are not going to include those for now.
			if( numInt <= 0 )
				return false;

			if( numInt > 0 )
				if( IsPointOn( i1 ) && s.IsPointOn( i1 ) )
					return true;
			if( numInt > 1 )
				if( IsPointOn( i2 ) && s.IsPointOn( i2 ) )
					return true;

			return false;
		}

		public void Reflect( Segment s )
		{
			// NOTES:
			// Arcs can go to lines, and lines to arcs.
			// Rotations may reverse arc directions as well.
			// Arc centers can't be transformed directly.

			// NOTE: We must calc this before altering the endpoints.
			Vector3D mid = Midpoint;
			if( Infinity.IsInfinite( mid ) )
				mid = Infinity.IsInfinite( s.P1 ) ? s.P2 * Infinity.FiniteScale : s.P1 * Infinity.FiniteScale;

			P1 = s.ReflectPoint( P1 );
			P2 = s.ReflectPoint( P2 );
			mid = s.ReflectPoint( mid );

			// Can we make a circle out of the reflected points?
			Circle temp = new Circle();
			if( !Infinity.IsInfinite( P1 ) && !Infinity.IsInfinite( P2 ) && !Infinity.IsInfinite( mid ) &&
				temp.From3Points( P1, mid, P2 ) )
			{
				Type = SegmentType.Arc;
				Center = temp.Center;

				// Work out the orientation of the arc.
				Vector3D t1 = P1 - Center;
				Vector3D t2 = mid - Center;
				Vector3D t3 = P2 - Center;
				double a1 = Euclidean2D.AngleToCounterClock( t2, t1 );
				double a2 = Euclidean2D.AngleToCounterClock( t3, t1 );
				Clockwise = a2 > a1;
			}
			else
			{
				// The circle construction fails if the points
				// are colinear (if the arc has been transformed into a line).
				Type = SegmentType.Line;

				// XXX - need to do something about this.
				// Turn into 2 segments?
				//if( isInfinite( mid ) )
				// Actually the check should just be whether mid is between p1 and p2.
			}
		}

		public void Transform( Mobius m )
		{
			TransformInternal( m );
		}

		public void Transform( Isometry i )
		{
			TransformInternal( i );
		}

		/// <summary>
		/// Apply a transform to us.
		/// </summary>
		private void TransformInternal<T>( T transform ) where T : ITransform
		{
			// NOTES:
			// Arcs can go to lines, and lines to arcs.
			// Rotations may reverse arc directions as well.
			// Arc centers can't be transformed directly.

			// NOTE: We must calc this before altering the endpoints.
			Vector3D mid = Midpoint;
			if( Infinity.IsInfinite( mid ) )
				mid = Infinity.IsInfinite( P1 ) ? P2 * Infinity.FiniteScale : P1 * Infinity.FiniteScale;

			P1 = transform.Apply( P1 );
			P2 = transform.Apply( P2 );
			mid = transform.Apply( mid );

			// Can we make a circle out of the transformed points?
			Circle temp = new Circle();
			if( !Infinity.IsInfinite( P1 ) && !Infinity.IsInfinite( P2 ) && !Infinity.IsInfinite( mid ) &&
				temp.From3Points( P1, mid, P2 ) )
			{
				Type = SegmentType.Arc;
				Center = temp.Center;

				// Work out the orientation of the arc.
				Vector3D t1 = P1 - Center;
				Vector3D t2 = mid - Center;
				Vector3D t3 = P2 - Center;
				double a1 = Euclidean2D.AngleToCounterClock( t2, t1 );
				double a2 = Euclidean2D.AngleToCounterClock( t3, t1 );
				Clockwise = a2 > a1;
			}
			else
			{
				// The circle construction fails if the points
				// are colinear (if the arc has been transformed into a line).
				Type = SegmentType.Line;

				// XXX - need to do something about this.
				// Turn into 2 segments?
				//if( isInfinite( mid ) )
				// Actually the check should just be whether mid is between p1 and p2.
			}
		}

		/// <summary>
		/// Apply a Euclidean translation to us.
		/// </summary>
		public void Translate( Vector3D v )
		{
			this.P1 += v;
			this.P2 += v;
			if( this.Type == SegmentType.Arc )
				this.Center += v;
		}

		/// <summary>
		/// Apply a Euclidean rotation to us.
		/// </summary>
		public void Rotate( Matrix4D m )
		{
			this.P1 = m.RotateVector( this.P1 );
			this.P2 = m.RotateVector( this.P2 );
			if( this.Type == SegmentType.Arc )
				this.Center = m.RotateVector( this.Center );
		}

		/// <summary>
		/// Euclidean scale us relative to some center point.
		/// NOTE: Currently only works for line segments.
		/// </summary>
		public void Scale( Vector3D center, double factor )
		{
			Translate( -center );
			if( this.Type == SegmentType.Line )
			{
				this.P1 *= factor;
				this.P2 *= factor;
			}
			else if( this.Type == SegmentType.Arc )
			{
				Vector3D p1 = this.P1;
				Vector3D p2 = this.P2;
				Vector3D mid = this.Midpoint;
				p1 *= factor;
				p2 *= factor;
				mid *= factor;
				Segment temp = Segment.Arc( p1, mid, p2 );

				this.P1 = p1;
				this.P2 = p2;
				this.Center = temp.Center;
			}
			Translate( center );
		}

		public Vector3D ReflectPoint( Vector3D input ) 
		{
			if( SegmentType.Arc == Type )
			{
				Circle c = this.Circle;
				return c.ReflectPoint( input );
			}
			else
			{
				return Euclidean2D.ReflectPointInLine( input, P1, P2 );
			}
		}

		/// <summary>
		/// Splits a segment into multiple segments based on a point.
		/// The new segments will be ordered in the same way as us (from p1 -> point and point -> p2 ).
		/// </summary>
		/// <returns>True if the segment was split, false otherwise (if passed in point is not on segment or an endpoint).</returns>
		public bool Split( Vector3D point, out List<Segment> split )
		{
			split = new List<Segment>();

			if( !IsPointOn( point ) )
			{
				Debug.Assert( false );
				return false;
			}

			// Endpoint?
			if( point.Compare( P1 ) || point.Compare( P2 ) )
				return false;

			Segment s1 = Clone();
			Segment s2 = Clone();
			s1.P2 = point;
			s2.P1 = point;
			split.Add( s1 );
			split.Add( s2 );
			return true;
		}

		/// <summary>
		/// Checks to see if two points are ordered on this segment, that is:
		/// P1 -> test1 -> test2 -> P2 returns true.
		/// P1 -> test2 -> test1 -> P2 returns false;
		/// Also returns false if test1 or test2 are equal, not on the segment, or are an endpoint.
		/// </summary>
		/// <param name="p1"></param>
		/// <param name="p2"></param>
		/// <returns></returns>
		public bool Ordered( Vector3D test1, Vector3D test2 )
		{
			if( test1.Compare( test2 ) )
			{
				Debug.Assert( false );
				return false;
			}
			if( !IsPointOn( test1 ) || !IsPointOn( test2 ) )
			{
				Debug.Assert( false );
				return false;
			}
			if( test1.Compare( P1 ) || test1.Compare( P2 ) ||
				test2.Compare( P1 ) || test2.Compare( P2 ) )
				return false;

			if( SegmentType.Arc == Type )
			{
				Vector3D t1 = P1 - Center;
				Vector3D t2 = test1 - Center;
				Vector3D t3 = test2 - Center;
				double a1 = Clockwise ? Euclidean2D.AngleToClock( t1, t2 ) : Euclidean2D.AngleToCounterClock( t1, t2 );
				double a2 = Clockwise ? Euclidean2D.AngleToClock( t1, t3 ) : Euclidean2D.AngleToCounterClock( t1, t3 );
				return a1 < a2;
			}
			else
			{
				double d1 = ( test1 - P1 ).MagSquared();
				double d2 = ( test2 - P1 ).MagSquared();
				return d1 < d2;
			}
		}
	}

	public class Polygon : ITransformable
	{
		public Polygon()
		{
			Segments = new List<Segment>();
			Center = new Vector3D();
		}

		public Vector3D Center { get; set; }
		public List<Segment> Segments { get; set; }

		/// <summary>
		/// Create a new polygon from a set of points.
		/// Line segments will be used.
		/// </summary>
		public static Polygon FromPoints( Vector3D[] points )
		{
			Polygon result = new Polygon();

			for( int i = 0; i < points.Length; i++ )
			{
				int idx1 = i;
				int idx2 = i == points.Length - 1 ? 0 : i + 1;

				Segment newSeg = Segment.Line( points[idx1], points[idx2] );
				result.Segments.Add( newSeg );
			}

			result.Center = result.CentroidApprox;
			return result;
		}

		public void Clear()
		{
			Segments.Clear();
		}

		public Polygon Clone()
		{
			Polygon newPoly = new Polygon();
			//newPoly.Segments = new List<Segment>( Segments );
			foreach( Segment s in Segments )
				newPoly.Segments.Add( s.Clone() );
			newPoly.Center = Center;
			return newPoly;
		}

		public void CreateRegular( int numSides, int q )
		{
			int p = numSides;

			Segments.Clear();
			List<Vector3D> points = new List<Vector3D>();

			Geometry g = Geometry2D.GetGeometry( p, q );
			double circumRadius = Geometry2D.GetNormalizedCircumRadius( p, q );

			double angle = 0;
			for( int i = 0; i < p; i++ )
			{
				Vector3D point = new Vector3D();
				point.X = (circumRadius * Math.Cos( angle ));
				point.Y = (circumRadius * Math.Sin( angle ));
				points.Add( point );
				angle += Utils.DegreesToRadians( 360.0 / p );
			}

			// Turn this into segments.
			for( int i = 0; i < points.Count; i++ )
			{
				int idx1 = i;
				int idx2 = i == points.Count - 1 ? 0 : i + 1;
				Segment newSegment = new Segment();
				newSegment.P1 = points[idx1];
				newSegment.P2 = points[idx2];

				if( g != Geometry.Euclidean )
				{
					newSegment.Type = SegmentType.Arc;

					if( 2 == p )
					{
						// Our magic formula below breaks down for digons.
						double factor = Math.Tan( Math.PI / 6 );
						newSegment.Center = newSegment.P1.X > 0 ?
							new Vector3D( 0, -circumRadius, 0 ) * factor :
							new Vector3D( 0, circumRadius, 0 ) * factor;
					}
					else
					{
						// Our segments are arcs in Non-Euclidean geometries.
						// Magically, the same formula turned out to work for both.
						// (Maybe this is because the Poincare Disc model of the
						// hyperbolic plane is stereographic projection as well).

						double piq = q == -1 ? 0 : Math.PI / q; // Handle q infinite.
						double t1 = Math.PI / p;
						double t2 = Math.PI / 2 - piq - t1;
						double factor = (Math.Tan( t1 ) / Math.Tan( t2 ) + 1) / 2;
						newSegment.Center = (newSegment.P1 + newSegment.P2) * factor;
					}

					newSegment.Clockwise = Geometry.Spherical == g ? false : true;
				}

				// XXX - Make this configurable?
				// This is the color of cell boundary lines.
				//newSegment.m_color = CColor( 1, 1, 0, 1 );
				Segments.Add( newSegment );
			}
		}

		/// <summary>
		/// Create a Euclidean polygon from a set of points.
		/// NOTE: Do not include starting point twice.
		/// </summary>
		public void CreateEuclidean( Vector3D[] points )
		{
			this.Segments.Clear();

			for( int i = 0; i < points.Length; i++ )
			{
				int idx1 = i;
				int idx2 = i + 1;
				if( idx2 == points.Length )
					idx2 = 0;
				Segment newSeg = Segment.Line( points[idx1], points[idx2] );
				this.Segments.Add( newSeg );
			}

			Center = this.CentroidApprox;
		}

		public int NumSides
		{
			get { return Segments.Count; }
		}

		public double Length
		{
			get
			{
				double totalLength = 0;
				for( int i = 0; i < NumSides; i++ )
				{
					Segment s = Segments[i];
					// ZZZ
					//if( s.valid() )
						totalLength += s.Length;
				}

				return totalLength;
			}
		}

		/// <summary>
		/// Find a centroid of the polygon.
		/// This is not fully accurate for arcs yet.
		/// </summary>
		public Vector3D CentroidApprox
		{
			get
			{
				Vector3D average = new Vector3D();
				for( int i = 0; i < NumSides; i++ )
				{
					// NOTE: This is not fully accurate for arcs (using midpoint instead of true centroid).
					//		 This was done on purpose in MagicTile v1, to help avoid drawing overlaps.
					//		 (it biases the calculated centroid towards large arcs.)
					Segment s = Segments[i];
					// ZZZ
					//if( s.valid() )
					average += s.Midpoint * s.Length;
				}

				average /= Length;
				return average;
			}
		}

		public Vector3D Normal
		{
			get
			{
				return NormalAfterTransform( v => v );
			}
		}

		/// <summary>
		/// Calculate a normal after a transformation function is applied
		/// to the points of the polygon.
		/// </summary>
		public Vector3D NormalAfterTransform( System.Func<Vector3D, Vector3D> transform )
		{
			if( this.NumSides < 1 )
				return new Vector3D( 0, 0, 1 );

			return Euclidean3D.NormalFrom3Points(
				this.Segments[0].P1, this.Segments[0].P2, this.Center, transform );
		}

		/// <summary>
		/// The first vertex.
		/// </summary>
		public Vector3D? Start
		{
			get
			{
				return Segments?.FirstOrDefault()?.P1;
			}
		}

		/// <summary>
		/// The middle point around the pologon.
		/// This is an edge midpoint if the polygon has an odd number of sides.
		/// </summary>
		public Vector3D? Mid
		{
			get
			{
				if( Segments == null || Segments.Count == 0 )
					return null;

				int count = Segments.Count;
				if( count % 2 == 0 )
					return Segments[count / 2].P1;
				return Segments[count / 2].Midpoint;
			}
		}

		/// <summary>
		/// Returns only the vertices of the polygon.
		/// </summary>
		public Vector3D[] Vertices
		{
			get
			{
				List<Vector3D> points = new List<Vector3D>();
				foreach( Segment s in this.Segments )
					points.Add( s.P1 );
				return points.ToArray();
			}
		}

		/// <summary>
		/// Returns all edge midpoints of the polygon.
		/// </summary>
		public Vector3D[] EdgeMidpoints
		{
			get
			{
				List<Vector3D> points = new List<Vector3D>();
				foreach( Segment s in this.Segments )
					points.Add( s.Midpoint );
				return points.ToArray();
			}
		}

		public Vector3D[] EdgePoints
		{
			get
			{
				double arcResolution = Utils.DegreesToRadians( 4.5 );
				int minSegs = 10;
				return CalcEdgePoints( arcResolution, minSegs, checkForInfinities: true );
			}
		}

		public Vector3D[] CalcEdgePoints( double arcResolution, int minSegs, bool checkForInfinities )
		{
			List<Vector3D> points = new List<Vector3D>();
			
			for( int i = 0; i < NumSides; i++ )
			{
				Segment s = Segments[i];

				// First point.
				// ZZZ - getting lazy
				//Debug.Assert( ! (isInfinite( s.m_p1 ) && isInfinite( s.m_p2 )) );
				Vector3D p1 = checkForInfinities && Infinity.IsInfinite( s.P1 ) ? 
					s.P2 * Infinity.FiniteScale : 
					s.P1;
				points.Add( p1 );

				// For arcs, add in a bunch of extra points.
				if( SegmentType.Arc == s.Type )
				{
					double maxAngle = s.Angle;
					Vector3D vs = s.P1 - s.Center;
					int numSegments = (int)(maxAngle / (arcResolution));
					if( numSegments < minSegs )
						numSegments = minSegs;
					double angle = maxAngle / numSegments;
					for( int j = 1; j < numSegments; j++ )
					{
						vs.RotateXY( s.Clockwise ? -angle : angle );
						points.Add( vs + s.Center );
					}
				}

				// Last point.
				Vector3D p2 = checkForInfinities && Infinity.IsInfinite( s.P2 ) ? 
					s.P1 * Infinity.FiniteScale : 
					s.P2;
				points.Add( p2 );
			}

			return points.ToArray();
		}

		/// <summary>
		/// Returns true if CCW, false if CW.
		/// NOTE: only makes sense for 2D polygons.
		/// </summary>
		public bool Orientation
		{
			get
			{
				double sArea = SignedArea;
				return sArea > 0;
			}
		}

		public double SignedArea
		{
			get
			{
				// Calculate the signed area.
				// ZZZ - I'm doing arcs piecemiel at this point.  Maybe there is a better way.
				double sArea = 0;
				Vector3D[] edgePoints = this.EdgePoints;
				for( int i = 0; i < edgePoints.Length; i++ )
				{
					Vector3D v1 = edgePoints[i];
					Vector3D v2 = edgePoints[i == edgePoints.Length - 1 ? 0 : i + 1];
					sArea += v1.X * v2.Y - v1.Y * v2.X;
				}
				sArea /= 2;
				return sArea;
			}
		}

		public CircleNE CircumCircle
		{
			get
			{
				CircleNE result = new CircleNE();
				if( Segments.Count > 2 )
					result.From3Points( Segments[0].P1, Segments[1].P1, Segments[2].P1 );
				result.CenterNE = this.Center;
				return result;
			}
		}

		public CircleNE InCircle
		{
			get
			{
				CircleNE result = new CircleNE();
				if( Segments.Count > 2 )
					result.From3Points( Segments[0].Midpoint, Segments[1].Midpoint, Segments[2].Midpoint );
				result.CenterNE = this.Center;
				return result;
			}
		}

		/// <summary>
		/// Calculate the bounding box of this polygon, using just the vertices.
		/// </summary>
		public System.Tuple<Vector3D,Vector3D> BoundingBox
		{
			get
			{
				double 
					minX = double.MaxValue, minY = double.MaxValue, 
					maxX = double.MinValue, maxY = double.MinValue;

				foreach( Vector3D v in Vertices )
				{
					minX = Math.Min( minX, v.X );
					minY = Math.Min( minY, v.Y );
					maxX = Math.Max( maxX, v.X );
					maxY = Math.Max( maxY, v.Y );
				}

				return new System.Tuple<Vector3D, Vector3D>( 
					new Vector3D( minX, minY ), 
					new Vector3D( maxX, maxY ) );
			}
		}

		public void Reverse()
		{
			// Reverse all our segments and swap the order of them.
			foreach( Segment s in Segments )
				s.Reverse();

			Segments.Reverse();
		}

		/// <summary>
		/// This will reorder our segments (in a CW sense).
		/// NOTE: The center will not be recalculated.
		/// </summary>
		public void Cycle( int num )
		{
			if( num < 0 || num > this.NumSides )
				throw new System.ArgumentException( "Cycle called with invalid input." );

			for( int i = 0; i < num; i++ )
			{
				// Move the first to the last (like a CW rotation).
				Segment first = Segments[0];
				Segments.RemoveAt( 0 );
				Segments.Add( first );
			}
		}

		public void Reflect( Segment s )
		{
			// Just reflect all our segments.
			for( int i = 0; i < Segments.Count; i++ )
				Segments[i].Reflect( s );
			Center = s.ReflectPoint( Center );
		}

		/// <summary>
		/// Apply a Mobius transform to us.
		/// </summary>
		public void Transform( Mobius m )
		{
			foreach( Segment s in this.Segments )
				s.Transform( m );
			Center = m.ApplyInfiniteSafe( Center );
		}

		/// <summary>
		/// Apply an isometry to us.
		/// </summary>
		public void Transform( Isometry isometry )
		{
			foreach( Segment s in this.Segments )
				s.Transform( isometry );
			Center = isometry.ApplyInfiniteSafe( Center );
		}

		/// <summary>
		/// Apply a Euclidean translation to us.
		/// </summary>
		public void Translate( Vector3D v )
		{
			foreach( Segment s in this.Segments )
				s.Translate( v );
			Center += v;
		}

		/// <summary>
		/// Apply a Euclidean rotation to us.
		/// </summary>
		public void Rotate( Matrix4D m )
		{
			foreach( Segment s in this.Segments )
				s.Rotate( m );
			Center = m.RotateVector( Center );
		}

		/// <summary>
		/// Euclidean scale us.
		/// </summary>
		public void Scale( double factor )
		{
			foreach( Segment s in this.Segments )
				s.Scale( this.Center, factor );
		}

		/// <summary>
		/// Checks to see if we intersect another polygon.
		/// </summary>
		public bool Intersects( Polygon p )
		{
			// If we are inside the polygon, or they are inside us, we intersect.
			// ZZZ - This isn't perfect and doesn't handle all cases.
			//		 We actually need to check this for any point, not just the center.
			if( p.IsPointInsideParanoid( Center ) ||
				IsPointInsideParanoid( p.Center ) )
				return true;

			// If any segments interset, we intersect.
			foreach( Segment s1 in Segments )
			foreach( Segment s2 in p.Segments )
				if( s1.Intersects( s2 ) )
					return true;

			return false;
		}

		/// <summary>
		/// Gets the intersection points between us and a generalized circle.
		/// </summary>
		public Vector3D[] GetIntersectionPoints( Circle line )
		{
			List<Vector3D> iPoints = new List<Vector3D>();
			for( int i = 0; i < NumSides; i++ )
				iPoints.AddRange( line.GetIntersectionPoints( Segments[i] ) );
			return iPoints.ToArray();
		}

		/// <summary>
		/// Attempts to return true if the polygon center is not inside the polygon.
		/// This is used in spherical case, and currently has some hardcoded hacks
		/// to make it work better.
		/// </summary>
		public bool IsInverted
		{
			get
			{
				// This is a hack to simply ignore a little more than one complete hemisphere of the spherical surface.
				// (and all of the Poincare disk).
				double factor = 1.5;	// Magic tunable number :(
				if( this.Center.Abs() < Geometry2D.DiskRadius * factor )
					return false;
				
				if( Infinity.IsInfinite( this.Center ) )
					return true;

				bool inverted = !this.IsPointInside( this.Center );
				if( !inverted )
					return inverted;

				// We think we're inverted.
				// However, we've been have too many inverted false positives here!

				// ZZZ - hack! hack! (The correct thing would be for the IsPointInside function to work robustly.)
				// We'll try two more times, and let the majority win (only need one more vote).
				// Since not many polygons will actually make it here, these extra calculations are ok.
				// It does seem to help a lot.
				Circle ray = new Circle();
				ray.From2Points( this.Center, this.Center + new Vector3D( 103, 10007 ) );
				if( !this.IsPointInside( this.Center, ray ) )
					return true;

				ray.From2Points( this.Center, this.Center + new Vector3D( 7001, 7993 ) );
				return !this.IsPointInside( this.Center, ray );
			}
		}

		private class HighToleranceVectorEqualityComparer : IEqualityComparer<Vector3D>
		{
			public bool Equals( Vector3D v1, Vector3D v2 )
			{
				return
					v1.Compare( v2, m_tolerance );
			}

			public int GetHashCode( Vector3D v )
			{
				return v.GetHashCode( m_tolerance );
			}

			// Argh, between a rock and a hard place.
			// Making this smaller causes issues, making this bigger causes issues.
			private double m_tolerance = 0.0001;
		}

		/// <summary>
		/// Try not to use this.
		/// See IsInverted method about this HACK.
		/// ZZZ - Need to find a better way to make IsPointInside more robust :(
		/// </summary>
		public bool IsPointInsideParanoid( Vector3D p )
		{
			int insideCount = 0;
			Circle ray = new Circle();

			if( this.IsPointInside( p ) )
				insideCount++;

			ray.From2Points( p, p + new Vector3D( 103, 10007 ) );
			if( this.IsPointInside( p, ray ) )
				insideCount++;

			ray.From2Points( p, p + new Vector3D( 7001, 7993 ) );
			if( this.IsPointInside( p, ray ) )
				insideCount++;

			return insideCount >= 2;
		}

		/// <summary>
		/// Warning, this suffers from FP tolerance issues,
		/// when the polygon has arc segments with very large radii (for instance).
		/// </summary>
		public bool IsPointInside( Vector3D p )
		{
			// ZZZ - Our "ray" will be half a circle for now.
			//		 (We'll throw out intersections where x <= p.X)
			Circle ray = new Circle();
			//ray.From3Points( p + new Vector3D( -500, 1 ), p, p + new Vector3D( 500, 1 ) );		// Circle was too huge (r ~= 125000), which caused tolerance issues.
			//ray.From3Points( p + new Vector3D( -103, 1 ), p, p + new Vector3D( 193, 1 ) );		// primes! (r ~= 10000)  Still suffering
			ray.From2Points( p, p + new Vector3D( 10007, 103 ) );									// Best, but still not perfect.

			return IsPointInside( p, ray );
		}

		/// <summary>
		/// Warning, this suffers from FP tolerance issues,
		/// when the polygon has arc segments with very large radii (for instance).
		/// </summary>
		private bool IsPointInside( Vector3D p, Circle ray )
		{
			// We use the ray casting since that will work for arcs as well.
			// NOTE: This impl is known to not be fully general yet,
			//		 since some issues won't arise in MagicTile.
			//		 See http://en.wikipedia.org/wiki/Point_in_polygon about
			//		 some of the degenerate cases that are possible.

			// I don't know why I had this in here, as it seems patently wrong in the general case.
			// I'm leaving it commented out.  In case removing this exposes some other issue,
			// maybe it will be easier to track down.
			//if( Tolerance.Zero( p.MagSquared() ) )
				//return true;

			// Get all of the the boundary intersection points.
			List<Vector3D> iPoints = GetIntersectionPoints( ray ).ToList();

			// Keep only the positive, distinct ones.
			iPoints = iPoints.Where( v => v.X > p.X ).Distinct( new HighToleranceVectorEqualityComparer() ).ToList();

			// Even number of intersection points means we're outside, odd means inside
			bool inside = Utils.Odd( iPoints.Count );
			return inside;
		}
	}

	/// <summary>
	/// For simple comparison of two polygons.
	/// Warning!  This will only check that they contain the same set of vertices,
	/// though the order of the vertices in the two polygons may be arbitrary.
	/// </summary>
	public class PolygonEqualityComparer : IEqualityComparer<Polygon>
	{
		public bool Equals( Polygon poly1, Polygon poly2 )
		{
			Vector3D[] orderedVerts1 = poly1.Vertices.OrderBy( v => v, new Vector3DComparer() ).ToArray();
			Vector3D[] orderedVerts2 = poly2.Vertices.OrderBy( v => v, new Vector3DComparer() ).ToArray();
			if( orderedVerts1.Length != orderedVerts2.Length )
				return false;

			for( int i = 0; i < orderedVerts1.Length; i++ )
				if( orderedVerts1[i] != orderedVerts2[i] )
					return false;

			return true;
		}

		public int GetHashCode( Polygon poly )
		{
			// Is this ok? (I'm assuming ^ operator commutes, and order of applying doesn't matter)
			int hCode = 0;
			foreach( Vector3D v in poly.Vertices )
				hCode = hCode ^ v.GetHashCode();

			return hCode.GetHashCode();
		}
	}

	public static class FunPolygons
	{
		public static Polygon Heart()
		{
			Polygon newPoly = new Polygon();
			double size = 0.12;
			double angle = -3 * Math.PI / 2;

			Vector3D p1 = new Vector3D( 0, -1.5 * size );
			Vector3D p2 = new Vector3D( -size, 0 );
			Vector3D p3 = new Vector3D( -size / 2, size );
			Vector3D p4 = new Vector3D( 0, size / 2 );
			Vector3D p5 = new Vector3D( size / 2, size );
			Vector3D p6 = new Vector3D( size, 0 );
			p1.RotateXY( angle );
			p2.RotateXY( angle );
			p3.RotateXY( angle );
			p4.RotateXY( angle );
			p5.RotateXY( angle );
			p6.RotateXY( angle );

			newPoly.Segments.Add( Segment.Line( p1, p2 ) );
			newPoly.Segments.Add( Segment.Arc( p2, p3, p4 ) );
			newPoly.Segments.Add( Segment.Arc( p4, p5, p6 ) );
			newPoly.Segments.Add( Segment.Line( p6, p1 ) );
			return newPoly;
		}
	}
}