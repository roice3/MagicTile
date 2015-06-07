namespace MagicTile
{
	using System.Windows.Forms;

	public partial class EnterStringDlg : Form
	{
		public EnterStringDlg()
		{
			InitializeComponent();
			ValidateText();
		}

		public string StringText
		{
			get { return m_text.Text; }
			set { m_text.Text = value; }
		}

		private void ValidateText()
		{
			bool error = string.IsNullOrEmpty( m_text.Text );
			m_errorProvider.SetError( m_text, error ? "Please specify a macro name." : string.Empty );
			btnOk.Enabled = !error;
		}

		private void m_text_TextChanged( object sender, System.EventArgs e )
		{
			ValidateText();
		}
	}
}
