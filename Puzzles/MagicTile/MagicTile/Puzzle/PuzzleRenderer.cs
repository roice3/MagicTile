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

			this.m_glControl.Resize += new System.EventHandler( this.m_glControl_Resize );
			this.m_glControl.MouseMove += new MouseEventHandler( this.MouseMove );
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
			foreach( Cell cell in m_puzzle.AllCells )
			{
				foreach( Sticker s in cell.Stickers )
					polygons.Add( s.Poly );
			}

			SVG.WritePolygons( "output.svg", polygons );
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
			m_mouseMotion.Reset( puzzle.Config.Geometry );

			this.TwistHandler.PuzzleUpdated( m_puzzle );

			// This is so we'll immediately highlight the appropriate twisting circle.
			FindClosestTwistingCircles( m_mouseMotion.Handler.LastX, m_mouseMotion.Handler.LastY );
		}

		public void ResetView()
		{
			Geometry g = m_puzzle == null ? Geometry.Hyperbolic : m_puzzle.Config.Geometry;
			m_mouseMotion.Reset( g );
		}

		public void GenTextures()
		{
			// No view scaling or rotation when drawing texture.
			SetOrthoForTexture();
			foreach( Cell master in m_puzzle.MasterCells )
				if( !m_renderToTexture.CreateTexture( master, m_settings.EnableTextureMipmaps, this.RenderObjectsForTexture ) )
					return;
		}

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

			m_glControl.SwapBuffers();
		}
		public double WaitRadius { get; set; }

		public void Render()
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

			bool useTexture = m_puzzle.Config.Geometry != Geometry.Spherical;
			if( useTexture )
			{
				GenTextures();

				if( this.ShowAsSkew )
				{
					bool forPicking = false;
					bool dummy = false;
					RenderIRP( forPicking, 0, 0, ref dummy );
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

			if( !this.ShowAsSkew )
				RenderClosestTwistingCircles();

			m_glControl.SwapBuffers();
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

		private void RenderClosestTwistingCircles()
		{
			if( m_closestTwistingCircles == null || !m_settings.HighlightTwistingCircles )
				return;

			GL.Disable( EnableCap.Texture2D );
			foreach( TwistData twistData in m_closestTwistingCircles.IdentifiedTwistData.TwistDataForDrawing )
			foreach( CircleNE slicingCircle in twistData.CirclesForSliceMask( this.SliceMaskEnsureSlice ) )
			{
				CircleNE toDraw = slicingCircle.Clone();
				int lod = LOD( toDraw );
				if( -1 == lod )
					continue;

				if( m_puzzle.Config.Geometry == Geometry.Spherical )
					GLUtils.DrawCircleSafe( toDraw, m_settings.ColorTwistingCircles, GrabModelTransform() );
				else
					GLUtils.DrawCircle( toDraw, m_settings.ColorTwistingCircles, GrabModelTransform() );
			}
		}
		private TwistData m_closestTwistingCircles = null;

		/// <summary>
		/// Grabs a model transform based on puzzle geometry and settings.
		/// </summary>
		private System.Func<Vector3D, Vector3D> GrabModelTransform()
		{
			System.Func<Vector3D, Vector3D> transform = null;
			if( m_puzzle.Config.Geometry == Geometry.Hyperbolic &&
				m_settings.HyperbolicModel == HModel.Klein )
				transform = HyperbolicModels.PoincareToKlein;

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

		private void SetupStandardGLSettings( Color backgroundColor )
		{
			// ZZZ:code - Move some of this setup to to GLUtils class?

			// Polygon antialiasing.
			bool antialias = m_settings.EnableAntialiasing &&
				( m_puzzle == null || m_puzzle.IsSpherical );

			if( antialias )
			{
				GL.Enable( EnableCap.Multisample );

				// Antialiasing for lines.
				GL.Enable( EnableCap.LineSmooth );
				GL.Hint( HintTarget.LineSmoothHint, HintMode.Nicest );

				// Antialiasing for polygons.
				GL.Enable( EnableCap.PolygonSmooth );
				GL.Hint( HintTarget.PolygonSmoothHint, HintMode.Nicest );
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

		private void RenderDirectly()
		{
			SetupStandardGLSettings( m_settings.ColorTileEdges );
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

			// Draw a background disk if we are using the fisheye model.
			if( m_settings.SphericalModel == SphericalModel.Fisheye )
			{
				Polygon p = new Polygon();
				List<Vector3D> cPoints = new List<Vector3D>();
				for( int i = 0; i < 100; i++ )
					cPoints.Add( new Vector3D( Math.Cos( Math.PI * i / 50 ), Math.Sin( Math.PI * i / 50 ) ) );
				p.CreateEuclidean( cPoints.ToArray() );
				p.Center = new Vector3D( 10.1, 0 );
				GLUtils.DrawConcavePolygon( p, m_settings.ColorBg, v => v );
			}

			GL.Disable( EnableCap.StencilTest );
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
				Color color = m_puzzle.State.GetStickerColor( sticker.CellIndex, sticker.StickerIndex );

				GLUtils.DrawConcavePolygon( p, color, GrabModelTransform() );
			}
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
						Color color = m_puzzle.State.GetStickerColor( sticker.CellIndex, sticker.StickerIndex );
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

		private void RenderObjectsForTexture( object key )
		{
			SetupStandardGLSettings( m_settings.ColorTileEdges );

			Cell template = m_puzzle.MasterCells.First();

			Cell master = (Cell)key;

			// Draw unmoving stickers.
			for( int i = 0; i < template.Stickers.Count; i++ )
			{
				if( master.Stickers[i].Twisting )
					continue;

				Sticker sticker = template.Stickers[i];
				Color color = m_puzzle.State.GetStickerColor( master.Stickers[i].CellIndex, master.Stickers[i].StickerIndex );
				GLUtils.DrawPolygonSolid( sticker.Poly, color );
			}

			// Draw moving stickers.
			SingleTwist twist = this.TwistHandler.CurrrentTwist;
			if( twist != null )
			{
				IdentifiedTwistData identifiedTwistData = twist.IdentifiedTwistData;
				double rotation = this.TwistHandler.SmoothedRotation;
				if( !twist.LeftClick )
					rotation *= -1;

				foreach( TwistData twistData in identifiedTwistData.TwistDataForStateCalcs )
				{
					if( !twistData.AffectedMasterCells.ContainsKey( master ) )
						continue;

					Mobius mobius = new Mobius();
					mobius.Elliptic( m_puzzle.Config.Geometry, twistData.Center, twistData.Reverse ? rotation * -1 : rotation );
					Isometry isometry = new Isometry( master.Isometry );
					isometry.Mobius *= mobius;

					foreach( List<Sticker> list in twistData.AffectedStickersForSliceMask( twist.SliceMask ) )
					foreach( Sticker sticker in list )	
					{
						Polygon clone = sticker.Poly.Clone();
						clone.Transform( isometry );
						Color color = m_puzzle.State.GetStickerColor( sticker.CellIndex, sticker.StickerIndex );
						GLUtils.DrawPolygonSolid( clone, color );
					}
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

				DrawCellUsingTexture( master );
				foreach( Cell slave in m_puzzle.SlaveCells( master ) )
					DrawCellUsingTexture( slave );
			}
		}

		private void SetSkewMouseControl()
		{
			if( m_puzzle.HasValidIRPConfig )
				m_mouseMotion.ControlType = ControlType.Mouse_3D;
			if( m_puzzle.HasValidSkewConfig )
				m_mouseMotion.ControlType = ControlType.Mouse_4D;
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

			int num = 250;
			GL.Begin( BeginMode.TriangleFan );

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
		private void RenderIRP( bool forPicking, int X, int Y, ref bool reverseTwist )
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
					RenderIRPForPickingInternal( X, Y );
				else
					RenderIRPInternal();

				//
				// Image 2
				//

				GL.Viewport( crossEyed ? width / 2 : 0, 0, width / 2, height );
				rightEye = false;
				SetupProjectionForStereo( rightEye, ratio, focalLength, eyesep, p2, p2 - from, up );

				if( forPicking )
					RenderIRPForPickingInternal( X, Y );
				else
					RenderIRPInternal();

				// Put this back.
				GL.Viewport( 0, 0, width, height );
			}
			else
			{
				SetPerspective();
				if( forPicking )
					RenderIRPForPickingInternal( X, Y );
				else
					RenderIRPInternal();
			}

			if( forPicking )
			{
				// Read Pixel under mouse cursor
				Byte4 pixel = new Byte4();
				GL.ReadPixels( X, m_glControl.Height - Y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, ref pixel );
				if( pixel.B != 0 )
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

		private void RenderTori()
		{
			Torus.Parameters parameters = new Torus.Parameters()
			{
				NumSegments1 = 50,
				NumSegments2 = 50
			};
			Torus t1 = Torus.CreateClifford( parameters );
			parameters.TubeRadius1 = 0.25;
			Torus t2 = Torus.CreateTorus( parameters );
			parameters.TubeRadius1 = 0.75;
			Torus t3 = Torus.CreateTorus( parameters );

			Matrix4D rot = m_mouseMotion.RotHandler4D.Current4dView;
			t1.Render( TransformFunc( rot ) );
			t2.Render( TransformFunc( rot ) );
			t3.Render( TransformFunc( rot ) );
		}

		private void RenderIRPInternal()
		{
			m_trackClosest = false;

			GL.Enable( EnableCap.DepthTest );
			GL.Color3( Color.White );
			
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

		private void RenderIRPForPickingInternal( int X, int Y )
		{
			m_trackClosest = false;
			m_closestTwistingCircles = null;

			GL.Enable( EnableCap.DepthTest );
			GL.Disable( EnableCap.Texture2D );
			GL.Enable( EnableCap.CullFace );	// We have to color backfacing/frontfacing polygons differently.

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
		private int LOD( CircleNE circleNE )
		{
			// First, see if we need to draw this one.
			circleNE.Transform( m_mouseMotion.Isometry );

			// ZZZ - Performance setting should control this cutoff.
			if( circleNE.Radius < .005 )
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
			return LOD( vCircle );
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
				model = m_settings.HyperbolicModel == HModel.Poincare ? HyperbolicModel.Poincare : HyperbolicModel.Klein;
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
			List<short> vboElements = new List<short>();

			bool skip = false;
			double factor = m_textureScale;
			for( int i = 0; i < elements.Length; i++ )
			{
				int idx = elements[i];
				Vector3D vert = textureVerts[idx];

				if( m_puzzle.HasValidSkewConfig )
				{
					TransformSkewVert( rot, ref vert );
					if( m_settings.ConstrainToHypersphere && i % 3 == 0 )
					{
						// Cull portions of large spheres which should really be out at infinity.
						Vector3D p1 = textureVerts[elements[i+0]];
						Vector3D p2 = textureVerts[elements[i+1]];
						Vector3D p3 = textureVerts[elements[i+2]];
						double length = Euclidean3D.MaxTriangleEdgeLengthAfterTransform( ref p1, ref p2, ref p3, TransformFunc( rot ) );
						skip = length >= 20;	// Tried an area check too, but this worked better.

						normal = Euclidean3D.NormalFrom3Points( p1, p2, p3 );
					}
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

		private VBO CreateIrpPickVbo( Cell irpCell, bool backFacing )
		{
			PickInfo[] pickInfoArray = irpCell.PickInfo;
			List<VertexPositionColor> vboVertices = new List<VertexPositionColor>();
			List<short> vboElements = new List<short>();

			Matrix4D rot = m_mouseMotion.RotHandler4D.Current4dView;
			foreach( PickInfo pi in pickInfoArray )
			{
				// We'll color backfacing differently, so that we can reverse twisting for those.
				Color c = Color.FromArgb( pi.TwistData.IdentifiedTwistData.Index, backFacing ? 1 : 0, 1 );	// ZZZ - limited to 256, so probably not always enough.

				if( m_puzzle.HasValidIRPConfig )
					VBO.PolygonToVerts( pi.Poly, c, vboVertices, vboElements );
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
						if( m_settings.ConstrainToHypersphere && i % 3 == 0 )
						{
							// Cull portions of large spheres which should really be out at infinity.
							Vector3D p1 = pi.SubdividedVerts[pi.SubdividedElements[i+0]];
							Vector3D p2 = pi.SubdividedVerts[pi.SubdividedElements[i+1]];
							Vector3D p3 = pi.SubdividedVerts[pi.SubdividedElements[i+2]];
							double length = Euclidean3D.MaxTriangleEdgeLengthAfterTransform( ref p1, ref p2, ref p3, TransformFunc( rot ) );
							skip = length >= 10;	// smaller cutoff.
						}

						if( skip )
							vert = Vector3D.DneVector();

						vboVertices.Add( new VertexPositionColor( (float)vert.X, (float)vert.Y, (float)vert.Z, c ) );
						vboElements.Add( (short)vboElements.Count );
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

		private void SetOrtho()
		{
			m_mouseMotion.ControlType = ControlType.Mouse_2D;

			float scale = (float)m_glControl.Height / (2 * m_mouseMotion.ViewScale);

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
		private void CalcTextureScale()
		{
			Cell template = m_puzzle.MasterCells.First();
			double width = 2 * template.VertexCircle.Radius;
			m_textureScale = (float)(2.0 / width);
		}

		private void SetOrthoForTexture()
		{
			m_mouseMotion.ControlType = ControlType.Mouse_2D;

			GL.MatrixMode( MatrixMode.Projection );
			OpenTK.Matrix4 proj = OpenTK.Matrix4.CreateOrthographic( 2.0f / m_textureScale, 2.0f / m_textureScale, 1, -1 );
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

			Vector3D? spaceCoordsNoMouseMotion = SpaceCoordsNoMouseMotion( X, Y );
			if( !spaceCoordsNoMouseMotion.HasValue )
			{
				bool ret = m_closestTwistingCircles != null;
				m_closestTwistingCircles = null;
				return ret;
			}

			object previous = (object)m_closestTwistingCircles;
			m_closestTwistingCircles = m_puzzle.ClosestTwistingCircles( spaceCoordsNoMouseMotion.Value );
			object newlyFound = (object)m_closestTwistingCircles;

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

			if( FindClosestTwistingCircles( m_lastX, m_lastY ) )
				m_glControl.Invalidate();
		}

		private void MouseLeave( object sender, System.EventArgs e )
		{
			m_lastX = m_lastY = -1;
			m_closestTwistingCircles = null;
			m_glControl.Invalidate();
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
			// From here on down, we're doing normal twisting.
			//

			bool skewReverseTwist = false;
			if( this.ShowAsSkew )
			{
				bool forPicking = true;
				if( m_puzzle.AllTwistData.Count > 0 )	// Trying to do picking on tilings will cause issues.
					RenderIRP( forPicking, clickData.X, clickData.Y, ref skewReverseTwist );
			}
			else
			{
				FindClosestTwistingCircles( clickData.X, clickData.Y );
			}

			if( m_closestTwistingCircles == null )
				return;

			SingleTwist twist = new SingleTwist();
			twist.IdentifiedTwistData = m_closestTwistingCircles.IdentifiedTwistData;
			twist.LeftClick = clickData.Button == MouseButtons.Left;
			twist.SliceMask = this.SliceMaskEnsureSlice;
			
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
		/// Ensures a slice if no slice selected.
		/// </summary>
		public int SliceMaskEnsureSlice
		{
			get
			{
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
