namespace R3.Drawing
{
	using OpenTK.Graphics.OpenGL;
	using R3.Geometry;
	using R3.Math;
	using System.Collections.Generic;
	using System.Drawing;

	using Math = System.Math;

	public static class GLUtils
	{
		/// <summary>
		/// Draws a circle in OpenGL immediate mode.
		/// </summary>
		public static void DrawCircle( Circle c, Color color, System.Func<Vector3D, Vector3D> transform, bool solid = false )
		{
			DrawCircleInternal( c, color, 100, transform, solid );
		}

		private static void DrawCircleInternal( Circle c, Color color, int divisions, 
			System.Func<Vector3D,Vector3D> transform, bool solid = false )
		{
			GL.Color3( color );
			if( !solid )
				GL.Begin( BeginMode.LineLoop );
			else
			{
				GL.Begin( BeginMode.TriangleFan );
				Vector3D temp = new Vector3D( c.Center.X, c.Center.Y );
				Vector3D transformed = transform == null ? temp : transform( temp );
				GL.Vertex3( transformed.X, transformed.Y, transformed.Z );
			}

			{
				Vector3D radius = new Vector3D( 0, c.Radius );

				for( int i = 0; i <= divisions; i++ )
				{
					radius.RotateXY( 2 * Math.PI / divisions );

					if( transform == null )
						GL.Vertex2( c.Center.X + radius.X, c.Center.Y + radius.Y );
					else
					{
						Vector3D temp = new Vector3D( c.Center.X + radius.X, c.Center.Y + radius.Y );
						Vector3D transformed = transform( temp );
						GL.Vertex3( transformed.X, transformed.Y, transformed.Z );
					}
				}
			}
			GL.End();
		}

		/// <summary>
		/// Draws a generalized circle (may be a line) in OpenGL immediate mode,
		/// and safely handle circles with large radii.
		/// This is slower, so we only want to use it when necessary.
		/// </summary>
		public static void DrawCircleSafe( CircleNE c, Color color,
			System.Func<Vector3D, Vector3D> transform )
		{
			GL.Color3( color );

			if( c.IsLine )
			{
				Vector3D start = Euclidean2D.ProjectOntoLine( new Vector3D(), c.P1, c.P2 );
				Vector3D d = c.P2 - c.P1;
				d.Normalize();
				d *= 50;

				if( transform == null )
				{
					GL.Begin( BeginMode.Lines );
						GL.Vertex2( start.X + d.X, start.Y + d.Y );
						GL.Vertex2( start.X - d.X, start.Y - d.Y );
					GL.End();
				}
				else
				{
					int divisions = 500;
					Vector3D begin = start + d, end = start - d, inc = ( end - begin ) / divisions;
					GL.Begin( BeginMode.LineStrip );
					for( int i=0; i<divisions; i++ )
					{
						Vector3D point = transform( begin + inc * i );
						GL.Vertex2( point.X, point.Y );
					}
					GL.End();
				}
			}
			else
			{
				Segment seg = BuildSegment( c );
				if( seg == null )
				{
					DrawCircleInternal( c, color, 500, transform );
				}
				else
				{
					DrawSeg( seg, 1000, transform );
				}
			}
		}

		public static void DrawHyperbolicGeodesic( CircleNE c, Color color,
			System.Func<Vector3D, Vector3D> transform )
		{
			GL.Color3( color );

			Segment seg = null;
			if( c.IsLine )
			{
				// It will go through the origin.
				Vector3D p = c.P1;
				p.Normalize();
				seg = Segment.Line( p, -p );
			}
			else
			{
				Vector3D p1, p2;
				Euclidean2D.IntersectionCircleCircle( c, new Circle(), out p1, out p2 );
				seg = Segment.Arc( p1, H3Models.Ball.ClosestToOrigin( new Circle3D() { Center = c.Center, Radius = c.Radius, Normal = new Vector3D(0,0,1) } ), p2 );
			}

			DrawSeg( seg, 75, transform );
		}

