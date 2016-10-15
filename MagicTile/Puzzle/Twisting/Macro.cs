namespace MagicTile
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Numerics;
	using System.Windows.Forms;
	using System.Xml.Linq;
	using MagicTile.Utils;
	using R3.Geometry;
	using R3.Math;

	using Math = System.Math;

	public class Macro
	{
		public Macro CloneAllButTwists()
		{
			Macro m = new Macro();
			m.DisplayName = this.DisplayName;
			m.Mobius = this.Mobius;
			m.ViewReflected = this.ViewReflected;
			return m;
		}

		public Macro Clone()
		{
			Macro m = CloneAllButTwists();
			m.m_twists = this.m_twists.Clone();
			return m;
		}

		/// <summary>
		/// Our Display Name.
		/// </summary>
		public string DisplayName { get; set; }

		/// <summary>
		/// This is the reference mobius transform for this Macro.
		/// (It is calculated by the point clicked when the macro was started).
		/// It is the transform which will reorient the macro to a standard position on the home tile.
		/// </summary>
		public Mobius Mobius { get; set; }

		/// <summary>
		/// Whether or not the view was reflected when we were defined,
		/// which can happen with non-orientable puzzles.
		/// This is necessary to make sure macros get reflected properly during application.
		/// </summary>
		public bool ViewReflected { get; set; }

		/// <summary>
		/// Does the calculation of the reference mobius transform for this macro.
		/// NOTE: The clicked point should be in coordinates untransformed by mouse motion.
		/// </summary>
		public void SetupMobius( Cell clickedCell, Vector3D clickedPoint, Puzzle puzzle, bool mouseMotionReflected )
		{
			this.Mobius = SetupIsometry( clickedCell, clickedPoint, puzzle ).Mobius;
			this.ViewReflected = mouseMotionReflected;
		}

		private static Isometry SetupIsometry( Cell clickedCell, Vector3D clickedPoint, Puzzle puzzle )
		{
			int p = puzzle.Config.P;
			Geometry g = puzzle.Config.Geometry;
			Isometry cellIsometry = clickedCell.Isometry.Clone();

			// Take out reflections.
			// ZZZ - Figuring out how to deal with these reflected isometries was a bit painful to figure out.
			//		 I wish I had just taken more care to not have any cell isometries with reflections.
			//		 Maybe I can rework that to be different at some point.
			if( cellIsometry.Reflection != null )
				cellIsometry = Isometry.ReflectX() * cellIsometry;

			// Round to nearest vertex.
			Vector3D centered = cellIsometry.Apply( clickedPoint );
			double angle = Euclidean2D.AngleToCounterClock( centered, new Vector3D( 1, 0 ) );
			double angleFromZeroToP = p * angle / ( 2 * Math.PI );
			angleFromZeroToP = Math.Round( angleFromZeroToP, 0 );
			if( p == (int)angleFromZeroToP )
				angleFromZeroToP = 0;
			angle = 2 * Math.PI * angleFromZeroToP / p;

			// This will take vertex to canonical position.
			Mobius rotation = new Mobius();
			rotation.Isometry( g, angle, new Complex() );
			Isometry rotIsometry = new Isometry( rotation, null );

			return rotIsometry * cellIsometry;
		}

		/// <summary>
		/// Transforms us into a new macro based on a different click location.
		/// </summary>
		public Macro Transform( Cell clickedCell, Vector3D clickedPoint, Puzzle puzzle, bool mouseMotionReflected )
		{
			Macro m = this.CloneAllButTwists();
			m.SetupMobius( clickedCell, clickedPoint, puzzle, mouseMotionReflected );

			// Did we have an odd number of view reflections?
			bool viewReflected = this.ViewReflected ^ m.ViewReflected;

			Isometry iso1 = new Isometry( m.Mobius, null );
			Isometry iso2 = new Isometry( this.Mobius, null );
			if( viewReflected )
				iso1 = Isometry.ReflectX() * iso1;
			Isometry combined = iso1.Inverse() * iso2;

			foreach( SingleTwist t in this.m_twists )
			{
				// Find the transformed twist data.
				// NOTE: We choose the one which will be closest to the origin after transformation,
				//		 which hopefully won't lead to performance problems.  
				//		 I initially just used the first TwistDataForStateCalcs list item, 
				//		 but that led to issues because sometimes it would get transformed 
				//		 to very near the disk boundary. We'd have run out of cells to 
				//		 find the correct closest, and the transformed macros got all messed up.
				TwistData tdOriginal = t.IdentifiedTwistData.TwistDataForStateCalcs
					.OrderBy( td => combined.Apply( td.Center ).MagSquared() )
					.First();
				Vector3D newCenter = combined.Apply( tdOriginal.Center );
				TwistData tdNew = puzzle.ClosestTwistingCircles( newCenter );

				SingleTwist tClone = t.Clone();
				tClone.IdentifiedTwistData = tdNew.IdentifiedTwistData;

				// If the reverse state of our transformed twist
				// has changed, we may need to reverse the new twist.
				bool reverse = tdOriginal.Reverse ^ tdNew.Reverse;
				if( reverse ^ viewReflected )	// NOTE: Very similar to code in Renderer.
					tClone.ReverseTwist();

				m.m_twists.Add( tClone );
			}

			return m;
		}

		/// <summary>
		/// Whether or not we are recording.
		/// </summary>
		public bool Recording { get; set; }

		public void Reset()
		{
			m_twists.Clear();
			Recording = false;
		}

		public void StartRecording()
		{
			m_twists.Clear();
			Recording = true;
		}

		public void Update( SingleTwist twist )
		{
			if( !Recording )
				return;

			// Check for undos.
			if( m_twists.Count > 0 )
			{
				SingleTwist last = m_twists.Last();
				if( twist.IsUndo( last ) )
				{
					m_twists.RemoveAt( m_twists.Count - 1 );
					return;
				}
			}

			m_twists.Add( twist );
		}

		public void StopRecording()
		{
			Recording = false;
		}

		/// <summary>
		/// Clear any markings on our twists.
		/// This is to avoid issues when using macros while creating other macros.
		/// (I don't want to mess with saving/loading that recursion).
		/// </summary>
		public void ClearStartEndMarkings()
		{
			foreach( SingleTwist t in m_twists )
				t.MacroStart = t.MacroEnd = false;
		}

		public SingleTwist[] Twists
		{
			get 
			{
				// Make clones so we don't have to worry about our properties getting updated.
				List<SingleTwist> result = new List<SingleTwist>();
				foreach( SingleTwist twist in m_twists )
					result.Add( twist.Clone() );
				return result.ToArray(); 
			}
		}

		public SingleTwist[] ReverseTwists
		{
			get
			{
				List<SingleTwist> result = new List<SingleTwist>();
				for( int i = m_twists.Count - 1; i >= 0; i-- )
				{
					SingleTwist temp = m_twists[i].Clone();
					temp.ReverseTwist();
					result.Add( temp );
				}
				return result.ToArray();
			}
		}

		public XElement SaveToXml( XElement xParent )
		{
			xParent.Add( new XElement( "DisplayName", this.DisplayName ) );
			xParent.Add( XElement.Parse( DataContractHelper.SaveToString( this.Mobius ) ) );
			xParent.Add( new XElement( "ViewReflected", this.ViewReflected ) );
			return m_twists.SaveToXml( xParent );
		}

		public void LoadFromXml( XElement xElement, List<IdentifiedTwistData> allTwistData )
		{
			this.DisplayName = xElement.Element( "DisplayName" ).Value;
			XElement xIsometry = xElement.Element( "Mobius" );
			this.Mobius = (Mobius)DataContractHelper.LoadFromString( typeof( Mobius ), xIsometry.ToString() );
			XElement xViewReflected = xElement.Element( "ViewReflected" );
			if( xViewReflected != null )
				this.ViewReflected = bool.Parse( xViewReflected.Value );
			m_twists.LoadFromXml( xElement, allTwistData );
		}

		private TwistList m_twists = new TwistList();
	}

	public class MacroList
	{
		public MacroList()
		{
			Macros = new List<Macro>();
		}

		/// <summary>
		/// The puzzle these apply to (set when saving out a macro list).
		/// We're not going to allow applying macros across different puzzles, because much weird stuff could happen.
		/// - Definitely wouldn't work across VT vs. ET vs. FT puzzles, even for the same tiling.
		/// - Probably wouldn't even work on a classic {3,7} vs. IRP {3,7} because of different coloring pattern.
		/// </summary>
		public string PuzzleID { get; private set; }
		public string PuzzleName { get; private set; }

		/// <summary>
		/// The macros.
		/// </summary>
		public List<Macro> Macros { get; set; }

		public void Clear()
		{
			PuzzleID = string.Empty;
			Macros.Clear();
		}

		public XElement SaveToXml( XElement xParent, Puzzle puzzle )
		{
			this.PuzzleID = puzzle.Config.ID;
			this.PuzzleName = puzzle.Config.DisplayName;

			xParent.Add( new XElement( "PuzzleID", this.PuzzleID ) );
			xParent.Add( new XElement( "PuzzleName", this.PuzzleName ) );
			foreach( Macro m in Macros )
			{
				XElement xMacro = new XElement( "Macro" );
				xMacro = m.SaveToXml( xMacro );
				xParent.Add( xMacro );
			}
			
			return xParent;
		}

		public void LoadFromXml( XElement xElement, List<IdentifiedTwistData> allTwistData )
		{
			this.Clear();

			this.PuzzleID = xElement.Element( "PuzzleID" ).Value;
			this.PuzzleName = xElement.Element( "PuzzleName" ).Value;
			foreach( XElement xMacro in xElement.Elements( "Macro" ) )
			{
				Macro m = new Macro();
				m.LoadFromXml( xMacro, allTwistData );
				this.Macros.Add( m );
			}
		}

		private static string m_macroFilter = "MagicTile macro files (*.xml)|*.xml|All files (*.*)|*.*";

		public void SaveToXml( Puzzle puzzle )
		{
			string fileName = Loader.GetSaveFileName( m_macroFilter );
			if( string.IsNullOrEmpty( fileName ) )
				return;

			XElement xMacros = new XElement( "MagicTileMacros", new XAttribute( "Version", puzzle.Config.Version ) );
			SaveToXml( xMacros, puzzle );
			XDocument xDocument = new XDocument( xMacros );
			xDocument.Save( fileName );
		}

		public void LoadFromXml( Puzzle puzzle )
		{
			string fileName = Loader.GetLoadFileName( m_macroFilter );
			if( !File.Exists( fileName ) )
				return;

			XDocument xDocument = XDocument.Load( fileName );

			// Check that it is actually a macro file?
			// Nah, loading macros from a saved puzzle file would be cool to support as well.

			// NOTE: We intentionally check the version before checking the puzzle ID.
			//		 This is because for a given (logical) ET or VT puzzle, we changed both 
			//		 the puzzle ID and version from 2.0 -> 2.1.  The version message will 
			//		 make more sense to the user for these cases.
			XAttribute xVersion = xDocument.Root.Attribute( "Version" );
			string savedVersion = string.Empty;
			if( xVersion != null )
				savedVersion = xVersion.Value;
			if( puzzle.Config.EdgeOrVertexTwisting && 0 != savedVersion.CompareTo( puzzle.Config.Version ) )
			{
				string message = string.Format(
					"Sorry, this macro file is not compatible with this puzzle.  The macro file " +
					"was saved with version {0}, but the puzzle is version {1}", savedVersion, puzzle.Config.Version );
				MessageBox.Show( message, "Unsupported", MessageBoxButtons.OK, MessageBoxIcon.Information );
				return;
			}

			string puzzleId = xDocument.Root.Element( "PuzzleID" ).Value;
			if( 0 != puzzleId.CompareTo( puzzle.Config.ID ) )
			{
				string puzzleName = xDocument.Root.Element( "PuzzleName" ).Value;
				string message = string.Format( "Sorry, we only support loading macro files which apply to the active puzzle. " +
					"These macros were saved for the puzzle with display name '{0}' and having id '{1}'", puzzleName, puzzleId );
				MessageBox.Show( message, "Unsupported", MessageBoxButtons.OK, MessageBoxIcon.Information );
				return;
			}

			LoadFromXml( xDocument.Root, puzzle.AllTwistData );
		}
	}
}
