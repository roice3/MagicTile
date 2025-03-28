namespace MagicTile
{
	using R3.Geometry;
	using R3.Math;
	using System.Collections.Generic;
	using System.Linq;
	using Math = System.Math;

	/// <summary>
	/// A class to hold the definition for a hyperbolic pair of pants.
	/// https://en.wikipedia.org/wiki/Pair_of_pants_(mathematics)
	/// </summary>
	public class Pants : ITransformable
	{
		public static Polygon TemplateHex = null;

		public Pants()
		{
			Hexagon = new Polygon();
			Isometry = new Isometry();
		}

		public Pants Clone()
		{
			Pants pants = new Pants();
			pants.Hexagon = Hexagon.Clone();
			pants.TestCircle = TestCircle.Clone();
			pants.CircumCircle = CircumCircle.Clone();
			return pants;
		}

		/// <summary>
		/// From wikipedia, "Formally, a pair of pants consists of two hexagonal fundamental polygons stitched together at every other side"
		/// This representation will be convenient for our use for these (a new kind of twist).
		/// We'll actually only store one of the hexagons, and will reflect it across it's sides to get the other.
		/// So this requires the two fundamental polygons be identical (which I'm not sure if is required in general).
		/// </summary>
		public Polygon Hexagon { get; set; }

		/// <summary>
		/// The net combination of isometries used during puzzle building.
		/// </summary>
		public Isometry Isometry { get; private set; }

		/// <summary>
		/// Test circles we use to help find affected cells/stickers.
		/// It's important that these are NE (non-euclidean) circles.
		/// </summary>
		public CircleNE TestCircle { get; private set; }
		public CircleNE CircumCircle { get; private set; }

		public void Transform( Mobius m )
		{
			Hexagon.Transform( m );
			TestCircle.Transform( m );
			CircumCircle.Transform( m );
			Isometry *= new Isometry( m, null );
		}

		public void Transform( Isometry i )
		{
			Hexagon.Transform( i );
			TestCircle.Transform( i );
			CircumCircle.Transform( i );
			Isometry *= i;
		}

		/// <summary>s
		/// Checks if the point is inside our hexagon,
		/// but takes advantage of things we know to make it faster.
		/// The generic polygon check is way too slow.
		/// This is meant to be used during puzzle building.
		/// </summary>
		/// <param name="p"></param>
		/// <returns></returns>
		public bool IsPointInsideOptimized( Vector3D p )
		{
			if( !CircumCircle.IsPointInsideFast( p ) )
				return false;

			// We will check that we are on the same side of every segment as the center.
			Vector3D cen = Hexagon.Center;
			foreach( Segment s in Hexagon.Segments )
			{
				if( s.Type == SegmentType.Line )
				{
					if( !Euclidean2D.SameSideOfLine( s.P1, s.P2, cen, p ) )
						return false;
				}
				else
				{
					bool inside1 = s.Circle.IsPointInside( cen );
					bool inside2 = s.Circle.IsPointInside( p );
					if( inside1 ^ inside2 )
						return false;
				}
			}

			return true;
		}

		public IEnumerable<CircleNE> HexagonEdges
		{
			get
			{
				foreach( Segment seg in Hexagon.Segments )
				{
					Circle c = seg.Circle;
					yield return new CircleNE()
					{
						Center = c.Center,
						Radius = c.Radius,
						P1 = c.P1,
						P2 = c.P2,
						CenterNE = Hexagon.Center
					};
				}
			}
		}

		public IEnumerable<Polygon> AdjacentHexagons
		{
			get
			{
				for( int i=0; i<6; i+= 2 )
				{
					Polygon clone = Hexagon.Clone();
					clone.Reflect( Hexagon.Segments[i] );
					yield return clone;
				}
			}
		}

		/// <summary>
		/// Return the segment where we chop in an earthquake
		/// </summary>
		public int ChoppedPantsSeg( Vector3D p )
		{
			int closestGeodesic = ClosestGeodesicSeg( p );
			return ChoppedPantsSeg( closestGeodesic );
		}

		public static int ChoppedPantsSeg( int closestGeodesicSeg )
		{
			if( closestGeodesicSeg == 1 )
				return 4;
			if( closestGeodesicSeg == 3 )
				return 0;
			if( closestGeodesicSeg == 5 )
				return 2;
			return -1;
		}

		/// 
		/// <summary>
		/// Return the index of the closest pant geodesic.
		/// Index is of the underlying hexagon segments.
		/// </summary>
		public int ClosestGeodesicSeg( Vector3D p )
		{
			// Needs to be non-euclidean calc,
			// Moving the hex to the center will make that be the case.
			Mobius m = MobiusToCenter;
			Polygon poly = Hexagon.Clone();
			poly.Transform( m );
			p = m.Apply( p );

			double d1 = poly.Segments[1].Midpoint.Dist( p );
			double d2 = poly.Segments[3].Midpoint.Dist( p );
			double d3 = poly.Segments[5].Midpoint.Dist( p );
			double min = Math.Min( d1, Math.Min( d2, d3 ) );

			// UGH, why are these remapped to different indices????
			// Not liking my past self right now.
			if( min == d1 )
				return 1;
			if( min == d2 )
				return 3;
			if( min == d3 )
				return 5;
			return -1;
		}

		private Mobius MobiusToCenter
		{
			get
			{
				Mobius m = new Mobius();
				m.Isometry( Geometry.Hyperbolic, 0, -Hexagon.Center );
				return m;
			}
		}

		public Vector3D TinyOffset( int towardSeg )
		{
			int awayFromSeg = ChoppedPantsSeg( towardSeg );

			// Center;
			Mobius m = MobiusToCenter;
			Vector3D p = Hexagon.Center;
			Polygon poly = Hexagon.Clone();
			poly.Transform( m );
			p = m.Apply( p );

			// Do the offset.
			p -= poly.Segments[awayFromSeg].Midpoint / 10;

			// Go back.
			p = m.Inverse().Apply( p );
			return p;
		}

		public void SetupHexagonForKQ()
		{
			Polygon centralTile = new Polygon();
			centralTile.CreateRegular( 7, 3 );
			Vector3D vertex0 = centralTile.Segments[0].P1;

			CircleNE[] otherThreeSides = OtherThreeSides();
			CircleNE[] systoles = SystolesForKQ();

			// Calc verts.
			List<Vector3D> verts = new List<Vector3D>();
			Vector3D t1, t2;
			Euclidean2D.IntersectionCircleCircle( otherThreeSides[0], systoles[0], out t1, out t2 );
			Vector3D intersection = t1.Abs() < 1 ? t1 : t2;
			verts.Add( intersection );
			intersection.Y *= -1;
			verts.Add( intersection );
			Mobius m = RotMobius( vertex0 );
			verts.Add( m.Apply( verts[0] ) );
			verts.Add( m.Apply( verts[1] ) );
			verts.Add( m.Apply( verts[2] ) );
			verts.Add( m.Apply( verts[3] ) );

			// Setup all the segments.
			bool clockwise = true;
			Hexagon.Segments.AddRange( new Segment[] 
			{
				Segment.Arc( verts[0], verts[1], otherThreeSides[0].Center, clockwise ),
				Segment.Arc( verts[1], verts[2], systoles[1].Center, clockwise ),
				Segment.Arc( verts[2], verts[3], otherThreeSides[1].Center, clockwise ),
				Segment.Arc( verts[3], verts[4], systoles[2].Center, clockwise ),
				Segment.Arc( verts[4], verts[5], otherThreeSides[2].Center, clockwise ),
				Segment.Arc( verts[5], verts[0], systoles[0].Center, clockwise ),
			} );
			Hexagon.Center = vertex0;

			// Setup the test circle.
			m.Isometry( Geometry.Hyperbolic, 0, -vertex0 );
			Polygon clone = Hexagon.Clone();
			clone.Transform( m );

			Circle temp = new Circle(
				clone.Segments[0].Midpoint,
				clone.Segments[2].Midpoint,
				clone.Segments[4].Midpoint );
			CircleNE tempNE = new CircleNE( temp, new Vector3D() );
			tempNE.Transform( m.Inverse() );
			TestCircle = tempNE;

			temp = new Circle(
				clone.Segments[0].P1,
				clone.Segments[1].P1,
				clone.Segments[2].P1 );
			tempNE = new CircleNE( temp, new Vector3D() );
			tempNE.Transform( m.Inverse() );
			CircumCircle = tempNE;
		}

		/// <summary>
		/// This will setup systolic pants for the Klein Quartic.
		/// </summary>
		public static CircleNE[] SystolesForKQ()
		{
			// 0th vertex of the central heptagon will be the center of our first hexagon.
			// Arnaud's applet is helpful to think about this.
			// http://www.math.univ-toulouse.fr/~cheritat/AppletsDivers/Klein/

			Polygon centralTile = new Polygon();
			centralTile.CreateRegular( 7, 3 );
			Vector3D vertex0 = centralTile.Segments[0].P1;
			Vector3D mid1 = centralTile.Segments[1].Midpoint;
			Vector3D mid2 = centralTile.Segments[2].Midpoint;

			Circle3D orthogonal;
			H3Models.Ball.OrthogonalCircleInterior( mid1, mid2, out orthogonal );

			// We make the non-euclidean center the center of the hexagon.
			CircleNE c1 = new CircleNE() { Center = orthogonal.Center, Radius = orthogonal.Radius, CenterNE = vertex0 };
			return CycleCircles( c1, vertex0 );
		}

		private static CircleNE[] OtherThreeSides()
		{
			Polygon centralTile = new Polygon();
			centralTile.CreateRegular( 7, 3 );
			Vector3D vertex0 = centralTile.Segments[0].P1;
			Vector3D p1 = centralTile.Segments[3].P1;
			Vector3D p2 = centralTile.Segments[3].P2;

			Circle3D orthogonal;
			H3Models.Ball.OrthogonalCircleInterior( p1, p2, out orthogonal );

			// We make the non-euclidean center the center of the hexagon.
			CircleNE c1 = new CircleNE() { Center = orthogonal.Center, Radius = orthogonal.Radius, CenterNE = vertex0 };
			CircleNE[] otherThreeSides = CycleCircles( c1, vertex0 );
			return otherThreeSides;
		}

		private static Mobius RotMobius( Vector3D vertex0 )
		{
			// A third rotation about the vertex.
			Mobius m = new Mobius();
			m.Elliptic( Geometry.Hyperbolic, vertex0, 2 * Math.PI / 3 );
			return m;
		}

		private static CircleNE[] CycleCircles( CircleNE template, Vector3D vertex0 )
		{
			Mobius m = RotMobius( vertex0 );
			List<CircleNE> result = new List<CircleNE>();
			result.Add( template );
			for( int i = 0; i < 2; i++ )
			{
				CircleNE next = result.Last().Clone();
				next.Transform( m );
				result.Add( next );
			}

			return result.ToArray();
		}
	}
}
