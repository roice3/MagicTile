namespace MagicTile.Utils
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;

	internal static class GAP
	{
		private static string m_filter = "GAP script (*.g)|*.g|All files (*.*)|*.*";

		class Cycle
		{
			public Cycle( int[] vals ) { Vals = vals; }

			public int[] Vals { get; set; }

			public override string ToString()
			{
				return '(' + string.Join( ",", Vals ) + ')';
			}
		}

		public static void SaveScript( Puzzle puzzle )
		{
			string filename = Loader.GetSaveFileName( m_filter );
			if( string.IsNullOrEmpty( filename ) )
				return;

			// Get the existing state, so we don't have to clobber the one in the puzzle.
			XElement xState = new XElement( "State" );
			puzzle.State.SaveToXml( xState );
			State state = new State( puzzle.State.NumCells, puzzle.State.NumStickers );
			state.LoadFromXml( xState );

			// Get all the cycles.
			HashSet<Sticker> completedStickers = new HashSet<Sticker>();
			HashSet<TwistData> completedTD = new HashSet<TwistData>();
			List<Cycle> cycles = new List<Cycle>();
			foreach( IdentifiedTwistData itd in puzzle.AllTwistData )
			{
				TwistData td = itd.TwistDataForStateCalcs.First();

				// We need to do this because we get repeats on or
				// near the boundary of the fundamental region of tiles.
				//if( completedTD.Contains( td ) )
				//	continue;
				completedTD.Add( td );

				for( int slice=0; slice<1; slice++ )
				{
					SingleTwist twist = new SingleTwist()
					{
						IdentifiedTwistData = itd,
						SliceMask = 1 << slice
					};

					state.Reset();
					var updated = Puzzle.UpdateState( puzzle.Config, state, twist );
					HashSet<Sticker> allStickers = new HashSet<Sticker>( updated.Keys.ToArray() );

					// We may have already done this one (say on spherical puzzles).
					//if( completedStickers.Contains( allStickers.First() ) )
					//	continue;
					foreach( Sticker s in allStickers )
						completedStickers.Add( s );

					int numCycles = updated.Count / td.Order;
					for( int c = 0; c < numCycles; c++ )
					{
						Sticker start = allStickers.First();
						allStickers.Remove( start );

						List<int> cycle = new List<int>();
						cycle.Add( GetStickerId( state, start ) );
						for( int t = 0; t < td.Order - 1; t++ )
						{
							Sticker next = updated[start];
							cycle.Add( GetStickerId( state, next ) );
							allStickers.Remove( next );
							start = next;
						}
						cycles.Add( new Cycle( cycle.ToArray() ) );
					}
				}
			}

			// Write everything out.
			using( StreamWriter sw = new StreamWriter( filename ) )
			{
				sw.WriteLine( "puzzle := Group(" );
				SaveCycleList( sw, cycles );
				sw.WriteLine( ");" );
				sw.WriteLine( "Print( Size( puzzle ) );" );
				sw.WriteLine( "Print( Collected( Factors( Size( puzzle ) ) ) );" );
				//sw.WriteLine( "Print( StructureDescription( puzzle ) );" );
			}
		}

		private static int GetStickerId( State state, Sticker s )
		{
			// The reason to add 1 is because GAP only likes positive integers for this.
			return 1 + s.CellIndex * state.NumStickers + s.StickerIndex;
		}

		private static void SaveCycleList( StreamWriter sw, List<Cycle> cycles )
		{
			string line = "";
			int count = 0;
			foreach( Cycle c in cycles )
			{
				string cycleString = c.ToString();
				line += cycleString;
				count++;

				if( count >= 5 )
				{
					if( c != cycles.Last() )
						line += ",";
					sw.WriteLine( line );
					line = "";
					count = 0;
				}
			}

			if( line != "" )
			{
				sw.WriteLine( line );
			}
		}
	}
}
