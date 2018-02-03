namespace MagicTile.Utils
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows.Forms;
	using System.Xml;
	using System.Xml.Linq;

	internal class MenuBuilder
	{
		public MenuBuilder( TreeView tree, ToolStripMenuItem puzzleRoot, 
			System.EventHandler showTreeHandler, System.EventHandler selectPuzzleHandler )
		{
			m_tree = tree;
			m_puzzleRoot = puzzleRoot;
			m_showTree = showTreeHandler;
			m_handler = selectPuzzleHandler;
		}

		private TreeView m_tree;
		ToolStripMenuItem m_puzzleRoot;
		System.EventHandler m_showTree;
		System.EventHandler m_handler;

		public int NumPuzzles { get; set; }
		public int NumTilings { get; set; }

		public void BuildMenu()
		{
			NumPuzzles = NumTilings = 0;
			m_puzzleRoot.DropDownItems.Clear();
			m_tree.BeginUpdate();
			m_tree.Nodes.Clear();

			ToolStripMenuItem showPuzzleTree = new ToolStripMenuItem();
			showPuzzleTree.Text = "Show Puzzle Tree";
			showPuzzleTree.Click += new System.EventHandler( m_showTree ); 
			m_puzzleRoot.DropDownItems.Add( showPuzzleTree );

			//m_puzzleRoot.DropDownItems.Add( new ToolStripSeparator() );
			ToolStripMenuItem f6 = new ToolStripMenuItem(), f7 = new ToolStripMenuItem();
			f6.Text = "F6 Toggles Surface View";
			f7.Text = "F7 Cycles Model for Spherical/Hyperbolic Puzzles";
			f6.Enabled = false;
			f7.Enabled = false;
			//m_puzzleRoot.DropDownItems.Add( f6 );
			//m_puzzleRoot.DropDownItems.Add( f7 );

			// Load the puzzles.
			IEnumerable<PuzzleConfigClass> standard, user;
			PuzzleConfigClass.LoadAllPuzzles( out standard, out user );

			// Load the menu config from a file.
			try
			{
				// Start here menu item.
				TreeNode startHereNode;
				ToolStripMenuItem startHereMenuItem;
				AddSection( "Start Here!", out startHereNode, out startHereMenuItem );
				XElement xStartHere;

				// Full configured menu.
				LoadMenu( standard.ToArray(), out xStartHere );

				// Now we can add in the "start here" items.
				SetupStartHereItems( xStartHere, startHereNode, startHereMenuItem );

				// User puzzles.
				if( user.Count() > 0 )
				{
					TreeNode userNode;
					ToolStripMenuItem userMenuItem;
					AddSection( "User", out userNode, out userMenuItem );

					foreach( PuzzleConfigClass configClass in user )
					{
						TreeNode groupNode;
						ToolStripMenuItem groupMenuItem;
						AddGroup( null, 0, configClass.ClassDisplayName, userNode, userMenuItem, out groupNode, out groupMenuItem );
						AddPuzzleClass( null, null, 0, configClass, groupNode, groupMenuItem );
					}
				}
			}
			catch( System.Exception e )
			{
				System.Diagnostics.Debug.WriteLine( "Failed to load menu." );
				System.Diagnostics.Debug.WriteLine( e.Message );
			}

			m_tree.EndUpdate();
		}

		private void AddSection( string name, out TreeNode node, out ToolStripMenuItem menuItem )
		{
			node = new TreeNode();
			menuItem = new ToolStripMenuItem();
			node.Text = menuItem.Text = name;
			m_tree.Nodes.Add( node );
			m_puzzleRoot.DropDownItems.Add( new ToolStripSeparator() );
			m_puzzleRoot.DropDownItems.Add( menuItem );
		}

		private void SetupStartHereItems( XElement xStartHere, TreeNode startHereNode, ToolStripMenuItem startHereMenuItem )
		{
			SetupStartHereGroup( xStartHere, startHereNode, startHereMenuItem );

			foreach( XElement xChild in xStartHere.Elements() )
			{
				if( xChild.Name == "Group" )
				{
					string groupName = GroupName( xChild );
					TreeNode groupNode;
					ToolStripMenuItem groupMenuItem;
					AddGroup( null, 1, groupName, startHereNode, startHereMenuItem, out groupNode, out groupMenuItem );

					SetupStartHereGroup( xChild, groupNode, groupMenuItem );
				}
			}
		}

		private void SetupStartHereGroup( XElement xElement, TreeNode node, ToolStripMenuItem menuItem )
		{
			foreach( XElement xChild in xElement.Elements() )
			{
				if( xChild.Name == "Group" )
					continue;

				string id = xChild.Elements( "ID" ).First().Value;
				PuzzleConfig config;
				if( !m_puzzleIds.TryGetValue( id, out config ) )
					continue;

				string name = xChild.Elements( "DisplayName" ).First().Value;
				AddPuzzleRef( name, config, node, menuItem );
			}
		}

		private void LoadMenu( PuzzleConfigClass[] standard, out XElement xStartHere )
		{
			// Uncomment line below to write out a MediaWiki file with tables for all puzzles.
			StreamWriter swList = null;
			StreamWriter swWiki = null;
			//using( StreamWriter swList = new StreamWriter( "puzzles.txt" ) )
			//using( StreamWriter swWiki = new StreamWriter( "table.txt" ) )
			using( var reader = XmlReader.Create( StandardPaths.MenuFile, Persistence.ReaderSettings ) )
			{
				XElement xRoot = XElement.Load( reader );
				XElement[] children = xRoot.Elements().ToArray();
				xStartHere = children[0].Elements( "Start" ).First();
				int level = 0;
				BuildMenuRecursive( swList, swWiki, level, children[0], standard, null, m_puzzleRoot );
			}
		}

		private void BuildMenuRecursive( StreamWriter swList, StreamWriter swWiki, int level, XElement xElement, PuzzleConfigClass[] configs, 
			TreeNode parentTreeNode, ToolStripMenuItem parentMenuItem )
		{
			level++;

			foreach( XElement xChild in xElement.Elements() )
			{
				if( xChild.Name == "Item" )
				{
					PuzzleConfigClass[] configClassArray = configs.Where( c => c.ClassID == xChild.Value ).ToArray();
					if( configClassArray.Length == 0 )
						continue;
					PuzzleConfigClass configClass = configClassArray[0];

					TreeNode groupNode;
					ToolStripMenuItem groupMenuItem;

					Breaks( swWiki, 1 );
					AddGroup( swWiki, level, configClass.ClassDisplayName, parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
					HorizontalRule( swWiki );
					AddPuzzleClass( swList, swWiki, level, configClass, groupNode, groupMenuItem );
				}
				else if( xChild.Name == "Start" )
				{
					// Nothing for now.
					// We need to load all puzzles first before filling out the "start here" menu.
				}
				else
				{
					string groupName = GroupName( xChild );

					TreeNode groupNode;
					ToolStripMenuItem groupMenuItem;

					Breaks( swWiki, 3 - level );
					AddGroup( swWiki, level, groupName, parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );

					BuildMenuRecursive( swList, swWiki, level, xChild, configs, groupNode, groupMenuItem );
				}
			}
		}

		static string GroupName( XElement xElement )
		{
			string groupName = string.Empty;
			if( xElement.Name == "Group" )
				groupName = xElement.Attribute( "Name" ).Value;
			else
				groupName = xElement.Name.ToString();
			return groupName;
		}

		private void AddGroup( StreamWriter sw, int level, string groupName, TreeNode parentTreeNode, ToolStripMenuItem parentMenuItem,
			out TreeNode groupNode, out ToolStripMenuItem groupMenuItem )
		{
			level++;

			groupNode = new TreeNode();
			groupMenuItem = new ToolStripMenuItem();
			groupMenuItem.Text = groupNode.Text = groupName;
			parentMenuItem.DropDownItems.Add( groupMenuItem );

			if( parentTreeNode != null )
				parentTreeNode.Nodes.Add( groupNode );
			else
				m_tree.Nodes.Add( groupNode );

			WriteTableGroup( sw, level, groupName );
		}

		private void AddPuzzleClass( StreamWriter swList, StreamWriter swWiki, int level, PuzzleConfigClass puzzleConfigClass,
			TreeNode parentTreeNode, ToolStripMenuItem parentMenuItem )
		{
			level++;

			PuzzleConfig[] tilings;
			PuzzleConfig[] face, edge, vertex, mixed, earthquake, toggles;
			puzzleConfigClass.GetPuzzles( out tilings, out face, out edge, out vertex, out mixed, out earthquake, out toggles );

			AddPuzzle( tilings[0], parentTreeNode, parentMenuItem, isTiling: true );
			AddPuzzle( tilings[1], parentTreeNode, parentMenuItem, isTiling: true );

			TreeNode groupNode;
			ToolStripMenuItem groupMenuItem;
			if( face.Length > 0 )
			{
				AddGroup( swWiki, level, "Face Twisting", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( swWiki );
				foreach( PuzzleConfig config in face )
				{
					if( swList != null )
						swList.WriteLine( config.DisplayName );
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( swWiki, config.MenuName );
				}
				EndTable( swWiki );
			}

			if( edge.Length > 0 )
			{
				AddGroup( swWiki, level, "Edge Twisting", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( swWiki );
				foreach( PuzzleConfig config in edge )
				{
					if( swList != null )
						swList.WriteLine( config.DisplayName );
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( swWiki, config.MenuName );
				}
				EndTable( swWiki );
			}

			if( vertex.Length > 0 )
			{
				AddGroup( swWiki, level, "Vertex Twisting", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( swWiki );
				foreach( PuzzleConfig config in vertex )
				{
					if( swList != null )
						swList.WriteLine( config.DisplayName );
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( swWiki, config.MenuName );
				}
				EndTable( swWiki );
			}

			if( mixed.Length > 0 )
			{
				AddGroup( swWiki, level, "Mixed Twisting", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( swWiki );
				foreach( PuzzleConfig config in mixed )
				{
					if( swList != null )
						swList.WriteLine( config.DisplayName );
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( swWiki, config.MenuName );
				}
				EndTable( swWiki );
			}

			if( earthquake.Length > 0 )
			{
				AddGroup( swWiki, level, "Special", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( swWiki );
				foreach( PuzzleConfig config in earthquake )
				{
					if( swList != null )
						swList.WriteLine( config.DisplayName );
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( swWiki, config.MenuName );
				}
				EndTable( swWiki );
			}

			if (toggles.Length > 0)
			{
				AddGroup(swWiki, level, "Lights On", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem);
				StartTable(swWiki);
				foreach (PuzzleConfig config in toggles)
				{
					if (swList != null)
						swList.WriteLine(config.DisplayName);
					AddPuzzle(config, groupNode, groupMenuItem, isTiling: false);
					WriteTableEntry(swWiki, config.MenuName);
				}
				EndTable(swWiki);
			}
		}

		private void AddPuzzle( PuzzleConfig config, TreeNode parentTreeNode, ToolStripMenuItem parentMenuItem, bool isTiling )
		{
			TreeNode newNode = new TreeNode();
			ToolStripMenuItem menuItem = new ToolStripMenuItem();

			menuItem.Text = config.DisplayName;
			menuItem.Tag = config;
			menuItem.Click += new System.EventHandler( m_handler );

			newNode.Text = config.MenuName;
			newNode.Tag = config;

			parentMenuItem.DropDownItems.Add( menuItem );
			parentTreeNode.Nodes.Add( newNode );

			if( isTiling )
			{
				// isTiling is true in a couple situations, so just do this once 
				// (doesn't really matter which case we do it for)
				if( config.CoxeterComplex )
				{
					NumTilings++;
				}
			}
			else
			{
				NumPuzzles++;
				m_puzzleIds[config.ID] = config;
				if( NumPuzzles != m_puzzleIds.Count )
				{
					System.Diagnostics.Debug.Assert( false );
					//throw new System.Exception( "Duplicate puzzle config IDs." );
				}
			}
		}

		private void AddPuzzleRef( string displayName, PuzzleConfig config, TreeNode parentTreeNode, ToolStripMenuItem parentMenuItem )
		{
			TreeNode newNode = new TreeNode();
			ToolStripMenuItem menuItem = new ToolStripMenuItem();

			menuItem.Text = displayName;
			menuItem.Tag = config;
			menuItem.Click += new System.EventHandler( m_handler );

			newNode.Text = displayName;
			newNode.Tag = config;

			parentMenuItem.DropDownItems.Add( menuItem );
			parentTreeNode.Nodes.Add( newNode );
		}

		private Dictionary<string, PuzzleConfig> m_puzzleIds = new Dictionary<string, PuzzleConfig>();

		#region MediaWiki

		private void WriteTableGroup( StreamWriter sw, int level, string name )
		{
			if( sw == null )
				return;

			string line = "";
			for( int i = 0; i < level; i++ )
				line += "=";
			line = line + " " + name + " " + line;
			sw.WriteLine( line );
		}

		private void StartTable( StreamWriter sw )
		{
			if( sw == null )
				return;
			sw.WriteLine( "{|border=\"1\" style=\"text-align:center;\"" );

			sw.WriteLine( "! style=\"color:green; width:250px;\" | Puzzle" );
			sw.WriteLine( "! style=\"color:green; width:250px;\" | Solver" );
			sw.WriteLine( "! style=\"color:green; width:250px;\" | Date" );
			sw.WriteLine( "! style=\"color:green; width:250px;\" | Number of twists" );
		}

		private void EndTable( StreamWriter sw )
		{
			if( sw == null )
				return;
			sw.WriteLine( "|}" );
		}

		private void WriteTableEntry( StreamWriter sw, string name )
		{
			if( sw == null )
				return;
			sw.WriteLine( "|-" );
			string line = string.Format( "|  {0}  ||  -  ||  -  ||  -  ", name );
			sw.WriteLine( line );
		}

		private void HorizontalRule( StreamWriter sw )
		{
			if( sw == null )
				return;
			sw.WriteLine( "----" );
		}

		private void Breaks( StreamWriter sw, int num )
		{
			if( sw == null || num < 1 )
				return;
			string breakString = "<br/>";
			string line = string.Empty;
			for( int i = 0; i < num; i++ )
				line += breakString;
			sw.WriteLine( line );
		}

		#endregion
	}
}
