namespace R3.Geometry
{
	using Math = System.Math;
	using System.Diagnostics;
	using System.Linq;
	using R3.Core;

	public class VectorND
	{
		public VectorND( int dimension )
		{
			X = new double[dimension];
		}

		public VectorND( double[] components )
		{
			X = components;
		}

		public VectorND Clone()
		{
			return new VectorND( (double[])this.X.Clone() );
		}

		public int Dimension 
		{
			get { return X.Length; }
			set { X = new double[value]; }
		}

		public double[] X { get; set; }

		public static VectorND operator /( VectorND v, double s )
		{
			double[] components = new double[v.Dimension];

			for( int i = 0; i < components.Length; i++ )
				components[i] = v.X[i] / s;

			return new VectorND( components );
		}

		public void Divide( double s )
		{
			for( int i = 0; i < Dimension; i++ )
				X[i] /= s;
		}

		public static VectorND operator *( VectorND v, double s )
		{
			double[] components = new double[v.Dimension];

			for( int i = 0; i < components.Length; i++ )
				components[i] = v.X[i] * s;

			return new VectorND( components );
		}

		public static VectorND operator *( double s, VectorND v )
		{
			return v * s;
		}

		public static VectorND operator +( VectorND v1, VectorND v2 )
		{
			Debug.Assert( v1.Dimension == v2.Dimension );
			double[] components = new double[v1.Dimension];

			for( int i = 0; i < components.Length; i++ )
				components[i] = v1.X[i] + v2.X[i];

			return new VectorND( components );
		}

		public static VectorND operator -( VectorND v )
		{
			double[] components = new double[v.Dimension];

			for( int i = 0; i < components.Length; i++ )
				components[i] = -v.X[i];

			return new VectorND( components );
		}

		public static VectorND operator -( VectorND v1, VectorND v2 )
		{
			return v1 + ( -v2 );
		}

		public double Dot( VectorND v )
		{
			double dot = 0;
			for( int i = 0; i < this.Dimension; i++ )
				dot += this.X[i] * v.X[i];
			return dot;
		}

		public bool Normalize()
		{
			double magnitude = Abs;
			if( Tolerance.Zero( magnitude ) )
				return false;
			Divide( magnitude );
			return true;
		}

		public double MagSquared
		{
			get
			{
				double result = 0;
				foreach( double x in this.X )
					result += x * x;
				return result;
			}
		}

		public double Abs
		{
			get
			{
				return Math.Sqrt( MagSquared );
			}
		}

		public double Dist( VectorND v )
		{
			return ( this - v ).Abs;
		}

		public bool IsOrigin
		{
			get
			{
				foreach( double x in this.X )
					if( !Tolerance.Zero( x ) )
						return false;
				return true;
			}
		}

		/// <summary>
		/// 4D -> 3D projection.
		/// </summary>
		public VectorND ProjectTo3D( double cameraDist )
		{
			double denominator = cameraDist - X[3];
			if( Tolerance.Zero( denominator ) )
				denominator = 0;

			// Make points with a negative denominator invalid.
			if( denominator < 0 )
				denominator = 0;

			VectorND result = new VectorND( new double[] {
				X[0] * cameraDist / denominator,
				X[1] * cameraDist / denominator,
				X[2] * cameraDist / denominator, 0  });
			return result;
		}

		/// <summary>
		/// 3D -> 2D projection.
		/// </summary>
		public VectorND ProjectTo2D( double cameraDist )
		{
			double denominator = cameraDist - X[2];
			if( Tolerance.Zero( denominator ) )
				denominator = 0;

			// Make points with a negative denominator invalid.
			if( denominator < 0 )
				denominator = 0;

			VectorND result = new VectorND( new double[] {
				X[0] * cameraDist / denominator,
				X[1] * cameraDist / denominator,
				0, 0 } );
			return result;
		}
	}
}
