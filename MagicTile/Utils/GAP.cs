namespace MagicTile.Utils
{
	using R3.Geometry;
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

		class Generator
		{
			public List<Cycle> Cycles { get; set; }

			public override string ToString()
			{
				string result = "";
				foreach( Cycle c in Cycles )
				{
					string cycleString = c.ToString();
					result += cycleString;
				}
				return result;
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

			// Get all the generators.
			List<Generator> generators = new List<Generator>();
			foreach( IdentifiedTwistData itd in puzzle.AllTwistData )
			{
				TwistData td = itd.TwistDataForStateCalcs.First();

				// NOTE: We use NumSlicesNoOpp rather than NumSlices on purpose,
				// to avoid problems in the group definition for spherical puzzles.
				for( int slice=0; slice<td.NumSlicesNoOpp; slice++ )
				{
					SingleTwist twist = new SingleTwist()
					{
						IdentifiedTwistData = itd,
						SliceMask = 1 << slice
					};

					state.Reset();
					var updated = Puzzle.UpdateState( puzzle.Config, state, twist );
					HashSet<Sticker> allStickers = new HashSet<Sticker>( updated.Keys.ToArray() );

					List<Cycle> cycles = new List<Cycle>();
					HashSet<List<int>> completedCycles = new HashSet<List<int>>( new SkewPolyhedron.CycleEqualityComparer() );
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

						// On hyperbolic puzzles, we can get duplicates.
						if( !completedCycles.Add( cycle ) )
							continue;
						cycles.Add( new Cycle( cycle.ToArray() ) );
					}

					generators.Add( new Generator() { Cycles = cycles } );
				}
			}

			// Write everything out.
			using( StreamWriter sw = new StreamWriter( filename ) )
			{
				sw.WriteLine( "puzzle := Group(" );
				SaveCycleList( sw, generators );
				sw.WriteLine( ");" );
				sw.WriteLine( "size := Size( puzzle );" );
				sw.WriteLine( "Print( size );" );
				sw.WriteLine( "Print( \"\\n\" );" );
				sw.WriteLine( "Print( Collected( Factors( size ) ) );" );
				//sw.WriteLine( "Print( StructureDescription( puzzle ) );" );
			}
		}

		private static int GetStickerId( State state, Sticker s )
		{
			// The reason to add 1 is because GAP only likes positive integers for this.
			return 1 + s.CellIndex * state.NumStickers + s.StickerIndex;
		}

		private static void SaveCycleList( StreamWriter sw, List<Generator> generators )
		{
			for( int g = 0; g < generators.Count; g++ )
			{
				string line = generators[g].ToString();
				if( g != generators.Count - 1 )
					line += ",";
				sw.WriteLine( line );
			}
		}
	}
}
