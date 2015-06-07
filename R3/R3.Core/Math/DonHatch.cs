namespace R3.Math
{
	using Math = System.Math;

	// This code from Don Hatch.
	public static class DonHatch
	{
		public static double 
		expm1( double x )
		{
			double u = Math.Exp(x);
			if (u == 1.0)
				return x;
			if (u-1.0 == -1.0)
				return -1;
			return (u-1.0)*x/Math.Log(u);
		}

		public static double
		log1p( double x )
		{
			double u = 1.0+x;
			return Math.Log(u) - ((u-1.0)-x)/u;
		}

		public static double
		tanh( double x )
		{
			double u = expm1(x);
			return u / (u*(u+2.0)+2.0) * (u+2.0);
		}

		public static double
		atanh( double x )
		{
			return .5 * log1p(2.0*x/(1.0-x));
		}

		public static double
		sinh( double x )
		{
			double u = expm1( x );
			return .5 * u / ( u + 1 ) * ( u + 2 );
		}

		public static double
		asinh( double x )
		{
			return log1p(x * (1.0 + x / (Math.Sqrt(x*x+1.0)+1.0)));
		}

		public static double
		cosh( double x )
		{
			double e_x = Math.Exp(x);
			return (e_x + 1.0/e_x) * .5;
		}

		public static double
		acosh( double x )
		{
			return 2 * Math.Log( Math.Sqrt( ( x + 1 ) * .5 ) + Math.Sqrt( ( x - 1 ) * .5 ) );
		}

		// hyperbolic to euclidean norm (distance from 0,0) in Poincare disk.
		public static double 
		h2eNorm( double hNorm )
		{
			if( double.IsNaN( hNorm ) )
				return 1.0;
			return tanh(.5*hNorm);
		}

		public static double 
	    e2hNorm( double eNorm )
		{
			return 2*atanh(eNorm);
		}
	}
}
