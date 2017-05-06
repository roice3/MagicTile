namespace R3.Geometry
{
	using R3.Math;
	using System;
	using System.Numerics;

	public enum SphericalModel
	{
		Sterographic,
		Gnomonic,
		Fisheye,
		HemisphereDisks,
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

		public static Vector3D ToDisks( Vector3D p )
		{
			if( p.Abs() <= 1 )
			{
				p.X -= 1;
				return p;
			}

			Mobius m = new Mobius();
			m.Elliptic( Geometry.Spherical, Complex.ImaginaryOne, Math.PI );
			p = m.Apply( p );
			p.X += 1;
			return p;
		}

		public static Vector3D FromDisks( Vector3D p, bool normalize = false )
		{
			if( p.X <= 0 )
			{
				p.X += 1;
				if( normalize || p.Abs() > 1 )
					p.Normalize();
				return p;
			}

			p.X -= 1;
			Mobius m = new Mobius();
			m.Elliptic( Geometry.Spherical, Complex.ImaginaryOne, Math.PI );
			p = m.Apply( p );
			if( normalize || p.Abs() > 1 )
				p.Normalize();
			return p;
		}

		private static double m_gScale = 0.5;
	}
}
