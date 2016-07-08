namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Numerics;
	using System.Runtime.Serialization;

	[DebuggerDisplay("X:{X} Y:{Y} Z:{Z} W:{W}")]
	[DataContract( Namespace = "" )]
	public struct Vector3D
	{
		public Vector3D( double x, double y, double z, double w )
			: this()
		{
			X = x;
			Y = y;
			Z = z;
			W = w;
		}

		public Vector3D( double x, double y, double z ) 
			: this()
		{
			X = x;
			Y = y;
			Z = z;
			W = 0;
		}

		public Vector3D( double x, double y )
			: this()
		{
			X = x;
			Y = y;
			Z = W = 0;
		}

		[DataMember]
		public double X { get; set; }
		[DataMember]
		public double Y { get; set; }
		[DataMember]
		public double Z { get; set; }
		[DataMember]
		public double W { get; set; }

		public override string ToString()
		{
			return string.Format( "{0},{1},{2},{3}", X, Y, Z, W );
		}

		public string Save()
		{
			return this.ToString();
		}

		public string ToStringXYZOnly()
		{
			return string.Format( "{0},{1},{2}", X, Y, Z );
		}

		public void Load( string s )
		{
			string[] split = s.Split( ',' );
			if( split.Length == 3 || split.Length == 4 )
			{
				X = double.Parse( split[0], CultureInfo.InvariantCulture );
				Y = double.Parse( split[1], CultureInfo.InvariantCulture );
				Z = double.Parse( split[2], CultureInfo.InvariantCulture );
			}

			if( split.Length == 4 )
			{
				W = double.Parse( split[3], CultureInfo.InvariantCulture );
			}
		}

		/// <summary>
		/// Implicit vector to complex conversion operator.
		/// </summary>>
		public static implicit operator Complex( Vector3D v ) 
		{
			return v.ToComplex();
		}

		public static bool operator ==( Vector3D v1, Vector3D v2 )
		{
			return v1.Compare( v2 );
		}

		public static bool operator !=( Vector3D v1, Vector3D v2 )
		{
			return !( v1 == v2 );
		}

		public override bool Equals( object obj )
		{
			Vector3D v = (Vector3D)obj;
			return ( v == this );
		}

		public override int GetHashCode()
		{
			return GetHashCode( Tolerance.Threshold );
		}

		public int GetHashCode( double tolerance )
		{
			// Normalize DNE vectors (since we consider any with any NaN component the same).
			if( this.DNE )
				return double.NaN.GetHashCode();

			// The hash code is dependent on the tolerance: more precision -> less rounding.
			// Rounding the hashcodes is necessary, since for a given tolerence we might 
			// consider two quantities to be equal, but their hashcodes might differ 
			// without the rounding.

			double inverse = 1 / tolerance;
			int decimals = (int)Math.Log10( inverse );

			return 
				Math.Round( X, decimals ).GetHashCode() ^ 
				Math.Round( Y, decimals ).GetHashCode() ^ 
				Math.Round( Z, decimals ).GetHashCode() ^
				Math.Round( W, decimals ).GetHashCode();

			// if 0 tolerance
			//return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
		}

		public bool Compare( Vector3D other, double threshold )
		{
			// NOTE: This is here because when the vector is infinite, it fails the tolerance checks below.
			if( ( X == other.X ) && ( Y == other.Y ) && ( Z == other.Z ) && W == other.W )
				return true;

			if( this.DNE && other.DNE )
				return true;

			if( this.DNE || other.DNE )
				return false;

			return ( Tolerance.Equal( X, other.X, threshold ) &&
					 Tolerance.Equal( Y, other.Y, threshold ) &&
					 Tolerance.Equal( Z, other.Z, threshold ) &&
					 Tolerance.Equal( W, other.W, threshold ) );
		}

		public bool Compare( Vector3D other )
		{
			return Compare( other, Tolerance.Threshold );
		}

		public static Vector3D operator *( Vector3D v, double s )
		{
			return new Vector3D( v.X*s, v.Y*s, v.Z*s, v.W*s );
		}

		public static Vector3D operator *( double s, Vector3D v )
		{
			return v*s;
		}

		public static Vector3D operator /( Vector3D v, double s )
		{
			return new Vector3D( v.X/s, v.Y/s, v.Z/s, v.W/s );
		}

		public void Divide( double s )
		{
			X /= s;
			Y /= s;
			Z /= s;
			W /= s;
		}

		public static Vector3D operator +( Vector3D v1, Vector3D v2 )
		{
			return new Vector3D( v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z, v1.W + v2.W );
		}

		public static Vector3D operator -( Vector3D v )
		{
			return new Vector3D( -v.X, -v.Y, -v.Z, -v.W );
		}

		public static Vector3D operator -( Vector3D v1, Vector3D v2 )
		{
			return v1 + ( -v2 );
		}

		public void Round( int digits )
		{
			for( int i=0; i<3; i++ )
				this[i] = Math.Round( this[i], digits );
		}

		public double this[int i]
		{
			get
			{
				switch( i )
				{
					case 0:
						return this.X;
					case 1:
						return this.Y;
					case 2:
						return this.Z;
					case 3:
						return this.W;
				}

				throw new System.ArgumentException();
			}
			set
			{
				switch( i )
				{
					case 0:
						this.X = value;
						break;
					case 1:
						this.Y = value;
						break;
					case 2:
						this.Z = value;
						break;
					case 3:
						this.W = value;
						break;
				}
			}
		}

		public bool Valid()
		{
			// ZZZ - This is what I did in MagicTile, but what about infinities?.
			// ZZZ - Make a property
			return ( !double.IsNaN( X ) &&
					 !double.IsNaN( Y ) &&
					 !double.IsNaN( Z ) &&
					 !double.IsNaN( W ) );
		}

		public bool DNE
		{
			get
			{
				return
					double.IsNaN( X ) ||
					double.IsNaN( Y ) ||
					double.IsNaN( Z ) ||
					double.IsNaN( W );
			}
		}

		public static Vector3D DneVector()
		{
			return new Vector3D( double.NaN, double.NaN, double.NaN, double.NaN );
		}

		public void Empty()
		{
			X = Y = Z = W = 0;
		}

		public bool Normalize()
		{
			double magnitude = Abs();
			if( Tolerance.Zero( magnitude ) )
				return false;
			Divide( magnitude );
			return true;
		}

		/// <summary>
		/// Normalize and scale.
		/// </summary>
		public bool Normalize( double scale )
		{
			if( !Normalize() )
				return false;

			this *= scale;
			return true;
		}

		public double MagSquared()
		{
			return X*X + Y*Y + Z*Z + W*W;
		}

		public double Abs()
		{
			return Math.Sqrt( MagSquared() );
		}

		public bool IsOrigin
		{
			get
			{
				return this == new Vector3D();
			}
		}

		public double Dist( Vector3D v )
		{
			return ( this - v ).Abs();
		}

		public double Dot( Vector3D v )
		{
			return ( X*v.X + Y*v.Y + Z*v.Z + W*v.W );
		}

		/// <summary>
		/// 3D cross product.
		/// 4th component does not enter into calculations.
		/// </summary>
		public Vector3D Cross( Vector3D v )
		{
			double xVal = Y * v.Z - Z * v.Y;
			double yVal = Z * v.X - X * v.Z;
			double zVal = X * v.Y - Y * v.X;
			return new Vector3D( xVal, yVal, zVal );
		}

		/// <summary>
		/// Rotate CCW in the XY plane by an angle in radians.
		/// </summary>
		public void RotateXY( double angle )
		{
			double component1 = X;
			double component2 = Y;

			X = Math.Cos( angle ) * component1 - Math.Sin( angle ) * component2; 
			Y = Math.Sin( angle ) * component1 + Math.Cos( angle ) * component2;
		}

		/// <summary>
		/// Rotate CCW in the XY plane about a center.  Angle is in radians.
		/// </summary>
		public void RotateXY( Vector3D center, double angle )
		{
			this -= center;
			RotateXY( angle );
			this += center;
		}

		// NOTE: angle should be in radians.
		public void RotateAboutAxis( Vector3D axis, double angle )
		{
			// normalize the axis
			axis.Normalize();
			double _x = axis.X;
			double _y = axis.Y;
			double _z = axis.Z;

			// build the rotation matrix - I got this from http://www.makegames.com/3dRotation/
			double c = Math.Cos( angle );
			double s = -1 * Math.Sin( angle );
			double t = 1 - c;
			double[,] mRot = new double[,]
			{
				{ t*_x*_x + c,		t*_x*_y - s*_z, t*_x*_z + s*_y },
				{ t*_x*_y + s*_z,	t*_y*_y + c,	t*_y*_z - s*_x },
				{ t*_x*_z - s*_y,	t*_y*_z + s*_x, t*_z*_z + c },
			};

			double x = this.X;
			double y = this.Y;
			double z = this.Z;

			// do the multiplication
			this = new Vector3D(
				mRot[0,0] * x + mRot[1,0] * y + mRot[2,0] * z,
				mRot[0,1] * x + mRot[1,1] * y + mRot[2,1] * z,
				mRot[0,2] * x + mRot[1,2] * y + mRot[2,2] * z );
		}

		/// <summary>
		/// Unsigned (not handed) angle between 0 and pi.
		/// </summary>
		public double AngleTo( Vector3D p2 )
		{
			double magmult = Abs() * p2.Abs();
			if( Tolerance.Zero( magmult ) )
				return 0;

			// Make sure the val we take acos() of is in range.
			// Floating point errors can make us slightly off and cause acos() to return bad values.
			double val = Dot( p2 ) / magmult;
			if( val > 1 )
			{	
				Debug.Assert( Tolerance.Zero( 1 - val ) );
				val = 1;
			}
			if( val < -1 )
			{
				Debug.Assert( Tolerance.Zero( -1 - val ) );
				val = -1;
			}

			return( Math.Acos( val ) );
		}

		/// <summary>
		/// Finds a perpendicular vector (just one of many possible).
		/// Result will be normalized.
		/// </summary>
		public Vector3D Perpendicular()
		{
			if( this.IsOrigin )
				return new Vector3D();

			Vector3D perp = this.Cross( new Vector3D( 0, 0, 1 ) );

			// If we are a vector on the z-axis, the above will result in the zero vector.
			if( perp.IsOrigin )
				perp = this.Cross( new Vector3D( 1, 0, 0 ) );

			if( !perp.Normalize() )
				throw new System.Exception( "Failed to find perpendicular." );

			return perp;
		}

		/// <summary>
		/// 4D -> 3D projection.
		/// The "safe" part is that we won't make any points invalid (only large).
		/// </summary>
		public Vector3D ProjectTo3DSafe( double cameraDist )
		{
			const double minDenom = 0.0001;	// The safe part.

			double denominator = cameraDist - W;
			if( Tolerance.Zero( denominator ) )
				denominator = minDenom;
			if( denominator < 0 )
				denominator = minDenom;

			Vector3D result = new Vector3D(
				X * cameraDist / denominator,
				Y * cameraDist / denominator,
				Z * cameraDist / denominator, 0 );
			return result;
		}

		/// <summary>
		/// 3D -> 2D projection.
		/// </summary>
		public Vector3D CentralProject( double cameraDist )
		{
			double denominator = cameraDist - Z;
			if( Tolerance.Zero( denominator ) )
				denominator = 0;

			// Make points with a negative denominator invalid.
			if( denominator < 0 )
				denominator = 0;

			Vector3D result = new Vector3D(
				X * cameraDist / denominator,
				Y * cameraDist / denominator,
				0 );
			return result;
		}

		public Complex ToComplex()
		{
			return new Complex( X, Y );
		}

		public static Vector3D FromComplex( Complex value )
		{
			return new Vector3D( value.Real, value.Imaginary );
		}
	}

	/// <summary>
	/// For comparing vectors (for ordering, etc.)
	/// NOTE: I made the comparison tolerance safe.
	/// </summary>
	public class Vector3DComparer : IComparer<Vector3D>
	{
		public int Compare( Vector3D v1, Vector3D v2 )
		{
			const int less = -1;
			const int greater = 1;

			if( Tolerance.LessThan( v1.X, v2.X ) )
				return less;
			if( Tolerance.GreaterThan( v1.X, v2.X ) )
				return greater;

			if( Tolerance.LessThan( v1.Y, v2.Y ) )
				return less;
			if( Tolerance.GreaterThan( v1.Y, v2.Y ) )
				return greater;

			if( Tolerance.LessThan( v1.Z, v2.Z ) )
				return less;
			if( Tolerance.GreaterThan( v1.Z, v2.Z ) )
				return greater;

			if( Tolerance.LessThan( v1.W, v2.W ) )
				return less;
			if( Tolerance.GreaterThan( v1.W, v2.W ) )
				return greater;

			// Making it here means we are equal.
			return 0;
		}
	}
}
