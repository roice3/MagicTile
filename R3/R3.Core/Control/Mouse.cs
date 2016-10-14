namespace R3.Control
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Windows.Forms;

	/// <summary>
	/// Data passed along with a click.
	/// </summary>
	public class ClickData
	{
		public ClickData( int x, int y ) { X = x; Y = y; }
		public int X { get; set; }
		public int Y { get; set; }
		public MouseButtons Button { get; set; }
	}

	/// <summary>
	/// Data passed along with a drag.
	/// </summary>
	public class DragData
	{
		/// <summary>
		/// The actual drag location.
		/// </summary>
		public int X { get; set; }
		public int Y { get; set; }

		/// <summary>
		/// The drag amount, in rectangular coords.
		/// </summary>
		public float XDiff { get; set; }
		public float YDiff { get; set; }
		public float XPercent { get; set; }
		public float YPercent { get; set; }

		/// <summary>
		/// The drag amount, in polar coords
		/// Rotation is in radians.
		/// </summary>
		public float Rotation { get; set; }
		public float Radial { get; set; }
		public float RadialPercent { get; set; }

		public MouseButtons Button { get; set; }
		public bool ShiftDown;
		public bool CtrlDown;
	}

	/// <summary>
	/// Class for doing drag/click logic.
	/// It handles some of the nuances of puzzle based inputs.
	/// For example, we can't simply handle the Click event on an a draw control, 
	/// because if you drag, that still fires when lifting the mouse button.
	/// </summary>
	public class MouseHandler
	{
		public MouseHandler()
		{
			m_dragging = m_spinning = m_skipClick = false;
			m_downX = m_downY = -1;
			m_lastX = m_lastY = 0;
		}

		public int LastX { get { return m_lastX; } }
		public int LastY { get { return m_lastY; } }

		public void Setup( Control drawSurface )
		{
			m_drawSurface = drawSurface;
			m_drawSurface.MouseDown += new MouseEventHandler( this.MouseDown );
			m_drawSurface.MouseMove += new MouseEventHandler( this.MouseMove );
			m_drawSurface.MouseUp += new MouseEventHandler( this.MouseUp );
			m_drawSurface.MouseWheel += new MouseEventHandler( MouseWheel );
		}

		public void SetClickHandler( Action<ClickData> clickHandler )
		{
			m_clickHandler = clickHandler;
		}

		public void SetDragHandler( Action<DragData> dragHandler )
		{
			m_dragHandler = dragHandler;
		}

		public void SetSpinHandler( Action spinHandler )
		{
			m_spinHandler = spinHandler;
		}

		//
		// Event handlers.
		//

		private void MouseDown( Object sender, MouseEventArgs e )
		{
			m_downX = e.X;
			m_downY = e.Y;

			// If we are spinning, don't let this turn into a click.
			if( m_spinning )
				m_skipClick = true;
			
			m_dragging = m_spinning = false;
		}

		private void MouseMove( Object sender, MouseEventArgs e )
		{
			// Make sure we have the focus.
			m_drawSurface.Select();

			// Are we starting a drag?
			// NOTE: The mousedown checks make sure we had a mouse down call and fixes a problem I was seeing
			//		 where the view would reset when you loaded in a log file.
			if( !m_dragging && e.Button != MouseButtons.None &&
				-1 != m_downX && -1 != m_downY &&
				((Math.Abs( e.X - m_downX ) > SystemInformation.DragSize.Width / 2) ||
				 (Math.Abs( e.Y - m_downY ) > SystemInformation.DragSize.Height / 2)) )
			{
				StartDrag();

				// Fake the original mouse position so we will get some drag motion immediately.
				m_lastX = m_downX;
				m_lastY = m_downY;
			}

			// Are we dragging?
			if( m_dragging )
			{
				PerformDrag( e.X, e.Y, e.Button );

				// This is so we can check if we want to start spinning.
				m_stopWatch.Restart();
			}

			m_lastX = e.X;
			m_lastY = e.Y;
		}

		private readonly Stopwatch m_stopWatch = new Stopwatch();

		private void MouseWheel( object sender, MouseEventArgs e )
		{
			int amount = e.Delta * SystemInformation.MouseWheelScrollLines / 120;
			int numberOfPixelsToMove = amount;
			if( numberOfPixelsToMove == 0 )
				return;

			if( m_dragHandler == null )
				return;

			DragData dragData = new DragData();
			dragData.YPercent = (float)(numberOfPixelsToMove) / (float)m_drawSurface.Height;
			dragData.Button = MouseButtons.Right;
			m_dragHandler( dragData );
		}

		private void MouseUp( Object sender, MouseEventArgs e )
		{
			// NOTE: The mousedown checks make sure we had a mouse down call and fixes a problem I was seeing
			//		 where where unintended sticker clicks could happen when loading a log file.
			if( -1 == m_downX || -1 == m_downY )
				return;

			m_downX = m_downY = -1;

			// Figure out if we were dragging, and if the drag is done.
			if( m_dragging )
			{
				if( Form.MouseButtons == MouseButtons.None )
				{
					FinishDrag();

					// Using elapsed time works much better than checking how many pixels moved (as MC4D does).
					m_spinning = m_stopWatch.ElapsedMilliseconds < 50;
					m_stopWatch.Stop();
					System.Diagnostics.Trace.WriteLine( string.Format( "Spinning = {0}, Elapsed = {1}", 
						m_spinning, m_stopWatch.ElapsedMilliseconds ) );
					if( m_spinning && m_spinHandler != null )
						m_spinHandler();
				}

				m_skipClick = false;
				return;
			}

			// Past here, the mouse-up represents a click.
			if( e.Button == MouseButtons.Left || 
				e.Button == MouseButtons.Right )
			{
				if( !m_skipClick && m_clickHandler != null )
				{
					ClickData clickData = new ClickData( e.X, e.Y );
					clickData.Button = e.Button;
					m_clickHandler( clickData );
				}

				m_skipClick = false;
			}
		}

		//
		// Drag helpers
		//

		void StartDrag()
		{
			m_dragging = true;
			m_drawSurface.Capture = true;
		}

		void PerformDrag( int x, int y, MouseButtons btn )
		{
			if( m_dragHandler == null )
				return;

			DragData dragData = new DragData();
			dragData.X = x;
			dragData.Y = y;
			dragData.XDiff = x - m_lastX;
			dragData.YDiff = y - m_lastY;

			// This is the increment we moved, scaled to the window size.
			dragData.XPercent = (float)( dragData.XDiff ) / (float)m_drawSurface.Width;
			dragData.YPercent = (float)( dragData.YDiff ) / (float)m_drawSurface.Height;

			// How much we rotated relative to center.
			double x1 = m_lastX - m_drawSurface.Width / 2, y1 = m_drawSurface.Height / 2 - m_lastY;
			double x2 = x - m_drawSurface.Width / 2, y2 = m_drawSurface.Height / 2 - y;
			double angle1 = Math.Atan2( y1, x1 );
			double angle2 = Math.Atan2( y2, x2 );
			dragData.Rotation = (float)angle2 - (float)angle1;

			// Our radial change.
			double r1 = Math.Sqrt( x1 * x1 + y1 * y1 );
			double r2 = Math.Sqrt( x2 * x2 + y2 * y2 );
			dragData.Radial = (float)r2 - (float)r1;
			dragData.RadialPercent = (float)r2 / (float)r1;

			dragData.Button = btn;
			dragData.ShiftDown = this.ShiftDown;
			dragData.CtrlDown = this.CtrlDown;

			m_dragHandler( dragData );
		}

		void FinishDrag()
		{
			m_drawSurface.Capture = false;
			m_dragging = false;
		}

		public bool IsSpinning
		{
			get { return m_spinning; }
			set { m_spinning = value; }
		}

		public bool IsDragging
		{
			get { return m_dragging; }
		}

		/// <summary>
		/// The control we'll be handling mouse input for.
		/// </summary>
		private Control m_drawSurface { get; set; }

		/// <summary>
		/// Tracking variables
		/// </summary>
		private int m_downX;
		private int m_downY;
		private bool m_dragging;
		private bool m_spinning;
		private bool m_skipClick;
		private int m_lastX;
		private int m_lastY;

		Action<ClickData> m_clickHandler;
		Action<DragData> m_dragHandler;
		Action m_spinHandler;

		private bool CtrlDown
		{
			get
			{
				return (Form.ModifierKeys & Keys.Control) == Keys.Control;
			}
		}

		private bool ShiftDown
		{
			get
			{
				return (Form.ModifierKeys & Keys.Shift) == Keys.Shift;
			}
		}
	}
}
