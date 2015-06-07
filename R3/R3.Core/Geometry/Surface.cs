namespace R3.Geometry
{
	using R3.Math;
	using System.Numerics;
	using Math = System.Math;

	public class Surface
	{
		/// <summary>
		/// Transform a mesh in the Poincare model to Dini's surface.
		/// </summary>
		public static Mesh Dini( Mesh mesh )
		{
			System.Func<Vector3D,Vector3D> transform = v =>
			{
				//v = DiskToUpper( v );
				//v.Y = Math.Log( v.Y );

				//if( v.Y < 1 || v.Y > 10 )
				//	return Infinity.InfinityVector;
				//if( v.X < -Math.PI || v.X > Math.PI )
				//if( v.X < -3*Math.PI || v.X > 3*Math.PI )
				//	return Infinity.InfinityVector;

				//v.Y = Math.Log( v.Y );
				//return v;
				return Dini( v );
			};

			Mesh result = new Mesh();
			for( int i=0; i<mesh.Triangles.Count; i++ )
			{
				Vector3D a = transform( mesh.Triangles[i].a );
				Vector3D b = transform( mesh.Triangles[i].b );
				Vector3D c = transform( mesh.Triangles[i].c );
				if( Infinity.IsInfinite( a ) ||
					Infinity.IsInfinite( b ) ||
					Infinity.IsInfinite( c ) )
					continue;

				result.Triangles.Add( new Mesh.Triangle( a, b, c ) );
			}

			return result;
		}

		public static Vector3D Dini( Vector3D uv )
		{
			double a = 1;
			double b = 0.0;
			return Dini( uv, a, b );
		}

		private static double Sech( double val )
		{
			return 1.0 / Math.Cosh( val );
		}

		public static Vector3D Dini( Vector3D uv, double a, double b )
		{
			uv = DiskToUpper( uv );

			// Eq 1.86 on p36 of book Backlund and Darboux Transformations
			double eta = Math.PI / 2 -Math.PI / 20;
			//double eta = Math.PI / 2;
			double p = 1;	// curvature
			double x = DonHatch.acosh( uv.Y );	// Used info on mathworld for tractrix to figure this out.
			//double x = DonHatch.acosh( Math.Exp( DonHatch.acosh( ( uv.Y * uv.Y + 1 ) / ( 2 * uv.Y ) ) ) );
			//double x = Math.Log( uv.Y );
			double y = uv.X;

			double pSinEta = p * Math.Sin( eta );
			double chi = (x - y * Math.Cos( eta )) / pSinEta;

			if( x <= -4 || x > 4 ||
				y < -3 * Math.PI || y > 3 * Math.PI )
				return Infinity.InfinityVector;

			Vector3D result = new Vector3D(
				pSinEta * Sech( chi ) * Math.Cos( y / p ),
				pSinEta * Sech( chi ) * Math.Sin( y / p ),
				x - pSinEta * Math.Tanh( chi ) );
			return result;

			/*
			System.Func<double, Complex> tractrix = new System.Func<double, Complex>(
			( t ) =>
			{
				//return new Complex( t - Math.Tanh( t ), 1.0 / Math.Cosh( t ) );
				return new Complex( - Math.Sqrt( 1 - 1 / (t*t) ) + DonHatch.acosh( t ), 1.0 / t );
			} );

			double logy = Math.Log( uv.Y );
			//Complex tract = tractrix( logy );
			Complex tract = tractrix( uv.Y );
			return new Vector3D(
				a * Math.Cos( uv.X ) * tract.Imaginary,
				a * Math.Sin( uv.X ) * tract.Imaginary,
				a * tract.Real + b * uv.X );
			*/
 
			/*
			return new Vector3D(
				a * Math.Cos( uv.X ) / Math.Cosh( uv.Y ),
				a * Math.Sin( uv.X ) / Math.Cosh( uv.Y ),
				a * (uv.Y - Math.Tanh( uv.Y )) + b * uv.X ); */

			/*return new Vector3D(
				a * Math.Cos( uv.X ) * Math.Sin( uv.Y ),
				a * Math.Sin( uv.X ) * Math.Sin( uv.Y ),
				a * (Math.Cos( uv.Y ) + Math.Log( Math.Tan( 0.5 * uv.Y ) )) + b * uv.X );*/
		}

		private static Vector3D DiskToUpper( Vector3D input )
		{
			Mobius m = new Mobius();
			m.UpperHalfPlane();
			return m.Apply( input );
		}

	/*
		{
			double a = 1;
			double b = m_settings.RotationRate;
			System.Func<Vector3D, Vector3D> dinis = new System.Func<Vector3D, Vector3D>( uv =>
			{
				Vector3D result = 

				Matrix4D rot = m_mouseMotion.RotHandler4D.Current4dView;
				return (TransformFunc( rot ))( result );
			} );

			int count = 50;
			double uInc = Math.PI * 12 / count;
			double vInc = Math.PI / 2 / count;
			for( int i = 0; i < count; i++ )
			for( int j = 1; j < count - 1; j++ )
			{
				Polygon poly = Polygon.FromPoints( new Vector3D[]
				{
					dinis( new Vector3D( i * uInc,		j * vInc ) ),
					dinis( new Vector3D( (i+1) * uInc,	j * vInc ) ),
					dinis( new Vector3D( (i+1) * uInc,	(j+1) * vInc ) ),
					dinis( new Vector3D( i * uInc,		(j+1) * vInc ) ),
				} );
				GLUtils.DrawPolygon( poly, Color.White );
			}
		}
	 */
	}
}
