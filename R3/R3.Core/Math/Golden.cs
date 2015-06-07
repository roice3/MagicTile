namespace R3.Math
{
	using Math = System.Math;
	using R3.Geometry;
	using System.Diagnostics;

	public struct GoldenVector4D
	{
		public GoldenVector4D( Golden x, Golden y, Golden z, Golden u )
			: this()
		{
			X = x;
			Y = y;
			Z = z;
			U = u;
		}

		public Golden X { get; set; }
		public Golden Y { get; set; }
		public Golden Z { get; set; }
		public Golden U { get; set; }

		/// <summary>
		/// This is here because parameterless constructor leads to 0/0 Fractions.
		/// I should find a better way to deal with this (maybe these all just need to be classes).
		/// </summary>
		public static GoldenVector4D Origin()
		{
			Golden g = new Golden( new Fraction( 0 ), new Fraction( 0 ) );
			return new GoldenVector4D( g, g, g, g );
		}

		public static GoldenVector4D operator +( GoldenVector4D v1, GoldenVector4D v2 )
		{
			return new GoldenVector4D( v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z, v1.U + v2.U );
		}

		public static bool operator ==( GoldenVector4D v1, GoldenVector4D v2 )
		{
			object o1 = (object)v1;
			object o2 = (object)v2;

			if( (o1 == null && o2 == null) )
				return true;

			if( (o1 == null || o2 == null) )
				return false;

			return ( v1.X == v2.X &&
					 v1.Y == v2.Y &&
					 v1.Z == v2.Z &&
					 v1.U == v2.U );
		}

		public static bool operator !=( GoldenVector4D v1, GoldenVector4D v2 )
		{
			return !(v1 == v2);
		}

		public override bool Equals( object obj )
		{
			GoldenVector4D v = (GoldenVector4D)obj;
			return (v == this);
		}

		public override int GetHashCode()
		{
			return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode() ^ U.GetHashCode();
		}

		public void ReadVector( string line )
		{
			//string[] components = line.Split( '\t' );
			string[] components = line.Split( new char[] { '\t', ' ' }, System.StringSplitOptions.RemoveEmptyEntries );
			U = ReadComponent( components[0] );
			X = ReadComponent( components[1] );
			Y = ReadComponent( components[2] );
			Z = ReadComponent( components[3] );
		}

		private static Golden ReadComponent( string component )
		{
			component = component.Trim( '(', ')' );
			string[] split = component.Split( ',' );
			Fraction b = new Fraction( int.Parse( split[0] ) );
			Fraction a = new Fraction( int.Parse( split[1] ) );
			Golden result = new Golden( a, b );
			return result;
			//Golden scale = new Golden( new Fraction( 5 ), new Fraction( -3 ) );
			//Golden scale = new Golden( new Fraction( 1 ), new Fraction( 0 ) );
			//return result * scale;
		}

		public string WriteVector()
		{
			return
				WriteComponent( X ) + "\t" +
				WriteComponent( Y ) + "\t" +
				WriteComponent( Z ) + "\t" +
				WriteComponent( U );
		}

		private static string WriteComponent( Golden val )
		{
			string result = "(" + val.B.A + "/" + val.B.B + "," + val.A.A + "/" + val.A.B + ")";
			return result;
		}

		public bool ProjectPerspective()
		{
			Golden distance = new Golden( new Fraction( -4 ), new Fraction( 4 ) );
			Golden denominator = distance - U;

			double magSquared =
				X.GetAsDouble() * X.GetAsDouble() +
				Y.GetAsDouble() * Y.GetAsDouble() +
				Z.GetAsDouble() * Z.GetAsDouble() +
				U.GetAsDouble() * U.GetAsDouble();

			// The projection.
			Golden scale = new Golden( new Fraction( 1 ), new Fraction( 0 ) );
			Golden factor = (scale * distance) / denominator;

			// Fake projecting to infinity.
			if( denominator.IsZero() || denominator.GetAsDouble() < 0 )
				factor = new Golden( new Fraction( 1000 ), new Fraction( 0 ) );

			X *= factor;
			Y *= factor;
			Z *= factor;
			U = new Golden( new Fraction( 0 ), new Fraction( 0 ) );
			return true;
		}

		public bool ProjectOrthographic()
		{
			U = new Golden( new Fraction( 0 ), new Fraction( 0 ) );
			return true;
		}

		public bool IsOrigin
		{
			get
			{
				return X.IsZero() && Y.IsZero() && Z.IsZero() && U.IsZero();
			}
		}

		public Vector3D ConvertToReal()
		{
			return new Vector3D(
				X.GetAsDouble(),
				Y.GetAsDouble(),
				Z.GetAsDouble(),
				U.GetAsDouble() );
		}
	}

	/// <summary>
	/// Class for numbers in the golden field (of the form: A + golden*B)
	/// </summary>
	[System.Diagnostics.DebuggerDisplay( "{A.A}/{A.B}+g*{B.A}/{B.B}" )]
	public struct Golden
	{
		public Golden( Fraction a, Fraction b ) 
			: this()
		{
			A = a;
			B = b;
		}

		public static double tau = (1 + Math.Sqrt( 5 )) / 2;

		public Fraction A { get; private set; }
		public Fraction B { get; private set; }

		public static Golden operator +( Golden g1, Golden g2 )
		{
			return new Golden( g1.A + g2.A, g1.B + g2.B );
		}

		public static Golden operator -( Golden g1, Golden g2 )
		{
			return new Golden( g1.A - g2.A, g1.B - g2.B );
		}

		public static Golden operator *( Golden g1, Golden g2 )
		{
			return new Golden( g1.A*g2.A + g1.B*g2.B, g1.B*g2.A + g1.A*g2.B + g1.B*g2.B );
		}

		public static Golden operator /( Golden g1, Golden g2 )
		{
			return new Golden( (g1.A * g2.A + g1.A * g2.B - g1.B * g2.B) / Denom( g2 ), (g1.B * g2.A - g1.A * g2.B) / Denom( g2 ) );
		}

		public static bool operator ==( Golden g1, Golden g2 )
		{
			object o1 = (object)g1;
			object o2 = (object)g2;

			if( (o1 == null && o2 == null) )
				return true;

			if( (o1 == null || o2 == null) )
				return false;

			return ( g1.A == g2.B &&
					 g1.A == g2.B );
		}

		public static bool operator !=( Golden g1, Golden g2 )
		{
			return !(g1 == g2);
		}

		public override bool Equals( object obj )
		{
			Golden v = (Golden)obj;
			return (v == this);
		}

		public override int GetHashCode()
		{
			return A.GetHashCode() ^ B.GetHashCode();
		}

		private static Fraction Denom( Golden g )
		{
			return g.A*g.A + g.A*g.B - g.B*g.B;
		}

		public bool IsZero()
		{
			return A.A == 0 && B.A == 0;
		}

		public double GetAsDouble()
		{
			return A.GetAsDouble() + B.GetAsDouble() * tau;
		}
	}

	[System.Diagnostics.DebuggerDisplay( "{A}/{B}" )]
	public struct Fraction
	{
		public Fraction( int a, int b ) 
			: this()
		{
			A = a;
			B = b;
			Reduce();
		}

		public Fraction( int a ) 
			: this()
		{
			A = a;
			B = 1;
			Reduce();
		}

		public int A { get; private set; }
		public int B { get; private set; }

		public static Fraction operator +( Fraction f1, Fraction f2 )
		{
			f1.Reduce();
			f2.Reduce();
			Fraction result = new Fraction( f1.A*f2.B + f1.B*f2.A, f1.B*f2.B  );
			result.Reduce();
			return result;
		}

		public static Fraction operator -( Fraction f1, Fraction f2 )
		{
			f1.Reduce();
			f2.Reduce();
			Fraction result = new Fraction( f1.A * f2.B - f1.B * f2.A, f1.B * f2.B );
			result.Reduce();
			return result;
		}

		public static Fraction operator *( Fraction f1, Fraction f2 )
		{
			f1.Reduce();
			f2.Reduce();
			Fraction result = new Fraction( f1.A * f2.A, f1.B * f2.B );
			result.Reduce();
			return result;
		}

		public static Fraction operator /( Fraction f1, Fraction f2 )
		{
			f1.Reduce();
			f2.Reduce();
			Fraction result = new Fraction( f1.A * f2.B, f1.B * f2.A );
			result.Reduce();
			return result;
		}

		public static bool operator ==( Fraction f1, Fraction f2 )
		{
			f1.Reduce();
			f2.Reduce();

			object o1 = (object)f1;
			object o2 = (object)f2;

			if( (o1 == null && o2 == null) )
				return true;

			if( (o1 == null || o2 == null) )
				return false;

			return ( f1.A == f2.B &&
					 f1.A == f2.B );
		}

		public static bool operator !=( Fraction f1, Fraction f2 )
		{
			return !(f1 == f2);
		}

		public override bool Equals( object obj )
		{
			Fraction v = (Fraction)obj;
			return (v == this);
		}

		public override int GetHashCode()
		{
			return A.GetHashCode() ^ B.GetHashCode();
		}

		private void Reverse()
		{
			A *= -1;
			B *= -1;
		}

		public void Reduce()
		{
			// Two wrongs do make a right.
			if( A < 0 && B < 0 )
				Reverse();
			else if( B < 0 )
			{
				// Normalize so that A is always < 0 for negatives.
				Reverse();
			}

			if( 0 == B )
			{
				//Debug.Assert( false );
				A = 0;
				B = 1;
				return;
			}

			if( 0 == A )
			{
				B = 1;
				return;
			}

			int gcd = GCD( System.Math.Abs( A ), System.Math.Abs( B ) );
			A = A / gcd;
			B = B / gcd;
		}

		// a and b should be positive!
		public int GCD( int a, int b )
		{
			while( b > 0 )
			{
				int rem = a % b;
				a = b;
				b = rem;
			}
			return a;
		}

		public double GetAsDouble()
		{
			return (double)A / B;
		}
	}
}
