namespace R3.Geometry
{
	using R3.Math;
	using System;
	using System.Numerics;

	public enum SphericalModel
	{
		Sterographic,
		Gnomonic
	}

	public class SphericalModels
	{
		public static Vector3D StereoToGnomonic( Vector3D p )
		{
			Vector3D sphere = Sterographic.PlaneToSphere( p );

			// We can't only represent the lower hemisphere.
			if( sphere.Z >= 0 )
			{
				sphere.Z = 0;
				sphere.Normalize();
				sphere *= Infinity.FiniteScale;
				return sphere;
			}

			double z = sphere.Z;
			sphere.Z = 0;
			return -sphere * m_gScale / z;
		}

		public static Vector3D GnomonicToStereo( Vector3D g )
		{
			g /= m_gScale;
			double dot = g.Dot( g ); // X^2 + Y^2
			double z = -1 / Math.Sqrt( dot + 1 );
			return g*z / (z - 1);
		}

		private static double m_gScale = 0.5;
	}
}
