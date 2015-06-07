namespace R3.Core
{
	using R3.Geometry;

	public static class Tolerance
	{
		//public static readonly double Threshold = 0.0000001;
		public static readonly double Threshold = 0.000001;	// Made less strict to avoid some problems near Poincare boundary.

		public static bool Equal( double d1, double d2 )
		{
			return Zero( d1 - d2 );
		}

		public static bool Zero( double d )
		{
			return Zero( d, Threshold );
		}

		public static bool LessThan( double d1, double d2 )
		{
			return LessThan( d1, d2, Threshold );
		}

		public static bool GreaterThan( double d1, double d2 )
		{
			return GreaterThan( d1, d2, Threshold );
		}

		public static bool LessThanOrEqual( double d1, double d2 )
		{
			return d1 <= ( d2 + Threshold );
		}

		public static bool GreaterThanOrEqual( double d1, double d2 )
		{
			return d1 > ( d2 + Threshold );
		}

		public static bool Equal( double d1, double d2, double threshold )
		{
			return Zero( d1 - d2, threshold );
		}

		public static bool Zero( double d, double threshold )
		{
			return ( ( d > -threshold ) && ( d < threshold ) ) ? true : false;
		}

		public static bool LessThan( double d1, double d2, double threshold )
		{
			return d1 < ( d2 - threshold );
		}

		public static bool GreaterThan( double d1, double d2, double threshold )
		{
			return d1 > ( d2 + threshold );
		}
	}

	public static class Utils
	{
		/// <summary>
		/// Converts a value from degrees to radians.
		/// </summary>
		public static double DegreesToRadians( double value )
		{
			return ( value / 180 * System.Math.PI );
		}

		/// <summary>
		/// Converts a value from radians to degrees.
		/// </summary>
		public static double RadiansToDegrees( double value )
		{
			return ( value / System.Math.PI * 180 );
		}

		// ZZZ - Make templated
		public static bool Even( int value )
		{
			return 0 == value % 2;
		}

		// ZZZ - Make templated
		public static bool Odd( int value )
		{
			return !Even( value );
		}

		public static void SwapPoints( ref Vector3D p1, ref Vector3D p2 )
		{
			Swap<Vector3D>( ref p1, ref p2 );
		}

		public static void Swap<Type>( ref Type t1, ref Type t2 )
		{
			Type t = t1;
			t1 = t2;
			t2 = t;
		}
	}
}
