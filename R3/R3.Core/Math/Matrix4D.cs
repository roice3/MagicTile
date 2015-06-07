namespace R3.Math
{
	using R3.Geometry;
	using System;
	using Math = System.Math;

	public class Matrix4D
	{
		public Matrix4D()
		{
			Initialize();
		}

		public Matrix4D( double[,] data )
		{
			Initialize();
			for( int i=0; i<4; i++ )
			for( int j=0; j<4; j++ )
				Data[i][j] = data[i,j];
		}

		private void Initialize()
		{
			Data = new double[4][];
			for( int i=0; i<4; i++ )
				Data[i] = new double[4];
		}

		public double[][] Data { get; set; }

		public Matrix4D Clone()
		{
			Matrix4D result = new Matrix4D();
			for( int i=0; i<4; i++ )
			for( int j=0; j<4; j++ )
				result.Data[i][j] = this.Data[i][j];
			return result;
		}

		public static Matrix4D Identity()
		{
			Matrix4D result = new Matrix4D();
			for( int i=0; i<4; i++ )
				result[i,i] = 1;
			return result;
		}

		/// <summary>
		/// Mixing multidim and jagged array notation here, but whatevs.
		/// </summary>
		public double this[int i, int j]
		{
			get
			{
				return Data[i][j];
			}
			set
			{
				Data[i][j] = value;
			}
		}

		public VectorND this[int i]
		{
			get
			{
				return new VectorND( Data[i] );
			}
			set
			{
				Data[i] = value.X;
			}
		}

		public static Matrix4D operator +( Matrix4D m1, Matrix4D m2 )
		{
			Matrix4D result = new Matrix4D();
			for( int i=0; i<4; i++ )
			for( int j=0; j<4; j++ )
				result[i, j] = m1[i, j] + m2[i, j];
			return result;
		}

		public static Matrix4D operator *( Matrix4D m1, Matrix4D m2 )
		{
			Matrix4D result = new Matrix4D();
			for( int i=0; i<4; i++ )
			for( int j=0; j<4; j++ )
			for( int k=0; k<4; k++ )
				result[i, j] += m1[i, k] * m2[k, j];
			return result;
		}

		public static Matrix4D operator *( Matrix4D m, double s )
		{
			Matrix4D result = new Matrix4D();
			for( int i=0; i<4; i++ )
			for( int j=0; j<4; j++ )
				result[i, j] = m[i, j] * s;
			return result;
		}

		public static Matrix4D Transpose( Matrix4D m )
		{
			Matrix4D result = new Matrix4D();
			for( int i=0; i<4; i++ )
			for( int j=0; j<4; j++ )
				result[i, j] = m[j, i];
			return result;
		}

		/// <summary>
		/// Gram-Schmidt orthonormalize
		/// </summary>
		public static Matrix4D GramSchmidt( Matrix4D input )
		{
			Matrix4D result = input;
			for( int i=0; i<4; i++ )
			{
				for( int j=0; j<i; j++ )
				{
					// result[j] is already unit length...
					// result[i] -= (result[i] dot result[j])*result[j]
					VectorND iVec = result[i];
					VectorND jVec = result[j];
					iVec -= ( iVec.Dot( jVec ) ) * jVec;
					result[i] = iVec;
				}
				result[i].Normalize();
			}

			return result;
		}

		/// <summary>
		/// Gram-Schmidt orthonormalize
		/// </summary>
		public static Matrix4D GramSchmidt( Matrix4D input,
			Func<VectorND, VectorND, double> innerProduct, Func<VectorND, VectorND> normalize )
		{
			Matrix4D result = input;
			for( int i=0; i<4; i++ )
			{
				for( int j=i+1; j<4; j++ )
				{
					VectorND iVec = result[i];
					VectorND jVec = result[j];
					iVec -= innerProduct( iVec, jVec ) * jVec;
					result[i] = iVec;
				}
				result[i] = normalize( result[i] );
			}

			return result;
		}

		/// <summary>
		/// Rotate a vector with this matrix.
		/// </summary>
		public Vector3D RotateVector( Vector3D input )
		{
			VectorND result = new VectorND( 4 );
			VectorND copy = new VectorND( new double[] { input.X, input.Y, input.Z, input.W } );
			for( int i = 0; i < 4; i++ )
			{
				result.X[i] =
					copy.X[0] * this[i, 0] +
					copy.X[1] * this[i, 1] +
					copy.X[2] * this[i, 2] +
					copy.X[3] * this[i, 3];
			}
			return new Vector3D( result.X[0], result.X[1], result.X[2], result.X[3] );
		}

		/// <summary>
		/// Rotate a vector with this matrix.
		/// </summary>
		public VectorND RotateVector( VectorND input )
		{
			VectorND result = new VectorND( 4 );
			VectorND copy = input.Clone();
			for( int i = 0; i < 4; i++ )
			{
				result.X[i] =
					copy.X[0] * this[i, 0] +
					copy.X[1] * this[i, 1] +
					copy.X[2] * this[i, 2] +
					copy.X[3] * this[i, 3];
			}
			return result;
		}

		/// <summary>
		/// Returns a matrix which will rotate in a coordinate plane by an angle in radians.
		/// </summary>
		public static Matrix4D MatrixToRotateinCoordinatePlane( double angle, int c1, int c2 )
		{
			Matrix4D result = Matrix4D.Identity();
			result[c1, c1] =  Math.Cos( angle );
			result[c1, c2] = -Math.Sin( angle );
			result[c2, c1] =  Math.Sin( angle );
			result[c2, c2] =  Math.Cos( angle );
			return result;
		}
	}
}
