namespace R3.Geometry
{
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Math = System.Math;

	public enum EHoneycomb
	{
		// Euclidean
		H434,

		// Hyperbolic
		H435,
		H534,
		H535,
		H353,

		// From here down, the exotic ones.
		H336,
		H436,
		H536,
		H344,

		// From here down, the really exotic ones.
		H444,
		H363,
		H636,

		// Lorentzian
		H337,
		H33I,
		// ...
	}

	public class Honeycomb
	{
		public static string String( EHoneycomb honeycomb, bool dual )
		{
			switch( honeycomb )
			{
				case EHoneycomb.H435:
					return dual ? "{5,3,4}" : "{4,3,5}";
				case EHoneycomb.H534:
					return dual ? "{4,3,5}" : "{5,3,4}";
				case EHoneycomb.H535:
					return "{5,3,5}";
				case EHoneycomb.H353:
					return "{3,5,3}";
				case EHoneycomb.H336:
					return dual ? "{6,3,3}" : "{3,3,6}";
				case EHoneycomb.H436:
					return dual ? "{6,3,4}" : "{4,3,6}";
				case EHoneycomb.H536:
					return dual ? "{6,3,5}" : "{5,3,6}";
				case EHoneycomb.H344:
					return dual ? "{4,4,3}" : "{3,4,4}";
				case EHoneycomb.H444:
					return "{4,4,4}";
				case EHoneycomb.H636:
					return "{6,3,6}";
				case EHoneycomb.H363:
					return "{3,6,3}";
				case EHoneycomb.H337:
					return dual ? "{7,3,3}" : "{3,3,7}";
				case EHoneycomb.H33I:
					return dual ? "{inf,3,3}" : "{3,3,inf}";
			}

			throw new System.ArgumentException( "Unknown honeycomb type" );
		}

		public static void PQR( EHoneycomb honeycomb, out int p, out int q, out int r )
		{
			switch( honeycomb )
			{
				case EHoneycomb.H435:
					p = 4; q = 3; r = 5; return;
				case EHoneycomb.H534:
					p = 5; q = 3; r = 4; return;
				case EHoneycomb.H535:
					p = 5; q = 3; r = 5; return;
				case EHoneycomb.H353:
					p = 3; q = 5; r = 3; return;
				case EHoneycomb.H336:
					p = 3; q = 3; r = 6; return;
				case EHoneycomb.H436:
					p = 4; q = 3; r = 6; return;
				case EHoneycomb.H536:
					p = 5; q = 3; r = 6; return;
				case EHoneycomb.H344:
					p = 3; q = 4; r = 4; return;
				case EHoneycomb.H444:
					p = 4; q = 4; r = 4; return;
				case EHoneycomb.H363:
					p = 3; q = 6; r = 3; return;
				case EHoneycomb.H636:
					p = 6; q = 3; r = 6; return;
				case EHoneycomb.H337:
					p = 3; q = 3; r = 7; return;
				case EHoneycomb.H33I:
					p = 3; q = 3; r = -1; return;
			}

			throw new System.ArgumentException();
		}

		public static double InRadius( EHoneycomb honeycomb )
		{
			int p, q, r;
			PQR( honeycomb, out p, out q, out r );
			return InRadius( p, q, r );
		}

		public static double MidRadius( EHoneycomb honeycomb )
		{
			int p, q, r;
			PQR( honeycomb, out p, out q, out r );
			return MidRadius( p, q, r );
		}

		public static double CircumRadius( EHoneycomb honeycomb )
		{
			int p, q, r;
			PQR( honeycomb, out p, out q, out r );
			return CircumRadius( p, q, r );
		}

		public static double EdgeLength( EHoneycomb honeycomb )
		{
			int p, q, r;
			PQR( honeycomb, out p, out q, out r );
			return EdgeLength( p, q, r );
		}

		public static Geometry GetGeometry( int p, int q, int r )
		{
			double t1 = Math.Sin( PiOverNSafe( p ) ) * Math.Sin( PiOverNSafe( r ) );
			double t2 = Math.Cos( PiOverNSafe( q ) );

			if( Tolerance.Equal( t1, t2 ) )
				return Geometry.Euclidean;

			if( Tolerance.GreaterThan( t1, t2 ) )
				return Geometry.Spherical;

			return Geometry.Hyperbolic;
		}

		/// <summary>
		/// Returns the in-radius, in the induced geometry.
		/// </summary>
		public static double InRadius( int p, int q, int r )
		{
			double pip = PiOverNSafe( p );
			double pir = PiOverNSafe( r );

			double pi_hpq = Pi_hpq( p, q );
			double inRadius = Math.Sin( pip ) * Math.Cos( pir ) / Math.Sin( pi_hpq );

			switch( GetGeometry( p, q, r ) )
			{
				case Geometry.Hyperbolic:
					return DonHatch.acosh( inRadius );
				case Geometry.Spherical:
					return Math.Acos( inRadius );
			}

			throw new System.NotImplementedException();
		}

		/// <summary>
		/// Returns the mid-radius, in the induced geometry.
		/// </summary>
		public static double MidRadius( int p, int q, int r )
		{
			double pir = PiOverNSafe( r );

			double inRadius = InRadius( p, q, r );
			double midRadius = DonHatch.sinh( inRadius ) / Math.Sin( pir );

			switch( GetGeometry( p, q, r ) )
			{
				case Geometry.Hyperbolic:
					return DonHatch.asinh( midRadius );
				case Geometry.Spherical:
					return Math.Asin( midRadius );
			}

			throw new System.NotImplementedException();
		}

		/// <summary>
		/// Returns the circum-radius, in the induced geometry.
		/// </summary>
		public static double CircumRadius( int p, int q, int r )
		{
			double pip = PiOverNSafe( p );
			double piq = PiOverNSafe( q );
			double pir = PiOverNSafe( r );

			double pi_hpq = Pi_hpq( p, q );
			double pi_hqr = Pi_hpq( q, r );
			double circumRadius = Math.Cos( pip ) * Math.Cos( piq ) * Math.Cos( pir ) / ( Math.Sin( pi_hpq ) * Math.Sin( pi_hqr ) );

			switch( GetGeometry( p, q, r ) )
			{
				case Geometry.Hyperbolic:
					return DonHatch.acosh( circumRadius );
				case Geometry.Spherical:
					return Math.Acos( circumRadius );
			}

			throw new System.NotImplementedException();
		}

		public static double EdgeLength( int p, int q, int r )
		{
			double pip = PiOverNSafe( p );
			double pir = PiOverNSafe( r );

			double pi_hqr = Pi_hpq( q, r );
			double edgeLength = 2 * DonHatch.acosh( Math.Cos( pip ) * Math.Sin( pir ) / Math.Sin( pi_hqr ) );
			return edgeLength;
		}

		private static double Pi_hpq( int p, int q )
		{
			double pi = Math.PI;
			double pip = PiOverNSafe( p );
			double piq = PiOverNSafe( q );

			double temp = Math.Pow( Math.Cos( pip ), 2 ) + Math.Pow( Math.Cos( piq ), 2 );
			double hab = pi / Math.Acos( Math.Sqrt( temp ) );

			// Infinity safe.
			double pi_hpq = pi / hab;
			if( Infinity.IsInfinite( hab ) )
				pi_hpq = 0;

			return pi_hpq;
		}

		public static double PiOverNSafe( int n )
		{
			return n == -1 ? 0 : Math.PI / n;
		}
	}
}
