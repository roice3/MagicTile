namespace R3.Drawing
{
	using OpenTK.Graphics.OpenGL;
	using System;
	using System.Collections.Generic;
	using System.Drawing;

	/// <summary>
	/// A class for rendering to texture.
	/// </summary>
	public class RenderToTexture : IDisposable
	{
		public RenderToTexture() 
		{
			m_textureHandles = new Dictionary<object, int>();
		}

		//private const int m_textureSize = 1024;
		// ZZZ - query max size allowed from video card?
		public int TextureSize { get { return m_textureSize; } set { m_textureSize = value; } }
		private int m_textureSize = 512;

		/// <summary>
		/// Frame Buffer Object handle (we'll use only one for all the textures we generate).
		/// </summary>
		private int m_textureFBO;

		/// <summary>
		/// We support handling multiple textures.
		/// This will allow us to associate an object to each texture for lookup.
		/// </summary>
		private Dictionary<object, int> m_textureHandles;

		/// <summary>
		/// Called to render to a square texture.  
		/// The main rendering happens in the passed in RenderDelegate.
		/// The passed in object will be used to reference the texture later.
		/// This will return true immediately if a texture has already been created with this key.
		/// </summary>
		public bool CreateTexture( object key, bool generateMipmaps, Action<object> RenderDelegate )
		{
			if( m_textureHandles.ContainsKey( key ) )
				return true;

			bool success;

			GL.Enable( EnableCap.Texture2D );
			GenTextureAndFBO( key, generateMipmaps );
			int texture = m_textureHandles[key];

			/* Loading a texture from a picture on disk.
			{
				LoadTexture();

				if( generateMipmaps )
					GL.Ext.GenerateMipmap( GenerateMipmapTarget.Texture2D );

				return true;
			} */

			GL.Ext.BindFramebuffer( FramebufferTarget.FramebufferExt, m_textureFBO );
			GL.Ext.FramebufferTexture2D( FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, TextureTarget.Texture2D, texture, 0 );
			//GL.Ext.FramebufferTexture( FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, texture, 0 );

			FramebufferErrorCode e = GL.Ext.CheckFramebufferStatus( FramebufferTarget.FramebufferExt );
			TestForError( e );
			success = e == FramebufferErrorCode.FramebufferCompleteExt;
			if( success )
			{
				GL.PushAttrib( AttribMask.ViewportBit );
				{
					GL.Viewport( 0, 0, m_textureSize, m_textureSize );

					// We need to disable textures while doing the actual rendering.
					GL.Disable( EnableCap.Texture2D );
					RenderDelegate( key );
					GL.Enable( EnableCap.Texture2D );
				}
				GL.PopAttrib();
			}

			//SaveTexture();
			GL.Ext.BindFramebuffer( FramebufferTarget.FramebufferExt, 0 );

			if( generateMipmaps )
				GL.Ext.GenerateMipmap( GenerateMipmapTarget.Texture2D );

			return success;
		}

		private void SaveTexture()
		{
			Bitmap bmp = new Bitmap( m_textureSize, m_textureSize );
			System.Drawing.Imaging.BitmapData data =
				bmp.LockBits( new System.Drawing.Rectangle( 0, 0, m_textureSize, m_textureSize ),
							 System.Drawing.Imaging.ImageLockMode.WriteOnly,
							 System.Drawing.Imaging.PixelFormat.Format24bppRgb );
			GL.ReadPixels( 0, 0, m_textureSize, m_textureSize,
						  OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
						  OpenTK.Graphics.OpenGL.PixelType.UnsignedByte,
						  data.Scan0 );
			bmp.UnlockBits( data );
			bmp.RotateFlip( RotateFlipType.RotateNoneFlipY );
			bmp.Save( "Screenshot.png", System.Drawing.Imaging.ImageFormat.Png );
		}

		private void LoadTexture()
		{
			Bitmap bitmap = new Bitmap( "Screenshot.png" );
			bitmap.RotateFlip( RotateFlipType.RotateNoneFlipY );

			System.Drawing.Imaging.BitmapData data = bitmap.LockBits( new System.Drawing.Rectangle( 0, 0, bitmap.Width, bitmap.Height ), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb );
			GL.TexImage2D( TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0 );
			GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear );
			//GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear );
			GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear );
			GL.Finish();
			bitmap.UnlockBits( data );
		}

		public void Dispose()
		{
			InvalidateAllTextures();

			if( m_textureFBO != 0 )
			{
				//GL.DeleteFramebuffers( 1, ref m_textureFBO );
				GL.Ext.DeleteFramebuffers( 1, ref m_textureFBO );
			}
		}

		/// <summary>
		/// Called to clear out a texture we've already created (in case it needs redrawing).
		/// </summary>
		public void InvalidateTexture( object key )
		{
			int texture;
			if( m_textureHandles.TryGetValue( key, out texture ) )
			{
				if( texture != 0 )
					GL.DeleteTexture( texture );

				m_textureHandles.Remove( key );
			}
		}

		public void InvalidateAllTextures()
		{
			foreach( int texture in m_textureHandles.Values )
			{
				if( texture != 0 )
					GL.DeleteTexture( texture );
			}

			m_textureHandles.Clear();
		}

		/// <summary>
		/// Called to bind to the texture associated with a key.
		/// </summary>
		public void BindTexture( object key )
		{
			int texture;
			if( !m_textureHandles.TryGetValue( key, out texture ) )
			{
				// Can happen if texture creation fails.
				return;
			}

			GL.Enable( EnableCap.Texture2D );
			GL.BindTexture( TextureTarget.Texture2D, texture );
		}

		private void StandardTextureSettings( bool generateMipmaps )
		{
			//See http://www.opengl.org/wiki/Common_Mistakes
			//bool generateMipmaps = true;

			if( generateMipmaps )
				GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear );
			else
				GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear );
			GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear );
			GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge );
			GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge );
			GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge );

			// Formatting without alpha didn't work on some machines.
			//GL.TexImage2D( TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb8, m_textureSize, m_textureSize, 0, PixelFormat.Bgr, PixelType.UnsignedByte, IntPtr.Zero );
			GL.TexImage2D( TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, m_textureSize, m_textureSize, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero );
			//GL.TexImage2DMultisample( TextureTargetMultisample.Texture2DMultisample, 0, PixelInternalFormat.Rgb, m_textureSize, m_textureSize, false );

			if( generateMipmaps )
				GL.Ext.GenerateMipmap( GenerateMipmapTarget.Texture2D );
		}

		/// <summary>
		/// Called to generate a texture handle and FBO.
		/// </summary>
		private void GenTextureAndFBO( object key, bool generateMipmaps )
		{
			// Generate the texture handle, if we haven't yet.
			int texture;
			if( !m_textureHandles.TryGetValue( key, out texture ) )
			{
				texture = GL.GenTexture();
				m_textureHandles[key] = texture;
			}

			BindTexture( key );
			StandardTextureSettings( generateMipmaps );

			// Generate the frame buffer object, if we haven't yet.
			if( m_textureFBO == 0 )
			{
				//GL.GenFramebuffers( 1, out m_textureFBO );
				GL.Ext.GenFramebuffers( 1, out m_textureFBO );
			}
		}

		/// <summary>
		/// This method copied from OpenTK example.
		/// </summary>
		/// <param name="e"></param>
		private void TestForError( FramebufferErrorCode e )
		{
			switch( e )
			{
				case FramebufferErrorCode.FramebufferCompleteExt:
					{
						//Console.WriteLine( "FBO: The framebuffer is complete and valid for rendering." );
						return;
					}
				case FramebufferErrorCode.FramebufferIncompleteAttachmentExt:
					{
						Console.WriteLine( "FBO: One or more attachment points are not framebuffer attachment complete. This could mean there’s no texture attached or the format isn’t renderable. For color textures this means the base format must be RGB or RGBA and for depth textures it must be a DEPTH_COMPONENT format. Other causes of this error are that the width or height is zero or the z-offset is out of range in case of render to volume." );
						break;
					}
				case FramebufferErrorCode.FramebufferIncompleteMissingAttachmentExt:
					{
						Console.WriteLine( "FBO: There are no attachments." );
						break;
					}
				/*               case  FramebufferErrorCode.GL_FRAMEBUFFER_INCOMPLETE_DUPLICATE_ATTACHMENT_EXT: 
									 {
										 Console.WriteLine("FBO: An object has been attached to more than one attachment point.");
										 break;
									 }*/
				case FramebufferErrorCode.FramebufferIncompleteDimensionsExt:
					{
						Console.WriteLine( "FBO: Attachments are of different size. All attachments must have the same width and height." );
						break;
					}
				case FramebufferErrorCode.FramebufferIncompleteFormatsExt:
					{
						Console.WriteLine( "FBO: The color attachments have different format. All color attachments must have the same format." );
						break;
					}
				case FramebufferErrorCode.FramebufferIncompleteDrawBufferExt:
					{
						Console.WriteLine( "FBO: An attachment point referenced by GL.DrawBuffers() doesn’t have an attachment." );
						break;
					}
				case FramebufferErrorCode.FramebufferIncompleteReadBufferExt:
					{
						Console.WriteLine( "FBO: The attachment point referenced by GL.ReadBuffers() doesn’t have an attachment." );
						break;
					}
				case FramebufferErrorCode.FramebufferUnsupportedExt:
					{
						Console.WriteLine( "FBO: This particular FBO configuration is not supported by the implementation." );
						break;
					}
				case (FramebufferErrorCode)All.FramebufferIncompleteLayerTargetsExt:
					{
						Console.WriteLine( "FBO: Framebuffer Incomplete Layer Targets." );
						break;
					}
				case (FramebufferErrorCode)All.FramebufferIncompleteLayerCountExt:
					{
						Console.WriteLine( "FBO: Framebuffer Incomplete Layer Count." );
						break;
					}
				default:
					{
						Console.WriteLine( "FBO: Status unknown. (yes, this is really bad.)" );
						break;
					}
			}

			// using FBO might have changed states, e.g. the FBO might not support stereoscopic views or double buffering
			int[] queryinfo = new int[6];
			GL.GetInteger( GetPName.MaxColorAttachmentsExt, out queryinfo[0] );
			GL.GetInteger( GetPName.AuxBuffers, out queryinfo[1] );
			GL.GetInteger( GetPName.MaxDrawBuffers, out queryinfo[2] );
			GL.GetInteger( GetPName.Stereo, out queryinfo[3] );
			GL.GetInteger( GetPName.Samples, out queryinfo[4] );
			GL.GetInteger( GetPName.Doublebuffer, out queryinfo[5] );
			Console.WriteLine( "max. ColorBuffers: " + queryinfo[0] + " max. AuxBuffers: " + queryinfo[1] + " max. DrawBuffers: " + queryinfo[2] +
							   "\nStereo: " + queryinfo[3] + " Samples: " + queryinfo[4] + " DoubleBuffer: " + queryinfo[5] );

			Console.WriteLine( "Last GL Error: " + GL.GetError() );
		}
	}
}
