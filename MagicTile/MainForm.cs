namespace MagicTile
{
	using MagicTile.Utils;
	using OpenTK.Graphics.OpenGL;
	using System;
	using System.Drawing;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Forms;
	using R3.Geometry;

	public partial class MainForm : Form, IStatusCallback
	{
		public MainForm()
		{
			InitializeComponent();
			Log.Message( "MainForm initialized." );
			Sandbox.Run();

			MenuBuilder mb = new MenuBuilder( m_puzzleTree, this.puzzleToolStripMenuItem, 
				this.menuShowPuzzleTree_Click, this.menuPuzzle_Click );
			mb.BuildMenu();
			UpdateNumPuzzles( mb.NumPuzzles, mb.NumTilings );
			BuildScrambleMenu();
			Log.Message( "Menus built." );

			m_settings = Settings.Load();
			m_renderer = new PuzzleRenderer( m_glControl, this.UpdateStatus, this.SelectedMacro, m_settings );
			m_propertyGrid.SelectedObject = m_settings;
			CollapseGroups();

			// Show side controls by default.
			m_splitContainer.Panel2Collapsed = false;

			PrepareWaitIcon();
			Log.Message( "MainForm constructed." );
		}

		private Puzzle m_puzzle;
		private PuzzleRenderer m_renderer;
		private Settings m_settings;

		public class MagicTileControl : OpenTK.GLControl
		{
			// We want a stencil buffer, and multisample antialiasing.
			public MagicTileControl()
				//: base() { }
				: base( new OpenTK.Graphics.GraphicsMode( new OpenTK.Graphics.ColorFormat( 8, 8, 8, 8 ), 8, 8, 4 ) ) { }
		}

		private void MainForm_Load( object sender, EventArgs e )
		{
			// Ensure the Viewport is set up correctly
			//m_renderer.m_glControl_Resize( this.m_glControl, EventArgs.Empty );
		}

		private void MainForm_Shown( object sender, EventArgs e )
		{
			CheckGL();
			m_puzzle = new Puzzle();
			BuildPuzzle( null );
			UpdateStatus();
		}

		private static void CheckGL()
		{
			if( !GL.GetString( StringName.Extensions ).Contains( "EXT_framebuffer_object" ) )
			{
				MessageBox.Show(
					"Your video card does not support Framebuffer Objects, so MagicTile will not work :(\n\n" +
					"Updating your drivers may help.\n\n" +
					"So long, Farewell, Auf Wiedersehen, Goodbye",
					"FBOs not supported", MessageBoxButtons.OK, MessageBoxIcon.Exclamation );
				Application.Exit();
			}
		}

		private void MainForm_FormClosed( object sender, FormClosedEventArgs e )
		{
			m_renderer.Dispose();
			Settings.Save( m_settings );
		}

		private void m_glControl_Paint( object sender, PaintEventArgs e )
		{
			//System.Diagnostics.Trace.WriteLine( "Paint" );

			// Manually update the UI in this case, so it doesn't lag.
			if( m_renderer.IntenseRendering )
				m_tabControl.Update();

			if( m_built )
				m_renderer.Render();
			else
				m_renderer.RenderForBuilding();
		}

		private void menuShowPuzzleTree_Click( object sender, EventArgs e )
		{
			m_splitContainer.Panel2Collapsed = false;
			m_tabControl.SelectedIndex = 0;
		}

		private void menuShowMacroPane_Click( object sender, EventArgs e )
		{
			m_splitContainer.Panel2Collapsed = false;
			m_tabControl.SelectedIndex = 2;
		}

		private void menuShowSettingsPane_Click( object sender, EventArgs e )
		{
			m_splitContainer.Panel2Collapsed = false;
			m_tabControl.SelectedIndex = 1;
		}

		private void menuCollapseControls_CheckedChanged( object sender, EventArgs e )
		{
			m_splitContainer.Panel2Collapsed = menuCollapseControls.Checked;
		}

		private void m_splitContainer_SplitterMoving( object sender, SplitterCancelEventArgs e )
		{
			// Otherwise, things look ugly.
			m_glControl.Invalidate();
		}

		private void m_propertyGrid_PropertyValueChanged( object s, PropertyValueChangedEventArgs e )
		{
			SettingsChanged();
		}

		private void SettingsChanged()
		{
			Settings.Save( m_settings );
			if( m_puzzle != null && m_puzzle.State != null )
				m_puzzle.State.UpdateColors( m_settings );
			m_renderer.InvalidateTextures();
			m_glControl.Invalidate();
		}

		private void BuildScrambleMenu()
		{
			int[] scrambles = new int[] { 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };
			foreach( int scramble in scrambles )
			{
				ToolStripMenuItem menuItem = new ToolStripMenuItem();
				menuItem.Text = scramble.ToString();
				menuItem.Tag = scramble;
				menuItem.Click += new System.EventHandler( this.menuScramble_Click );
				if( scramble == 1 )
					menuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D1)));
				if( scramble == 5 )
					menuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D5)));
				this.scrambleToolStripMenuItem.DropDownItems.Add( menuItem );
			}
		}

		private void Scramble( int num )
		{
			// This can take a while so show a wait cursor.
			WaitCursor();
			m_renderer.TwistHandler.Scramble( num );
			this.Cursor = Cursors.Default;
		}

		private void BuildPuzzle( PuzzleConfig config )
		{
			BuildPuzzle( config, null );
		}

		private void PrepareWaitIcon()
		{
			string filename = "WaitCursor.ico";
			if( File.Exists( filename ) )
				m_waitIcon = new Icon( filename );
		}
		Icon m_waitIcon;

		private void WaitCursor()
		{
			if( m_waitIcon != null )
				this.Cursor = new Cursor( m_waitIcon.Handle );
			else
				this.Cursor = Cursors.WaitCursor;
		}

		private void BuildPuzzle( PuzzleConfig config, Action<Puzzle> finishLoad )
		{
			Log.Message( "Building Puzzle..." );
			m_cancel = new CancellationTokenSource();
			m_renderer.WaitRadius = 0;
			m_renderer.ResetView();

			m_built = false;
			m_buildTimer.Enabled = true;
			WaitCursor();
			m_task = Task.Factory.StartNew( () => BuildPuzzleThread( config, this.FinishBuild, finishLoad ), m_cancel.Token );
		}
		private bool m_built = false;
		private Task m_task = null;

		private bool Building
		{
			get { return m_task != null && m_task.Status == TaskStatus.Running; }
		}

		private void BuildPuzzleThread( PuzzleConfig config, Action<Puzzle> finishBuild, Action<Puzzle> finishLoad )
		{
			Puzzle puzzle = new Puzzle();
			try
			{
				if( config != null )
					puzzle.Config = config;
				puzzle.Build( this );
			}
			catch( System.Exception )
			{
				puzzle = null;
				ExecuteOnThisThread( () => InformationMessage( "Build Failure", "So sorry, there was a puzzle build failure." ) );
			}

			try
			{
				if( finishLoad != null )
					finishLoad( puzzle );
			}
			catch( System.Exception )
			{
				puzzle = null;
				ExecuteOnThisThread( () => InformationMessage( "Load Failure", "So sorry, there was a loading failure." ) );
			}
			finally
			{
				finishBuild( puzzle );
			}
		}

		private void FinishBuild( Puzzle puzzle )
		{
			Action finish = () => FinishBuildInternal( puzzle );
			ExecuteOnThisThread( finish );
		}

		private void FinishBuildInternal( Puzzle puzzle )
		{
			m_puzzle = puzzle;
			if( puzzle == null )
				return;

			puzzle.State.UpdateColors( m_settings );
			m_loader.ClearFilename();
			m_renderer.PuzzleUpdated( m_puzzle );
			this.Text = "MagicTile - " + m_puzzle.Config.DisplayName;

			this.Cursor = Cursors.Default;

			m_buildTimer.Enabled = false;
			m_built = true;

			// Forcing a GC collection here helps a great deal with memory usage, as puzzle building allocates a lot.
			// An idea of the difference this made: 100MB vs. 250MB used when repeatedly building {7,3}).
			// Clearing out memory used by the previous puzzle is probably part of this help.
			// ZZZ - Spend some time doing proper memory profiling.
			GC.Collect();

			UpdateStatus();
			RefreshMacroUI();
			m_glControl.Invalidate();
		}

		private void menuResetView_Click( object sender, EventArgs e )
		{
			m_renderer.ResetView();
			m_glControl.Invalidate();
		}

		void IStatusCallback.Status( string message )
		{
			Log.Message( message );
		}

		bool IStatusCallback.Cancelled 
		{
			get { return m_cancel != null && m_cancel.IsCancellationRequested; }
		}
		private CancellationTokenSource m_cancel = null;

		/// <summary>
		/// Ensures that the given action will execute on the thread which created this control.
		/// </summary>
		private void ExecuteOnThisThread( Action action )
		{
			if( this.InvokeRequired )
				this.Invoke( action );
			else
				action();
		}

		private void InformationMessage( string title, string message )
		{
			MessageBox.Show( this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information );
		}

		private void m_buttonSetDefaults_Click( object sender, EventArgs e )
		{
			m_settings.SetDefaults();
			m_propertyGrid.Refresh();
			SettingsChanged();
		}

		private void CollapseGroups()
		{
			SetLabelColumnWidth( m_propertyGrid );

			GridItem root = m_propertyGrid.SelectedGridItem;
			while( root.Parent != null )
				root = root.Parent;

			if( root != null )
			{
				foreach( GridItem g in root.GridItems )
				{
					if( g.GridItemType == GridItemType.Category && g.Label != "Behavior" )
						g.Expanded = false;
				}
			}
		}

		/// <summary>
		/// Makes the label width slightly larger than the default.
		/// </summary>
		private static void SetLabelColumnWidth( PropertyGrid grid )
		{
			FieldInfo gridViewFieldInfo = typeof( PropertyGrid ).GetField( "gridView",
				BindingFlags.Instance | BindingFlags.NonPublic );
			object propertyGridView = gridViewFieldInfo.GetValue( grid );

			FieldInfo labelRatioFieldInfo = propertyGridView.GetType().GetField( "labelRatio", 
				BindingFlags.Instance | BindingFlags.Public );
			labelRatioFieldInfo.SetValue( propertyGridView, 1.5 );
    	}

		private void UpdateStatus()
		{
			double twists = 0, scrambles = 0, total = 0;
			if( m_puzzle != null && m_puzzle.TwistHistory != null )
			{
				total = m_puzzle.TwistHistory.AllTwists.Count();
				scrambles = m_puzzle.TwistHistory.Scrambles;
				twists = total - scrambles;
			}

			string status = string.Format( "Twists: {0}    ●    Scrambles: {1}    ●    Total: {2}",
				twists, scrambles, total );
			m_status.Text = status;

			UpdateStatus2();
		}

		private void UpdateNumPuzzles( int numPuzzles, int numTilings )
		{
			m_numPuzzles.Text = string.Format( "{0} Puzzles and {1} Tilings Available", numPuzzles, numTilings );
		}

		private bool AllowUI
		{
			get
			{
				if( m_renderer.TwistHandler.Twisting ||
					m_renderer.TwistHandler.Solving ||
					this.Building )
					return false;

				return true;
			}
		}

		private void m_buildTimer_Tick( object sender, EventArgs e )
		{
			m_glControl.Invalidate();
		}

		private void m_glControl_KeyDown( object sender, KeyEventArgs e )
		{
			if( e.KeyCode == Keys.F5 )
			{
				MenuBuilder mb = new MenuBuilder( m_puzzleTree, this.puzzleToolStripMenuItem,
					this.menuShowPuzzleTree_Click, this.menuPuzzle_Click );
				mb.BuildMenu();
				UpdateNumPuzzles( mb.NumPuzzles, mb.NumTilings );
			}

			if( e.KeyCode == Keys.F6 )
			{
				m_settings.SurfaceDisplay = !m_settings.SurfaceDisplay;
			}

			if( e.KeyCode == Keys.F7 )
			{
				if( m_puzzle != null && m_puzzle.Config != null )
				{
					if( m_puzzle.Config.Geometry == Geometry.Spherical )
						m_settings.SphericalModel = (SphericalModel)((int)(m_settings.SphericalModel + 1) % 3);

					if( m_puzzle.Config.Geometry == Geometry.Hyperbolic )
						m_settings.HyperbolicModel = (HModel)(((int)m_settings.HyperbolicModel + 1) % 2);
				}
			}

			if( e.KeyCode == Keys.F12 )
			{
				m_renderer.SaveToSvg();
			}

			if( e.KeyCode == Keys.Escape )
			{
				TwistHandler handler = m_renderer.TwistHandler;
				if( handler.Solving )
					handler.Solving = false;

				m_renderer.TwistHandler.m_setupMoves.Reset();
				m_renderer.TwistHandler.m_workingMacro.Reset();
				UpdateStatus2();
			}

			if( e.KeyCode == Keys.X )
			{
				if( ShiftDown )
					m_settings.XLevels += 2;
				else
					m_settings.XLevels -= 2;
			}

			if( e.KeyCode == Keys.Y )
			{
				if( ShiftDown )
					m_settings.YLevels += 2;
				else
					m_settings.YLevels -= 2;
			}

			if( e.KeyCode == Keys.Z )
			{
				if( ShiftDown )
					m_settings.ZLevels += 2;
				else
					m_settings.ZLevels -= 2;
			}

			// Slicemask
			if( e.KeyCode == Keys.NumPad1 || e.KeyCode == Keys.D1 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_1;
			if( e.KeyCode == Keys.NumPad2 || e.KeyCode == Keys.D2 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_2;
			if( e.KeyCode == Keys.NumPad3 || e.KeyCode == Keys.D3 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_3;
			if( e.KeyCode == Keys.NumPad4 || e.KeyCode == Keys.D4 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_4;
			if( e.KeyCode == Keys.NumPad5 || e.KeyCode == Keys.D5 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_5;
			if( e.KeyCode == Keys.NumPad6 || e.KeyCode == Keys.D6 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_6;
			if( e.KeyCode == Keys.NumPad7 || e.KeyCode == Keys.D7 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_7;
			if( e.KeyCode == Keys.NumPad8 || e.KeyCode == Keys.D8 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_8;
			if( e.KeyCode == Keys.NumPad9 || e.KeyCode == Keys.D9 )
				m_renderer.SliceMask |= SliceMask.SLICEMASK_9;

			m_settings.ClampLevels();
			m_propertyGrid.SelectedObject = m_settings;
			m_glControl.Invalidate();
		}

		private void m_glControl_KeyUp( object sender, KeyEventArgs e )
		{
			// Slicemask
			if( e.KeyCode == Keys.NumPad1 || e.KeyCode == Keys.D1 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_1;
			if( e.KeyCode == Keys.NumPad2 || e.KeyCode == Keys.D2 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_2;
			if( e.KeyCode == Keys.NumPad3 || e.KeyCode == Keys.D3 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_3;
			if( e.KeyCode == Keys.NumPad4 || e.KeyCode == Keys.D4 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_4;
			if( e.KeyCode == Keys.NumPad5 || e.KeyCode == Keys.D5 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_5;
			if( e.KeyCode == Keys.NumPad6 || e.KeyCode == Keys.D6 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_6;
			if( e.KeyCode == Keys.NumPad7 || e.KeyCode == Keys.D7 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_7;
			if( e.KeyCode == Keys.NumPad8 || e.KeyCode == Keys.D8 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_8;
			if( e.KeyCode == Keys.NumPad9 || e.KeyCode == Keys.D9 )
				m_renderer.SliceMask &= ~SliceMask.SLICEMASK_9;

			m_glControl.Invalidate();
		}

		private bool ShiftDown
		{
			get
			{
				return (Form.ModifierKeys & Keys.Shift) == Keys.Shift;
			}
		}

		private void menuOpen_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			m_loader.LoadFromFile( this.BuildPuzzle );
		}

		private void menuSave_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			m_loader.SaveToFile( m_puzzle, false );
		}

		private void menuSaveAs_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			m_loader.SaveToFile( m_puzzle, true );
		}
		private Loader m_loader = new Loader();

		private void menuPuzzle_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			ToolStripMenuItem item = (ToolStripMenuItem)sender;
			PuzzleConfig config = (PuzzleConfig)item.Tag;
			BuildPuzzle( config );
		}

		private void m_puzzleTree_DoubleClick( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			TreeNode selected = m_puzzleTree.SelectedNode;
			if( selected == null || selected.Tag == null )
				return;

			PuzzleConfig config = (PuzzleConfig)selected.Tag;
			BuildPuzzle( config );
		}

		private void menuScramble_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			ToolStripMenuItem item = (ToolStripMenuItem)sender;
			int scramble = (int)item.Tag;
			Scramble( scramble );
		}

		private void menuUndo_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			m_renderer.TwistHandler.Undo();
		}

		private void menuRedo_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			m_renderer.TwistHandler.Redo();
		}

		private void menuSolve_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			m_renderer.TwistHandler.Solve();
		}

		private void menuResetState_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			m_renderer.TwistHandler.ResetState();
		}

		private void menuStopRecordingMacro_Click( object sender, EventArgs e )
		{
			Macro m = m_renderer.TwistHandler.m_workingMacro;
			if( !m.Recording )
				return;

			m.StopRecording();
			UpdateStatus2();

			if( m.Twists.Length == 0 )
			{
				InformationMessage( "Invalid Macro", "There were no twists recorded for the macro." );
				return;
			}

			Macro clone = m.Clone();

			// Prompt for name.
			EnterStringDlg dlg = new EnterStringDlg();
			if( DialogResult.OK == dlg.ShowDialog( this ) )
			{
				clone.DisplayName = dlg.StringText;
				clone.ClearStartEndMarkings();
				m_puzzle.MacroList.Macros.Add( clone );
				RefreshMacroUI( m_puzzle.MacroList.Macros.Count - 1 );
			}
		}

		private void m_macroListView_SelectedIndexChanged( object sender, EventArgs e )
		{
			UpdateEnabled();
		}

		private void m_btnRename_Click( object sender, EventArgs e )
		{
			Macro m = SelectedMacro();
			if( m == null )
				return;

			EnterStringDlg dlg = new EnterStringDlg();
			dlg.StringText = m.DisplayName;
			if( DialogResult.OK == dlg.ShowDialog( this ) )
			{
				m.DisplayName = dlg.StringText;
				RefreshMacroUI();
			}
		}

		private void m_btnDelete_Click( object sender, EventArgs e )
		{
			DeleteSelectedMacro();
		}

		private void m_macroListView_KeyDown( object sender, KeyEventArgs e )
		{
			if( e.KeyCode == Keys.Delete )
			{
				DeleteSelectedMacro();
			}
		}

		private void DeleteSelectedMacro()
		{
			MacroItem i = SelectedMacroItem();
			if( i == null )
				return;

			int idx = i.Index;
			m_puzzle.MacroList.Macros.Remove( i.Macro );
			m_macroListView.Items.Remove( i );
			UpdateEnabled();
		}

		private void RefreshMacroUI( int select = -1 )
		{
			m_macroListView.Items.Clear();
			m_puzzle.MacroList.Macros.ForEach( macro => m_macroListView.Items.Add( new MacroItem( macro ) ) );
			if( -1 != select )
			{
				m_macroListView.SelectedIndices.Clear();
				m_macroListView.SelectedIndices.Add( select );
				// Bummer: doesn't work until the ListView is shown at least once.
				// http://msdn.microsoft.com/en-us/library/system.windows.forms.listview.selectedindices.aspx
				//System.Diagnostics.Trace.WriteLine( "handle created: " + m_macroListView.IsHandleCreated );
			}
			UpdateEnabled();
		}

		private void UpdateEnabled()
		{
			bool macroSelected = m_macroListView.SelectedItems.Count > 0;
			this.m_btnRename.Enabled = macroSelected;
			this.m_btnDelete.Enabled = macroSelected;
		}

		internal class MacroItem : ListViewItem
		{
			public MacroItem( Macro macro )
			{
				this.Text = macro.DisplayName;
				this.SubItems.Add( macro.Twists.Length.ToString() );

				this.Macro = macro;
			}

			public Macro Macro { get; private set; }
		}

		private MacroItem SelectedMacroItem()
		{
			bool macroSelected = m_macroListView.SelectedItems.Count > 0;
			if( !macroSelected )
				return null;

			return (MacroItem)m_macroListView.SelectedItems[0];
		}

		internal Macro SelectedMacro()
		{
			MacroItem i = SelectedMacroItem();
			if( i == null )
				return null;
			return i.Macro;
		}

		private void menuLoadMacros_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			try
			{
				m_puzzle.MacroList.LoadFromXml( m_puzzle );
				RefreshMacroUI();
			}
			catch( System.Exception ) { }
		}

		private void menuSaveMacros_Click( object sender, EventArgs e )
		{
			if( !this.AllowUI )
				return;

			m_puzzle.MacroList.SaveToXml( m_puzzle );
		}

		private void menuStartSetupMoves_Click( object sender, EventArgs e )
		{
			m_renderer.TwistHandler.m_setupMoves.StartRecording();
			UpdateStatus2();
		}

		private void menuEndSetupMoves_Click( object sender, EventArgs e )
		{
			m_renderer.TwistHandler.m_setupMoves.StopRecording();
			UpdateStatus2();
		}

		private void UpdateStatus2()
		{
			SetupMoves s = m_renderer.TwistHandler.m_setupMoves;
			if( s.RecordingSetup )
				m_status2.Text = "Recording Setup Moves";
			else if( s.RecordingCommutator )
				m_status2.Text = "Recording Commutator Moves";
			else if( m_renderer.TwistHandler.m_workingMacro.Recording )
				m_status2.Text = "Recording Macro";
			else
				m_status2.Text = m_puzzle == null ? string.Empty : m_puzzle.Topology;
		}

		private void menuUnwindSetupMoves_Click( object sender, EventArgs e )
		{
			m_renderer.TwistHandler.Unwind();
			UpdateStatus2();
		}

		private void menuCommutator_Click( object sender, EventArgs e )
		{
			m_renderer.TwistHandler.Commutator();
			UpdateStatus2();
		}

		private void menuMouseCommands_Click( object sender, EventArgs e )
		{
			Commands commands = new Commands();
			commands.Show();
		}

		private void menuAbout_Click( object sender, EventArgs e )
		{
			string text =
				"MagicTile by Roice Nelson\n" +
				"www.roice3.org/magictile   " +
				"\n\nSpecial thanks to:\n" +
				"  Melinda Green, for IRP data and encouragement\n" +
				"  Nan Ma, for excellent feedback and usage testing\n" +
				"  Rob Nelson, for brainstorming and ideas\n" +
				"  Andrey Astrelin, for helping with puzzle configurations\n" + 
				"  Ed Baumann, for enthusiasm and solving so many puzzles\n" +
				"  Burkard Polster, for great suggestions and popularizing\n" +
				"  Don Hatch and Fritz Obermeyer, for making inspiring software\n" +
				"  The 4D_Cubing Yahoo group";
			string caption = "About";
			MessageBox.Show( this, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information );
		}
	}
}
