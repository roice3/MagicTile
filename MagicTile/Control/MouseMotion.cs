namespace MagicTile.Control
{
	using R3.Control;
	using R3.Core;
	using R3.Drawing;
	using R3.Geometry;
	using R3.Math;
	using System.Collections.Generic;
	using System.Linq;
	using System.Numerics;
	using System.Windows.Forms;

	using Math = System.Math;


	enum ControlType
	{
		Mouse_2D,
		Mouse_3D,
		Mouse_4D
	}

	/// <summary>
	/// Helper class to hold the code for panning/rotating/zooming the view.
	/// </summary>
	internal class MouseMotion
	{
		public MouseMotion( OpenTK.GLControl glControl, Settings settings, 
			System.Action<ClickData> clickHandler, System.Action tickleMouse )
		{
			m_glControl = glControl;
			m_settings = settings;
			RotHandler4D = new RotationHandler4D();
			m_tickle = tickleMouse;

			Reset( Geometry.Spherical );
			ControlType = ControlType.Mouse_2D;

			Handler = new MouseHandler();
			Handler.Setup( m_glControl );
			Handler.SetDragHandler( this.PerformDrag );
			Handler.SetSpinHandler( this.PerformSpin );
			Handler.SetClickHandler( clickHandler );

			// Setup our auto-motion timer.
			m_timer = new System.Timers.Timer( 30 );
			m_timer.SynchronizingObject = glControl;
			m_timer.Enabled = false;
			m_timer.Elapsed += new System.Timers.ElapsedEventHandler( SpinStep );
		}

		public MouseHandler Handler { get; private set; }
		private float m_viewScale { get; set; }
		private float m_rotation { get; set; }
		private Isometry m_isometry { get; set; }
		private ImageSpace m_iSpace;

		// 4D related variables.
		public RotationHandler4D RotHandler4D { get; private set; }
		public double ProjectionDistance4D { get; set; }

		// References we need.
		private OpenTK.GLControl m_glControl;
		private Settings m_settings;
		private Geometry m_geometry;
		private System.Action m_tickle;

		/// <summary>
		/// Called to reset us (needs to be done when the puzzle changes).
		/// </summary>
		public void Reset( Geometry g )
		{
			m_geometry = g;
			m_isometry = new Isometry();
			m_rotation = 0;
			m_viewScale = 1.1f;
			m_viewLookFrom3D = new Vector3D( 15, 15, 0 );
			m_viewLookFrom4D = new Vector3D( 0, 0, 7 );
			m_up3D = new Vector3D( 0, 0, 1 );
			RotHandler4D.Current4dView = null;
			ProjectionDistance4D = 1.0;
			UpdateImageSpace();
		}

		/// <summary>
		/// The type of mouse control we're doing.
		/// </summary>
		public ControlType ControlType { get; set; }

		/// <summary>
		/// Returns the current isometry from the mouse motion.
		/// </summary>
		public Isometry Isometry
		{
			get
			{
				return m_isometry;
			}
		}

		/// <summary>
		/// Returns the current rotation from the mouse motion.
		/// </summary>
		public float Rotation
		{
			get
			{
				return m_rotation;
			}
		}

		/// <summary>
		/// Returns the current view scale from the mouse motion.
		/// </summary>
		public float ViewScale
		{
			get
			{
				return m_viewScale;
			}
			set
			{
				m_viewScale = value;
			}
		}

		/// <summary>
		/// The camera location, when the view is 3D.
		/// </summary>
		public Vector3D ViewLookFrom 
		{
			get
			{
				if( this.ControlType == ControlType.Mouse_4D )
					return m_viewLookFrom4D;
				return m_viewLookFrom3D;
			}
		}
		private Vector3D m_viewLookFrom3D;
		private Vector3D m_viewLookFrom4D;

		public void ScaleLookFrom3D( double scale )
		{
			m_viewLookFrom3D *= scale;
		}

		public void ScaleLookFrom4D( double scale )
		{
			m_viewLookFrom4D *= scale;
		}

		/// <summary>
		/// The camera up vector, when the view is 3D.
		/// </summary>
		public Vector3D ViewUp 
		{
			get
			{
				if( this.ControlType == ControlType.Mouse_4D )
					return new Vector3D( 0, 1, 0 );
				return m_up3D;
			}
		}
		private Vector3D m_up3D;

		/// <summary>
		/// The cell closest to the center of the view after a drag.
		/// This is calculated at rendering time and must be set here from there.
		/// It is used to continually recenter infinite tilings.
		/// </summary>
		public Cell Closest { get; set; }
		
		/// <summary>
		/// ZZZ - only here for numerical accuracy.
		/// </summary>
		public Cell Template { get; set; }

		/// <summary>
		/// Helper to go from screen to GL coords.
		/// </summary>
		public Vector3D ScreenToGL( Vector3D screenCoords )
		{
			// Our absolute coordinates of dragged sreen point.
			Vector3D result = m_iSpace.Point( screenCoords );
			result.RotateXY( -m_rotation );
			return ToStandardIfNeeded( result );
		}

		/// <summary>
		/// Helper to transform to standard model if needed.
		/// </summary>
		private Vector3D ToStandardIfNeeded( Vector3D point )
		{
			if( m_geometry == Geometry.Hyperbolic &&
				m_settings.HyperbolicModel != HModel.Poincare )
			{
				if( m_settings.HyperbolicModel == HModel.Klein )
					return HyperbolicModels.KleinToPoincare( point );

				if( m_settings.HyperbolicModel == HModel.UpperHalfPlane )
					return HyperbolicModels.UpperToPoincare( point );

				if( m_settings.HyperbolicModel == HModel.Orthographic )
					return HyperbolicModels.OrthoToPoincare( point );
			}

			if( m_geometry == Geometry.Spherical )
			{
				if( m_settings.SphericalModel == SphericalModel.Gnomonic )
					return SphericalModels.GnomonicToStereo( point );

				if( m_settings.SphericalModel == SphericalModel.Fisheye )
					return SphericalModels.StereoToGnomonic( point );

				if( m_settings.SphericalModel == SphericalModel.HemisphereDisks )
					return SphericalModels.FromDisks( point*2, normalize: false );
			}

			return point;
		}

		/// <summary>
		/// This is here to keep our coordinates consistent after resizing and zooming.
		/// </summary>
		public void UpdateImageSpace()
		{
			m_iSpace = new ImageSpace( m_glControl.Width, m_glControl.Height );
			double aspect = (double)m_glControl.Width / m_glControl.Height;
			m_iSpace.XMax = aspect * m_viewScale;
			m_iSpace.XMin = -aspect * m_viewScale;
			m_iSpace.YMax = m_viewScale;
			m_iSpace.YMin = -m_viewScale;
		}

		/// <summary>
		/// Drag handler.
		/// </summary>
		private void PerformDrag( DragData dragData )
		{
			PerformDragInternal( dragData );

			// Save this for spinning.
			m_lastDragDataQueue.Enqueue( dragData );
			if( m_lastDragDataQueue.Count > m_numToAverage )
				m_lastDragDataQueue.Dequeue();

			m_glControl.Invalidate();
		}

		private void PerformDragInternal( DragData dragData )
		{
			switch( this.ControlType )
			{
				case ControlType.Mouse_2D:
					PerformDrag2D( dragData );
					break;

				case ControlType.Mouse_3D:
					PerformDrag3D( dragData );
					break;

				case ControlType.Mouse_4D:
					PerformDrag4D( dragData );
					break;
			}
		}

		// Spin variables.
		// The queue is so we can average the last couple drag values.
		// This helped with making autorotation smoother.
		private readonly Queue<DragData> m_lastDragDataQueue = new Queue<DragData>();
		private DragData m_lastDragData;
		private int m_numToAverage = 2;
		private readonly System.Timers.Timer m_timer;

		private void PerformSpin()
		{
			if( m_lastDragDataQueue.Count < m_numToAverage )
				return;
			if( 0 == Glide )
				return;

			System.Diagnostics.Trace.WriteLine( "Start Spin" );

			// Set the last drag data to averaged out values.
			m_lastDragData = m_lastDragDataQueue.Last();
			m_lastDragData.XDiff = m_lastDragDataQueue.Average( dd => dd.XDiff );
			m_lastDragData.YDiff = m_lastDragDataQueue.Average( dd => dd.YDiff );
			m_lastDragData.XPercent = m_lastDragDataQueue.Average( dd => dd.XPercent );
			m_lastDragData.YPercent = m_lastDragDataQueue.Average( dd => dd.YPercent );

			m_timer.Enabled = true;
		}

		private void StopTimer()
		{
			m_timer.Enabled = false;
		}

		private float Glide
		{
			get
			{
				float glide = (float)Math.Pow( m_settings.Gliding, 0.15 );	// 0 to 1
				if( glide > 1 )
					glide = 1;
				if( glide < 0 )
					glide = 0;
				return glide;
			}
		}

		private void SpinStep( object source, System.Timers.ElapsedEventArgs e )
		{
			// Dampen as needed..
			float glide = Glide;
			m_lastDragData.XDiff *= glide;
			m_lastDragData.YDiff *= glide;
			m_lastDragData.XPercent *= glide;
			m_lastDragData.YPercent *= glide;
			const float cutoff = 0.01f;
			bool done =
				Math.Abs( m_lastDragData.XDiff ) < cutoff ||
				Math.Abs( m_lastDragData.YDiff ) < cutoff;

			if( !this.Handler.IsSpinning || done )
			{
				System.Diagnostics.Trace.WriteLine( "End Spin" );
				this.Handler.IsSpinning = false;
				m_lastDragDataQueue.Clear();
				StopTimer();
			}
			else
			{
				//System.Diagnostics.Trace.WriteLine( "Spin Step" );
				PerformDrag( m_lastDragData );
			}

			// This will invalidate the control.
			m_tickle();
		}

		private void PerformDrag2D( DragData dragData )
		{
			switch( dragData.Button )
			{
				case MouseButtons.Left:
				{
					Recenter();

					//System.Diagnostics.Trace.WriteLine( "DragData:" + dragData.X + ":" + dragData.Y );

					// Our absolute coordinates of dragged sreen point.
					Vector3D point1 = ScreenToGL( new Vector3D( dragData.X - dragData.XDiff, dragData.Y - dragData.YDiff ) );
					Vector3D point2 = ScreenToGL( new Vector3D( dragData.X, dragData.Y ) );

					// We're going to use a different motion model for Don's code.
					switch( m_geometry )
					{
						case Geometry.Hyperbolic:
							{
								Mobius pan = new Mobius();

								// Clamp it.
								const double max = 0.98;
								if( point1.Abs() > max || point2.Abs() > max )
									break;

								// Don's pure translation code has to be composed in the correct order.
								// (the pan must be applied first).
								pan.PureTranslation( Geometry.Hyperbolic, point1, point2 );
								m_isometry.Mobius = pan * m_isometry.Mobius;

								// Numerical stability hack.
								// Things explode after panning for a while otherwise.
								{
									Mobius temp = m_isometry.Mobius;
									temp.Round( digits: 5 );	// 6 didn't turn out to be enough for one puzzle.  Downside to rounding too much?
									m_isometry.Mobius = temp;

									// Should have a 0 imaginary component.
									//System.Diagnostics.Trace.WriteLine( "TraceSquared:" + m_isometry.Mobius.TraceSquared );
								}

								break;
							}
						case Geometry.Euclidean:
						case Geometry.Spherical:
							{
								// Do a geodesic pan.
								GeodesicPan( point1, point2 );
								break;
							}
					}

					break;
				}
				case MouseButtons.Middle:
				{
					m_rotation += dragData.Rotation;
					break;
				}
				case MouseButtons.Right:
				{
					m_viewScale += 3 * m_viewScale * dragData.YPercent;

					const float smallestScale = .02f;
					float largestScale = 3.0f;
					if( m_geometry == Geometry.Spherical )
						largestScale *= 20;
					if( m_geometry == Geometry.Hyperbolic &&
						m_settings.HyperbolicModel == HModel.Orthographic )
						largestScale *= 5;
					if( m_viewScale < smallestScale )
						m_viewScale = smallestScale;
					if( m_viewScale > largestScale )
						m_viewScale = largestScale;

					UpdateImageSpace();
					break;
				}
			}
		}

		private void PerformDrag3D( DragData dragData )
		{
			switch( dragData.Button )
			{
				case MouseButtons.Left:
				{
					// The spherical coordinate radius.
					double radius = m_viewLookFrom3D.Abs();
					if( !Tolerance.Zero( radius ) )
					{
						Vector3D newLookFrom = m_viewLookFrom3D;
						Vector3D newUp = m_up3D;

						Vector3D rotationAxis = newUp.Cross( newLookFrom );
						rotationAxis.Normalize();

						double angle = System.Math.Atan2( dragData.XDiff, dragData.YDiff );
						rotationAxis.RotateAboutAxis( newLookFrom, angle );

						double magnitude = -System.Math.Sqrt( dragData.XDiff * dragData.XDiff + dragData.YDiff * dragData.YDiff ) / 100;
						newLookFrom.RotateAboutAxis( rotationAxis, magnitude );
						newUp.RotateAboutAxis( rotationAxis, magnitude );

						m_viewLookFrom3D = newLookFrom;
						m_up3D = newUp;
					}

					break;
				}

				case MouseButtons.Right:
				{
					m_viewLookFrom3D = PerformDrag3DRight( dragData, m_viewLookFrom3D );
					break;
				}
			}
		}

		private static Vector3D PerformDrag3DRight( DragData dragData, Vector3D lookFrom )
		{					
			// The view vector magnitude.
			double abs = lookFrom.Abs();
			Vector3D newLookFrom = lookFrom;

			// Increment it.
			abs += 5 * abs * dragData.YPercent;
			newLookFrom.Normalize();
			newLookFrom *= abs;

			double smallestRadius = .02;
			if( newLookFrom.Abs() < smallestRadius )
			{
				newLookFrom.Normalize();
				newLookFrom *= smallestRadius;
			}

			double largestRadius = 100.0;
			if( newLookFrom.Abs() > largestRadius )
			{
				newLookFrom.Normalize();
				newLookFrom *= largestRadius;
			}

			return newLookFrom;
		}

		private void UpdateProjectionDistance( DragData dragData )
		{
			double val = ProjectionDistance4D;

			// Increment it.
			val += 5 * val * dragData.YPercent;

			if( val < 1 )
				val = 1;
			if( val > 10 )
				val = 10;

			ProjectionDistance4D = val;
		}

		private void PerformDrag4D( DragData dragData )
		{
			bool left = dragData.Button == MouseButtons.Left;
			bool right = dragData.Button == MouseButtons.Right;
			bool shiftDown = dragData.ShiftDown;
			bool ctrlDown = dragData.CtrlDown;

			if( left )
				RotHandler4D.MouseDragged( dragData.XDiff, -dragData.YDiff, 
					!shiftDown && !ctrlDown, shiftDown, ctrlDown );

			// Zooming and Projection Distance.
			if( right )
			{
				if( shiftDown || ctrlDown )
					UpdateProjectionDistance( dragData );
				else
					m_viewLookFrom4D = PerformDrag3DRight( dragData, m_viewLookFrom4D );
			}
		}

		public void Recenter()
		{
			if( Closest == null || !Closest.IsSlave )
				return;

			// Isometry which will move center tile to closest.
			Isometry recenter = Closest.Isometry.Inverse();

			// NOTE: This shouldn't be called multiple times before recalculating new center
			//		 so we safeguard against that by setting Closest to null after.
			m_isometry *= recenter;
			Closest = null;
		}

		/// <summary>
		/// Helper to do a geodesic pan.
		/// </summary>
		private void GeodesicPan( Vector3D p1, Vector3D p2 )
		{
			Mobius pan = new Mobius();
			Isometry inverse = m_isometry.Inverse();
			p1 = inverse.Apply( p1 );
			p2 = inverse.Apply( p2 );
			pan.Geodesic( m_geometry, p1, p2 );
			m_isometry.Mobius *= pan;
		}
	}
}
