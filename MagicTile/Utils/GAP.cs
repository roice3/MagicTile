namespace MagicTile.Utils
{
	using R3.Core;
	using R3.Geometry;
	using R3.Math;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;

	using Math = System.Math;

	internal static class GAP
	{
		private static string m_filter = "GAP script (*.g)|*.g|All files (*.*)|*.*";

		public class Cycle
		{
			public Cycle( int[] vals ) { Vals = vals; }

			public int[] Vals { get; set; }

			public override string ToString()
			{
				return '(' + string.Join( ",", Vals ) + ')';
			}
		}

		public class Generator
		{
			public Generator() { Cycles = new List<Cycle>(); }

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
			WriteFile( filename, generators );
		}

		private static void WriteFile( string filename, List<Generator> generators )
		{
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

		private static int GetStickerId( int numStickers, int cellIndex, int stickerIndex )
		{
			// The reason to add 1 is because GAP only likes positive integers for this.
			return 1 + cellIndex *  numStickers + stickerIndex;
		}

		private static int GetStickerId( State state, Sticker s )
		{
			return GetStickerId( state.NumStickers, s.CellIndex, s.StickerIndex );
		}

		private static void GetStickerInfo( int numStickers, int gapId, out int cellIndex, out int stickerIndex )
		{
			gapId -= 1;
			cellIndex = gapId / numStickers;
			stickerIndex = gapId - cellIndex * numStickers;
		}

		private static void GetStickerInfo( State state, int gapId, out int cellIndex, out int stickerIndex )
		{
			GetStickerInfo( state.NumStickers, gapId, out cellIndex, out stickerIndex );
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

		public static void SaveMC4D()
		{
			double stickerOffset = 1.0;
			double faceOffset = 2.0;

			List<Vector3D> stickers = new List<Vector3D>();
			for( int i = -1; i <= 1; i++ )
				for( int j = -1; j <= 1; j++ )
					for( int k = -1; k <= 1; k++ )
						stickers.Add( new Vector3D( stickerOffset * i, stickerOffset * j, stickerOffset * k ) );

			List<Vector3D> allStickers = new List<Vector3D>();
			{
				Matrix4D m = Matrix4D.MatrixToRotateinCoordinatePlane( Math.PI / 2, 0, 3 );
				allStickers.AddRange( stickers.Select( s => { s = m.RotateVector( s ); return s + new Vector3D( faceOffset, 0, 0, 0 ); } ) );
				allStickers.AddRange( stickers.Select( s => { s = m.RotateVector( s ); return s - new Vector3D( faceOffset, 0, 0, 0 ); } ) );
			}
			{
				Matrix4D m = Matrix4D.MatrixToRotateinCoordinatePlane( Math.PI / 2, 1, 3 );
				allStickers.AddRange( stickers.Select( s => { s = m.RotateVector( s ); return s + new Vector3D( 0, faceOffset, 0, 0 ); } ) );
				allStickers.AddRange( stickers.Select( s => { s = m.RotateVector( s ); return s - new Vector3D( 0, faceOffset, 0, 0 ); } ) );
			}
			{
				Matrix4D m = Matrix4D.MatrixToRotateinCoordinatePlane( Math.PI / 2, 2, 3 );
				allStickers.AddRange( stickers.Select( s => { s = m.RotateVector( s ); return s + new Vector3D( 0, 0, faceOffset, 0 ); } ) );
				allStickers.AddRange( stickers.Select( s => { s = m.RotateVector( s ); return s - new Vector3D( 0, 0, faceOffset, 0 ); } ) );
			}
			{
				allStickers.AddRange( stickers.Select( s => { return s + new Vector3D( 0, 0, 0, faceOffset ); } ) );
				allStickers.AddRange( stickers.Select( s => { return s - new Vector3D( 0, 0, 0, faceOffset ); } ) );
			}

			Dictionary<Vector3D, int> stickerMap = new Dictionary<Vector3D, int>();
			for( int i = 0; i < allStickers.Count; i++ )
				stickerMap[allStickers[i]] = i + 1;	// GAP requires positive integers.

			// 6 gens per axis.
			System.Func<int, int[], Generator[]> OneAxis = new System.Func<int, int[], Generator[]>( ( axis, axes ) =>
			{
				List<Generator> result = new List<Generator>();
				Matrix4D[] matrices = new Matrix4D[] {
					Matrix4D.MatrixToRotateinCoordinatePlane( Math.PI / 2, axes[0], axes[1] ),
					Matrix4D.MatrixToRotateinCoordinatePlane( Math.PI / 2, axes[0], axes[2] ),
					Matrix4D.MatrixToRotateinCoordinatePlane( Math.PI / 2, axes[1], axes[2] ) };

				for( int i = 0; i <= 1; i++ )
					foreach( Matrix4D m in matrices )
					{
						Dictionary<int, int> updated = new Dictionary<int, int>();
						int count = 0;
						foreach( Vector3D s in allStickers )
						{
							if( i == 0 && !Tolerance.LessThan( s[axis], 0 ) )
								continue;
							if( i == 1 && !Tolerance.GreaterThan( s[axis], 0 ) )
								continue;

							int from = stickerMap[s];
							Vector3D n = s;
							double offset = i == 0 ? -faceOffset : faceOffset;
							n[axis] -= faceOffset;
							n = m.RotateVector( n );
							n[axis] += faceOffset;
							int to = stickerMap[n];
							updated[from] = to;
							count++;
						}

						updated = updated.Where( kvp => kvp.Key != kvp.Value ).ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

						HashSet<int> updatedList = new HashSet<int>( updated.Keys );
						List<Cycle> cycles = new List<Cycle>();
						HashSet<List<int>> completedCycles = new HashSet<List<int>>( new SkewPolyhedron.CycleEqualityComparer() );
						int order = 4;
						int numCycles = updated.Count / order;
						for( int c = 0; c < numCycles; c++ )
						{
							int start = updatedList.First();
							updatedList.Remove( start );

							List<int> cycle = new List<int>();
							cycle.Add( start );
							for( int t = 0; t < order - 1; t++ )
							{
								int next = updated[start];
								cycle.Add( next );
								updatedList.Remove( next );
								start = next;
							}

							if( !completedCycles.Add( cycle ) )
								continue;
							cycles.Add( new Cycle( cycle.ToArray() ) );
						}

						result.Add( new Generator() { Cycles = cycles } );
					}

				return result.ToArray();
			} );

			// 24 generators.
			List<Generator> generators = new List<Generator>();
			generators.AddRange( OneAxis( 0, new int[] { 1, 2, 3 } ) );
			generators.AddRange( OneAxis( 1, new int[] { 0, 2, 3 } ) );
			generators.AddRange( OneAxis( 2, new int[] { 0, 1, 3 } ) );
			generators.AddRange( OneAxis( 3, new int[] { 0, 1, 2 } ) );

			WriteFile( "3x3x3x3.g", generators );
		}

		public static void LoadGenerators( Puzzle puzzle )
		{
			Dictionary<Sticker, int> newState = new Dictionary<Sticker, int>();

			using( StreamReader sr = new StreamReader( @"C:\Users\hrn\Documents\roice\GitHub\MagicTile\kq_superflip.txt" ) )
			{
				Generator[] gens = LoadGenerators( sr );
				foreach( Generator gen in gens )
					foreach( Cycle cycle in gen.Cycles )
					{
						for( int i = 0; i < cycle.Vals.Length; i++ )
						{
							int from = cycle.Vals[i];
							int to = i == cycle.Vals.Length - 1 ? cycle.Vals[0] : cycle.Vals[i + 1];

							int cellIndex, stickerIndex;
							GetStickerInfo( puzzle.State, to, out cellIndex, out stickerIndex );
							Sticker stick = new Sticker( cellIndex, stickerIndex, null );
							GetStickerInfo( puzzle.State, from, out cellIndex, out stickerIndex );

							newState[stick] = puzzle.State.GetStickerColorIndex( cellIndex, stickerIndex );
						}
					}
			}

			foreach( var kvp in newState )
				puzzle.State.SetStickerColorIndex( kvp.Key.CellIndex, kvp.Key.StickerIndex, kvp.Value );
			puzzle.State.CommitChanges();
		}

		/// <summary>
		/// Loads a set of generators from a file.
		/// Currently only works if nothing else is in the file.
		/// </summary>
		public static Generator[] LoadGenerators( StreamReader sr )
		{
			List<Generator> result = new List<Generator>();
			Generator currentGen = new Generator();
			while( !sr.EndOfStream )
			{
				string currentLine = sr.ReadLine();

				// Get rid of whitespace.
				currentLine = Regex.Replace( currentLine, @"\s+", "" );

				string[] splitPerms = currentLine.Split( new char[] { '(' } );
				foreach( string permutation in splitPerms )
				{
					if( string.IsNullOrEmpty( permutation ) )
						continue;

					List<int> currentPerm = new List<int>();
					string trimmed = permutation.TrimEnd( ')' );
					string[] split = trimmed.Split( new char[] { ',' } );
					foreach( string s in split )
					{
						currentPerm.Add( int.Parse( s ) );
					}
					currentGen.Cycles.Add( new Cycle( currentPerm.ToArray() ) );
				}
			}

			result.Add( currentGen );
			return result.ToArray();
		}
	}
}
