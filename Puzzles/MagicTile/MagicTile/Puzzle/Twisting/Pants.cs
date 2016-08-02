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
	public class Pants
	{
		public Pants()
		{
			SetupHexagonForKQ();
		}

		/// <summary>
		/// From wikipedia, "Formally, a pair of pants consists of two hexagonal fundamental polygons stitched together at every other side"
		/// This representation will be convenient for our use for these (a new kind of twist).
		/// We'll actually only store one of the hexagons, and will reflect it across it's sides to get the other.
		/// So this requires the two fundamental polygons be identical (which I'm not sure if is required in general).
		/// </summary>
		Polygon Hexagon { get; set; }

		private void SetupHexagonForKQ()
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
			Hexagon.Segments.AddRange( new Segment[] {
				Segment.Arc( verts[0], verts[1], otherThreeSides[0].Center, clockwise ),
				Segment.Arc( verts[1], verts[2], systoles[0].Center, clockwise ),
				Segment.Arc( verts[2], verts[3], otherThreeSides[1].Center, clockwise ),
				Segment.Arc( verts[3], verts[4], systoles[1].Center, clockwise ),
				Segment.Arc( verts[4], verts[5], otherThreeSides[2].Center, clockwise ),
				Segment.Arc( verts[5], verts[0], systoles[2].Center, clockwise ),
			} );
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
			for( int i = 0; i < 3; i++ )
			{
				CircleNE next = result.Last().Clone();
				next.Transform( m );
				result.Add( next );
			}

			return result.ToArray();
		}
	}
}
