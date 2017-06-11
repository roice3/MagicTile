namespace R3.Math
{
	using Math = System.Math;

	using R3.Geometry;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Numerics;
	using System.Runtime.Serialization;

	[DebuggerDisplay( "A:{A} B:{B} C:{C} D:{D}" )]
	[DataContract( Namespace = "" )]
	public struct Mobius : ITransform
	{
		public Mobius( Complex a, Complex b, Complex c, Complex d ) 
			: this()
		{
			A = a;
			B = b;
			C = c;
			D = d;
		}

		/// <summary>
		/// This transform will map z1 to Zero, z2, to One, and z3 to Infinity.
		/// </summary>
		public Mobius( Complex z1, Complex z2, Complex z3 )
			: this()
		{
			MapPoints( z1, z2, z3 );
		}

		[DataMember]
		public Complex A { get; private set; }
		[DataMember]
		public Complex B { get; private set; }
		[DataMember]
		public Complex C { get; private set; }
		[DataMember]
		public Complex D { get; private set; }

		public override string ToString()
		{
			return string.Format( "A:{0} B:{1} C:{2} D:{3}", A, B, C, D );
		}

		public static Mobius operator *( Mobius m1, Mobius m2 )
		{
			Mobius result = new Mobius(
				m1.A * m2.A + m1.B * m2.C,
				m1.A * m2.B + m1.B * m2.D,
				m1.C * m2.A + m1.D * m2.C,
				m1.C * m2.B + m1.D * m2.D );

			result.Normalize();
			return result;
		}

		/// <summary>
		/// Normalize so that ad - bc = 1
		/// </summary>
		public void Normalize()
		{
			// See Visual Complex Analysis, p150
			Complex k = Complex.Reciprocal( Complex.Sqrt( A*D - B*C ) );
			ScaleComponents( k );
		}

		public void ScaleComponents( Complex k )
		{
			A *= k;
			B *= k;
			C *= k;
			D *= k;
		}

		public Complex Trace
		{
			get
			{
				return ( A + D );
			}
		}

		public Complex TraceSquared
		{
			get
			{
				return Trace * Trace;
			}
		}

		/// <summary>
		/// This will calculate the Mobius transform that represents an isometry in the given geometry.
		/// The isometry will rotate CCW by angle A about the origin, then translate the origin to P (and -P to the origin).
		/// </summary>
		public void Isometry( Geometry g, double angle, Complex P )
		{
			// As Don notes in the hypebolic case:
			// Any isometry of the Poincare disk can be expressed as a complex function of z of the form:
			// (T*z + P)/(1 + conj(P)*T*z), where T and P are complex numbers, |P| < 1 and |T| = 1.
			// This indicates a rotation by T around the origin followed by moving the origin to P (and -P to the origin).
			// 
			// I figured out that the other cases can be handled with simple variations of the C coefficients.
			Complex T = new Complex( Math.Cos( angle ), Math.Sin( angle ) );
			A = T;
			B = P;
			D = 1;

			switch( g )
			{
				case Geometry.Spherical:
				{
					C = Complex.Conjugate( P ) * T * -1;
					break;
				}
				case Geometry.Euclidean:
				{
					C = 0;
					break;
				}
				case Geometry.Hyperbolic:
				{
					C = Complex.Conjugate( P ) * T;
					break;
				}
			}
		}

		/// <summary>
		/// The identity Mobius transformation.
		/// </summary>
		public void Unity()
		{
			A = Complex.One;
			B = Complex.Zero;
			C = Complex.Zero;
			D = Complex.One;
		}

		/// <summary>
		/// The pure translation (i.e. moves the origin straight in some direction) that takes p1 to p2.
		/// I borrowed this from Don's hyperbolic applet.
		/// </summary>
		public void PureTranslation( Geometry g, Complex p1, Complex p2 )
		{
			Complex A = p2 - p1;
			Complex B = p2 * p1;
			double denom = 1 - (B.Real*B.Real + B.Imaginary*B.Imaginary);
			Complex P = new Complex(
					( A.Real * ( 1 + B.Real ) + A.Imaginary * B.Imaginary ) / denom,
					( A.Imaginary * ( 1 - B.Real ) + A.Real * B.Imaginary ) / denom );
			Isometry( g, 0, P );
			Normalize();
		}

		/// <summary>
		/// Move from a point p1 -> p2 along a geodesic.
		/// Also somewhat from Don.
		/// factor can be used to only go some fraction of the distance from p1 to p2.
		/// </summary>
		public void Geodesic( Geometry g, Complex p1, Complex p2, double factor = 1.0 )
		{
			Mobius t = new Mobius();
			t.Isometry( g, 0, p1 * -1 );
			Complex p2t = t.Apply( p2 );

			// Only implemented for hyperbolic so far.
			if( factor != 1.0 && g == Geometry.Hyperbolic )
			{
				double newMag = DonHatch.h2eNorm( DonHatch.e2hNorm( p2t.Magnitude ) * factor );
				Vector3D temp = Vector3D.FromComplex( p2t );
				temp.Normalize();
				temp *= newMag;
				p2t = temp.ToComplex();
			}

			Mobius m1 = new Mobius(), m2 = new Mobius();
			m1.Isometry( g, 0, p1 * -1 );
			m2.Isometry( g, 0, p2t );
			Mobius m3 = m1.Inverse();
			this = m3 * m2 * m1;
		}

		public void Hyperbolic( Geometry g, Complex fixedPlus, double scale )
		{
			// To the origin.
			Mobius m1 = new Mobius();
			m1.Isometry( g, 0, fixedPlus * -1 );

			// Scale.
			Mobius m2 = new Mobius();
			m2.A = scale;
			m2.B = m2.C = 0;
			m2.D = 1;

			// Back.
			//Mobius m3 = m1.Inverse();	// Doesn't work well if fixedPlus is on disk boundary.
			Mobius m3 = new Mobius();
			m3.Isometry( g, 0, fixedPlus );

			// Compose them (multiply in reverse order).
			this = m3 * m2 * m1;
		}

		/// <summary>
		/// Allow a hyperbolic transformation using an absolute offset.
		/// offset is specified in the respective geometry.
		/// </summary>
		public void Hyperbolic2( Geometry g, Complex fixedPlus, Complex point, double offset )
		{
			// To the origin.
			Mobius m = new Mobius();
			m.Isometry( g, 0, fixedPlus * -1 );
			double eRadius = m.Apply( point ).Magnitude;

			double scale = 1;
			switch( g )
			{
				case Geometry.Spherical:
					double sRadius = Spherical2D.e2sNorm( eRadius );
					sRadius += offset;
					scale = Spherical2D.s2eNorm( sRadius ) / eRadius;
					break;
				case Geometry.Euclidean:
					scale = (eRadius + offset) / eRadius;
					break;
				case Geometry.Hyperbolic:
					double hRadius = DonHatch.e2hNorm( eRadius );
					hRadius += offset;
					scale = DonHatch.h2eNorm( hRadius ) / eRadius;
					break;
			}

			Hyperbolic( g, fixedPlus, scale );
		}

		public void Elliptic( Geometry g, Complex fixedPlus, double angle )
		{
			// To the origin.
			Mobius origin = new Mobius();
			origin.Isometry( g, 0, fixedPlus * -1 );

			// Rotate.
			Mobius rotate = new Mobius();
			rotate.Isometry( g, angle, new Complex() );

			// Conjugate.
			this = origin.Inverse() * rotate * origin;
		}

		/// <summary>
		/// This will transform the unit disk to the upper half plane.
		/// </summary>
		public void UpperHalfPlane()
		{
			MapPoints( -Complex.ImaginaryOne, Complex.One, Complex.ImaginaryOne );
		}

		/// <summary>
		/// This transform will map z1 to Zero, z2 to One, and z3 to Infinity.
		/// http://en.wikipedia.org/wiki/Mobius_transformation#Mapping_first_to_0.2C_1.2C_.E2.88.9E
		/// If one of the zi is ∞, then the proper formula is obtained by first 
		/// dividing all entries by zi and then taking the limit zi → ∞
		/// </summary>
		public void MapPoints( Complex z1, Complex z2, Complex z3 )
		{
			if( Infinity.IsInfinite( z1 ) )
			{
				A = 0;
				B = -1 * (z2 - z3);
				C = -1;
				D = z3;
			}
			else if( Infinity.IsInfinite( z2 ) )
			{
				A = 1;
				B = -z1;
				C = 1;
				D = -z3;
			}
			else if( Infinity.IsInfinite( z3 ) )
			{
				A = -1;
				B = z1;
				C = 0;
				D = -1 * (z2 - z1);
			}
			else
			{
				A = z2 - z3;
				B = -z1 * (z2 - z3);
				C = z2 - z1;
				D = -z3 * (z2 - z1);
			}

			Normalize();
		}

		/// <summary>
		/// This transform will map the z points to the respective w points.
		/// </summary>
		public void MapPoints( Complex z1, Complex z2, Complex z3, Complex w1, Complex w2, Complex w3 )
		{
			Mobius m1 = new Mobius(), m2 = new Mobius();
			m1.MapPoints( z1, z2, z3 );
			m2.MapPoints( w1, w2, w3 );
			this =  m2.Inverse() * m1;
		}

		/// <summary>
		/// Applies a Mobius transformation to a vector.
		/// </summary>
		/// <remarks>Use the complex number version if you can.</remarks>
		public Vector3D Apply( Vector3D z )
		{
			Complex cInput = z;
			Complex cOutput = Apply( cInput );
			return Vector3D.FromComplex( cOutput );
		}

		/// <summary>
		/// Applies a Mobius transformation to a complex number.
		/// </summary>
		public Complex Apply( Complex z )
		{
			return ( ( A*z + B ) / ( C*z + D ) );
		}

		public Vector3D ApplyInfiniteSafe( Vector3D z )
		{
			return Vector3D.FromComplex( ApplyInfiniteSafe( z.ToComplex() ) );
		}

		public Complex ApplyInfiniteSafe( Complex z )
		{
			if( Infinity.IsInfinite( z ) )
				return ApplyToInfinite();

			Complex result = Apply( z );
			if( Infinity.IsInfinite( result ) )
				return Infinity.InfinityVector2D;

			return result;
		}

		/// <summary>
		/// Applies a Mobius transformation to the point at infinity.
		/// </summary>
		public Vector3D ApplyToInfinite()
		{
			if( C == 0 )
				return Infinity.InfinityVector2D;

			return Vector3D.FromComplex( A / C );
		}

		/// <summary>
		/// Applies a Mobius transformation to a quaternion with a zero k component (handled as a vector).
		/// The complex Mobius coefficients are treated as quaternions with zero j,k values.
		/// This is also infinity safe.
		/// </summary>
		public Vector3D ApplyToQuaternion( Vector3D q )
		{
			if( Infinity.IsInfinite( q ) )
				return ApplyToInfinite(); // Is this ok?

			Vector3D a = Vector3D.FromComplex( A );
			Vector3D b = Vector3D.FromComplex( B );
			Vector3D c = Vector3D.FromComplex( C );
			Vector3D d = Vector3D.FromComplex( D );

			return DivideQuat( MultQuat( a, q ) + b, MultQuat( c, q ) + d );
		}

		private Vector3D MultQuat( Vector3D a, Vector3D b )
		{
			return new Vector3D(
				a.X*b.X - a.Y*b.Y - a.Z*b.Z - a.W*b.W,
				a.X*b.Y + a.Y*b.X + a.Z*b.W - a.W*b.Z,
				a.X*b.Z - a.Y*b.W + a.Z*b.X + a.W*b.Y,
				a.X*b.W + a.Y*b.Z - a.Z*b.Y + a.W*b.X );
		}

		private Vector3D DivideQuat( Vector3D a, Vector3D b )
		{
			double magSquared = b.MagSquared();
			Vector3D bInv = new Vector3D( b.X / magSquared, -b.Y / magSquared, -b.Z / magSquared, -b.W / magSquared );
			return MultQuat( a, bInv );
		}

		/// <summary>
		/// Returns a new Mobius transformation that is the inverse of us.
		/// </summary>
		public Mobius Inverse()
		{
			// See http://en.wikipedia.org/wiki/Möbius_transformation
			Mobius result = new Mobius( D, -B, -C, A );
			result.Normalize();
			return result;
		}

		public static Mobius Identity()
		{
			Mobius m = new Mobius();
			m.Unity();
			return m;
		}

		public static Mobius Scale( double scale )
		{
			return new Mobius( scale, Complex.Zero, Complex.Zero, Complex.One );
		}

		/// <summary>
		/// This is only here for a numerical accuracy hack.
		/// Please don't make a habit of using!
		/// </summary>
		public void Round( int digits )
		{
			int d = digits;
			A = new Complex( Math.Round( A.Real, d ), Math.Round( A.Imaginary, d ) );
			B = new Complex( Math.Round( B.Real, d ), Math.Round( B.Imaginary, d ) );
			C = new Complex( Math.Round( C.Real, d ), Math.Round( C.Imaginary, d ) );
			D = new Complex( Math.Round( D.Real, d ), Math.Round( D.Imaginary, d ) );
		}
	}

	public class MobiusEqualityComparer : IEqualityComparer<Mobius>
	{
		public bool Equals( Mobius m1, Mobius m2 )
		{
			m1.Normalize(); m2.Normalize();
			return
				Vector3D.FromComplex( m1.A ).Equals( Vector3D.FromComplex( m2.A ) ) &&
				Vector3D.FromComplex( m1.B ).Equals( Vector3D.FromComplex( m2.B ) ) &&
				Vector3D.FromComplex( m1.C ).Equals( Vector3D.FromComplex( m2.C ) ) &&
				Vector3D.FromComplex( m1.D ).Equals( Vector3D.FromComplex( m2.D ) );
		}

		public int GetHashCode( Mobius m )
		{
			m.Normalize();
			return
				Vector3D.FromComplex( m.A ).GetHashCode() ^
				Vector3D.FromComplex( m.B ).GetHashCode() ^
				Vector3D.FromComplex( m.C ).GetHashCode() ^
				Vector3D.FromComplex( m.D ).GetHashCode();
		}
	}
}
