namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using R3.Math;
	using System.Diagnostics;

	public enum Geometry
	{
		Spherical,
		Euclidean,
		Hyperbolic
	}

	public static class PlatonicSolids
	{
		public static int NumFacets( int p, int q )
		{
			if( p == 3 && q == 3 )
				return 4;
			else if( p == 4 && q == 3 )
				return 6;
			else if( p == 5 && q == 3 )
				return 12;
			else if( p == 3 && q == 4 )
				return 8;
			else if( p == 3 && q == 5 )
				return 20;
			else
				throw new System.ArgumentException();
		}
	}

	public static class Geometry2D
	{
		// Returns the geometry induced by a polygon with p points, q meeting at a vertex.
		public static Geometry GetGeometry( int p, int q )
		{
			double test = 1.0 / p + 1.0 / q;
			if( test > 0.5 )
				return Geometry.Spherical;
			else if( test == 0.5 )
				return Geometry.Euclidean;

			return Geometry.Hyperbolic;
		}

		private static double EuclideanHypotenuse = 1.0 / 3;	// ZZZ - ??????????
		public static double DiskRadius = 1;

		public static double GetNormalizedCircumRadius( int p, int q )
		{
			Geometry g = Geometry2D.GetGeometry( p, q );

			double hypot = GetTriangleHypotenuse( p, q );

			switch( g )
			{
				case Geometry.Spherical:
					return Spherical2D.s2eNorm( hypot ) * DiskRadius;

				case Geometry.Euclidean:
					return EuclideanHypotenuse;

				case Geometry.Hyperbolic:
					return DonHatch.h2eNorm( hypot ) * DiskRadius;
			}

			Debug.Assert( false );
			return 1;
		}

		/// <summary>
		/// In the induced geometry.
		/// </summary>
		public static double GetTriangleHypotenuse( int p, int q )
		{
			Geometry g = Geometry2D.GetGeometry( p, q );
			if( g == Geometry.Euclidean )
				return EuclideanHypotenuse;

			// We have a 2,q,p triangle, where the right angle alpha 
			// is opposite the hypotenuse (the length we want).
			double alpha = Math.PI / 2;
			double beta = Math.PI / q;
			double gamma = Math.PI / p;
			return GetTriangleSide( g, alpha, beta, gamma );
		}

		/// <summary>
		/// Get the side length opposite angle PI/P,
		/// In the induced geometry.
		/// </summary>
		public static double GetTrianglePSide( int p, int q )
		{
			Geometry g = Geometry2D.GetGeometry( p, q );

			double alpha = Math.PI / 2;
			double beta = Math.PI / q;
			double gamma = Math.PI / p;	// The one we want.
			if( g == Geometry.Euclidean )
				return EuclideanHypotenuse * Math.Sin( gamma );
			return GetTriangleSide( g, gamma, beta, alpha );
		}

		/// <summary>
		/// Get the side length opposite angle PI/Q,
		/// In the induced geometry.
		/// </summary>
		public static double GetTriangleQSide( int p, int q )
		{
			Geometry g = Geometry2D.GetGeometry( p, q );

			double alpha = Math.PI / 2;
			double beta = Math.PI / q;	// The one we want.
			double gamma = Math.PI / p;
			if( g == Geometry.Euclidean )
				return EuclideanHypotenuse * Math.Sin( beta );

			return GetTriangleSide( g, beta, gamma, alpha );
		}

		/// <summary>
		/// Get the length of the side of a triangle opposite alpha, given the three angles of the triangle.
		/// NOTE: This does not work in Euclidean geometry!
		/// </summary>
		public static double GetTriangleSide( Geometry g, double alpha, double beta, double gamma )
		{
			switch( g )
			{
				case Geometry.Spherical:
					{
						// Spherical law of cosines
						return Math.Acos( ( Math.Cos( alpha ) + Math.Cos( beta ) * Math.Cos( gamma ) ) / ( Math.Sin( beta ) * Math.Sin( gamma ) ) );
					}
				case Geometry.Euclidean:
					{
						// Not determined in this geometry.
						Debug.Assert( false );
						return 0.0;
					}
				case Geometry.Hyperbolic:
					{
						// Hyperbolic law of cosines
						// http://en.wikipedia.org/wiki/Hyperbolic_law_of_cosines
						return DonHatch.acosh( ( Math.Cos( alpha ) + Math.Cos( beta ) * Math.Cos( gamma ) ) / ( Math.Sin( beta ) * Math.Sin( gamma ) ) );
					}
			}

			// Not determined in this geometry.
			Debug.Assert( false );
			return 0.0;
		}


	}

}
