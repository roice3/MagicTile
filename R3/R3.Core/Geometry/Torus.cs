namespace R3.Geometry
{
	using Math = System.Math;
	using OpenTK.Graphics.OpenGL;
	using System.Diagnostics;
	using System.Drawing;

	/// <summary>
	/// Class to generate tori on a 3-sphere
	/// </summary>
	public class Torus
	{
		/// <summary>
		/// The things that define us.
		/// </summary>
		public class Parameters
		{
			public Parameters()
			{
				NumSegments1 = NumSegments2 = 50;
				TubeRadius1 = 0.5;
				Radius = 1.0;
			}

			/// <summary>
			/// The number of segments to generate in the first direction of the torus surface.
			/// </summary>
			public int NumSegments1 { get; set; }

			/// <summary>
			/// The number of segments to generate in the second direction of the torus surface.
			/// </summary>
			public int NumSegments2 { get; set; }

			/// <summary>
			/// The first tube radius of our torus.  
			/// NOTES: 
			///		- The second tube radius is determined by this and the 3-sphere radius.
			///		- This radius must be less than or equal the 3-sphere radius
			///		- If equal 0 or equal to the 3-sphere radius, one tube will be empty (torus will be a line).
			/// </summary>
			public double TubeRadius1 { get; set; }

			/// <summary>
			/// The radius of our 3-sphere
			/// </summary>
			public double Radius { get; set; }
		}

		public Parameters Params { get; set; }

		/// <summary>
		/// Our vertices.
		/// NOTE: Not realy a Vector3D here (need to rename my class).
		/// </summary>
		public Vector3D[][] Vertices { get; set; }

		/// <summary>
		/// Size our Vertices matrix.
		/// </summary>
		private void InitVerts()
		{
			int n1 = this.Params.NumSegments1;
			int n2 = this.Params.NumSegments2;

			Vertices = new Vector3D[n1][];
			for( int i = 0; i < n1; i++ )
				Vertices[i] = new Vector3D[n2];
		}

		/// <summary>
		/// Special case of CreateTorus for the Clifford Torus.
		/// </summary>
		public static Torus CreateClifford( Parameters parameters )
		{
			parameters.TubeRadius1 = parameters.Radius / 2;
			return CreateTorus( parameters );
		}

		/// <summary>
		/// Calculates a torus which divides the 3-sphere in two.
		/// </summary>
		public static Torus CreateTorus( Parameters parameters )
		{
			Torus t = new Torus();
			t.Params = parameters;
			t.InitVerts();

			// Shorter var names for inputs.
			int n1 = parameters.NumSegments1;
			int n2 = parameters.NumSegments2;
			double r = parameters.Radius;
			double r1 = parameters.TubeRadius1;

			// Calc r2.
			double r2 = r - r1;
			if( r2 < 0 )
				r2 = 0;

			double angleInc1 = 2 * Math.PI / n1;
			double angleInc2 = 2 * Math.PI / n2;

			double angle1 = 0;
			for( int i = 0; i < n1; i++ )
			{
				double angle2 = 0;
				for( int j = 0; j < n2; j++ )
				{
					t.Vertices[i][j].X = r1 * Math.Cos( angle1 );
					t.Vertices[i][j].Y = r1 * Math.Sin( angle1 );
					t.Vertices[i][j].Z = r2 * Math.Cos( angle2 );
					t.Vertices[i][j].W = r2 * Math.Sin( angle2 );
					angle2 += angleInc2;
				}
				angle1 += angleInc1;
			}

			return t;
		}

		/// <summary>
		/// Render our Torus using OpenGL.
		/// </summary>
		public void Render( System.Func<Vector3D, Vector3D> rotateAndProject )
		{
			GL.PushAttrib(
				AttribMask.LightingBit |
				AttribMask.PolygonBit |
				AttribMask.EnableBit );

			GL.Disable( EnableCap.Lighting );
			GL.Enable( EnableCap.DepthTest );

			RenderInternal( rotateAndProject );

			GL.PopAttrib();
		}

		public void RenderInternal( System.Func<Vector3D, Vector3D> rotateAndProject )
		{
			if( this.Vertices.Length <= 0 ||
				this.Params.NumSegments1 != this.Vertices.Length ||
				this.Params.NumSegments2 != this.Vertices[0].Length )
			{
				// Our vertices and parameters are out of sync.
				Debug.Assert( false );
				return;
			}

			// Direction 1
			GL.Color3( Color.Blue );
			for( int i = 0; i < this.Params.NumSegments2; i++ )
			{
				GL.Begin( BeginMode.LineLoop );
				for( int j = 0; j < this.Params.NumSegments1; j++ )
				{
					Vector3D transformed = rotateAndProject( this.Vertices[j][i] );
					GL.Vertex3(
						transformed.X,
						transformed.Y,
						transformed.Z );
				}
				GL.End();
			}

			// Direction 2
			GL.Color3( Color.Red );
			for( int i = 0; i < this.Params.NumSegments1; i++ )
			{
				GL.Begin( BeginMode.LineLoop );
				for( int j = 0; j < this.Params.NumSegments2; j++ )
				{
					Vector3D transformed = rotateAndProject( this.Vertices[i][j] );
					GL.Vertex3(
						transformed.X,
						transformed.Y,
						transformed.Z );
				}
				GL.End();
			}
		}

		/// <summary>
		/// Maps a point in a rhombus to a point in the unit square, via a simple affine transformation.
		/// b1 and b2 are the two basis vectors of the rhombus.
		/// Does not currently check the input point.
		/// </summary>
		public static Vector3D MapRhombusToUnitSquare( Vector3D b1, Vector3D b2, Vector3D v )
		{
			double a = Euclidean2D.AngleToCounterClock( b1, new Vector3D( 1, 0 ) );
			v.RotateXY( a );
			b1.RotateXY( a );
			b2.RotateXY( a );

			// Shear
			v.X -= b2.X * ( v.Y / b2.Y );

			// Scale x and y.
			v.X /= b1.X;
			v.Y /= b2.Y;

			return v;
		}

		/// <summary>
		/// Maps a vector in the unit square to a point on the Clifford Torus.
		/// Does not currently check the input point.
		/// </summary>
		public static Vector3D MapToClifford( Vector3D v )
		{
			v *= 2 * Math.PI;
			Vector3D result = new Vector3D(
				Math.Cos( v.X ), 
				Math.Sin( v.X ), 
				Math.Cos( v.Y ), 
				Math.Sin( v.Y ) );
			return Math.Sqrt( 0.5 ) * result;
		}
	}
}
