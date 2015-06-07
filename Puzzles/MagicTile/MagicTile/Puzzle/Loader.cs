namespace MagicTile
{
	using MagicTile.Utils;
	using System.IO;
	using System.Linq;
	using System.Windows.Forms;
	using System.Xml.Linq;

	public class PuzzleInfo
	{
		public PuzzleConfig Config { get; set; }
		public State State { get; set; }
		public TwistHistory TwistHistory { get; set; }
	}

	public class Loader
	{
		private string m_filename;

		public void ClearFilename()
		{
			m_filename = string.Empty;
		}

		public static string VersionPreview = @"2.0";
		public static string VersionCurrent = @"2.1";

		/// <summary>
		/// I had to change persistence for ET and VT puzzles.
		/// This will load the saved file version into the passed in puzzle config,
		/// so we can build the puzzle in a backward compatible way if needed.
		/// </summary>
		public static void SetVersionOnConfig( PuzzleConfig puzzleConfig, XElement root )
		{
			XAttribute xVersion = root.Attribute( "Version" );
			if( xVersion == null )
				puzzleConfig.Version = VersionPreview;
			puzzleConfig.Version = xVersion.Value;
		}

		public void SaveToFile( Puzzle puzzle, bool saveas )
		{
			// Get the filename to save to.
			string fileName = GetSaveFileNameInternal( saveas );
			if( string.IsNullOrEmpty( fileName ) )
				return;

			XDocument xDocument =
				new XDocument( 
					new XElement( "MagicTileLog",
						new XAttribute( "Version", puzzle.Config.Version ),
					SaveConfig( puzzle.Config ),
					SaveState( puzzle.State ),
					SaveHistory( puzzle.TwistHistory ),
					SaveMacros( puzzle )
			) );

			xDocument.Save( fileName );
		}

		private XElement SaveConfig( PuzzleConfig config )
		{
			return XElement.Parse( DataContractHelper.SaveToString( config ) );
		}

		private XElement SaveState( State state )
		{
			XElement xState = new XElement( "State" );
			return state.SaveToXml( xState );
		}

		private XElement SaveHistory( TwistHistory history )
		{
			XElement xHistory = new XElement( "History" );
			return history.SaveToXml( xHistory );
		}

		private XElement SaveMacros( Puzzle puzzle )
		{
			XElement xMacros = new XElement( "Macros" );
			return puzzle.MacroList.SaveToXml( xMacros, puzzle );
		}

		public void LoadFromFile( System.Action<PuzzleConfig, System.Action<Puzzle>> buildPuzzle )
		{
			string fileName = GetLoadFileNameInternal();
			if( !File.Exists( fileName ) )
				return;

			XDocument xDocument = XDocument.Load( fileName );
			XElement xConfig = xDocument.Root.Element( "PuzzleConfig" );
			PuzzleConfig config = (PuzzleConfig)DataContractHelper.LoadFromString( typeof( PuzzleConfig ), xConfig.ToString() );
			SetVersionOnConfig( config, xDocument.Root );
			System.Action<Puzzle> finishLoad = p => LoadStateAndHistoryAndMacros( xDocument, p );
			buildPuzzle( config, finishLoad );
		}

		private void LoadStateAndHistoryAndMacros( XDocument xDocument, Puzzle puzzle )
		{
			XElement xState = xDocument.Root.Element( "State" );
			puzzle.State.LoadFromXml( xState );

			XElement xHistory = xDocument.Root.Element( "History" );
			puzzle.TwistHistory.LoadFromXml( xHistory, puzzle.AllTwistData );

			// We haven't always had the macro node saved out.
			XElement xMacros = xDocument.Root.Element( "Macros" );
			if( xMacros != null )
				puzzle.MacroList.LoadFromXml( xMacros, puzzle.AllTwistData );
		}

		private static string m_filter = "MagicTile log files (*.xml)|*.xml|All files (*.*)|*.*";

		private string GetSaveFileNameInternal( bool forcePrompt ) 
		{
			if( 0 == m_filename.Length || forcePrompt )
			{
				string filename = GetSaveFileName( m_filter );
				if( string.IsNullOrEmpty( filename ) )
					return string.Empty;
				m_filename = filename;
			}

			return m_filename;
		}

		private string GetLoadFileNameInternal() 
		{
			string filename = GetLoadFileName( m_filter );
			if( !string.IsNullOrEmpty( filename ) )
				m_filename = filename;
			return filename;
		}

		public static string GetSaveFileName( string filter )
		{
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.AddExtension = true;
			dlg.OverwritePrompt = true;
			dlg.Filter = filter;
			if( DialogResult.OK != dlg.ShowDialog() )
				return string.Empty;
			return dlg.FileName;
		}

		public static string GetLoadFileName( string filter )
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.CheckFileExists = true;
			dlg.Filter = filter;
			if( DialogResult.OK != dlg.ShowDialog() )
				return string.Empty;
			return dlg.FileName;
		}
	}
}
