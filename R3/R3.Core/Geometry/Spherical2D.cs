namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using System.Diagnostics;

	public static class Spherical2D
	{
		// The next two methods mimic Don's stuff for the hyperbolic plane.
		public static double
		s2eNorm( double sNorm )
		{
			//if( double.IsNaN( sNorm ) )
			//	return 1.0;
			return Math.Tan( .5 * sNorm );
		}

		public static double
		e2sNorm( double eNorm )
		{
			return 2 * Math.Atan( eNorm );
		}

		/// <summary>
		/// Sphere geometry is implicit.  A radius 1 sphere with the center at the origin.
		/// </summary>
		public static Vector3D SphereToPlane( Vector3D spherePoint )
		{
			Vector3D projected = spherePoint.CentralProject( 1.0 );
			return projected;
		}

		/// <summary>
		/// Sphere geometry is implicit.  A radius 1 sphere with the center at the origin.
		/// </summary>
		public static Vector3D PlaneToSphere( Vector3D planePoint )
		{
			planePoint.Z = 0;	// Just to be safe.
			double magSquared = planePoint.MagSquared();
			Vector3D result = new Vector3D(
				2 * planePoint.X / ( 1.0 + magSquared ),
				2 * planePoint.Y / ( 1.0 + magSquared ),
				( magSquared - 1.0 ) / ( magSquared + 1.0 ) );
			return result;
		}

		/// <summary>
		/// Calculates the two poles of a great circle defined by two points.
		/// </summary>
		public static void GreatCirclePole( Vector3D sphereCenter, Vector3D p1, Vector3D p2,
			out Vector3D pole1, out Vector3D pole2 )
		{
			double sphereRadius = p1.Dist( sphereCenter );
			Debug.Assert( Tolerance.Equal( sphereRadius, p2.Dist( sphereCenter ) ) );

			Vector3D v1 = p1 - sphereCenter;
			Vector3D v2 = p2 - sphereCenter;
			pole1 = v1.Cross( v2 ) + sphereCenter;
			pole2 = v2.Cross( v1 ) + sphereCenter;
		}

		/// <summary>
		/// Same as above, but with implicit sphere geometry.  A radius 1 sphere with the center at the origin.
		/// </summary>
		public static void GreatCirclePole( Vector3D p1, Vector3D p2,
			out Vector3D pole1, out Vector3D pole2 )
		{
			GreatCirclePole( new Vector3D( 0, 0, 0 ), p1, p2, out pole1, out pole2 );
		}
	}
}
