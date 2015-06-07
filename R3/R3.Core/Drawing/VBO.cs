namespace R3.Drawing
{
	using OpenTK;
	using OpenTK.Graphics.OpenGL;
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Runtime.InteropServices;
	using R3.Geometry;

	/// <summary>
	/// Used to specify a vertex (position, normal and texture coordinates).
	/// </summary>
	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	public struct VertexPositionNormalTexture
	{
		public Vector3 Position;
		public Vector3 Normal;
		public Vector2 TexCoord;

		public VertexPositionNormalTexture( float x, float y, float z, float nx, float ny, float nz, float u, float v )
		{
			Position = new Vector3( x, y, z );
			Normal = new Vector3( nx, ny, nz );
			TexCoord = new Vector2( u, v );
		}
	}

	/// <summary>
	/// Used to specify a vertex (position and color coordinates).
	/// </summary>
	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	public struct VertexPositionColor
	{
		public Vector3 Position;
		public uint Color;

		public VertexPositionColor( float x, float y, float z, Color color )
		{
			Position = new Vector3( x, y, z );
			Color = ToRgba( color );
		}

		private static uint ToRgba( Color color )
		{
			return (uint)color.A << 24 | (uint)color.B << 16 | (uint)color.G << 8 | (uint)color.R;
		}
	}

	/// <summary>
	/// A class for easing use of OpenGL Vertex Buffer Objects.
	/// Currently only supports BeginMode.Triangles
	/// </summary>
	public class VBO : IDisposable
	{
		private struct VboHandle 
		{
			public int m_vboId;
			public int m_eboId;
			public int m_numElements;
			public int m_vertexStride;
			public bool m_colorVertex;
		}

		private VboHandle m_handle;

		/// <summary>
		/// Create and load our VBO. This will dispose previous VBO handles and data, if it existed.
		/// </summary>
		public void Create<TVertex>( TVertex[] vertices, short[] elements ) where TVertex : struct
		{
			Dispose();
			m_handle = new VboHandle();
			int size;

			// To create a VBO:
			// 1) Generate the buffer handles for the vertex and element buffers.
			// 2) Bind the vertex buffer handle and upload your vertex data. Check that the buffer was uploaded correctly.
			// 3) Bind the element buffer handle and upload your element data. Check that the buffer was uploaded correctly.

			m_handle.m_vertexStride = BlittableValueType.StrideOf( vertices );

			GL.GenBuffers( 1, out m_handle.m_vboId );
			GL.BindBuffer( BufferTarget.ArrayBuffer, m_handle.m_vboId );
			GL.BufferData( BufferTarget.ArrayBuffer, (IntPtr)( vertices.Length * m_handle.m_vertexStride ), vertices,
						  BufferUsageHint.StaticDraw );
			GL.GetBufferParameter( BufferTarget.ArrayBuffer, BufferParameterName.BufferSize, out size );
			if( vertices.Length * m_handle.m_vertexStride != size )
				throw new ApplicationException( "Vertex data not uploaded correctly" );

			GL.GenBuffers( 1, out m_handle.m_eboId );
			GL.BindBuffer( BufferTarget.ElementArrayBuffer, m_handle.m_eboId );
			GL.BufferData( BufferTarget.ElementArrayBuffer, (IntPtr)( elements.Length * sizeof( short ) ), elements,
						  BufferUsageHint.StaticDraw );
			GL.GetBufferParameter( BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize, out size );
			if( elements.Length * sizeof( short ) != size )
				throw new ApplicationException( "Element data not uploaded correctly" );

			m_handle.m_numElements = elements.Length;
			m_handle.m_colorVertex = typeof( TVertex ) == typeof( VertexPositionColor );
		}

		public void Draw()
		{
			// To draw a VBO:
			// 1) Ensure that the VertexArray client state is enabled.
			// 2) Bind the vertex and element buffer handles.
			// 3) Set up the data pointers (vertex, normal, color) according to your vertex format.
			// 4) Call DrawElements. (Note: the last parameter is an offset into the element buffer
			//    and will usually be IntPtr.Zero).
			
			GL.EnableClientState( ArrayCap.VertexArray );
			if( m_handle.m_colorVertex )
			{
				GL.EnableClientState( ArrayCap.ColorArray );
				GL.DisableClientState( ArrayCap.NormalArray );
				GL.DisableClientState( ArrayCap.TextureCoordArray );
			}
			else
			{
				GL.EnableClientState( ArrayCap.NormalArray );
				GL.EnableClientState( ArrayCap.TextureCoordArray );
				GL.DisableClientState( ArrayCap.ColorArray );
			}

			GL.BindBuffer( BufferTarget.ArrayBuffer, m_handle.m_vboId );
			GL.BindBuffer( BufferTarget.ElementArrayBuffer, m_handle.m_eboId );

			GL.VertexPointer( 3, VertexPointerType.Float, m_handle.m_vertexStride, new IntPtr( 0 ) );
			if( m_handle.m_colorVertex )
				GL.ColorPointer( 4, ColorPointerType.UnsignedByte, m_handle.m_vertexStride, new IntPtr( 12 ) );
			else
			{
				GL.NormalPointer( NormalPointerType.Float, m_handle.m_vertexStride, new IntPtr( 12 ) );
				GL.TexCoordPointer( 2, TexCoordPointerType.Float, m_handle.m_vertexStride, new IntPtr( 24 ) );
			}

			GL.DrawElements( BeginMode.Triangles, m_handle.m_numElements, DrawElementsType.UnsignedShort, IntPtr.Zero );
		}

		/// <summary>
		/// Helper for creating lists of vertices and elements.
		/// The polygon will be divided into triangles, and those will be appended to the lists.
		/// </summary>
		public static void PolygonToVerts( Polygon poly, Color c, 
			List<VertexPositionColor> vboVertices, List<short> vboElements )
		{
			for( int i=0; i<poly.Vertices.Length; i++ )
			{
				int i2 = i == poly.Vertices.Length - 1 ? 0 : i + 1;
				Vector3D[] tri = new Vector3D[]
				{
					poly.Vertices[i],
					poly.Vertices[i2],
					poly.Center
				};

				foreach( Vector3D vert in tri )
				{
					vboVertices.Add( new VertexPositionColor(
						(float)vert.X, (float)vert.Y, (float)vert.Z, c ) );
					vboElements.Add( (short)vboElements.Count );
				}
			}
		}

		public void Dispose()
		{
			if( m_handle.m_vboId != 0 )
				GL.DeleteBuffers( 1, ref m_handle.m_vboId );
			if( m_handle.m_eboId != 0 )
				GL.DeleteBuffers( 1, ref m_handle.m_eboId );
		}
	}
}