		public static void DrawSeg( Segment seg, int div, System.Func<Vector3D, Vector3D> transform )
		{
			GL.Begin( BeginMode.LineStrip );
			{
				foreach( Vector3D point in seg.Subdivide( div ) )
				{
					if( transform == null )
						GL.Vertex2( point.X, point.Y );
					else
					{
						Vector3D transformed = transform( point );
						GL.Vertex2( transformed.X, transformed.Y );
					}
				}
			}
			GL.End();
		}

		private static Polygon m_box;
		private static Polygon Box
		{
			get
			{
				if( m_box != null )
					return m_box;

				// We could try to make this be dynamic, but it doesn't seem necessary.
				Vector3D box = new Vector3D( 25, 25 );
				Polygon poly = new Polygon();
				Vector3D p1 = new Vector3D( -box.X, -box.Y );
				Vector3D p2 = new Vector3D( box.X, -box.Y );
				Vector3D p3 = new Vector3D( box.X, box.Y );
				Vector3D p4 = new Vector3D( -box.X, box.Y );
				poly.Segments.Add( Segment.Line( p1, p2 ) );
				poly.Segments.Add( Segment.Line( p2, p3 ) );
				poly.Segments.Add( Segment.Line( p3, p4 ) );
				poly.Segments.Add( Segment.Line( p4, p1 ) );
				m_box = poly;
				return m_box;
			}
		}

		/// <summary>
		/// Builds a segment going through the box (if we cross it).
		/// </summary>
		private static Segment BuildSegment( CircleNE c )
		{
			Vector3D[] iPoints = Box.GetIntersectionPoints( c );
			if( 2 != iPoints.Length )
				return null;

			if( c.IsLine )
				return Segment.Line( iPoints[0], iPoints[1] );

			// Find the midpoint.  Probably a better way.
			Vector3D t1 = iPoints[0] - c.Center;
			Vector3D t2 = iPoints[1] - c.Center;
			double angle1 = Euclidean2D.AngleToCounterClock( t1, t2 );
			double angle2 = Euclidean2D.AngleToClock( t1, t2 );
			Vector3D mid1 = t1, mid2 = t1;
			mid1.RotateXY( angle1 / 2 );
			mid2.RotateXY( -angle2 / 2 );
			mid1 += c.Center;
			mid2 += c.Center;
			Vector3D mid = mid1;
			if( mid2.Abs() < mid1.Abs() )
				mid = mid2;
			return Segment.Arc( iPoints[0], mid, iPoints[1] );
		}

		/// <summary>
		/// Draws a polygon in OpenGL immediate mode.
		/// Draws only line mode.
		/// </summary>
		public static void DrawPolygon( Polygon p, Color color,
			System.Func<Vector3D, Vector3D> transform )
		{
			GL.Color3( color );
			GL.Begin( BeginMode.LineLoop );
			{
				Vector3D[] edgePoints = p.EdgePoints;
				for( int i = 0; i < edgePoints.Length; i++ )
				{
					Vector3D transformed = transform == null ?
						edgePoints[i] : transform( edgePoints[i] );
					GL.Vertex2( transformed.X, transformed.Y );
				}
			}
			GL.End();
		}

