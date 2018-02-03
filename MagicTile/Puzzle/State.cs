namespace MagicTile
{
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Drawing;
	using System.Text;
	using System.Xml.Linq;

	public class State
	{
		public State( int nCells, int nStickers )
		{
			m_state = new List<List<int>>();
			m_copy = new List<List<int>>();
			m_originalState = new List<List<int>>();
			m_colors = new Dictionary<int, Color>();
			m_nCells = nCells;
			m_nStickers = nStickers;
			InitializeState();
		}

		public void Reset()
		{
			InitializeState();
		}

		public XElement SaveToXml( XElement xParent )
		{
			for( int c = 0; c < m_nCells; c++ )
			{
				string cell = SaveCell( c );
				xParent.Add( new XElement( "Cell", cell ) );
			}
			return xParent;
		}

		public void LoadFromXml( XElement xElement )
		{
			int c = 0;
			foreach( XElement xCell in xElement.Elements( "Cell" ) )
			{
				LoadCell( c, xCell.Value );
				c++;
			}
		}

		private string SaveCell( int c )
		{
			StringBuilder sb = new StringBuilder();
			{
				for( int s = 0; s < m_nStickers; s++ )
				{
					int colorInt = GetStickerColorIndex( c, s );
					if( colorInt < 16 )
						sb.Append( "0" );
					sb.Append( System.Convert.ToString( colorInt, 16 ) );
				}
			}
			return sb.ToString();
		}

		private void LoadCell( int c, string saved )
		{
			System.Diagnostics.Debug.Assert( 2*m_nStickers == saved.Length );
			for( int s=0; s<m_nStickers; s++ )
				SetStickerColorIndex( c, s, System.Convert.ToInt32( saved.Substring( 2 * s, 2 ), 16 ) );
			CommitChanges( c );
		}

		/// <summary>
		/// Get the color for a sticker.
		/// </summary>
		public Color GetStickerColor( int cell, int sticker )
		{
			Debug.Assert( cell < m_nCells && sticker < m_nStickers );
			return m_colors[GetStickerColorIndex( cell, sticker )];
		}

		/// <summary>
		/// Get the color index for a sticker.
		/// </summary>
		public int GetStickerColorIndex( int cell, int sticker )
		{
			Debug.Assert( cell < m_nCells && sticker < m_nStickers );
			return m_state[cell][sticker];
		}

		/// <summary>
		/// State Changes.
		/// </summary>

		public void SetStickerColorIndex( int cell, int sticker, int color )
		{
			m_copy[cell][sticker] = color;
		}

		public void ToggleStickerColorIndex(int cell, int sticker)
		{
			var beforeColor = m_copy[cell][sticker];
			if (beforeColor == m_offColorIndex)
			{
				m_copy[cell][sticker] = m_originalState[cell][sticker];
			}
			else
			{
				m_copy[cell][sticker] = m_offColorIndex;
			}
		}

		public bool IsAllOn
		{
			get
			{
				for (int c = 0; c < m_nCells; c++)
				{
					for (int s = 0; s < m_nStickers; s++)
					{
						if (m_state[c][s] != m_originalState[c][s])
							return false;
					}
				}

				return true;
			}
		}

		public void CommitChanges()
		{
			for( int c = 0; c < m_nCells; c++ )
			for( int s = 0; s < m_nStickers; s++ )
				m_state[c][s] = m_copy[c][s];
		}
		public void CommitChanges( int changedCell )
		{
			for( int s = 0; s < m_nStickers; s++ )
				m_state[changedCell][s] = m_copy[changedCell][s];
		}
		public void CommitChanges( int[] changedCells, int num )
		{
			for( int i = 0; i < num; i++ )
				CommitChanges( changedCells[i] );
		}

		/// <summary>
		/// Whether we are solved.
		/// </summary>
		public bool IsSolved
		{
			get
			{
				for( int c = 0; c < m_nCells; c++ )
				{
					int cellColor = GetStickerColorIndex( c, 0 );
					for( int s = 1; s < m_nStickers; s++ )
					{
						if( GetStickerColorIndex( c, s ) != cellColor )
							return false;
					}
				}

				return true;
			}
		}

		// Size access.
		public int NumCells { get { return m_nCells; } }
		private int m_nCells;

		public int NumStickers { get { return m_nStickers; } }
		private int m_nStickers;

		private void InitializeState()
		{
			m_state.Clear();
			m_copy.Clear();
			m_originalState.Clear();

			for( int c = 0; c < m_nCells; c++ )
			{
				m_state.Add( new List<int>() );
				m_copy.Add( new List<int>() );
				m_originalState.Add(new List<int>());

				for( int s = 0; s < m_nStickers; s++ )
				{
					m_state[c].Add( c );
					m_copy[c].Add( c );
					m_originalState[c].Add(c);
				}
			}
		}

		// This matrix will hold a representation of the puzzle state.
		// The left index cycles through cells.
		// The right index cycles through stickers on those cells.
		// The matrix integers represent the sticker colors.
		private List<List<int>> m_state;
		private List<List<int>> m_copy;
		private List<List<int>> m_originalState;

		/// <summary>
		/// Called to update our colors.
		/// </summary>
		public void UpdateColors( Settings settings )
		{
			m_colors[m_offColorIndex] = settings.ColorOff;
			int face = 0;
			m_colors[face++] = settings.Color1;
			m_colors[face++] = settings.Color2;
			m_colors[face++] = settings.Color3;
			m_colors[face++] = settings.Color4;
			m_colors[face++] = settings.Color5;
			m_colors[face++] = settings.Color6;
			m_colors[face++] = settings.Color7;
			m_colors[face++] = settings.Color8;
			m_colors[face++] = settings.Color9;
			m_colors[face++] = settings.Color10;
			m_colors[face++] = settings.Color11;
			m_colors[face++] = settings.Color12;
			m_colors[face++] = settings.Color13;
			m_colors[face++] = settings.Color14;
			m_colors[face++] = settings.Color15;
			m_colors[face++] = settings.Color16;
			m_colors[face++] = settings.Color17;
			m_colors[face++] = settings.Color18;
			m_colors[face++] = settings.Color19;
			m_colors[face++] = settings.Color20;
			m_colors[face++] = settings.Color21;
			m_colors[face++] = settings.Color22;
			m_colors[face++] = settings.Color23;
			m_colors[face++] = settings.Color24;
			m_colors[face++] = settings.Color25;
			m_colors[face++] = settings.Color26;
			m_colors[face++] = settings.Color27;
			m_colors[face++] = settings.Color28;
			m_colors[face++] = settings.Color29;
			m_colors[face++] = settings.Color30;
			m_colors[face++] = settings.Color31;
			m_colors[face++] = settings.Color32;
			m_colors[face++] = settings.Color33;
			m_colors[face++] = settings.Color34;
			m_colors[face++] = settings.Color35;
			m_colors[face++] = settings.Color36;
			m_colors[face++] = settings.Color37;
			m_colors[face++] = settings.Color38;
			m_colors[face++] = settings.Color39;
			m_colors[face++] = settings.Color40;
			m_colors[face++] = settings.Color41;
			m_colors[face++] = settings.Color42;
			m_colors[face++] = settings.Color43;
			m_colors[face++] = settings.Color44;
			m_colors[face++] = settings.Color45;
			m_colors[face++] = settings.Color46;
			m_colors[face++] = settings.Color47;
			m_colors[face++] = settings.Color48;
			m_colors[face++] = settings.Color49;
			m_colors[face++] = settings.Color50;
			m_colors[face++] = settings.Color51;
			m_colors[face++] = settings.Color52;
			m_colors[face++] = settings.Color53;
			m_colors[face++] = settings.Color54;
			m_colors[face++] = settings.Color55;
			m_colors[face++] = settings.Color56;
		}
		private Dictionary<int, Color> m_colors;
		private int m_offColorIndex = -1;
	}
}
