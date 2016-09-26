namespace MagicTile.Utils
{
	using OpenTK;

	/// <summary>
	/// OpenTK didn't support gluUnproject
	/// Links:
	///    http://www.opentk.com/files/Glu.cs (code copied from here).
	///    http://www.opentk.com/node/1276
	/// </summary>
	public static class GLUtils
	{
		public static int Project( Vector3d obj, Matrix4d modelMatrix, Matrix4d projMatrix, int[] viewport, ref Vector3d win )
		{
			return gluProject( obj.X, obj.Y, obj.Z, modelMatrix, projMatrix, viewport, ref win.X, ref win.Y, ref win.Z );
		}

		static int gluProject( double objx, double objy, double objz, Matrix4d modelMatrix, Matrix4d projMatrix, int[] viewport, ref double winx, ref double winy, ref double winz )
		{
			Vector4d _in;
			Vector4d _out;

			_in.X = objx;
			_in.Y = objy;
			_in.Z = objz;
			_in.W = 1.0;
			//__gluMultMatrixVecd(modelMatrix, in, out);
			//__gluMultMatrixVecd(projMatrix, out, in);
			//TODO: check if multiplication is in right order
			_out = Vector4d.Transform( _in, modelMatrix );
			_in = Vector4d.Transform( _out, projMatrix );

			if( _in.W == 0.0 )
				return ( 0 );
			_in.X /= _in.W;
			_in.Y /= _in.W;
			_in.Z /= _in.W;
			/* Map x, y and z to range 0-1 */
			_in.X = _in.X * 0.5 + 0.5;
			_in.Y = _in.Y * 0.5 + 0.5;
			_in.Z = _in.Z * 0.5 + 0.5;

			/* Map x,y to viewport */
			_in.X = _in.X * viewport[2] + viewport[0];
			_in.Y = _in.Y * viewport[3] + viewport[1];

			winx = _in.X;
			winy = _in.Y;
			winz = _in.Z;
			return ( 1 );
		}

		public static int UnProject( Vector3d win, Matrix4d modelMatrix, Matrix4d projMatrix, int[] viewport, ref Vector3d obj )
		{
			return gluUnProject( win.X, win.Y, win.Z, modelMatrix, projMatrix, viewport, ref obj.X, ref obj.Y, ref obj.Z );
		}

		static int gluUnProject( double winx, double winy, double winz, Matrix4d modelMatrix, Matrix4d projMatrix, int[] viewport, ref double objx, ref double objy, ref double objz )
		{
			Matrix4d finalMatrix;
			Vector4d _in;
			Vector4d _out;

			finalMatrix = Matrix4d.Mult( modelMatrix, projMatrix );

			//if (!__gluInvertMatrixd(finalMatrix, finalMatrix)) return(GL_FALSE);
			finalMatrix.Invert();

			_in.X = winx;
			_in.Y = winy;
			_in.Z = winz;
			_in.W = 1.0;

			/* Map x and y from window coordinates */
			_in.X = ( _in.X - viewport[0] ) / viewport[2];
			_in.Y = ( _in.Y - viewport[1] ) / viewport[3];

			/* Map to range -1 to 1 */
			_in.X = _in.X * 2 - 1;
			_in.Y = _in.Y * 2 - 1;
			_in.Z = _in.Z * 2 - 1;

			//__gluMultMatrixVecd(finalMatrix, _in, _out);
			// check if this works:
			_out = Vector4d.Transform( _in, finalMatrix );

			if( _out.W == 0.0 )
				return ( 0 );
			_out.X /= _out.W;
			_out.Y /= _out.W;
			_out.Z /= _out.W;
			objx = _out.X;
			objy = _out.Y;
			objz = _out.Z;
			return ( 1 );
		}
	}
}
