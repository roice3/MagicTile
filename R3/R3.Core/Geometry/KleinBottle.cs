namespace R3.Geometry
{
	using R3.Math;
	using Math = System.Math;

	public class KleinBottle
	{
		/// <summary>
		/// Maps a vector in the unit square to a point on the "Clifford" Klein Bottle.
		/// Does not currently check the input point, which should live in the unit square.
		/// This assumes the left/right edges of the square are glued in reversing orientation.
		/// </summary>
		public static Vector3D MapToLawson( Vector3D v, bool smoothShear )
		{
			v = Shear( v, smoothShear );
			v.X *= 2 * Math.PI;
			v.Y *= Math.PI;

			// Lawson equatio 7.1, http://www.math.jhu.edu/~js/Math646/lawson.s3.pdf
			double x = v.Y;
			double y = v.X;
			int m = 1;
			int k = 2;
			Vector3D result = new Vector3D(
				Math.Cos( x * m ) * Math.Cos( y ),
				Math.Sin( x * m ) * Math.Cos( y ),
				Math.Cos( x * k ) * Math.Sin( y ),
				Math.Sin( x * k ) * Math.Sin( y )
			);

			// Put the bottle in a nicer starting orientation.
			result = m_rot.RotateVector( result );

			return result;

			/*
			// https://en.wikipedia.org/wiki/Klein_bottle#4-D_non-intersecting
			double a1 = v.X / 1.03;
			double a2 = v.Y / 1.03;
			double r = Math.Sqrt( 0.5 );
			double p = r;

			Vector3D result = new Vector3D(
				p * Math.Cos( a1 ),
				p * Math.Sin( a1 ),
				r * (Math.Cos( a1 ) * Math.Cos( a2 ) - Math.Sin( a1 ) * Math.Sin( 2 * a2 )),	// altered this, and this paramaterization gives a twisted torus.
				r * (Math.Sin( a1 ) * Math.Cos( a2 ) + Math.Cos( a1 ) * Math.Sin( 2 * a2 ))
			);
			return result; */
		}

		private static Matrix4D m_rot = Matrix4D.MatrixToRotateinCoordinatePlane( -Math.PI / 4, 0, 3 );

		private static Vector3D Shear( Vector3D v, bool smooth )
		{
			Vector3D result = v;

			if( smooth )
				v.X -= (Math.Sin( Math.PI / 2 * (2 * v.Y - 1) ) + 1) * 0.25;    // Smooth shear from 0 to 1
			else
				v.X -= v.Y * 0.5;

			if( v.X < 0 )
				v.X += 1;
			if( v.X > 1 )
				v.X -= 1;
			return v;
		}
	}
}
