namespace MagicTile
{
	using System.Drawing;
	using System.Windows.Forms;

	public partial class Commands : Form
	{
		public Commands()
		{
			InitializeComponent();
			WriteText();
		}

		private void WriteText()
		{
			Font bold = new Font( m_text.Font, FontStyle.Bold );
			Font normal = new Font( m_text.Font, FontStyle.Regular );

			m_text.SelectionFont = bold;
			m_text.AppendText( "2D Navigation( dragging with mouse )\r\n" );
			m_text.SelectionFont = normal;
			m_text.AppendText( "  Left Button: Panning\r\n" );
			m_text.AppendText( "  Middle Button: Rotation\r\n" );
			m_text.AppendText( "  Right Button: Zooming\r\n\r\n" );

			m_text.SelectionFont = bold;
			m_text.AppendText( "3D Navigation\r\n" );
			m_text.SelectionFont = normal;
			m_text.AppendText( "  Left Button: Rotation\r\n" );
			m_text.AppendText( "  Right Button: Zooming\r\n\r\n" );

			m_text.SelectionFont = bold;
			m_text.AppendText( "4D Navigation\r\n" );
			m_text.SelectionFont = normal;
			m_text.AppendText( "  Shift+Left Button: 4D Rotation (xw and yw)\r\n" );
			m_text.AppendText( "  Ctrl+Left Button: 4D Rotation (zw) and 3D Rolling\r\n" );
			m_text.AppendText( "  Shift+Right Button: 4D Zooming (4D camera distance)\r\n\r\n" );

			m_text.SelectionFont = bold;
			m_text.AppendText( "Twisting\r\n" );
			m_text.SelectionFont = normal;
			m_text.AppendText( "  Right Click: Twist a tile clockwise\r\n" );
			m_text.AppendText( "  Left Click: Twist a tile counterclockwise\r\n" );
			m_text.AppendText( "  Slice Mask: Hold down number keys while twisting\r\n\r\n" );

			m_text.SelectionFont = bold;
			m_text.AppendText( "Macros\r\n" );
			m_text.SelectionFont = normal;
			m_text.AppendText( "  Ctrl-Alt-Left Click: Start macro definition\r\n" );
			m_text.AppendText( "  Ctrl-m: End macro definition\r\n" );
			m_text.AppendText( "  Alt-Left Click: Apply selected macro\r\n" );
			m_text.AppendText( "  Alt-Right Click: Apply selected macro in reverse\r\n\r\n" );

			m_text.SelectionFont = bold;
			m_text.AppendText( "IRP\r\n" );
			m_text.SelectionFont = normal;
			m_text.AppendText( "  x,y,z keys: Remove Layers\r\n" );
			m_text.AppendText( "  X,Y,Z keys: Add Layers\r\n\r\n" );

			m_text.SelectionFont = bold;
			m_text.AppendText( "Other\r\n" );
			m_text.SelectionFont = normal;
			m_text.AppendText( "  F6: Toggles surface display\r\n" );
			m_text.AppendText( "  F7: Cycles projection models for spherical and hyperbolic puzzles\r\n\r\n" );
		}
	}
}
