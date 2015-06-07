namespace R3.Geometry
{
	using R3.Geometry;
	using Math = System.Math;

	public static class Sterographic
	{
		public static Vector3D PlaneToSphereSafe( Vector3D planePoint )
		{
			if( Infinity.IsInfinite( planePoint ) )
				return new Vector3D( 0, 0, 1 );

			return PlaneToSphere( planePoint );
		}

		public static Vector3D PlaneToSphere( Vector3D planePoint )
		{
			planePoint.Z = 0;
			double dot = planePoint.Dot( planePoint ); // X^2 + Y^2
			return new Vector3D(
				2 * planePoint.X / ( dot + 1 ),
				2 * planePoint.Y / ( dot + 1 ),
				( dot - 1 ) / ( dot + 1 ) );
		}

		public static Vector3D SphereToPlane( Vector3D spherePoint )
		{
			double z = spherePoint.Z;
			return new Vector3D(
				spherePoint.X / ( 1 - z ),
				spherePoint.Y / ( 1 - z ) );
		}

		/// <summary>
		/// See http://en.wikipedia.org/wiki/Poincar%C3%A9_disk_model#Relation_to_the_hyperboloid_model
		/// </summary>
		public static Vector3D PlaneToHyperboloid( Vector3D planePoint )
		{
			double temp = planePoint.X * planePoint.X + planePoint.Y * planePoint.Y;
			return new Vector3D(
				2*planePoint.X / ( 1 - temp ),
				2*planePoint.Y / ( 1 - temp ),
				( 1 + temp ) / ( 1 - temp ) );
		}

		public static Vector3D HyperboloidToPlane( Vector3D hyperboloidPoint )
		{
			double z = hyperboloidPoint.Z;
			return new Vector3D(
				hyperboloidPoint.X / ( 1 + z ),
				hyperboloidPoint.Y / ( 1 + z ) );
		}

		public static void NormalizeToHyperboloid( ref Vector3D v )
		{
			double normSquared = v.Z * v.Z - ( v.X * v.X + v.Y * v.Y );
			double norm = Math.Sqrt( normSquared );
			v /= norm;
		}
	}
}
