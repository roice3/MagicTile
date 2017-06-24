namespace MagicTile
{
	using System.Collections.Generic;
	using System.Xml.Linq;

	public class TwistHistory
	{
		public TwistHistory()
		{
			m_undoMode = m_redoMode = false;

			m_twists = new TwistList();
			m_redoTwists = new TwistList();
			m_toggles = new List<Cell>();
		}

		public void Clear()
		{
			m_twists.Clear();
			m_redoTwists.Clear();
			m_toggles.Clear();
			this.Scrambles = 0;
		}

		public XElement SaveToXml( XElement xParent )
		{
			xParent.Add( new XElement( "Scrambles", this.Scrambles ) );
			return m_twists.SaveToXml( xParent );
		}

		public void LoadFromXml( XElement xElement, List<IdentifiedTwistData> AllTwistData )
		{
			Clear();

			XElement xScrambles = xElement.Element( "Scrambles" );
			if( xScrambles != null )
				this.Scrambles = System.Convert.ToInt32( xScrambles.Value );
			else
				this.Scrambles = 0;

			m_twists.LoadFromXml( xElement, AllTwistData );
		}

		/// <summary>
		/// Whether or not we've been scrambled.
		/// </summary>
		public bool Scrambled 
		{ 
			get 
			{ 
				return 0 != this.Scrambles; 
			}
			set
			{
				this.Scrambles = 0;
			}
		}

		/// <summary>
		/// Whether we are undoing.
		/// </summary>
		public bool Undoing { get { return m_undoMode; } }

		/// <summary>
		/// The number of scrambles that have been applied to us.
		/// </summary>
		public int Scrambles { get; set; }

		/// <summary>
		/// Adds a twist to our history.
		/// </summary>
		public void Update( SingleTwist twist )
		{
			if( m_undoMode )
			{
				// Remove from twist list.
				m_twists.RemoveAt( m_twists.Count-1 );
		
				// Save in our redo list.
				SingleTwist temp = twist.Clone();
				temp.ReverseTwist();
				m_redoTwists.Add( temp );
				m_undoMode = false;
				return;
			}

			if( m_redoMode )
			{
				m_redoTwists.RemoveAt( m_redoTwists.Count - 1 );
				m_redoMode = false;
			}
			else
			{
				m_redoTwists.Clear();
			}

			// This block should apply to normal twists and redo twists.
			m_twists.Add( twist );
		}

		public void Update(Cell toggleCell)
		{
			m_toggles.Add( toggleCell );
		}


		/// <summary>
		/// Get undo rotation parameters.
		/// Calling this will set us in the "undo" state for the next rotation.
		/// Returns false if there are no more twists to undo.
		/// </summary>
		public bool GetUndoTwist( out SingleTwist twist )
		{
			twist = null;

			if( 0 == m_twists.Count )
				return false;

			twist = m_twists[m_twists.Count - 1].Clone();
			twist.ReverseTwist();

			m_undoMode = true;
			return true;
		}

		/// <summary>
		/// Get redo rotation parameters.
		/// Calling this will set us in the "redo" state for the next rotation.
		/// Returns false if there are no more twists to redo.
		/// </summary>
		public bool GetRedoTwist( out SingleTwist twist )
		{
			twist = null;

			if( 0 == m_redoTwists.Count )
				return false;

			twist = m_redoTwists[m_redoTwists.Count - 1];

			m_redoMode = true;
			return true;
		}

		/// <summary>
		/// Access to the internal set of twists.
		/// </summary>
		public List<SingleTwist> AllTwists
		{
			get { return m_twists; }
		}

		public List<Cell> AllToggles => m_toggles;

		public int AllMovesCount => AllTwists.Count + AllToggles.Count;

		// Whether we are in undo/redo modes.
		private bool m_undoMode, m_redoMode;

		// Our twist history.
		private TwistList m_twists;
		private TwistList m_redoTwists;
		private List<Cell> m_toggles;
	}
}