		/// <summary>
		/// Draws a filled in polygon that might be concave.
		/// We use the stencil buffer for this, as described here:
		/// http://zrusin.blogspot.com/2006/07/hardware-accelerated-polygon-rendering.html
		/// This function assumes the stencil buffer is already cleared,
		/// and it will leave it cleared and disabled when done.
		/// </summary>
		public static void DrawConcavePolygon( Polygon p, Color color,
			System.Func<Vector3D, Vector3D> transform )
		{
			bool inverted = p.IsInverted;

			GL.StencilFunc( StencilFunction.Always, 0, ~0 );
			GL.StencilOp( StencilOp.Keep, StencilOp.Keep, StencilOp.Invert );

			//
			// Create our stencil, tracking max/min
			//

			Vector3D cen = p.Center;
			if( Infinity.IsInfinite( cen ) )
				cen = Infinity.LargeFiniteVector;
			Vector3D min = cen, max = cen;

			// Turn coloring off.
			GL.ColorMask( false, false, false, false );

			GL.PolygonMode( MaterialFace.FrontAndBack, PolygonMode.Fill );
			GL.Begin( BeginMode.TriangleFan );
			{
				GL.Vertex2( cen.X, cen.Y );

				Vector3D[] edgePoints = p.EdgePoints;
				for( int i = 0; i < edgePoints.Length; i++ )
				{
					Vector3D transformed = transform == null ?
						edgePoints[i] : transform( edgePoints[i] );

					GL.Vertex2( transformed.X, transformed.Y );

					if( transformed.X < min.X )
						min.X = transformed.X;
					if( transformed.Y < min.Y )
						min.Y = transformed.Y;
					if( transformed.X > max.X )
						max.X = transformed.X;
					if( transformed.Y > max.Y )
						max.Y = transformed.Y;
				}
			}
			GL.End();

			//
			// Now draw quad that is stenciled
			//

			// Turn coloring on.
			GL.ColorMask( true, true, true, true );

			GL.StencilFunc( StencilFunction.Equal, inverted ? 0 : 1, ~0 );
			GL.StencilOp( StencilOp.Zero, StencilOp.Zero, StencilOp.Zero );	// So we leave the stencil buffer as we found it (zeroed).

			if( inverted )
			{
				const double large = 1000;
				min = new Vector3D( -large, -large );
				max = new Vector3D( large, large );
			}

			GL.Color4( color );
			GL.Begin( BeginMode.Quads );
			GL.Vertex2( min.X, min.Y );
			GL.Vertex2( min.X, max.Y );
			GL.Vertex2( max.X, max.Y );
			GL.Vertex2( max.X, min.Y );
			GL.End();
		}

		public static void DrawPolygonSolid( Polygon p, Color color, bool fast = false )
		{
			GL.Color3( color );
			GL.PolygonMode( MaterialFace.FrontAndBack, PolygonMode.Fill );
			GL.Begin( BeginMode.TriangleFan );
			{
				GL.Vertex3( p.Center.X, p.Center.Y, p.Center.Z );

				Vector3D[] edgePoints = fast ?
					p.CalcEdgePoints( arcResolution: R3.Core.Utils.DegreesToRadians( 9.0 ), 
						minSegs: 5, checkForInfinities: false ) :
					p.EdgePoints;
				for( int i = 0; i < edgePoints.Length; i++ )
				{
					GL.Vertex3( edgePoints[i].X, edgePoints[i].Y, edgePoints[i].Z );
				}
			}
			GL.End();
		}

		public static void DrawPolygonSolid( Polygon p, Isometry isometry, Color color )
		{
			GL.Color3( color );
			GL.PolygonMode( MaterialFace.FrontAndBack, PolygonMode.Fill );
			GL.Begin( BeginMode.TriangleFan );
			{
				Vector3D center = isometry.Apply( p.Center );
				GL.Vertex2( center.X, center.Y );

				Vector3D[] edgePoints = p.EdgePoints;
				for( int i = 0; i < edgePoints.Length; i++ )
				{
					Vector3D draw = isometry.Apply( edgePoints[i] );
					GL.Vertex2( draw.X, draw.Y );
				}
			}
			GL.End();
		}

		public static void SaveImage( string fileName, int width, int height )
		{
			Bitmap bmp = new Bitmap( width, height );
			System.Drawing.Imaging.BitmapData data =
				bmp.LockBits( new System.Drawing.Rectangle( 0, 0, width, height ),
							 System.Drawing.Imaging.ImageLockMode.WriteOnly,
							 System.Drawing.Imaging.PixelFormat.Format24bppRgb );
			GL.ReadPixels( 0, 0, width, height,
						  OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
						  OpenTK.Graphics.OpenGL.PixelType.UnsignedByte,
						  data.Scan0 );
			bmp.UnlockBits( data );
			bmp.RotateFlip( RotateFlipType.RotateNoneFlipY );
			bmp.Save( fileName, System.Drawing.Imaging.ImageFormat.Png );
		}

	}
}
