namespace R3.Geometry
{
	using OpenTK.Graphics.OpenGL;
	using R3.Math;
	using System;
	using System.Numerics;

	public enum HyperbolicModel
	{
		Poincare,
		Klein,
		Pseudosphere,
		Hyperboloid,
		Band,
		UpperHalfPlane
	}

	public class HyperbolicModels
	{
		private static void Vertex( Vector3D v )
		{
			GL.Vertex2( v.X, v.Y );
		}

		private static void Vertex( Complex c )
		{
			GL.Vertex2( c.Real, c.Imaginary );
		}

		public static Vector3D PoincareToKlein( Vector3D p )
		{
			double mag = 2 / (1 + p.Dot( p ));
			return p * mag;
		}

		public static Vector3D KleinToPoincare( Vector3D k )
		{
			double dot = k.Dot( k );
			if( dot > 1 )	// This avoids some NaN problems I saw.
				dot = 1;
			double mag = (1 - Math.Sqrt( 1 - dot )) / dot;
			return k * mag;
		}

		public static void DrawElements( HyperbolicModel model, Vector3D[] textureCoords, Vector3D[] textureVerts, int[] elements, 
			Isometry mouseIsometry, double textureScale )
		{
			////////////////////// ZZZ - Use VBOs
			GL.Begin( BeginMode.Triangles );
			{
				double factor = textureScale;
				int skipped = 0;

				for( int i=0; i<elements.Length; i++ )
				{
					int idx = elements[i];

					// In Poincare model.
					GL.TexCoord2( (textureCoords[idx].X * factor + 1) / 2, (textureCoords[idx].Y * factor + 1) / 2 );
					Complex transformed = mouseIsometry.Apply( textureVerts[idx].ToComplex() );

					switch( model )
					{
						case HyperbolicModel.Poincare:
						{
							Vertex( transformed );
							break;
						}
						case HyperbolicModel.Klein:
						{
							Vector3D temp = Vector3D.FromComplex( transformed );
							Vertex( PoincareToKlein( temp ) );
							break;
						}
						case HyperbolicModel.Pseudosphere:
						{
							Mobius m = new Mobius();
							m.UpperHalfPlane();
							Complex u = m.Apply( transformed );
							double x = u.Real;
							double y = u.Imaginary;
							double max = 1 * System.Math.PI;
							double min = -1 * System.Math.PI;
							if( 0 == i % 3 && ( x < min - 1 || x > max + 1 || y < 0 ) )
							{
								skipped = 1;
								continue;
							}

							if( skipped > 0 && skipped < 3 )
							{
								skipped++;
								continue;
							}

							skipped = 0;

							GL.TexCoord2( ( textureCoords[idx].X * factor + 1 ) / 2, ( textureCoords[idx].Y * factor + 1 ) / 2 );

							// Pseudosphere
							Func<double, Complex> tractrix = new Func<double, Complex>(
							( t ) =>
							{
								return new Complex( t - Math.Tanh( t ), 1.0 / Math.Cosh( t ) );
							} );

							//Vector3D temp1 = Vector3D.FromComplex( u );
							if( x < min )
								x = min;
							if( x > max )
								x = max;
							if( y < 1 )
								y = 1;
							Vector3D temp1 = new Vector3D( x, y );

							double logy = Math.Log( temp1.Y );
							Complex tract = tractrix( logy );
							Vector3D temp2 = new Vector3D(
								Math.Cos( temp1.X ) * tract.Imaginary,
								Math.Sin( temp1.X ) * tract.Imaginary,
								tract.Real );

							GL.Vertex3( temp2.X, temp2.Y, temp2.Z );

							//temp1 = m.Inverse().Apply( temp1 );
							//GL.Vertex3( temp1.X, temp1.Y, temp1.Z );
							//Vertex( temp1 );

							break;
						}
						case HyperbolicModel.Hyperboloid:
						{
							Vector3D hyper = Sterographic.PlaneToHyperboloid( Vector3D.FromComplex( transformed ) );		// Hyperboloid
							GL.Vertex3( hyper.X, hyper.Y, hyper.Z );
							break;
						}
						default:
						{
							System.Diagnostics.Debug.Assert( false );
							break;
						}
					}

					/* // PETALS
					int petals = 7;
					double newMag = transformed.Magnitude * ( 1 + 0.5 * Math.Sin( transformed.Phase * petals ) );
					double newPhase = transformed.Phase + ( -0.2 * newMag * Math.Pow( Math.Sin( newMag * 3 ), 1 ) * Math.Cos( transformed.Phase * petals ) );
					transformed = Complex.FromPolarCoordinates( newMag, newPhase );
					
					Vertex( transformed );
					 * */

					//double mag = System.Math.Pow( transformed.Magnitude, 3 ) / transformed.Magnitude;				// nice
					//double mag = System.Math.Pow( transformed.Magnitude - 3, 2 ) + .0;							// looks spherical

					//double mag = transformed.Magnitude + 0.1* System.Math.Sin( transformed.Magnitude * 15 );		// Fun warping (20 is cool too)
					//Vertex( transformed * mag );	

					/*double xmag = 1;
					double ymag = transformed.Imaginary + 0.1 * System.Math.Sin( transformed.Imaginary * 15 );
					xmag = System.Math.Abs( xmag );
					ymag = System.Math.Abs( ymag );
					Vertex( new Complex( transformed.Real * xmag, transformed.Imaginary * ymag ) );	*/

					//Vertex( 2 / System.Math.PI * Complex.Log( ( 1 + transformed ) / ( 1 - transformed ) ) );		// Band model
					//Vertex( Complex.Pow( transformed, 3 ) / transformed.Magnitude );								// Spikey

					// Spiral
					//Complex band = 2 / System.Math.PI * Complex.Log( ( 1 + transformed ) / ( 1 - transformed ) );
					//band = new Complex( band.Real, band.Imaginary + 0.3 * System.Math.Sin( band.Real * 2 ) );
					//band = new Complex( band.Real * .5, band.Imaginary );
					//band += new Complex( 0, .5 );
					//Vertex( band );

					/*
					double x = band.Real;
					double y = band.Imaginary;

					double r = System.Math.Exp( x );
					double theta = 3*( x + y/1.75 ); */
					//Vertex( new Complex( r * System.Math.Sin( theta ), r * System.Math.Cos( theta ) ) );			// Spiral
				}
			}
			GL.End();
		}

	}
}
