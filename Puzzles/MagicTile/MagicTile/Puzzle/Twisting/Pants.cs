namespace MagicTile
{
	using R3.Geometry;
	using R3.Math;
	using Math = System.Math;

	/// <summary>
	/// A class to hold the definition for a hyperbolic pair of pants.
	/// https://en.wikipedia.org/wiki/Pair_of_pants_(mathematics)
	/// </summary>
	public class Pants
	{
		/// <summary>
		/// From wikipedia, "Formally, a pair of pants consists of two hexagonal fundamental polygons stitched together at every other side"
		/// This representation will be convenient for our use for these (a new kind of twist).
		/// We'll actually only store one of the hexagons, and will reflect it across it's sides to get the other.
		/// So this requires the two fundamental polygons be identical (which I'm not sure if is required in general).
		/// </summary>
		Polygon Hexagon { get; set; }

		/// <summary>
		/// This will setup systolic pants for the Klein Quartic.
		/// </summary>
		public static CircleNE[] PantsCirclesForKQ()
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

			// The other two circles just rotate this one about the first vertex.
			Mobius m = new Mobius();
			m.Elliptic( Geometry.Hyperbolic, vertex0, 2 * Math.PI / 3 );

			CircleNE c2 = c1.Clone();
			c2.Transform( m );
			CircleNE c3 = c2.Clone();
			c3.Transform( m );

			return new CircleNE[] { c1, c2, c3 };
		}
	}
}
