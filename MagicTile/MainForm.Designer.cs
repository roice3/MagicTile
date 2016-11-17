namespace MagicTile
{
	partial class MainForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && (components != null) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			this.m_glControl = new MagicTile.MainForm.MagicTileControl();
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuOpen = new System.Windows.Forms.ToolStripMenuItem();
			this.menuSave = new System.Windows.Forms.ToolStripMenuItem();
			this.menuSaveAs = new System.Windows.Forms.ToolStripMenuItem();
			this.puzzleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuShowPuzzleTree = new System.Windows.Forms.ToolStripMenuItem();
			this.scrambleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.macroToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuShowMacroPane = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
			this.tipSeeWebsiteForInstructionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.startMacroDefinitionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuStopRecordingMacro = new System.Windows.Forms.ToolStripMenuItem();
			this.applyMacroToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.applyMacroReversedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
			this.menuStartSetupMoves = new System.Windows.Forms.ToolStripMenuItem();
			this.menuEndSetupMoves = new System.Windows.Forms.ToolStripMenuItem();
			this.menuUnwindSetupMoves = new System.Windows.Forms.ToolStripMenuItem();
			this.menuCommutator = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.menuSaveMacros = new System.Windows.Forms.ToolStripMenuItem();
			this.menuLoadMacros = new System.Windows.Forms.ToolStripMenuItem();
			this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuShowSettingsPane = new System.Windows.Forms.ToolStripMenuItem();
			this.menuCollapseControls = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
			this.menuUndo = new System.Windows.Forms.ToolStripMenuItem();
			this.menuRedo = new System.Windows.Forms.ToolStripMenuItem();
			this.menuSolve = new System.Windows.Forms.ToolStripMenuItem();
			this.menuResetState = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
			this.menuResetView = new System.Windows.Forms.ToolStripMenuItem();
			this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuMouseCommands = new System.Windows.Forms.ToolStripMenuItem();
			this.menuAbout = new System.Windows.Forms.ToolStripMenuItem();
			this.m_propertyGrid = new System.Windows.Forms.PropertyGrid();
			this.m_splitContainer = new System.Windows.Forms.SplitContainer();
			this.m_tabControl = new System.Windows.Forms.TabControl();
			this.m_tabPuzzles = new System.Windows.Forms.TabPage();
			this.statusStrip2 = new System.Windows.Forms.StatusStrip();
			this.m_numPuzzles = new System.Windows.Forms.ToolStripStatusLabel();
			this.m_puzzleTree = new System.Windows.Forms.TreeView();
			this.m_tabSettings = new System.Windows.Forms.TabPage();
			this.m_buttonSetDefaults = new System.Windows.Forms.Button();
			this.m_tabMacros = new System.Windows.Forms.TabPage();
			this.m_btnRename = new System.Windows.Forms.Button();
			this.m_btnDelete = new System.Windows.Forms.Button();
			this.m_macroListView = new System.Windows.Forms.ListView();
			this.columnHeaderName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeaderNumMoves = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.m_buildTimer = new System.Windows.Forms.Timer(this.components);
			this.splitter1 = new System.Windows.Forms.Splitter();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.m_status = new System.Windows.Forms.ToolStripStatusLabel();
			this.m_status2 = new System.Windows.Forms.ToolStripStatusLabel();
			this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
			this.menuGap = new System.Windows.Forms.ToolStripMenuItem();
			this.menuStrip1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.m_splitContainer)).BeginInit();
			this.m_splitContainer.Panel1.SuspendLayout();
			this.m_splitContainer.Panel2.SuspendLayout();
			this.m_splitContainer.SuspendLayout();
			this.m_tabControl.SuspendLayout();
			this.m_tabPuzzles.SuspendLayout();
			this.statusStrip2.SuspendLayout();
			this.m_tabSettings.SuspendLayout();
			this.m_tabMacros.SuspendLayout();
			this.statusStrip1.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_glControl
			// 
			this.m_glControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.m_glControl.BackColor = System.Drawing.Color.Black;
			this.m_glControl.Location = new System.Drawing.Point(3, 3);
			this.m_glControl.MinimumSize = new System.Drawing.Size(200, 200);
			this.m_glControl.Name = "m_glControl";
			this.m_glControl.Size = new System.Drawing.Size(516, 513);
			this.m_glControl.TabIndex = 0;
			this.m_glControl.VSync = false;
			this.m_glControl.Paint += new System.Windows.Forms.PaintEventHandler(this.m_glControl_Paint);
			this.m_glControl.KeyDown += new System.Windows.Forms.KeyEventHandler(this.m_glControl_KeyDown);
			this.m_glControl.KeyUp += new System.Windows.Forms.KeyEventHandler(this.m_glControl_KeyUp);
			// 
			// menuStrip1
			// 
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.puzzleToolStripMenuItem,
            this.scrambleToolStripMenuItem,
            this.macroToolStripMenuItem,
            this.optionsToolStripMenuItem,
            this.helpToolStripMenuItem});
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(809, 24);
			this.menuStrip1.TabIndex = 1;
			this.menuStrip1.Text = "menuStrip1";
			// 
			// fileToolStripMenuItem
			// 
			this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuOpen,
            this.menuSave,
            this.menuSaveAs,
            this.toolStripSeparator6,
            this.menuGap});
			this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
			this.fileToolStripMenuItem.Text = "File";
			// 
			// menuOpen
			// 
			this.menuOpen.Name = "menuOpen";
			this.menuOpen.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
			this.menuOpen.Size = new System.Drawing.Size(166, 22);
			this.menuOpen.Text = "Open...";
			this.menuOpen.Click += new System.EventHandler(this.menuOpen_Click);
			// 
			// menuSave
			// 
			this.menuSave.Name = "menuSave";
			this.menuSave.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
			this.menuSave.Size = new System.Drawing.Size(166, 22);
			this.menuSave.Text = "Save";
			this.menuSave.Click += new System.EventHandler(this.menuSave_Click);
			// 
			// menuSaveAs
			// 
			this.menuSaveAs.Name = "menuSaveAs";
			this.menuSaveAs.Size = new System.Drawing.Size(166, 22);
			this.menuSaveAs.Text = "Save As...";
			this.menuSaveAs.Click += new System.EventHandler(this.menuSaveAs_Click);
			// 
			// puzzleToolStripMenuItem
			// 
			this.puzzleToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuShowPuzzleTree});
			this.puzzleToolStripMenuItem.Name = "puzzleToolStripMenuItem";
			this.puzzleToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
			this.puzzleToolStripMenuItem.Text = "Puzzle";
			// 
			// menuShowPuzzleTree
			// 
			this.menuShowPuzzleTree.Name = "menuShowPuzzleTree";
			this.menuShowPuzzleTree.Size = new System.Drawing.Size(165, 22);
			this.menuShowPuzzleTree.Text = "Show Puzzle Tree";
			this.menuShowPuzzleTree.Click += new System.EventHandler(this.menuShowPuzzleTree_Click);
			// 
			// scrambleToolStripMenuItem
			// 
			this.scrambleToolStripMenuItem.Name = "scrambleToolStripMenuItem";
			this.scrambleToolStripMenuItem.Size = new System.Drawing.Size(68, 20);
			this.scrambleToolStripMenuItem.Text = "Scramble";
			// 
			// macroToolStripMenuItem
			// 
			this.macroToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuShowMacroPane,
            this.toolStripSeparator4,
            this.tipSeeWebsiteForInstructionsToolStripMenuItem,
            this.startMacroDefinitionToolStripMenuItem,
            this.menuStopRecordingMacro,
            this.applyMacroToolStripMenuItem,
            this.applyMacroReversedToolStripMenuItem,
            this.toolStripSeparator5,
            this.menuStartSetupMoves,
            this.menuEndSetupMoves,
            this.menuUnwindSetupMoves,
            this.menuCommutator,
            this.toolStripSeparator1,
            this.menuSaveMacros,
            this.menuLoadMacros});
			this.macroToolStripMenuItem.Name = "macroToolStripMenuItem";
			this.macroToolStripMenuItem.Size = new System.Drawing.Size(53, 20);
			this.macroToolStripMenuItem.Text = "Macro";
			// 
			// menuShowMacroPane
			// 
			this.menuShowMacroPane.Name = "menuShowMacroPane";
			this.menuShowMacroPane.Size = new System.Drawing.Size(332, 22);
			this.menuShowMacroPane.Text = "Show Macro Pane";
			this.menuShowMacroPane.Click += new System.EventHandler(this.menuShowMacroPane_Click);
			// 
			// toolStripSeparator4
			// 
			this.toolStripSeparator4.Name = "toolStripSeparator4";
			this.toolStripSeparator4.Size = new System.Drawing.Size(329, 6);
			// 
			// tipSeeWebsiteForInstructionsToolStripMenuItem
			// 
			this.tipSeeWebsiteForInstructionsToolStripMenuItem.Enabled = false;
			this.tipSeeWebsiteForInstructionsToolStripMenuItem.Name = "tipSeeWebsiteForInstructionsToolStripMenuItem";
			this.tipSeeWebsiteForInstructionsToolStripMenuItem.Size = new System.Drawing.Size(332, 22);
			this.tipSeeWebsiteForInstructionsToolStripMenuItem.Text = "Tip! See website for macro instructions";
			// 
			// startMacroDefinitionToolStripMenuItem
			// 
			this.startMacroDefinitionToolStripMenuItem.Enabled = false;
			this.startMacroDefinitionToolStripMenuItem.Name = "startMacroDefinitionToolStripMenuItem";
			this.startMacroDefinitionToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+Alt+Left Click";
			this.startMacroDefinitionToolStripMenuItem.Size = new System.Drawing.Size(332, 22);
			this.startMacroDefinitionToolStripMenuItem.Text = "Start Macro Definition";
			// 
			// menuStopRecordingMacro
			// 
			this.menuStopRecordingMacro.Name = "menuStopRecordingMacro";
			this.menuStopRecordingMacro.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.M)));
			this.menuStopRecordingMacro.Size = new System.Drawing.Size(332, 22);
			this.menuStopRecordingMacro.Text = "End Macro Definition";
			this.menuStopRecordingMacro.Click += new System.EventHandler(this.menuStopRecordingMacro_Click);
			// 
			// applyMacroToolStripMenuItem
			// 
			this.applyMacroToolStripMenuItem.Enabled = false;
			this.applyMacroToolStripMenuItem.Name = "applyMacroToolStripMenuItem";
			this.applyMacroToolStripMenuItem.ShortcutKeyDisplayString = "Alt+Left Click";
			this.applyMacroToolStripMenuItem.Size = new System.Drawing.Size(332, 22);
			this.applyMacroToolStripMenuItem.Text = "Apply Selected Macro";
			// 
			// applyMacroReversedToolStripMenuItem
			// 
			this.applyMacroReversedToolStripMenuItem.Enabled = false;
			this.applyMacroReversedToolStripMenuItem.Name = "applyMacroReversedToolStripMenuItem";
			this.applyMacroReversedToolStripMenuItem.ShortcutKeyDisplayString = "Alt+Right Click";
			this.applyMacroReversedToolStripMenuItem.Size = new System.Drawing.Size(332, 22);
			this.applyMacroReversedToolStripMenuItem.Text = "Apply Selected Macro in Reverse";
			// 
			// toolStripSeparator5
			// 
			this.toolStripSeparator5.Name = "toolStripSeparator5";
			this.toolStripSeparator5.Size = new System.Drawing.Size(329, 6);
			// 
			// menuStartSetupMoves
			// 
			this.menuStartSetupMoves.Name = "menuStartSetupMoves";
			this.menuStartSetupMoves.ShortcutKeys = System.Windows.Forms.Keys.F1;
			this.menuStartSetupMoves.Size = new System.Drawing.Size(332, 22);
			this.menuStartSetupMoves.Text = "Start Setup Moves";
			this.menuStartSetupMoves.Click += new System.EventHandler(this.menuStartSetupMoves_Click);
			// 
			// menuEndSetupMoves
			// 
			this.menuEndSetupMoves.Name = "menuEndSetupMoves";
			this.menuEndSetupMoves.ShortcutKeys = System.Windows.Forms.Keys.F2;
			this.menuEndSetupMoves.Size = new System.Drawing.Size(332, 22);
			this.menuEndSetupMoves.Text = "End Setup Moves";
			this.menuEndSetupMoves.Click += new System.EventHandler(this.menuEndSetupMoves_Click);
			// 
			// menuUnwindSetupMoves
			// 
			this.menuUnwindSetupMoves.Name = "menuUnwindSetupMoves";
			this.menuUnwindSetupMoves.ShortcutKeys = System.Windows.Forms.Keys.F3;
			this.menuUnwindSetupMoves.Size = new System.Drawing.Size(332, 22);
			this.menuUnwindSetupMoves.Text = "Unwind Setup Moves";
			this.menuUnwindSetupMoves.Click += new System.EventHandler(this.menuUnwindSetupMoves_Click);
			// 
			// menuCommutator
			// 
			this.menuCommutator.Name = "menuCommutator";
			this.menuCommutator.ShortcutKeys = System.Windows.Forms.Keys.F4;
			this.menuCommutator.Size = new System.Drawing.Size(332, 22);
			this.menuCommutator.Text = "Commutator";
			this.menuCommutator.Click += new System.EventHandler(this.menuCommutator_Click);
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			this.toolStripSeparator1.Size = new System.Drawing.Size(329, 6);
			// 
			// menuSaveMacros
			// 
			this.menuSaveMacros.Name = "menuSaveMacros";
			this.menuSaveMacros.Size = new System.Drawing.Size(332, 22);
			this.menuSaveMacros.Text = "Save Macro File...";
			this.menuSaveMacros.Click += new System.EventHandler(this.menuSaveMacros_Click);
			// 
			// menuLoadMacros
			// 
			this.menuLoadMacros.Name = "menuLoadMacros";
			this.menuLoadMacros.Size = new System.Drawing.Size(332, 22);
			this.menuLoadMacros.Text = "Load Macro File...";
			this.menuLoadMacros.Click += new System.EventHandler(this.menuLoadMacros_Click);
			// 
			// optionsToolStripMenuItem
			// 
			this.optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuShowSettingsPane,
            this.menuCollapseControls,
            this.toolStripSeparator2,
            this.menuUndo,
            this.menuRedo,
            this.menuSolve,
            this.menuResetState,
            this.toolStripSeparator3,
            this.menuResetView});
			this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
			this.optionsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
			this.optionsToolStripMenuItem.Text = "Options";
			// 
			// menuShowSettingsPane
			// 
			this.menuShowSettingsPane.Name = "menuShowSettingsPane";
			this.menuShowSettingsPane.Size = new System.Drawing.Size(202, 22);
			this.menuShowSettingsPane.Text = "Show Settings Pane";
			this.menuShowSettingsPane.Click += new System.EventHandler(this.menuShowSettingsPane_Click);
			// 
			// menuCollapseControls
			// 
			this.menuCollapseControls.CheckOnClick = true;
			this.menuCollapseControls.Name = "menuCollapseControls";
			this.menuCollapseControls.Size = new System.Drawing.Size(202, 22);
			this.menuCollapseControls.Text = "Collapse Side Controls";
			this.menuCollapseControls.CheckedChanged += new System.EventHandler(this.menuCollapseControls_CheckedChanged);
			// 
			// toolStripSeparator2
			// 
			this.toolStripSeparator2.Name = "toolStripSeparator2";
			this.toolStripSeparator2.Size = new System.Drawing.Size(199, 6);
			// 
			// menuUndo
			// 
			this.menuUndo.Name = "menuUndo";
			this.menuUndo.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z)));
			this.menuUndo.Size = new System.Drawing.Size(202, 22);
			this.menuUndo.Text = "Undo";
			this.menuUndo.Click += new System.EventHandler(this.menuUndo_Click);
			// 
			// menuRedo
			// 
			this.menuRedo.Name = "menuRedo";
			this.menuRedo.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y)));
			this.menuRedo.Size = new System.Drawing.Size(202, 22);
			this.menuRedo.Text = "Redo";
			this.menuRedo.Click += new System.EventHandler(this.menuRedo_Click);
			// 
			// menuSolve
			// 
			this.menuSolve.Name = "menuSolve";
			this.menuSolve.Size = new System.Drawing.Size(202, 22);
			this.menuSolve.Text = "Solve (escape to Cancel)";
			this.menuSolve.Click += new System.EventHandler(this.menuSolve_Click);
			// 
			// menuResetState
			// 
			this.menuResetState.Name = "menuResetState";
			this.menuResetState.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D0)));
			this.menuResetState.Size = new System.Drawing.Size(202, 22);
			this.menuResetState.Text = "Reset State";
			this.menuResetState.Click += new System.EventHandler(this.menuResetState_Click);
			// 
			// toolStripSeparator3
			// 
			this.toolStripSeparator3.Name = "toolStripSeparator3";
			this.toolStripSeparator3.Size = new System.Drawing.Size(199, 6);
			// 
			// menuResetView
			// 
			this.menuResetView.Name = "menuResetView";
			this.menuResetView.Size = new System.Drawing.Size(202, 22);
			this.menuResetView.Text = "Reset View";
			this.menuResetView.Click += new System.EventHandler(this.menuResetView_Click);
			// 
			// helpToolStripMenuItem
			// 
			this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuMouseCommands,
            this.menuAbout});
			this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
			this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
			this.helpToolStripMenuItem.Text = "Help";
			// 
			// menuMouseCommands
			// 
			this.menuMouseCommands.Name = "menuMouseCommands";
			this.menuMouseCommands.Size = new System.Drawing.Size(239, 22);
			this.menuMouseCommands.Text = "Mouse/Keyboard Commands...";
			this.menuMouseCommands.Click += new System.EventHandler(this.menuMouseCommands_Click);
			// 
			// menuAbout
			// 
			this.menuAbout.Name = "menuAbout";
			this.menuAbout.Size = new System.Drawing.Size(239, 22);
			this.menuAbout.Text = "About...";
			this.menuAbout.Click += new System.EventHandler(this.menuAbout_Click);
			// 
			// m_propertyGrid
			// 
			this.m_propertyGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.m_propertyGrid.Location = new System.Drawing.Point(6, 6);
			this.m_propertyGrid.Name = "m_propertyGrid";
			this.m_propertyGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
			this.m_propertyGrid.Size = new System.Drawing.Size(233, 449);
			this.m_propertyGrid.TabIndex = 2;
			this.m_propertyGrid.ToolbarVisible = false;
			this.m_propertyGrid.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.m_propertyGrid_PropertyValueChanged);
			// 
			// m_splitContainer
			// 
			this.m_splitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.m_splitContainer.Location = new System.Drawing.Point(12, 27);
			this.m_splitContainer.Name = "m_splitContainer";
			// 
			// m_splitContainer.Panel1
			// 
			this.m_splitContainer.Panel1.Controls.Add(this.m_glControl);
			// 
			// m_splitContainer.Panel2
			// 
			this.m_splitContainer.Panel2.Controls.Add(this.m_tabControl);
			this.m_splitContainer.Size = new System.Drawing.Size(785, 519);
			this.m_splitContainer.SplitterDistance = 522;
			this.m_splitContainer.TabIndex = 3;
			this.m_splitContainer.SplitterMoving += new System.Windows.Forms.SplitterCancelEventHandler(this.m_splitContainer_SplitterMoving);
			// 
			// m_tabControl
			// 
			this.m_tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.m_tabControl.Controls.Add(this.m_tabPuzzles);
			this.m_tabControl.Controls.Add(this.m_tabSettings);
			this.m_tabControl.Controls.Add(this.m_tabMacros);
			this.m_tabControl.Location = new System.Drawing.Point(3, 0);
			this.m_tabControl.Name = "m_tabControl";
			this.m_tabControl.SelectedIndex = 0;
			this.m_tabControl.Size = new System.Drawing.Size(253, 516);
			this.m_tabControl.TabIndex = 4;
			// 
			// m_tabPuzzles
			// 
			this.m_tabPuzzles.Controls.Add(this.statusStrip2);
			this.m_tabPuzzles.Controls.Add(this.m_puzzleTree);
			this.m_tabPuzzles.Location = new System.Drawing.Point(4, 22);
			this.m_tabPuzzles.Name = "m_tabPuzzles";
			this.m_tabPuzzles.Padding = new System.Windows.Forms.Padding(3);
			this.m_tabPuzzles.Size = new System.Drawing.Size(245, 490);
			this.m_tabPuzzles.TabIndex = 2;
			this.m_tabPuzzles.Text = "Puzzles";
			this.m_tabPuzzles.UseVisualStyleBackColor = true;
			// 
			// statusStrip2
			// 
			this.statusStrip2.BackColor = System.Drawing.SystemColors.Window;
			this.statusStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.m_numPuzzles});
			this.statusStrip2.Location = new System.Drawing.Point(3, 465);
			this.statusStrip2.Name = "statusStrip2";
			this.statusStrip2.Size = new System.Drawing.Size(239, 22);
			this.statusStrip2.TabIndex = 2;
			this.statusStrip2.Text = "statusStrip2";
			// 
			// m_numPuzzles
			// 
			this.m_numPuzzles.Name = "m_numPuzzles";
			this.m_numPuzzles.Size = new System.Drawing.Size(106, 17);
			this.m_numPuzzles.Text = "X Puzzles Available";
			// 
			// m_puzzleTree
			// 
			this.m_puzzleTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.m_puzzleTree.Location = new System.Drawing.Point(7, 6);
			this.m_puzzleTree.Name = "m_puzzleTree";
			this.m_puzzleTree.Size = new System.Drawing.Size(232, 456);
			this.m_puzzleTree.TabIndex = 1;
			this.m_puzzleTree.DoubleClick += new System.EventHandler(this.m_puzzleTree_DoubleClick);
			// 
			// m_tabSettings
			// 
			this.m_tabSettings.Controls.Add(this.m_propertyGrid);
			this.m_tabSettings.Controls.Add(this.m_buttonSetDefaults);
			this.m_tabSettings.Location = new System.Drawing.Point(4, 22);
			this.m_tabSettings.Name = "m_tabSettings";
			this.m_tabSettings.Padding = new System.Windows.Forms.Padding(3);
			this.m_tabSettings.Size = new System.Drawing.Size(245, 490);
			this.m_tabSettings.TabIndex = 0;
			this.m_tabSettings.Text = "Settings";
			this.m_tabSettings.UseVisualStyleBackColor = true;
			// 
			// m_buttonSetDefaults
			// 
			this.m_buttonSetDefaults.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.m_buttonSetDefaults.Location = new System.Drawing.Point(6, 461);
			this.m_buttonSetDefaults.Name = "m_buttonSetDefaults";
			this.m_buttonSetDefaults.Size = new System.Drawing.Size(233, 23);
			this.m_buttonSetDefaults.TabIndex = 3;
			this.m_buttonSetDefaults.Text = "Reset to Defaults";
			this.m_buttonSetDefaults.UseVisualStyleBackColor = true;
			this.m_buttonSetDefaults.Click += new System.EventHandler(this.m_buttonSetDefaults_Click);
			// 
			// m_tabMacros
			// 
			this.m_tabMacros.Controls.Add(this.m_btnRename);
			this.m_tabMacros.Controls.Add(this.m_btnDelete);
			this.m_tabMacros.Controls.Add(this.m_macroListView);
			this.m_tabMacros.Location = new System.Drawing.Point(4, 22);
			this.m_tabMacros.Name = "m_tabMacros";
			this.m_tabMacros.Padding = new System.Windows.Forms.Padding(3);
			this.m_tabMacros.Size = new System.Drawing.Size(245, 490);
			this.m_tabMacros.TabIndex = 1;
			this.m_tabMacros.Text = "Macros";
			this.m_tabMacros.UseVisualStyleBackColor = true;
			// 
			// m_btnRename
			// 
			this.m_btnRename.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.m_btnRename.Location = new System.Drawing.Point(83, 461);
			this.m_btnRename.Name = "m_btnRename";
			this.m_btnRename.Size = new System.Drawing.Size(75, 23);
			this.m_btnRename.TabIndex = 1;
			this.m_btnRename.Text = "Rename...";
			this.m_btnRename.UseVisualStyleBackColor = true;
			this.m_btnRename.Click += new System.EventHandler(this.m_btnRename_Click);
			// 
			// m_btnDelete
			// 
			this.m_btnDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.m_btnDelete.Location = new System.Drawing.Point(164, 461);
			this.m_btnDelete.Name = "m_btnDelete";
			this.m_btnDelete.Size = new System.Drawing.Size(75, 23);
			this.m_btnDelete.TabIndex = 2;
			this.m_btnDelete.Text = "Delete";
			this.m_btnDelete.UseVisualStyleBackColor = true;
			this.m_btnDelete.Click += new System.EventHandler(this.m_btnDelete_Click);
			// 
			// m_macroListView
			// 
			this.m_macroListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.m_macroListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderName,
            this.columnHeaderNumMoves});
			this.m_macroListView.FullRowSelect = true;
			this.m_macroListView.HideSelection = false;
			this.m_macroListView.Location = new System.Drawing.Point(6, 6);
			this.m_macroListView.MultiSelect = false;
			this.m_macroListView.Name = "m_macroListView";
			this.m_macroListView.Size = new System.Drawing.Size(233, 449);
			this.m_macroListView.TabIndex = 0;
			this.m_macroListView.UseCompatibleStateImageBehavior = false;
			this.m_macroListView.View = System.Windows.Forms.View.Details;
			this.m_macroListView.SelectedIndexChanged += new System.EventHandler(this.m_macroListView_SelectedIndexChanged);
			this.m_macroListView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.m_macroListView_KeyDown);
			// 
			// columnHeaderName
			// 
			this.columnHeaderName.Text = "Name";
			this.columnHeaderName.Width = 101;
			// 
			// columnHeaderNumMoves
			// 
			this.columnHeaderNumMoves.Text = "Number of Moves";
			this.columnHeaderNumMoves.Width = 126;
			// 
			// m_buildTimer
			// 
			this.m_buildTimer.Interval = 25;
			this.m_buildTimer.Tick += new System.EventHandler(this.m_buildTimer_Tick);
			// 
			// splitter1
			// 
			this.splitter1.Location = new System.Drawing.Point(0, 24);
			this.splitter1.Name = "splitter1";
			this.splitter1.Size = new System.Drawing.Size(3, 547);
			this.splitter1.TabIndex = 4;
			this.splitter1.TabStop = false;
			// 
			// statusStrip1
			// 
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.m_status,
            this.m_status2});
			this.statusStrip1.Location = new System.Drawing.Point(3, 549);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(806, 22);
			this.statusStrip1.TabIndex = 5;
			this.statusStrip1.Text = "statusStrip1";
			// 
			// m_status
			// 
			this.m_status.Name = "m_status";
			this.m_status.Size = new System.Drawing.Size(40, 17);
			this.m_status.Text = "Twists";
			// 
			// m_status2
			// 
			this.m_status2.Name = "m_status2";
			this.m_status2.Size = new System.Drawing.Size(751, 17);
			this.m_status2.Spring = true;
			this.m_status2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// toolStripSeparator6
			// 
			this.toolStripSeparator6.Name = "toolStripSeparator6";
			this.toolStripSeparator6.Size = new System.Drawing.Size(163, 6);
			// 
			// menuGap
			// 
			this.menuGap.Name = "menuGap";
			this.menuGap.Size = new System.Drawing.Size(166, 22);
			this.menuGap.Text = "Save GAP Script...";
			this.menuGap.Click += new System.EventHandler(this.menuGap_Click);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(809, 571);
			this.Controls.Add(this.statusStrip1);
			this.Controls.Add(this.splitter1);
			this.Controls.Add(this.m_splitContainer);
			this.Controls.Add(this.menuStrip1);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MainMenuStrip = this.menuStrip1;
			this.Name = "MainForm";
			this.Text = "MagicTile";
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
			this.Load += new System.EventHandler(this.MainForm_Load);
			this.Shown += new System.EventHandler(this.MainForm_Shown);
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			this.m_splitContainer.Panel1.ResumeLayout(false);
			this.m_splitContainer.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.m_splitContainer)).EndInit();
			this.m_splitContainer.ResumeLayout(false);
			this.m_tabControl.ResumeLayout(false);
			this.m_tabPuzzles.ResumeLayout(false);
			this.m_tabPuzzles.PerformLayout();
			this.statusStrip2.ResumeLayout(false);
			this.statusStrip2.PerformLayout();
			this.m_tabSettings.ResumeLayout(false);
			this.m_tabMacros.ResumeLayout(false);
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private MagicTileControl m_glControl;
		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem puzzleToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem scrambleToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
		private System.Windows.Forms.PropertyGrid m_propertyGrid;
		private System.Windows.Forms.ToolStripMenuItem menuUndo;
		private System.Windows.Forms.ToolStripMenuItem menuRedo;
		private System.Windows.Forms.ToolStripMenuItem menuSolve;
		private System.Windows.Forms.ToolStripMenuItem menuResetState;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
		private System.Windows.Forms.ToolStripMenuItem menuCollapseControls;
		private System.Windows.Forms.SplitContainer m_splitContainer;
		private System.Windows.Forms.ToolStripMenuItem menuResetView;
		private System.Windows.Forms.Button m_buttonSetDefaults;
		private System.Windows.Forms.ToolStripMenuItem menuOpen;
		private System.Windows.Forms.ToolStripMenuItem menuSave;
		private System.Windows.Forms.ToolStripMenuItem menuSaveAs;
		private System.Windows.Forms.ToolStripMenuItem menuMouseCommands;
		private System.Windows.Forms.ToolStripMenuItem menuAbout;
		private System.Windows.Forms.Timer m_buildTimer;
		private System.Windows.Forms.Splitter splitter1;
		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.ToolStripStatusLabel m_status;
		private System.Windows.Forms.ToolStripMenuItem macroToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem menuStopRecordingMacro;
		private System.Windows.Forms.ToolStripMenuItem menuLoadMacros;
		private System.Windows.Forms.ToolStripMenuItem menuSaveMacros;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.ToolStripMenuItem menuStartSetupMoves;
		private System.Windows.Forms.ToolStripMenuItem menuEndSetupMoves;
		private System.Windows.Forms.ToolStripMenuItem menuUnwindSetupMoves;
		private System.Windows.Forms.ToolStripMenuItem menuCommutator;
		private System.Windows.Forms.TabControl m_tabControl;
		private System.Windows.Forms.TabPage m_tabSettings;
		private System.Windows.Forms.TabPage m_tabMacros;
		private System.Windows.Forms.ListView m_macroListView;
		private System.Windows.Forms.ColumnHeader columnHeaderName;
		private System.Windows.Forms.ColumnHeader columnHeaderNumMoves;
		private System.Windows.Forms.Button m_btnRename;
		private System.Windows.Forms.Button m_btnDelete;
		private System.Windows.Forms.ToolStripStatusLabel m_status2;
		private System.Windows.Forms.TabPage m_tabPuzzles;
		private System.Windows.Forms.TreeView m_puzzleTree;
		private System.Windows.Forms.StatusStrip statusStrip2;
		private System.Windows.Forms.ToolStripStatusLabel m_numPuzzles;
		private System.Windows.Forms.ToolStripMenuItem menuShowPuzzleTree;
		private System.Windows.Forms.ToolStripMenuItem menuShowMacroPane;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
		private System.Windows.Forms.ToolStripMenuItem menuShowSettingsPane;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
		private System.Windows.Forms.ToolStripMenuItem startMacroDefinitionToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem applyMacroToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem applyMacroReversedToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
		private System.Windows.Forms.ToolStripMenuItem tipSeeWebsiteForInstructionsToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
		private System.Windows.Forms.ToolStripMenuItem menuGap;
	}
}

