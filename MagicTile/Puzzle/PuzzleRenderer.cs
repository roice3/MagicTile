namespace MagicTile
{
	using MagicTile.Control;
	using OpenTK.Graphics.OpenGL;
	using R3.Control;
	using R3.Core;
	using R3.Drawing;
	using R3.Geometry;
	using R3.Math;
	using System.Collections.Generic;
	using System.Drawing;
	using System.IO;
	using System.Linq;
	using System.Numerics;
	using System.Windows.Forms;
	using Math = System.Math;

	internal class PuzzleRenderer : System.IDisposable
	{
		public PuzzleRenderer( OpenTK.GLControl glControl, System.Action status, System.Func<Macro> selectedMacro, Settings settings )
		{
			m_glControl = glControl;
			m_settings = settings;

			m_mouseMotion = new MouseMotion( m_glControl, m_settings, this.PerformClick, this.MouseMoveInternal );
			m_renderToTexture = new RenderToTexture();
			this.TwistHandler = new TwistHandler( m_glControl, status, settings, m_renderToTexture );

			this.m_glControl.Resize += new System.EventHandler( m_glControl_Resize );
			this.m_glControl.MouseMove += new MouseEventHandler( MouseMove );
			this.m_glControl.MouseLeave += new System.EventHandler( MouseLeave );

			m_status = status;
			m_selectedMacro = selectedMacro;
		}

		public void Dispose()
		{
			m_renderToTexture.Dispose();
		}

		// Internal variables.
		private MouseMotion m_mouseMotion;
		private RenderToTexture m_renderToTexture;

		// References we need.
		private OpenTK.GLControl m_glControl;
		private Puzzle m_puzzle;
		private Settings m_settings;
		System.Action m_status;
		private System.Func<Macro> m_selectedMacro;

		/// <summary>
		/// Our twist handler.
		/// </summary>
		public TwistHandler TwistHandler { get; set; }

		// Save the current display to an svg file.
		public void SaveToSvg()
		{
			var transform = GrabModelTransform();

			List<Polygon> polygons = new List<Polygon>();
			Cell template = m_puzzle.MasterCells.First();
			foreach( Cell cell in m_puzzle.AllCells )
			{
				foreach( Sticker s in template.Stickers )
				{
					Polygon clone = s.Poly.Clone();
					clone.Transform( cell.Isometry.Inverse() );
					polygons.Add( clone );
				}
			}

			SVG.WritePolygons( "output.svg", polygons );
		}

		/// <summary>
		/// Save to a vrml file.
		/// Only works when in surface mode.
		/// </summary>
		public void SaveVrml()
		{
			if( !(m_settings.SurfaceDisplay && m_puzzle.HasSurfaceConfig) )
				return;

			Matrix4D rot = m_mouseMotion.RotHandler4D.Current4dView;
			var boundingBox = m_puzzle.SurfacePoly.BoundingBox;
			Vector3D mid = (boundingBox.Item1 + boundingBox.Item2) / 2;
			double factor = m_surfaceTextureScale;

			Vector3D[] textureVerts = SurfaceTransformedTextureVerts().Select( v =>
			{
				TransformSkewVert( rot, ref v );
				return v;
			} ).ToArray();
			int[] elements = m_puzzle.SurfaceElementIndices;
			Vector3D[] textureCoords = m_puzzle.SurfaceTextureCoords.Select( v => ( ( v - mid ) * factor + new Vector3D( 1, 1 ) ) / 2 ).ToArray();

			string filename = "output.wrl";
			File.Delete( filename );
			VRML.AppendShape( filename, "fundamental.png", textureVerts, elements, 
				textureCoords, reverse: true, skipMiddle: false );
		}

		/// <summary>
		/// Returns true if we are doing repeated rendering from timers.
		/// This was starving UI refreshes.
		/// </summary>
		public bool IntenseRendering
		{
			get
			{
				return
					TwistHandler.Solving ||
					m_mouseMotion.Handler.IsSpinning;
			}
		}

		public void PuzzleUpdated( Puzzle puzzle )
		{
			// Clear out previous textures.
			m_renderToTexture.Dispose();

			m_puzzle = puzzle;
			CalcTextureScale();
			ResetView();

			this.TwistHandler.PuzzleUpdated( m_puzzle );

			// This is so we'll immediately highlight the appropriate twisting circle.
			FindClosestTwistingCircles( m_mouseMotion.Handler.LastX, m_mouseMotion.Handler.LastY );
		}

		public void ResetView()
		{
			Geometry g = m_puzzle == null ? Geometry.Hyperbolic : m_puzzle.Config.Geometry;
			m_mouseMotion.Reset( g );
			if( m_puzzle != null &&
				m_puzzle.HasSurfaceConfig )
			{
				if( m_surface == Surface.Sphere )
					m_mouseMotion.ScaleLookFrom4D( 0.45 );
				if( m_surface == Surface.LawsonKleinBottle )
					m_mouseMotion.ScaleLookFrom4D( 1.5 );
			}
		}

		public const string SurfaceTexture1 = "SurfaceTexture1";
		public const string SurfaceTexture2 = "SurfaceTexture2";	// Needed for spherical puzzles, northern hemisphere.

		public void GenTextures()
		{
			// No view scaling or rotation when drawing texture.
			SetOrthoForTexture();

			// We generate the texture differently when rendering to a surface.
			// We only use a single texture in this case.
			// ZZZ - This may change in the future with more complicated surfaces.
			if( this.ShowOnSurface )
			{
				m_renderToTexture.TextureSize = 2048;
				m_lowerHemisphere = true;
				m_renderToTexture.CreateTexture( SurfaceTexture1, m_settings.EnableTextureMipmaps, RenderSurfaceForTexture );
				m_lowerHemisphere = false;
				m_renderToTexture.CreateTexture( SurfaceTexture2, m_settings.EnableTextureMipmaps, RenderSurfaceForTexture );
				return;
			}

			m_renderToTexture.TextureSize = 512;	// Could be smart about this and increase if number of textures is small.
			foreach( Cell master in m_puzzle.MasterCells )
				if( !m_renderToTexture.CreateTexture( master, m_settings.EnableTextureMipmaps, RenderCellForTexture ) )
					return;
		}

		private bool m_lowerHemisphere = true;

		public void InvalidateTextures()
		{
			m_renderToTexture.InvalidateAllTextures();
		}

		public void RenderForBuilding()
		{
			m_glControl.MakeCurrent();
			SetOrtho();
			GL.Disable( EnableCap.Texture2D );
			SetupStandardGLSettings();

			WaitRadius += .01;
			if( WaitRadius > 5 )
				WaitRadius -= 1;
			for( double radius = WaitRadius; radius >= 0; radius -= .25 )
			{
				CircleNE toDraw = new CircleNE();
				toDraw.Radius = radius;
				GLUtils.DrawCircle( toDraw, Color.DarkSlateBlue, null );
			}
		}

		public void SwapBuffers()
		{
			m_glControl.SwapBuffers();
		}

		public double WaitRadius { get; set; }

		public void Render(bool forceUpdateTexture = false)
		{
			/* Optimization is pretty tricky.
			 * 
			 * Some thoughts:
			 * If we transform before rendering it, we could render the texture for the fundamental area just once.
			 * If we do this, we'll need to store the texture coords though.
			 * Another big downside is that some master cells will be quite warped during render, so texture won't map well onto all slaves.
			 * 
			 * We could render a separate texture for every master cell (to help with distortion problems).
			 * 
			 * We are going to need more triangles for texture mapping in certain situations.  
			 * Texture coords should not be Euclidean!
			 * (Where texture approach seems bad is for lines from centers to edge midpoints, which should be curved in tile images, but is not.)
			 * 
			 * I think the sweet spot is going to be:
			 * Render a separate texture for every master cell. (ugh, during twisting, this will require rendering adjacent cells too.)
			 * Then render all masters directly. (? Maybe just render via textures - doesn't look quite as good though, no antialiasing)
			 * Then render all slaves via master textures.
			 */

			m_glControl.MakeCurrent();

			bool spherical = m_puzzle.Config.Geometry == Geometry.Spherical;
			bool useTexture = !spherical || ShowOnSurface;
			if( useTexture )
			{
				if( forceUpdateTexture )
					InvalidateTextures();

				GenTextures();

				if( this.ShowOnSurface || this.ShowAsSkew )
				{
					bool forPicking = false;
					bool dummy = false;
					RenderSurface( forPicking, 0, 0, ref dummy );
				}
				else
				{
					//SetPerspective();	// Might do this for some models, like pseudosphere.
					SetOrtho();
					RenderUsingTexture();
				}
			}
			else
			{
				SetOrtho();
				RenderDirectly();
			}

			if( !(ShowOnSurface || ShowAsSkew) )
				RenderClosestTwistingCircles();

			//RenderMasterBoundary();
			//Draw14gon();
		}

		private void Draw14gon()
		{
			GL.Disable( EnableCap.Texture2D );
			if( m_14gon == null )
			{
				Tiling tiling = new Tiling();
				tiling.Generate( new TilingConfig( 14, 7, 1 ) );
				m_14gon = tiling;
			}

			Polygon p = m_14gon.Tiles.First().Boundary.Clone();
			p.Transform( m_mouseMotion.Isometry );
			Color color = Color.Gray;
			//GLUtils.DrawConcavePolygon( p, color, GrabModelTransform() );
			foreach( Segment seg in p.Segments )
			{
				Circle c = new Circle( seg.P1, seg.Midpoint, seg.P2 );
				GLUtils.DrawCircle( c, Color.White, GrabModelTransform(), solid: true );
				//GLUtils.DrawSeg( seg, 20, GrabModelTransform() );
			}
		}
		Tiling m_14gon;

		private bool ShowOnSurface
		{
			get
			{
				if( m_puzzle == null )
					return false;

				if( RenderingDisks )
					return true;

				return
					m_settings.SurfaceDisplay && 
					m_puzzle.HasSurfaceConfig;
			}
		}

		private bool RenderingDisks
		{
			get
			{
				if( m_puzzle == null )
					return false;

				// We want the surface display setting to take precedence.
				if( m_puzzle.Config.Geometry == Geometry.Spherical &&
					m_settings.SphericalModel == SphericalModel.HemisphereDisks &&
					!m_settings.SurfaceDisplay )
					return true;

				return false;
			}
		}

		private bool ShowAsSkew
		{
			get
			{
				if( m_puzzle == null )
					return false;

				return
					m_puzzle.OnlySkew ||
					( m_settings.ShowAsSkew && m_puzzle.HasSkew );
			}
		}

		private void RenderClosestTwistingCircles( Isometry additionalTransform = null )
		{
			if( m_closestTwistingCircles == null || !m_settings.HighlightTwistingCircles )
				return;

			if( additionalTransform == null )
				GL.Disable( EnableCap.Texture2D );
			foreach( TwistData twistData in m_closestTwistingCircles.IdentifiedTwistData.TwistDataForDrawing )
			foreach( CircleNE slicingCircle in twistData.CirclesForSliceMask( this.SliceMaskSafe ) )
			{
				CircleNE toDraw = slicingCircle.Clone();
				System.Func<Vector3D,Vector3D> t = null;

				if( additionalTransform == null )
				{
					int lod = LOD( toDraw, stateCalcCell: false );
					if( -1 == lod )
						continue;

					t = GrabModelTransform();
				}
				else
					t = v => additionalTransform.Apply( v );

				var c = m_settings.ColorTwistingCircles;

				// Various special casing for different situations.
				/*if( this.ShowOnSurface )
				{
					GL.Enable( EnableCap.DepthTest );
					// ZZZ - Would need to do polygon offset
					t = v =>
					{
						v = SurfaceTransformedTextureVert( v );
						TransformSkewVert( m_mouseMotion.RotHandler4D.Current4dView, ref v );
						return v;
					};
					GLUtils.DrawCircle( toDraw, c, t );
				}
				else*/ if( m_puzzle.Config.Geometry == Geometry.Spherical )
				{ 
					GLUtils.DrawCircleSafe( toDraw, c, t );
				}
				else if( m_puzzle.Config.Systolic )
				{
					GLUtils.DrawHypercycle( toDraw, c, t );

					// Debugging systolic, but leaving this in because it might be nice to provide an option to draw the pants at some point.
					/*if( m_puzzle.Config.Systolic  )
					{
						GLUtils.DrawPolygonVaryingColor( twistData.Pants.Hexagon, new Color[] { Color.White, Color.Red, Color.White, Color.Green, Color.White, Color.Blue }, t );
						//GLUtils.DrawPolygon( twistData.Pants.Hexagon, Color.DimGray, t );
					}*/

					// Earthquakes have the pants chopped off, and we need to draw that.
					if( m_puzzle.Config.Earthquake && m_closestGeodesicSeg != -1 )
					{
						int pantsSeg = Pants.ChoppedPantsSeg( m_closestGeodesicSeg );
						Polygon clone = twistData.Pants.Hexagon.Clone();
						clone.Transform( m_mouseMotion.Isometry );
						int div = 15;
						GL.Color3( c );
						GLUtils.DrawSeg( clone.Segments[pantsSeg], div, t );
					}
				}
				else
				{
					GLUtils.DrawCircle( toDraw, c, t );
				}
			}
		}
		private TwistData m_closestTwistingCircles = null;

		// This is for systolic puzzles. We need more info than just the closest twisting circles.
		private int m_closestGeodesicSeg = -1;

		/// <summary>
		/// Grabs a model transform based on puzzle geometry and settings.
		/// </summary>
		private System.Func<Vector3D, Vector3D> GrabModelTransform()
		{
			System.Func<Vector3D, Vector3D> transform = null;
			if( m_puzzle.Config.Geometry == Geometry.Hyperbolic &&
				m_settings.HyperbolicModel != HModel.Poincare )
			{
				switch( m_settings.HyperbolicModel )
				{
				case HModel.Klein:
					transform = HyperbolicModels.PoincareToKlein;
					break;
				case HModel.UpperHalfPlane:
					transform = HyperbolicModels.PoincareToUpper;
					break;
				case HModel.Orthographic:
					transform = HyperbolicModels.PoincareToOrtho;
					break;
				}
			}

			if( m_puzzle.Config.Geometry == Geometry.Spherical )
			{
				if( m_settings.SphericalModel == SphericalModel.Gnomonic )
					transform = SphericalModels.StereoToGnomonic;

				if( m_settings.SphericalModel == SphericalModel.Fisheye )
					transform = SphericalModels.GnomonicToStereo;
			}

			return transform;
		}

		private void SetupStandardGLSettings()
		{
			SetupStandardGLSettings( m_settings.ColorBg );
		}

		private void EnableAntiAlias()
		{
			GL.Enable( EnableCap.Multisample );

			// Antialiasing for lines.
			GL.Enable( EnableCap.LineSmooth );
			GL.Hint( HintTarget.LineSmoothHint, HintMode.Nicest );
		}

		private void SetupStandardGLSettings( Color backgroundColor )
		{
			// ZZZ:code - Move some of this setup to to GLUtils class?

			// Polygon antialiasing.
			bool antialias = m_settings.EnableAntialiasing &&
				( m_puzzle == null || m_puzzle.IsSpherical );

			if( antialias )
			{
				EnableAntiAlias();
			}
			else
			{
				GL.Disable( EnableCap.Multisample );
				GL.Disable( EnableCap.LineSmooth );
				GL.Disable( EnableCap.PolygonSmooth );
			}

			GL.Disable( EnableCap.DepthTest );

			GL.ClearColor( backgroundColor );
			GL.Clear( ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit );

			// Alpha blending.
			GL.Enable( EnableCap.Blend );
			GL.BlendFunc( BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha );

			// Culling disabled by default.
			// This is an attempt to avoid some issues Nan saw.
			GL.Disable( EnableCap.CullFace );

			GL.LineWidth( 2.0f );
		}

		private void RenderDirectly( bool renderingTexture = false )
		{
			SetupStandardGLSettings( m_settings.ColorTileEdges );
			if( !renderingTexture )
				GL.Disable( EnableCap.Texture2D );

			// We use the stencil buffer,
			// and need the depth buffer enabled as well.
			GL.ClearStencil( 0 );
			GL.Clear( ClearBufferMask.StencilBufferBit );
			GL.StencilMask( 0x01 );
			GL.Enable( EnableCap.StencilTest );
			GL.Disable( EnableCap.DepthTest );

			// Track what is close to the center.
			ResetClosest();

			foreach( Cell master in m_puzzle.MasterCells )
			{
				DrawCellDirectly( master );

				foreach( Cell slave in m_puzzle.SlaveCells( master ) )
					DrawCellDirectly( slave );
			}

			DrawMovingStickersDirectly();

			// Draw a background disk if needed.
			if( m_settings.SphericalModel == SphericalModel.Fisheye )
				FillBackgroundExceptDisk();

			GL.Disable( EnableCap.StencilTest );
		}

		private void RenderMasterBoundary()
		{
			GL.Disable( EnableCap.Texture2D );
			EnableAntiAlias();
			GL.Enable( EnableCap.PolygonOffsetLine );
			GL.PolygonOffset( 1.0f, 3.0f );
			foreach( Segment s in m_puzzle.MasterBoundary )
			{
				GL.Color3( Color.Red );
				GL.LineWidth( 8.0f );
				Segment clone = s.Clone();
				clone.Transform( m_mouseMotion.Isometry );
				GLUtils.DrawSeg( clone, 75, GrabModelTransform() );
			}
		}

		/// <summary>
		/// Helper to draw a cell.
		/// </summary>
		private void DrawCellDirectly( Cell cell )
		{
			// What will be the closest to origin after panning transform?
			TrackClosest( cell );

			int lod = LOD( cell );
			if( -1 == lod )
				return;

			if( m_settings.ShowOnlyFundamental && !cell.IsMaster )
				return;

			// ZZZ:performance - avoid cloning these by passing in mobius to drawing functions?
			foreach( Sticker sticker in cell.Stickers )
			{
				if( sticker.Twisting )
					continue;

				Polygon p = sticker.Poly.Clone();
				p.Transform( m_mouseMotion.Isometry );
				Color color = GetStickerColor( sticker );
				GLUtils.DrawConcavePolygon( p, color, GrabModelTransform() );
			}
		}

		private Color GetStickerColor( Sticker sticker )
		{
			Color color = m_puzzle.State.GetStickerColor( sticker.CellIndex, sticker.StickerIndex );

			if( m_puzzle.Config.CoxeterComplex )
			{
				bool parity = sticker.StickerIndex % 2 == 0;
				if( m_puzzle.MasterCells[sticker.CellIndex].Isometry.Reflected )
					parity = !parity;

				// Go around 180 degrees on the color wheel. https://stackoverflow.com/a/1165145
				//float hue = color.GetHue();
				//hue = (hue + 180) % 360;
				//color = ColorUtil.HslToRgb( new Vector3D( hue, color.GetSaturation(), color.GetBrightness() ) );

				// Another "reversal" option.
				//color = Color.FromArgb( 255, 255 - color.R, 255 - color.G, 255 - color.B );

				// Simple light-dark scheme.
				color = parity ? Color.White : Color.Gray;
			}

			return color;
		}

		private void DrawMovingStickersDirectly()
		{
			// ZZZ - Code share (95% copied from below).
			SingleTwist twist = this.TwistHandler.CurrrentTwist;
			if( twist != null )
			{
				IdentifiedTwistData identifiedTwistData = twist.IdentifiedTwistData;
				double rotation = this.TwistHandler.SmoothedRotation;
				if( !twist.LeftClick )
					rotation *= -1;

				foreach( TwistData twistData in identifiedTwistData.TwistDataForDrawing )	// NOTE: Must use all twist data.
				{
					Mobius mobius = new Mobius();
					mobius.Elliptic( m_puzzle.Config.Geometry, twistData.Center, twistData.Reverse ? rotation * -1 : rotation );
					Isometry isometry = new Isometry( m_mouseMotion.Isometry );
					isometry.Mobius *= mobius;

					foreach( List<Sticker> list in twistData.AffectedStickersForSliceMask( twist.SliceMask ) )
					foreach( Sticker sticker in list )
					{
						Polygon clone = sticker.Poly.Clone();
						clone.Transform( isometry );
						Color color = GetStickerColor( sticker );
						GLUtils.DrawConcavePolygon( clone, color, GrabModelTransform() );
					}
				}
			}
		}

		private void Vertex( Vector3D v )
		{
			GL.Vertex2( v.X, v.Y );
		}

		private void Vertex( Complex c )
		{
			GL.Vertex2( c.Real, c.Imaginary );
		}

		/// <summary>
		/// Rendering function for generating a texture that can map to a surface.
		/// ZZZ - not general.  I'm coding this for euclidean/spherical puzzles to start.
		/// </summary>
		private void RenderSurfaceForTexture( object key )
		{
			SetupStandardGLSettings( m_settings.ColorTileEdges );

			Mobius m = new Mobius();
			Isometry trans = new Isometry();
			bool clipForElliptical = false;
			if( m_surface == Surface.Sphere || m_surface == Surface.Boys || RenderingDisks )
			{
				// This didn't work because the stencil testing didn't work when rendering to a texture.
				// Maybe there is a trick I can use to make this work later.
				// Hopefully concave polys won't be a big problem in the mean time.
				// RenderDirectly( renderingTexture: true );

				clipForElliptical = true;
				if( !m_lowerHemisphere )
				{
					m.Elliptic( Geometry.Spherical, Complex.ImaginaryOne, Math.PI );
					trans = new Isometry( m, null );
				}

				if( RenderingDisks )
					trans *= m_mouseMotion.Isometry;
			}
			else
			{
				var boundingBox = m_puzzle.SurfacePoly.BoundingBox;
				Vector3D mid = (boundingBox.Item1 + boundingBox.Item2) / 2;
				m.Isometry( Geometry.Euclidean, 0, -mid.ToComplex() );
				trans = new Isometry( m, null );
			}

			foreach( Cell c in m_puzzle.SurfaceRenderingCells )
				RenderCellForTexture( c.IsMaster ? c : c.Master, trans * c.Isometry.Inverse(), clipForElliptical );

			GL.Disable( EnableCap.DepthTest );
			GL.LineWidth( 10.0f );	// Needs to be big to be visible.
			RenderClosestTwistingCircles( trans );
		}

		private Surface m_surface
		{
			get
			{
				Surface? result = m_puzzle?.Config?.SurfaceConfig?.Surface;
				return result == null ? Surface.None : result.Value;
			}
		}

		private void RenderCellForTexture( object key )
		{
			SetupStandardGLSettings( m_settings.ColorTileEdges );

			Cell master = (Cell)key;
			RenderCellForTexture( master, null );
		}

		/// <summary>
		/// Renders a master cell in a standard, origin-centered position.
		/// To render a slave cell in its actual position, pass in its master cell and the isometry for the slave.
		/// </summary>
		private void RenderCellForTexture( Cell master, Isometry transform, bool clipForElliptical = false )
		{
			RenderUnmoving( master, transform, clipForElliptical );
			RenderMoving( master, transform, clipForElliptical );
		}

		const double m_ellipticalClipCutoff = 2.0;

		private void RenderUnmoving( Cell master, Isometry transform, bool clipForElliptical = false )
		{
			Cell template = m_puzzle.MasterCells.First();

			// Draw unmoving stickers.
			for( int i = 0; i < template.Stickers.Count; i++ )
			{
				if( master.Stickers[i].Twisting )
					continue;

				Polygon stickerPoly = template.Stickers[i].Poly;
				if( transform != null )
				{
					stickerPoly = stickerPoly.Clone();
					stickerPoly.Transform( transform );
				}

				if( clipForElliptical )
					if( stickerPoly.Center.Abs() > m_ellipticalClipCutoff )
						continue;

				Color color = GetStickerColor( master.Stickers[i] );
				GLUtils.DrawPolygonSolid( stickerPoly, color );
			}
		}

		private void RenderMoving( Cell master, Isometry transform, bool clipForElliptical = false )
		{
			bool systolic = m_puzzle.Config.Systolic;

			// Draw moving stickers.
			SingleTwist twist = this.TwistHandler.CurrrentTwist;
			if( twist == null )
				return;
			
			IdentifiedTwistData identifiedTwistData = twist.IdentifiedTwistData;
			double rotation = this.TwistHandler.SmoothedRotation;
			if( !twist.LeftClick )
				rotation *= -1;

			int count = 0;
			foreach( TwistData twistData in twist.StateCalcTD )
			{
				count++;
				if( !twistData.AffectedMasterCells.ContainsKey( master ) )
					continue;

				Mobius mobius = twistData.MobiusForTwist( m_puzzle.Config, twist, rotation, 
					count > identifiedTwistData.TwistDataForStateCalcs.Count );
				Isometry isometry = new Isometry( master.Isometry );
				isometry.Mobius *= mobius;

				foreach( List<Sticker> list in twistData.AffectedStickersForSliceMask( twist.SliceMask ) )
				foreach( Sticker sticker in list )	
				{
					if( clipForElliptical && !master.Stickers.Contains( sticker ) )
						continue;

					// Performance boost for systolic.
					if( systolic )
					{
						Vector3D test = sticker.Poly.Center;
						test = isometry.Apply( test );
						if( test.Abs() > 0.5 )
							continue;
					}

					Polygon clone = sticker.Poly.Clone();
					clone.Transform( isometry );
					if( transform != null )
						clone.Transform( transform );

					if( clipForElliptical )
						if( clone.Center.Abs() > m_ellipticalClipCutoff )
							continue;

					Color color = m_puzzle.State.GetStickerColor( sticker.CellIndex, sticker.StickerIndex );
					GLUtils.DrawPolygonSolid( clone, color, fast: systolic );
				}
			}
		}

		private void RenderUsingTexture()
		{
			SetupStandardGLSettings();
			RenderDisk();

			// This was necessary, or the colors got all whack!
			GL.Color3( Color.White );

			ResetClosest();

			for( int i=0; i<m_puzzle.MasterCells.Count; i++ )
			{
				m_trackClosest = i == 0;

				Cell master = m_puzzle.MasterCells[i];
				m_renderToTexture.BindTexture( master );

				GL.Color3( Color.White );
				DrawCellUsingTexture( master );
				foreach( Cell slave in m_puzzle.SlaveCells( master ) )
					DrawCellUsingTexture( slave );
			}
		}

		private void SetSkewMouseControl()
		{
			if( m_puzzle.HasValidIRPConfig )
				m_mouseMotion.ControlType = ControlType.Mouse_3D;
			if( m_puzzle.HasSurfaceConfig || m_puzzle.HasValidSkewConfig )
			{
				m_mouseMotion.ControlType = ControlType.Mouse_4D;

				if( RenderingDisks )
					m_mouseMotion.ControlType = ControlType.Mouse_2D;
			}
		}

		/// <summary>
		/// Helper to fill in the background, except for a disk.
		/// </summary>
		private void FillBackgroundExceptDisk()
		{
			int num = 200;

			Polygon p = new Polygon();
			List<Vector3D> cPoints = new List<Vector3D>();
			for( int i = 0; i < num; i++ )
				cPoints.Add( new Vector3D( Math.Cos( 2 * Math.PI * i / num ), Math.Sin( 2 * Math.PI * i / num ) ) );
			p.CreateEuclidean( cPoints.ToArray() );
			p.Center = new Vector3D( 10.1, 0 );
			GLUtils.DrawConcavePolygon( p, m_settings.ColorBg, v => v );
		}

		/// <summary>
		/// This was necessary to avoid artifacts when the edge color was not the same
		/// as the background color.
		/// </summary>
		private void RenderDisk()
		{
			if( m_puzzle.Config.Geometry != Geometry.Hyperbolic )
				return;

			// Only needed if background color not equal edge color.
			if( m_settings.ColorBg == m_settings.ColorTileEdges )
				return;

			GL.Color3( m_settings.ColorTileEdges );

			if( m_settings.HyperbolicModel == HModel.UpperHalfPlane ||
				m_settings.HyperbolicModel == HModel.Orthographic )
			{
				double big = 10000;
				bool upper = m_settings.HyperbolicModel == HModel.UpperHalfPlane;

				// Our disk is a big rectangle.
				GL.Begin( PrimitiveType.Polygon );
					Vertex( new Vector3D( big, upper ? -1 : -big ) );
					Vertex( new Vector3D( big, big ) );
					Vertex( new Vector3D( -big, big ) );
					Vertex( new Vector3D( -big, upper ? -1 : -big ) );
				GL.End();

				return;
			}

			int num = 250;
			GL.Begin( PrimitiveType.TriangleFan );
				Vertex( new Vector3D() );
				for( int i=0; i<=num; i++ )
				{
					double angle = 2 * Math.PI * i / num;
					Vertex( new Vector3D( Math.Cos( angle ), Math.Sin( angle ), 0 ) );
				}
			GL.End();
		}

		/// <summary>
		/// X, Y, and reverseTwist are only important when forPicking = true.
		/// </summary>
		private void RenderSurface( bool forPicking, int X, int Y, ref bool reverseTwist )
		{
			SetupStandardGLSettings( forPicking ?
				Color.Black :
				m_settings.ColorBg );

			GL.PushAttrib( 
				AttribMask.LightingBit | 
				AttribMask.PolygonBit |
				AttribMask.EnableBit );

			// We need to leave lighting off for picking.
			if( m_settings.EnableLighting && !forPicking )
			{
				GL.Enable( EnableCap.Lighting );
				GL.Enable( EnableCap.Normalize );
				GL.ShadeModel( ShadingModel.Smooth );

				float ambient = (float)m_settings.AmbientLighting;
				Lighting.SetupAmbient( ambient );
				
				// Light mostly directly above (slightly to the right and up from the observer POV)
				Lighting.SetupLightZero( new Vector3D( 3, 2, 10 ), ambient );

				Lighting.SetDefaultMaterial( ambient );
			}

			if( m_settings.EnableStereo )
			{
				SetSkewMouseControl();

				int width = m_glControl.ClientSize.Width;
				int height = m_glControl.ClientSize.Height;
				double ratio = (double)( width / 2 ) / height;	// Each side will have half the full width.

				Vector3D from = m_mouseMotion.ViewLookFrom;
				Vector3D up = m_mouseMotion.ViewUp;

				// Focal length and eye separation depend on settings.
				double focalLength = from.Abs() * m_settings.FocalLength / 2;
				if( focalLength < zNear )
					focalLength = zNear;
				double eyesep = focalLength / 30;
				eyesep *= m_settings.StereoSeparation;
				bool crossEyed = m_settings.StereoType == Settings.EStereoType.CrossEyed;

				// Calc p1/p2 (the two stereo lookfrom points).
				Vector3D cross = up.Cross( from );
				cross.Normalize();
				cross *= eyesep / 2;
				Vector3D p1 = from + cross;
				Vector3D p2 = from - cross;

				//
				// Image 1
				//

				GL.Viewport( crossEyed ? 0 : width / 2, 0, width / 2, height );
				bool rightEye = true;
				SetupProjectionForStereo( rightEye, ratio, focalLength, eyesep, p1, p1 - from, up );

				if( forPicking )
					RenderSurfaceForPickingInternal( X, Y );
				else
					RenderSurfaceInternal();

				//
				// Image 2
				//

				GL.Viewport( crossEyed ? width / 2 : 0, 0, width / 2, height );
				rightEye = false;
				SetupProjectionForStereo( rightEye, ratio, focalLength, eyesep, p2, p2 - from, up );

				if( forPicking )
					RenderSurfaceForPickingInternal( X, Y );
				else
					RenderSurfaceInternal();

				// Put this back.
				GL.Viewport( 0, 0, width, height );
			}
			else
			{
				SetPerspective();
				if( forPicking )
					RenderSurfaceForPickingInternal( X, Y );
				else
					RenderSurfaceInternal();
			}

			if( forPicking )
			{
				// Read Pixel under mouse cursor
				Byte4 pixel = new Byte4();
				GL.ReadPixels( X, m_glControl.Height - Y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, ref pixel );
				if( pixel.B != 0 && m_puzzle.AllTwistData.Count > 0 )
				{
					int twistDataIndex = pixel.R;
					m_closestTwistingCircles = m_puzzle.AllTwistData[twistDataIndex].TwistDataForStateCalcs.First();
					reverseTwist = pixel.G != 0;
				}
			}

			GL.PopAttrib();
		}

		private void SetupProjectionForStereo( bool rightEye, double ratio, double focalLength, double eyesep,
			Vector3D camera, Vector3D lookat, Vector3D up )
		{
			// Reference:
			// http://paulbourke.net/miscellaneous/stereographics/stereorender/

			double radians = this.FOV / 2;
			double wd2 = zNear * System.Math.Tan( radians );
			double ndfl = zNear / focalLength;
			double frustrumOffset = 0.5 * eyesep * ndfl;
			if( rightEye )
				frustrumOffset *= -1;

			GL.MatrixMode( MatrixMode.Projection );
			GL.LoadIdentity();
			double left = -ratio * wd2 + frustrumOffset;
			double right = ratio * wd2 + frustrumOffset;
			double top = wd2;
			double bottom = -wd2;
			GL.Frustum( left, right, bottom, top, zNear, zFar );

			GluLookAt( camera, lookat, up );
		}

		private void RenderSurfaceInternal()
		{
			m_trackClosest = false;

			GL.Enable( EnableCap.DepthTest );
			GL.Color3( Color.White );

			if( this.ShowOnSurface )
				RenderOnSurfaceInternal();
			else
				RenderIRPInternal();
		}

		private void RenderOnSurfaceInternal()
		{
			ShowTextureTrianglesIfNeeded();

			m_lowerHemisphere = true;
			m_renderToTexture.BindTexture( SurfaceTexture1 );
			using( VBO vbo = CreateSurfaceVbo() )
				vbo.Draw();

			if( m_surface == Surface.Sphere || m_surface == Surface.Boys )
			{
				m_lowerHemisphere = false;
				m_renderToTexture.BindTexture( SurfaceTexture2 );
				using( VBO vbo = CreateSurfaceVbo() )
					vbo.Draw();
			}
		}

		private void RenderIRPInternal()
		{
			foreach( Cell irpCell in m_puzzle.IRPCells )
			{
				int masterIndex = irpCell.IndexOfMaster;
				if( masterIndex == -1 )
					continue;
				Cell master = m_puzzle.MasterCells[masterIndex];
				m_renderToTexture.BindTexture( master );

				ShowTextureTrianglesIfNeeded();

				using( VBO vbo = CreateIrpVbo( irpCell ) )
				{
					vbo.Draw();
					if( m_settings.ShowOnlyFundamental )
						continue;

					foreach( Puzzle.Translation translation in m_puzzle.IRPTranslations )
					{
						if( translation.m_x * 2 + 1 > m_settings.XLevels ||
							translation.m_y * 2 + 1 > m_settings.YLevels ||
							translation.m_z * 2 + 1 > m_settings.ZLevels )
							continue;

						GL.PushMatrix();
						{
							GL.Translate( translation.m_translation.X, translation.m_translation.Y, translation.m_translation.Z );
							vbo.Draw();
						}
						GL.PopMatrix();
					}
				}
			}
		}

		private void RenderSurfaceForPickingInternal( int X, int Y )
		{
			m_trackClosest = false;
			m_closestTwistingCircles = null;

			GL.Enable( EnableCap.DepthTest );
			GL.Disable( EnableCap.Texture2D );
			GL.Enable( EnableCap.CullFace );    // We have to color backfacing/frontfacing polygons differently.

			if( this.ShowOnSurface )
				RenderOnSurfaceForPickingInternal( X, Y );
			else
				RenderIRPForPickingInternal( X, Y );
		}

		private void RenderOnSurfaceForPickingInternal( int X, int Y )
		{
			m_lowerHemisphere = true;
			using( VBO vbo1 = CreateSurfacePickVbo( backFacing: false ) )
			using( VBO vbo2 = CreateSurfacePickVbo( backFacing: true ) )
			{
				GL.CullFace( CullFaceMode.Back );
				vbo1.Draw();
				GL.CullFace( CullFaceMode.Front );
				vbo2.Draw();
			}

			if( m_surface == Surface.Sphere || m_surface == Surface.Boys )
			{
				m_lowerHemisphere = false;
				using( VBO vbo1 = CreateSurfacePickVbo( backFacing: false ) )
				using( VBO vbo2 = CreateSurfacePickVbo( backFacing: true ) )
				{
					GL.CullFace( CullFaceMode.Back );
					vbo1.Draw();
					GL.CullFace( CullFaceMode.Front );
					vbo2.Draw();
				}
			}
		}

		private void RenderIRPForPickingInternal( int X, int Y )
		{
			foreach( Cell irpCell in m_puzzle.IRPCells )
			{
				using( VBO vbo1 = CreateIrpPickVbo( irpCell, backFacing: false ) )
				using( VBO vbo2 = CreateIrpPickVbo( irpCell, backFacing: true ) )
				{
					DrawBothSides( vbo1, vbo2, irpCell );
					if( m_settings.ShowOnlyFundamental )
						continue;

					foreach( Puzzle.Translation translation in m_puzzle.IRPTranslations )
					{
						if( translation.m_x * 2 + 1 > m_settings.XLevels ||
							translation.m_y * 2 + 1 > m_settings.YLevels ||
							translation.m_z * 2 + 1 > m_settings.ZLevels )
							continue;

						GL.PushMatrix();
						{
							GL.Translate( translation.m_translation.X, translation.m_translation.Y, translation.m_translation.Z );
							DrawBothSides( vbo1, vbo2, irpCell );
						}
						GL.PopMatrix();
					}
				}
			}
		}

		private void DrawBothSides( VBO vbo1, VBO vbo2, Cell irpCell )
		{
			// "backfacing" doesn't necessarily mean the same as it does for GL,
			// because some of the cells in our tiling are reflected.
			GL.CullFace( !irpCell.Reflected ? CullFaceMode.Back : CullFaceMode.Front );
			vbo1.Draw();
			GL.CullFace( !irpCell.Reflected ? CullFaceMode.Front : CullFaceMode.Back );
			vbo2.Draw();
		}

		/// <summary>
		/// Copied from OpenTK example.
		/// </summary>
		struct Byte4
		{
			public byte R, G, B, A;

			public Byte4( byte[] input )
			{
				R = input[0];
				G = input[1];
				B = input[2];
				A = input[3];
			}

			public override string ToString()
			{
				return this.R + ", " + this.G + ", " + this.B + ", " + this.A;
			}
		}

		/// <summary>
		/// Gets the LOD to use for texturing this cell.
		/// If -1 is returned, don't even draw it.
		/// Higher LOD is more accurate.
		/// NOTE: This transforms in the input circle.
		/// </summary>
		private int LOD( CircleNE circleNE, bool stateCalcCell )
		{
			// First, see if we need to draw this one.
			circleNE.Transform( m_mouseMotion.Isometry );

			// ZZZ - Performance setting should control this cutoff.
			if( !stateCalcCell && circleNE.Radius < .005 )
				return -1;

			// Nothing warps for Euclidean, so we can go low res.
			if( m_puzzle.Config.Geometry == Geometry.Euclidean )
				return 0;

			// Sometimes the transform causes this.
			if( double.IsPositiveInfinity( circleNE.Radius ) )
				return 3;

			//int lod = (int)(vCircle.Radius*15);
			int lod = (int)( System.Math.Pow( circleNE.Radius, .4 ) * 8 );
			if( lod > 3 )
				lod = 3;
			if( lod < 0 )
			{
				System.Diagnostics.Debug.Assert( false );
				lod = 0;
			}

			return lod;
		}

		private int LOD( Cell cell )
		{
			CircleNE vCircle = cell.VertexCircle.Clone();
			return LOD( vCircle, stateCalcCell: m_puzzle.IsStateCalcCell( cell ) );
		}

		/// <summary>
		/// Draw a cell using template info for texture coordinates.
		/// </summary>
		private void DrawCellUsingTexture( Cell cell )
		{
			TrackClosest( cell );

			int lod = LOD( cell );
			if( -1 == lod )
				return;

			if( m_settings.ShowStateCalcCells && !m_puzzle.IsStateCalcCell( cell ) )
				return;

			if( m_settings.ShowOnlyFundamental && !cell.IsMaster )
				return;

			ShowTextureTrianglesIfNeeded();

			// NOTE: Everybody uses texture coords of template!
			Cell template = m_puzzle.MasterCells.First();
			Vector3D[] textureCoords = template.TextureCoords;
			Vector3D[] textureVerts = cell.TextureVertices;

			int[] elements = m_puzzle.TextureHelper.ElementIndices[lod];

			//abcxq72::+switch (from Sarah, expert genius coder)
			HyperbolicModel model = HyperbolicModel.Poincare;
			if( m_puzzle.Config.Geometry == Geometry.Hyperbolic )
			{
				switch( m_settings.HyperbolicModel )
				{
				case HModel.Poincare:
					model = HyperbolicModel.Poincare;
					break;
				case HModel.Klein:
					model = HyperbolicModel.Klein;
					break;
				case HModel.UpperHalfPlane:
					model = HyperbolicModel.UpperHalfPlane;
					break;
				case HModel.Orthographic:
					model = HyperbolicModel.Orthographic;
					break;
				}
			}
			
			HyperbolicModels.DrawElements( model, textureCoords, textureVerts, elements, m_mouseMotion.Isometry, m_textureScale );
		}

		private void ShowTextureTrianglesIfNeeded()
		{
			if( !m_settings.ShowTextureTriangles )
				return;

			GL.Color3( m_puzzle.State.GetStickerColor( 0, 0 ) );
			GL.Disable( EnableCap.Texture2D );
			GL.PolygonMode( MaterialFace.FrontAndBack, PolygonMode.Line );
			GL.LineWidth( 1.25f );
		}

		private void CalcNormal( Vector3D[] textureVerts, int[] elements, int idx, ref Vector3D normal, double lengthCutoff, ref bool skip )
		{
			if( idx % 3 != 0 )
				return;

			// Cull portions of large spheres which should really be out at infinity.
			Vector3D p1 = textureVerts[elements[idx + 0]];
			Vector3D p2 = textureVerts[elements[idx + 1]];
			Vector3D p3 = textureVerts[elements[idx + 2]];
			double length = Euclidean3D.MaxTriangleEdgeLengthAfterTransform( ref p1, ref p2, ref p3, 
				TransformFunc( m_mouseMotion.RotHandler4D.Current4dView ) );
			skip = length >= lengthCutoff;    // Tried an area check too, but this worked better.

			normal = Euclidean3D.NormalFrom3Points( p1, p2, p3 );
		}

		/// <summary>
		/// ZZZ - This is inefficient.  We could precalculate this at puzzle build time.
		/// </summary>
		private Vector3D[] SurfaceTransformedTextureVerts()
		{
			Vector3D[] textureCoords = m_puzzle.SurfaceTextureCoords;
			Vector3D[] textureVerts = textureCoords.Select( v => SurfaceTransformedTextureVert( v ) ).ToArray();
			return textureVerts;
		}

		private Vector3D SurfaceTransformedTextureVert( Vector3D v )
		{
			if( RenderingDisks )
			{
				Vector3D off = new Vector3D( 1, 0 );
				if( m_lowerHemisphere )
					v -= off;
				else
					v += off;
				return v;
			}

			switch( m_surface )
			{
			case Surface.Sphere:
				v = Spherical2D.PlaneToSphere( v );
				if( !m_lowerHemisphere )
					v.RotateAboutAxis( new Vector3D( 0, 1 ), Math.PI );
				break;
			case Surface.Boys:
				v = R3.Geometry.Surface.MapToBoys( v );
				break;
			case Surface.CliffordTorus:
				v = Torus.MapRhombusToUnitSquare( m_puzzle.SurfacePoly.Vertices[1], m_puzzle.SurfacePoly.Vertices[3], v );
				v = Torus.MapToClifford( v );
				break;
			case Surface.LawsonKleinBottle:
				v = Torus.MapRhombusToUnitSquare( m_puzzle.SurfacePoly.Vertices[1], m_puzzle.SurfacePoly.Vertices[3], v );
				v = KleinBottle.MapToLawson( v,
					m_puzzle.Config.P == 4 && m_puzzle.Config.ExpectedNumColors == 9 ); // ZZZ - Hacky to handle this one puzzle differently.
				break;
			}

			return v;
		}

		private VBO CreateSurfaceVbo()
		{
			var boundingBox = m_puzzle.SurfacePoly.BoundingBox;
			Vector3D mid = (boundingBox.Item1 + boundingBox.Item2) / 2;

			Matrix4D rot = m_mouseMotion.RotHandler4D.Current4dView;
			Vector3D[] textureCoords = m_puzzle.SurfaceTextureCoords;
			Vector3D[] textureVerts = SurfaceTransformedTextureVerts();
			int[] elements = m_puzzle.SurfaceElementIndices;

			List<VertexPositionNormalTexture> vboVertices = new List<VertexPositionNormalTexture>();
			List<int> vboElements = new List<int>();

			Vector3D normal = Vector3D.DneVector();
			bool skip = false;
			double factor = m_surfaceTextureScale;
			for( int i = 0; i < elements.Length; i++ )
			{
				int idx = elements[i];
				Vector3D vert = textureVerts[idx];

				TransformSkewVert( rot, ref vert );
				CalcNormal( textureVerts, elements, i, ref normal, 20, ref skip );
				if( skip )
					vert = Vector3D.DneVector();

				// This is because we centered the texture when creating it.
				Vector3D texCoord = textureCoords[idx] - mid;

				vboVertices.Add( new VertexPositionNormalTexture(
					(float)vert.X, (float)vert.Y, (float)vert.Z,
					(float)normal.X, (float)normal.Y, (float)normal.Z,
					(float)(texCoord.X * factor + 1) / 2, (float)(texCoord.Y * factor + 1) / 2 ) );
				vboElements.Add( (int)i );
			}

			VBO vbo = new VBO();
			vbo.Create( vboVertices.ToArray(), vboElements.ToArray() );
			return vbo;
		}

		private VBO CreateIrpVbo( Cell irpCell )
		{
			Cell template = m_puzzle.MasterCells.First();
			int lod = 3;

			Vector3D[] textureVerts = irpCell.TextureVertices;
			Vector3D[] textureCoords = template.TextureCoords;
			int[] elements = m_puzzle.TextureHelper.ElementIndices[lod];

			Matrix4D rot = m_mouseMotion.RotHandler4D.Current4dView;
			Vector3D normal = irpCell.Boundary.Normal;
			if( m_puzzle.HasValidSkewConfig )
				normal = irpCell.Boundary.NormalAfterTransform( TransformFunc( rot ) );

			List<VertexPositionNormalTexture> vboVertices = new List<VertexPositionNormalTexture>();
			List<int> vboElements = new List<int>();

			bool skip = false;
			double factor = m_textureScale;
			for( int i = 0; i < elements.Length; i++ )
			{
				int idx = elements[i];
				Vector3D vert = textureVerts[idx];

				if( m_puzzle.HasValidSkewConfig )
				{
					TransformSkewVert( rot, ref vert );
					if( m_settings.ConstrainToHypersphere )
						CalcNormal( textureVerts, elements, i, ref normal, 20, ref skip );
				}

				if( skip )
					vert = Vector3D.DneVector();

				vboVertices.Add( new VertexPositionNormalTexture(
					(float)vert.X, (float)vert.Y, (float)vert.Z,
					(float)normal.X, (float)normal.Y, (float)normal.Z,
					(float)( textureCoords[idx].X * factor + 1 ) / 2, (float)( textureCoords[idx].Y * factor + 1 ) / 2 ) );
				vboElements.Add( (short)i );
			}

			VBO vbo = new VBO();
			vbo.Create( vboVertices.ToArray(), vboElements.ToArray() );
			return vbo;
		}

		/// <summary>
		/// Helper to do some view transformations needed for skew puzzles.
		/// </summary>
		private void TransformSkewVert( Matrix4D rot, ref Vector3D vert )
		{
			if( RenderingDisks )
				return;

			if( m_settings.ConstrainToHypersphere )
				vert.Normalize();
			vert = rot.RotateVector( vert );
			vert = vert.ProjectTo3DSafe( m_mouseMotion.ProjectionDistance4D );
		}

		/// <summary>
		/// Convenient delegate version of TransformSkewVert.
		/// </summary>
		private System.Func<Vector3D, Vector3D> TransformFunc( Matrix4D rot )
		{
			return v =>
			{
				TransformSkewVert( rot, ref v );
				return v;
			};
		}

		private VBO CreateSurfacePickVbo( bool backFacing )
		{
			Matrix4D rot = m_mouseMotion.RotHandler4D.Current4dView;
			Vector3D[] textureVerts = SurfaceTransformedTextureVerts();
			int[] elements = m_puzzle.SurfaceElementIndices;

			List<VertexPositionColor> vboVertices = new List<VertexPositionColor>();
			List<int> vboElements = new List<int>();

			Vector3D normal = Vector3D.DneVector();
			bool skip = false;
			double factor = m_surfaceTextureScale;
			for( int i = 0; i < elements.Length; i++ )
			{
				int idx = elements[i];
				Vector3D vert = textureVerts[idx];

				TransformSkewVert( rot, ref vert );
				CalcNormal( textureVerts, elements, i, ref normal, 20, ref skip );
				if( skip )
					vert = Vector3D.DneVector();

				// We'll color backfacing differently, so that we can reverse twisting for those.
				// We also have to take into consideration the orienation of the twist data itself.
				var td = m_puzzle.SurfaceElementTwistData1[i / 3];
				if( m_surface == Surface.Sphere && !m_lowerHemisphere )
					td = m_puzzle.SurfaceElementTwistData2[i / 3];
				Color c = Color.Black;
				if( td != null )
				{
					int orientation = backFacing ^ td.Reverse ? 1 : 0;
					c = Color.FromArgb( td.IdentifiedTwistData.Index, orientation, 1 );
				}

				vboVertices.Add( new VertexPositionColor( (float)vert.X, (float)vert.Y, (float)vert.Z, c ) );
				vboElements.Add( (int)vboElements.Count );
			}

			VBO vbo = new VBO();
			vbo.Create( vboVertices.ToArray(), vboElements.ToArray() );
			return vbo;
		}

		private VBO CreateIrpPickVbo( Cell irpCell, bool backFacing )
		{
			PickInfo[] pickInfoArray = irpCell.PickInfo;
			List<VertexPositionColor> vboVertices = new List<VertexPositionColor>();
			List<int> vboElements = new List<int>();

			Matrix4D rot = m_mouseMotion.RotHandler4D.Current4dView;
			foreach( PickInfo pi in pickInfoArray )
			{
				// We'll color backfacing differently, so that we can reverse twisting for those.
				Color c = Color.FromArgb( pi.TwistData.IdentifiedTwistData.Index, backFacing ? 1 : 0, 1 );  // ZZZ - limited to 256, so probably not always enough.

				if( m_puzzle.HasValidIRPConfig )
				{
                    List<short> _elements = new List<short>();
					VBO.PolygonToVerts( pi.Poly, c, vboVertices, _elements );
                    vboElements = _elements.Select(i => (int)i).ToList();
                }
				else // HasValidSkewConfig
				{
					bool skip = false;
					for( int i = 0; i < pi.SubdividedElements.Length; i++ )
					{
						int idx = pi.SubdividedElements[i];
						Vector3D vert = pi.SubdividedVerts[idx];
						TransformSkewVert( rot, ref vert );

						// NOTE: This culling doesn't exactly match drawing since PickInfo coords don't match TextureCoords.
						//		 The PickInfo coords are more dense. We'll therefore use a smaller cutoff here,  
						//		 causing more culling and twisting to not work in outer areas.
						//		 That should be better than unintended twisting happening where it shouldn't.
						Vector3D dummy = new Vector3D();
						CalcNormal( pi.SubdividedVerts, pi.SubdividedElements, i, ref dummy, 10, ref skip );
						if( skip )
							vert = Vector3D.DneVector();

						vboVertices.Add( new VertexPositionColor( (float)vert.X, (float)vert.Y, (float)vert.Z, c ) );
						vboElements.Add( (int)vboElements.Count );
					}
				}
			}

			VBO vbo = new VBO();
			vbo.Create( vboVertices.ToArray(), vboElements.ToArray() );
			return vbo;
		}

		private bool m_trackClosest = false;
		private double m_closestDist = double.MaxValue;

		private void ResetClosest()
		{
			m_closestDist = double.MaxValue;
			m_mouseMotion.Closest = null;
			m_mouseMotion.Template = m_puzzle.MasterCells.First();
		}

		/// <summary>
		/// Tracks the cell which is currently being drawn closest to the origin.
		/// </summary>
		private void TrackClosest( Cell cell )
		{
			// Are we tracking?
			if( !m_trackClosest )
				return;

			Complex transformedCenter = m_mouseMotion.Isometry.Apply( cell.Boundary.Center.ToComplex() );
			
			double distToOrigin = transformedCenter.Magnitude;
			if( distToOrigin < m_closestDist )
			{
				m_closestDist = distToOrigin;
				m_mouseMotion.Closest = cell;
			}
		}

		private void SetOrtho( float scaleFactor = 1.0f )
		{
			m_mouseMotion.ControlType = ControlType.Mouse_2D;

			float scale = (float)m_glControl.Height / (2 * m_mouseMotion.ViewScale * scaleFactor );

			GL.MatrixMode( MatrixMode.Projection );
			OpenTK.Matrix4 proj = OpenTK.Matrix4.CreateOrthographic( m_glControl.Width / scale, m_glControl.Height / scale, 1, -1 );
			OpenTK.Matrix4 rot = OpenTK.Matrix4.CreateRotationZ( m_mouseMotion.Rotation );
			OpenTK.Matrix4 result = OpenTK.Matrix4.Mult( rot, proj );
			GL.LoadMatrix( ref result );

			GL.MatrixMode( MatrixMode.Modelview );
			GL.LoadIdentity();
		}

		private void SetPerspective()
		{
			SetSkewMouseControl();

			if( RenderingDisks )
			{
				m_mouseMotion.RotHandler4D.Current4dView = null;
				SetOrtho( 2.0f );
				return;
			}

			OpenTK.Matrix4d proj, modelView;
			SetPerspective( m_glControl.Width, m_glControl.Height, m_mouseMotion.ViewLookFrom,
				new Vector3D(), m_mouseMotion.ViewUp, out proj, out modelView );
		}

		private void SetPerspective( int width, int height, Vector3D lookFrom, Vector3D lookAt, Vector3D up,
			out OpenTK.Matrix4d proj, out OpenTK.Matrix4d modelView )
		{
			if( height > 0 )
			{
				GL.MatrixMode( MatrixMode.Projection );
				proj = OpenTK.Matrix4d.CreatePerspectiveFieldOfView( this.FOV,
					(double)width / height, zNear, zFar );
				GL.LoadMatrix( ref proj );
			}
			else
				proj = new OpenTK.Matrix4d();

			// Setup the view.
			modelView = GluLookAt( lookFrom, lookAt, up );
		}

		private double zNear
		{
			get { return 1.0; }
		}

		private double zFar
		{
			get { return 200.0; }
		}

		private double FOV
		{
			get { return System.Math.PI / 4; }
		}

		private OpenTK.Matrix4d GluLookAt( Vector3D lookFrom, Vector3D lookAt, Vector3D up )
		{
			GL.MatrixMode( MatrixMode.Modelview );
			OpenTK.Vector3d fromTK = new OpenTK.Vector3d( lookFrom.X, lookFrom.Y, lookFrom.Z );
			OpenTK.Vector3d atTK = new OpenTK.Vector3d( lookAt.X, lookAt.Y, lookAt.Z );
			OpenTK.Vector3d upTK = new OpenTK.Vector3d( up.X, up.Y, up.Z );
			OpenTK.Matrix4d modelView = OpenTK.Matrix4d.LookAt( fromTK, atTK, upTK );
			GL.LoadMatrix( ref modelView );
			return modelView;
		}

		float m_textureScale = 1.0f;
		float m_surfaceTextureScale = 1.0f;
		private void CalcTextureScale()
		{
			Cell template = m_puzzle.MasterCells.First();
			double width = 2 * template.VertexCircle.Radius;
			m_textureScale = (float)(2.0 / width);

			if( m_puzzle.SurfacePoly == null )
				return;

			if( m_surface == Surface.Sphere || m_surface == Surface.Boys )
				m_surfaceTextureScale = 1.0f;
			else
			{
				var boundingBox = m_puzzle.SurfacePoly.BoundingBox;
				Vector3D diag = boundingBox.Item2 - boundingBox.Item1;
				width = Math.Max( Math.Abs( diag.X ), Math.Abs( diag.Y ) );
				m_surfaceTextureScale = (float)(2.0 / width);
			}
		}

		private void SetOrthoForTexture()
		{
			m_mouseMotion.ControlType = ControlType.Mouse_2D;

			GL.MatrixMode( MatrixMode.Projection );
			float texScale = ShowOnSurface ?
				m_surfaceTextureScale :
				m_textureScale;
			OpenTK.Matrix4 proj = OpenTK.Matrix4.CreateOrthographic( 2.0f / texScale, 2.0f / texScale, 1, -1 );
			GL.LoadMatrix( ref proj );

			GL.MatrixMode( MatrixMode.Modelview );
			GL.LoadIdentity();
		}

		internal void m_glControl_Resize( object sender, System.EventArgs e )
		{
			OpenTK.GLControl c = sender as OpenTK.GLControl;

			// This was in the OpenTK sample, but caused a bug when minimizing the window.
			//if( c.ClientSize.Height == 0 )
			//	c.ClientSize = new System.Drawing.Size( c.ClientSize.Width, 1 );

			GL.Viewport( 0, 0, c.ClientSize.Width, c.ClientSize.Height );
			m_mouseMotion.UpdateImageSpace();

			// Resizing messes this up!  So just turn it off.
			m_mouseMotion.Handler.IsSpinning = false;

			c.Invalidate();
		}

		/////////////////////////////////////////////////////////////////////////////// 
		// ZZZ - Everything from here down should be moved out of the renderer

		/// <summary>
		/// Given screen coords, find the closest twisting circles.
		/// Returns true if the they changed.
		/// </summary>
		private bool FindClosestTwistingCircles( int X, int Y )
		{
			if( m_puzzle == null )
				return false;

			if( m_lastX < 0 || m_lastY < 0 )
			{
				m_closestTwistingCircles = null;
				return true;
			}

			object previous = (object)m_closestTwistingCircles;

			// If we are showing on the surface, we have to do a complete
			// render for picking to figure out what is closest.
			if( ShowOnSurface && !RenderingDisks )
			{
				// Don't do this when animating (spinning, twisting or dragging).  It's too slow.
				if( TwistHandler.Twisting || 
					m_mouseMotion.Handler.IsDragging ||
					m_mouseMotion.Handler.IsSpinning )
					return false;

				bool forPicking = true;
				bool reverseTwist = false;
				RenderSurface( forPicking, X, Y, ref reverseTwist );
				return previous != (object)m_closestTwistingCircles;
			}

			Vector3D? spaceCoordsNoMouseMotion = SpaceCoordsNoMouseMotion( X, Y );
			if( !spaceCoordsNoMouseMotion.HasValue )
			{
				bool ret = m_closestTwistingCircles != null;
				m_closestTwistingCircles = null;
				return ret;
			}

			m_closestTwistingCircles = m_puzzle.ClosestTwistingCircles( spaceCoordsNoMouseMotion.Value );
			object newlyFound = (object)m_closestTwistingCircles;

			// Twisting is more subtle for systolic (including earthquake) puzzles.
			if( m_puzzle.Config.Systolic )
			{
				int prev = m_closestGeodesicSeg;
				Vector3D mouse = spaceCoordsNoMouseMotion.Value;
				m_closestGeodesicSeg = m_closestTwistingCircles.Pants.ClosestGeodesicSeg( mouse );
				if( prev != m_closestGeodesicSeg )
					return true;
			}

			// Did we change?
			if( newlyFound != previous )
			{
				//System.Diagnostics.Trace.WriteLine( string.Format( "coords:{0}\treverse:{1}",
				//	spaceCoordsNoMouseMotion, m_closestTwistingCircles.Reverse ) );
				return true;
			}

			return false;
		}

		private Cell FindClosestCell( int X, int Y, out Vector3D? spaceCoordsNoMouseMotion )
		{
			spaceCoordsNoMouseMotion = SpaceCoordsNoMouseMotion( X, Y );

			if( m_puzzle == null || !spaceCoordsNoMouseMotion.HasValue )
				return null;

			return m_puzzle.ClosestCell( spaceCoordsNoMouseMotion.Value );
		}

		/// <summary>
		/// Go from screen to space coords.
		/// NOTE: This will return null if we are outside the bounds of the Poincare Disk (for hyperbolic geometry).
		/// </summary>
		private Vector3D? SpaceCoordsNoMouseMotion( int X, int Y )
		{
			if( m_puzzle == null )
				return null;

			m_mouseMotion.Recenter();
			Vector3D screenCoords = new Vector3D( X, Y );
			Vector3D spaceCoords = m_mouseMotion.ScreenToGL( screenCoords );
			Vector3D spaceCoordsNoMouseMotion = m_mouseMotion.Isometry.Inverse().Apply( spaceCoords );

			// Clamp it.
			// ZZZ - magic number here should be shared with code in Mouse.cs.
			if( m_puzzle.Config.Geometry == Geometry.Hyperbolic )
			{
				const double max = 0.98;
				if( spaceCoords.Abs() > max )
					return null;
			}

			return spaceCoordsNoMouseMotion;
		}

		private void MouseMove( object sender, MouseEventArgs e )
		{
			m_lastX = e.X;
			m_lastY = e.Y;
			MouseMoveInternal();
		}
		int m_lastX, m_lastY;

		private void MouseMoveInternal()
		{
			if( this.ShowAsSkew )
				return;

			if( m_findingTwistingCircles )
				return;

			m_findingTwistingCircles = true;
			if( FindClosestTwistingCircles( m_lastX, m_lastY ) || RenderingDisks )
			{
				if( ShowOnSurface )
					InvalidateTextures();

				m_glControl.Invalidate();
			}
			m_findingTwistingCircles = false;
		}
		private bool m_findingTwistingCircles = false;

		private void MouseLeave( object sender, System.EventArgs e )
		{
			m_lastX = m_lastY = -1;
			m_closestTwistingCircles = null;
			m_glControl.Invalidate();
		}

		private void PerformTogglingClick(ClickData clickData)
		{
			if (this.ShowAsSkew)
			{
				string message = "Sorry, Lights On moves are not supported on the skew view at this time. To turn off the skew view, go to Settings -> Skew Polyhedra and set \"Show as Skew\" to False";
				System.Windows.Forms.MessageBox.Show(message, "Unsupported", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			Vector3D? spaceCoordsNoMouseMotion;
			Cell closest = FindClosestCell(clickData.X, clickData.Y, out spaceCoordsNoMouseMotion);
			if (closest == null || !spaceCoordsNoMouseMotion.HasValue)
				return;
			this.TwistHandler.Toggle(closest);

			m_glControl.Invalidate();
			Render(true);
		}

		private void PerformClick( ClickData clickData )
		{
			// Handle macros.
			if( AltDown )
			{
				if( this.ShowAsSkew )
				{
					string message = "Sorry, macros not supported on skew polyhedra puzzles at this time.";
					System.Windows.Forms.MessageBox.Show( message, "Unsupported", MessageBoxButtons.OK, MessageBoxIcon.Information );
				}

				// Get info about where we've clicked.
				Vector3D? spaceCoordsNoMouseMotion;
				Cell closest = FindClosestCell( clickData.X, clickData.Y, out spaceCoordsNoMouseMotion );
				if( closest == null || !spaceCoordsNoMouseMotion.HasValue )	// ZZZ - make more robust
					return;

				// Starting a macro?
				if( CtrlDown && clickData.Button == MouseButtons.Left )
				{
					Macro m = this.TwistHandler.m_workingMacro;
					m.SetupMobius( closest, spaceCoordsNoMouseMotion.Value, m_puzzle, m_mouseMotion.Isometry.Reflected );
					m.StartRecording();
					m_status();
				}
				else
				{
					// We're executing a macro.
					bool left = clickData.Button == MouseButtons.Left;
					bool right = clickData.Button == MouseButtons.Right;
					if( left || right )
					{
						Macro selected = m_selectedMacro();
						if( selected == null )
							return;

						Macro transformedMacro = selected.Transform( closest, spaceCoordsNoMouseMotion.Value, 
							m_puzzle, m_mouseMotion.Isometry.Reflected );
						this.TwistHandler.ApplyMacro( transformedMacro, right );
					}
				}

				return;
			}	

			//
			// From here on down, we're doing normal (non-macro) twisting.
			//
			bool skewReverseTwist = false;
			if( (ShowOnSurface || ShowAsSkew) && !RenderingDisks )
			{
				bool forPicking = true;
				if( m_puzzle.AllTwistData.Count > 0 )	// Trying to do picking on tilings will cause issues.
					RenderSurface( forPicking, clickData.X, clickData.Y, ref skewReverseTwist );
			}
			else
			{
				FindClosestTwistingCircles( clickData.X, clickData.Y );
			}

			if( m_puzzle.Config.IsToggling)
			{
				PerformTogglingClick(clickData);
			}

			if( m_closestTwistingCircles == null )
			{
				return;
			}

			SingleTwist twist = new SingleTwist();
			twist.IdentifiedTwistData = m_closestTwistingCircles.IdentifiedTwistData;
			twist.LeftClick = clickData.Button == MouseButtons.Left;
			
			if( m_puzzle.Config.Systolic )
			{
				twist.SliceMask = MagicTile.SliceMask.DirSegToMask( m_closestGeodesicSeg );

				// Only the earthquake really needs the following,
				// though I did consider including for all systolic puzzles (since there are effectively these extra identifications).
				if( m_puzzle.Config.Earthquake )
				{
					TwistData td = m_closestTwistingCircles;
					Vector3D lookup = td.Pants.TinyOffset( m_closestGeodesicSeg );
					int choppedPantsSeg = Pants.ChoppedPantsSeg( m_closestGeodesicSeg );
					Vector3D reflected = td.Pants.Hexagon.Segments[choppedPantsSeg].ReflectPoint( lookup );
					TwistData tdSystolic = m_puzzle.ClosestTwistingCircles( reflected );

					twist.IdentifiedTwistDataSystolic = tdSystolic.IdentifiedTwistData;
					twist.SliceMaskSystolic = MagicTile.SliceMask.DirSegToMask( tdSystolic.Pants.ClosestGeodesicSeg( reflected ) );
				}
			}
			else
				twist.SliceMask = this.SliceMaskSafe;
			
			// Correction when clicking on mirrored tiles for non-orientable puzzles.
			// We want the user to always see the tiles they left-click turn CCW.
			if( m_mouseMotion.Isometry.Reflected ^ m_closestTwistingCircles.Reverse )
				twist.LeftClick = !twist.LeftClick;
			
			// This correction is for skew puzzles.
			if( skewReverseTwist )
				twist.LeftClick = !twist.LeftClick;

			this.TwistHandler.StartRotate( twist );
		}

		/// <summary>
		/// The current slicemask.  
		/// ZZZ - We need this for rendering too, but should this live elsewhere?
		/// </summary>
		public int SliceMask { get; set; }

		/// <summary>
		/// Uses the correct "slice" for systolic puzzles, 
		/// and ensures a slice if no slice selected.
		/// </summary>
		public int SliceMaskSafe
		{
			get
			{
				if( m_puzzle.Config.Systolic )
					return MagicTile.SliceMask.DirSegToMask( m_closestGeodesicSeg );

				return 0 == SliceMask ? 1 : SliceMask;
			}
		}	

		private bool CtrlDown
		{
			get
			{
				return ( Form.ModifierKeys & Keys.Control ) == Keys.Control;
			}
		}

		private bool AltDown
		{
			get
			{
				return ( Form.ModifierKeys & Keys.Alt ) == Keys.Alt;
			}
		}
	}
}
