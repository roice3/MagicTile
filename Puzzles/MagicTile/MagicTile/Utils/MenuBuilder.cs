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
			m_puzzleRoot.DropDownItems.Add( new ToolStripSeparator() );

			// Load the puzzles.
			IEnumerable<PuzzleConfigClass> standard, user;
			PuzzleConfigClass.LoadAllPuzzles( out standard, out user );

			// Load the menu config from a file.
			try
			{
				LoadMenu( standard.ToArray() );

				if( user.Count() > 0 )
				{
					TreeNode userNode = new TreeNode();
					ToolStripMenuItem userMenuItem = new ToolStripMenuItem();
					userNode.Text = userMenuItem.Text = "User";
					m_tree.Nodes.Add( userNode );
					m_puzzleRoot.DropDownItems.Add( new ToolStripSeparator() );
					m_puzzleRoot.DropDownItems.Add( userMenuItem );

					foreach( PuzzleConfigClass configClass in user )
					{
						TreeNode groupNode;
						ToolStripMenuItem groupMenuItem;
						AddGroup( null, 0, configClass.ClassDisplayName, userNode, userMenuItem, out groupNode, out groupMenuItem );
						AddPuzzleClass( null, 0, configClass, groupNode, groupMenuItem );
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

		private void LoadMenu( PuzzleConfigClass[] standard )
		{
			// Uncomment line below to write out a MediaWiki file with tables for all puzzles.
			StreamWriter sw = null;
			//using( StreamWriter sw = new StreamWriter( "table.txt" ) )
			using( var reader = XmlReader.Create( StandardPaths.MenuFile, Persistence.ReaderSettings ) )
			{
				XElement xRoot = XElement.Load( reader );
				XElement[] children = xRoot.Elements().ToArray();
				int level = 0;
				BuildMenuRecursive( sw, level, children[0], standard, null, m_puzzleRoot );
			}
		}

		private void BuildMenuRecursive( StreamWriter sw, int level, XElement xElement, PuzzleConfigClass[] configs, 
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

					Breaks( sw, 1 );
					AddGroup( sw, level, configClass.ClassDisplayName, parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
					HorizontalRule( sw );
					AddPuzzleClass( sw, level, configClass, groupNode, groupMenuItem );
				}
				else
				{
					string groupName = GroupName( xChild );

					TreeNode groupNode;
					ToolStripMenuItem groupMenuItem;

					Breaks( sw, 3 - level );
					AddGroup( sw, level, groupName, parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );

					BuildMenuRecursive( sw, level, xChild, configs, groupNode, groupMenuItem );
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

		private void AddPuzzleClass( StreamWriter sw, int level, PuzzleConfigClass puzzleConfigClass,
			TreeNode parentTreeNode, ToolStripMenuItem parentMenuItem )
		{
			level++;

			PuzzleConfig tiling;
			PuzzleConfig[] face, edge, vertex, mixed, earthquake;
			puzzleConfigClass.GetPuzzles( out tiling, out face, out edge, out vertex, out mixed, out earthquake );

			AddPuzzle( tiling, parentTreeNode, parentMenuItem, isTiling: true );

			TreeNode groupNode;
			ToolStripMenuItem groupMenuItem;
			if( face.Length > 0 )
			{
				AddGroup( sw, level, "Face Twisting", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( sw );
				foreach( PuzzleConfig config in face )
				{
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( sw, config.MenuName );
				}
				EndTable( sw );
			}

			if( edge.Length > 0 )
			{
				AddGroup( sw, level, "Edge Twisting", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( sw );
				foreach( PuzzleConfig config in edge )
				{
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( sw, config.MenuName );
				}
				EndTable( sw );
			}

			if( vertex.Length > 0 )
			{
				AddGroup( sw, level, "Vertex Twisting", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( sw );
				foreach( PuzzleConfig config in vertex )
				{
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( sw, config.MenuName );
				}
				EndTable( sw );
			}

			if( mixed.Length > 0 )
			{
				AddGroup( sw, level, "Mixed Twisting", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( sw );
				foreach( PuzzleConfig config in mixed )
				{
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( sw, config.MenuName );
				}
				EndTable( sw );
			}

			if( earthquake.Length > 0 )
			{
				AddGroup( sw, level, "Special", parentTreeNode, parentMenuItem, out groupNode, out groupMenuItem );
				StartTable( sw );
				foreach( PuzzleConfig config in earthquake )
				{
					AddPuzzle( config, groupNode, groupMenuItem, isTiling: false );
					WriteTableEntry( sw, config.MenuName );
				}
				EndTable( sw );
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
				NumTilings++;
			else
			{
				NumPuzzles++;
				m_puzzleIds.Add( config.ID );
				if( NumPuzzles != m_puzzleIds.Count )
				{
					System.Diagnostics.Debug.Assert( false );
					throw new System.Exception( "Duplicate puzzle config IDs." );
				}
			}
		}

		private HashSet<string> m_puzzleIds = new HashSet<string>();

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
