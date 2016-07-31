namespace MagicTile
{
	using MagicTile.Utils;
	using R3.Geometry;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;

	/// <summary>
	/// Holds puzzle specific configuration.
	/// </summary>
	[DataContract( Namespace = "" )]
	public class PuzzleSpecific
	{
		/// <summary>
		/// A unique identifier for this puzzle.  If empty, this will be auto-generated.
		/// NOTE: This should be left empty for all new puzzles.  The only reason I added
		///		  this in was for backward compatibility of macros, which may have saved out
		///		  some older puzzle IDs.
		/// </summary>
		[DataMember]
		public string ID { get; set; }

		/// <summary>
		/// The display name. If empty, this will be auto-generated.
		/// </summary>
		[DataMember]
		public string DisplayName { get; set; }

		/// <summary>
		/// Slicing circles.
		/// These may be face, edge, or vertex centered.
		/// (That or twist order needs to be specified (2,p,q))
		/// Only need to list for the single generating tile.
		[DataMember]
		public SlicingCircles SlicingCircles { get; set; }

		/// <summary>
		/// Build a unique identifier.
		/// </summary>
		public string AutoUniqueId()
		{
			// We only need to be unique up to slicing circles.
			// (The manually set BaseIDs in PuzzleConfigClass will take care of the rest.)
			// Invariant culture is important!
			string result = string.Format( CultureInfo.InvariantCulture, "T{0}", SlicingCircles.Thickness );
			result += CirclesToString();
			return result;
		}

		/// <summary>
		/// Automatically make things that look like "F1:1:0 E0:1:0 V0.6:2:2"
		/// This has most all the unique information required to identify a puzzle.
		/// We used to return the tiling config here too, but that felt too repetetive in the UI.
		/// </summary>
		public string AutoDisplayName()
		{
			string result = CirclesToString();
			result = result.TrimStart();
			return result;
		}

		private string CirclesToString()
		{
			string result = string.Empty;

			// Invariant culture is important!
			if( this.SlicingCircles.FaceTwisting )
				foreach( Distance d in this.SlicingCircles.FaceCentered )
					result += string.Format( CultureInfo.InvariantCulture, " F{0}", d.SaveToStringShort() );
			if( this.SlicingCircles.EdgeTwisting )
				foreach( Distance d in this.SlicingCircles.EdgeCentered )
					result += string.Format( CultureInfo.InvariantCulture, " E{0}", d.SaveToStringShort() );
			if( this.SlicingCircles.VertexTwisting )
				foreach( Distance d in this.SlicingCircles.VertexCentered )
					result += string.Format( CultureInfo.InvariantCulture, " V{0}", d.SaveToStringShort() );
			return result;
		}
	}

	[CollectionDataContract( Namespace = "", ItemName = "Puzzle" )]
	public class PuzzleSpecificList : List<PuzzleSpecific> { }

	/// <summary>
	/// Used to handled a class of puzzles associated with a tiling and coloring (one particular shape).
	/// This mainly allows us to collect all of the slicing configurations into one place, vs. having a separate file for every slicing config.
	/// NOTE: There is a lot of member variable repetition between this and PuzzleConfig.  I first made a base class to share
	///		  these properties, but that broke the DataContract loading, so I just repeated things here.
	/// </summary>
	[DataContract( Namespace = "" )]
	public class PuzzleConfigClass
	{
		/// <summary>
		/// Unique identifier for this class of puzzles.
		/// </summary>
		[DataMember]
		public string ClassID { get; set; }

		/// <summary>
		/// Display name for this class of puzzles.
		/// </summary>
		[DataMember]
		public string ClassDisplayName { get; set; }

		/// <summary>
		/// The number of sides in a polygonal face.
		/// </summary>
		[DataMember]
		public int P { get; set; }

		/// <summary>
		/// The number of polygons meeting at each vertex.
		/// </summary>
		[DataMember]
		public int Q { get; set; }

		/// <summary>
		/// Puzzle specific config.
		/// </summary>
		[DataMember( Name = "Specific" )]
		public PuzzleSpecificList PuzzleSpecificList { get; set; }

		/// <summary>
		/// Used to control the gap between tiles.
		/// It is different than in the other programs. (Here we apply this before slicing).
		/// It needs to be part of the puzzle config, because this setting changes the nature of a puzzle.
		/// A 1.0 factor means no shrink.
		/// </summary>
		[DataMember]
		public double TileShrink { get; set; }

		/// <summary>
		/// Tile identifications we need to go from master -> slave cells.
		/// </summary>
		[DataMember]
		public IdentificationList Identifications { get; set; }

		/// <summary>
		/// The number of colors we expect in the puzzle.
		/// This isn't strictly necessary since it is determined by the identifications, 
		/// but it allows us to do some quality checks in our puzzle building code.
		/// </summary>
		[DataMember]
		public int ExpectedNumColors { get; set; }

		/// <summary>
		/// If configured, the number of tiles to generate (some puzzles need more to look nice).
		/// </summary>
		[DataMember]
		public int NumTiles { get; set; }

		/// <summary>
		/// Config required to draw a puzzle on its rolled up surface.
		/// </summary>
		[DataMember]
		public SurfaceConfig SurfaceConfig { get; set; }

		/// <summary>
		/// An IRP to associate with this puzzle.
		/// </summary>
		[DataMember]
		public IRPConfig IRPConfig { get; set; }

		/// <summary>
		/// A regular 4D skew polyhedron to associate with this puzzle.
		/// </summary>
		[DataMember]
		public Skew4DConfig Skew4DConfig { get; set; }

		/// <summary>
		/// Convenient accessor for the induced geometry.
		/// </summary>
		public Geometry Geometry
		{
			get
			{
				return Geometry2D.GetGeometry( this.P, this.Q );
			}
		}

		/// <summary>
		/// All the puzzles we represent.
		/// </summary>
		public void GetPuzzles( out PuzzleConfig tiling, out PuzzleConfig[] face,
			 out PuzzleConfig[] edge, out PuzzleConfig[] vertex, out PuzzleConfig[] mixed )
		{
			tiling = NonSpecific();
			tiling.MenuName = "Tiling";
			tiling.DisplayName = this.ClassDisplayName + " " + tiling.MenuName;
			tiling.SlicingCircles = null;

			List<PuzzleConfig> puzzles = new List<PuzzleConfig>();
			foreach( PuzzleSpecific puzzleSpecific in this.PuzzleSpecificList )
			{
				PuzzleConfig config = NonSpecific();

				// Specific stuff.
				config.SlicingCircles = puzzleSpecific.SlicingCircles;

				// NOTE: I decided to not propagate old IDs for edge or vertex turning puzzles.
				//		 We won't be able to load preview version macros for these anyway, 
				//		 due to the build/persistence changes from 2.0 -> 2.1
				if( string.IsNullOrEmpty( puzzleSpecific.ID ) || config.EdgeOrVertexTwisting )
					config.ID = this.ClassID + " " + puzzleSpecific.AutoUniqueId();
				else
					config.ID = puzzleSpecific.ID;

				if( string.IsNullOrEmpty( puzzleSpecific.DisplayName ) )
					config.MenuName = puzzleSpecific.AutoDisplayName();
				else
					config.MenuName = puzzleSpecific.AutoDisplayName() + " (" + puzzleSpecific.DisplayName + ")";
				config.DisplayName = this.ClassDisplayName + " " + config.MenuName;

				puzzles.Add( config );
			}

			face = puzzles.Where( p => p.SlicingCircles.FaceTwisting && !p.SlicingCircles.EdgeTwisting && !p.SlicingCircles.VertexTwisting ).ToArray();
			edge = puzzles.Where( p => !p.SlicingCircles.FaceTwisting && p.SlicingCircles.EdgeTwisting && !p.SlicingCircles.VertexTwisting ).ToArray();
			vertex = puzzles.Where( p => !p.SlicingCircles.FaceTwisting && !p.SlicingCircles.EdgeTwisting && p.SlicingCircles.VertexTwisting ).ToArray();
			mixed = puzzles.Except( face ).Except( edge ).Except( vertex ).ToArray();
		}

		private PuzzleConfig NonSpecific()
		{
			PuzzleConfig config = new PuzzleConfig();

			// Non-specific stuff.
			config.P = this.P;
			config.Q = this.Q;
			config.TileShrink = this.TileShrink;
			config.Identifications = this.Identifications;
			config.ExpectedNumColors = this.ExpectedNumColors;
			config.NumTiles = this.NumTiles;
			config.SurfaceConfig = this.SurfaceConfig;
			config.IRPConfig = this.IRPConfig;
			config.Skew4DConfig = this.Skew4DConfig;

			return config;
		}

		/// <summary>
		/// NOTE: filePath is relative to the base puzzle directory.
		/// </summary>
		public static void Save( PuzzleConfigClass config, string filePath )
		{
			try
			{
				string fullPath = Path.GetFullPath( Path.Combine( StandardPaths.ConfigDir, filePath ) );
				Directory.CreateDirectory( Path.GetDirectoryName( fullPath ) );
				DataContractHelper.SaveToXml( config, fullPath );
			}
			catch( System.Exception e )
			{
				string message = string.Format( "Failed to save puzzle config class.\n{0}", e.Message );
				Log.Error( message );
			}
		}

		/// <summary>
		/// NOTE: filePath is relative to the base puzzle directory.
		/// </summary>
		public static PuzzleConfigClass Load( string filePath )
		{
			try
			{
				string fullPath = Path.GetFullPath( Path.Combine( StandardPaths.ConfigDir, filePath ) );
				return (PuzzleConfigClass)DataContractHelper.LoadFromXml( typeof( PuzzleConfigClass ), fullPath );
			}
			catch( System.Exception e )
			{
				string message = string.Format( "Failed to load puzzle config class.\n{0}", e.Message );
				Log.Error( message );

				return new PuzzleConfigClass();
			}
		}

		/// <summary>
		/// Loads all the puzzles (recursively) from the puzzles dir.
		/// </summary>
		public static void LoadAllPuzzles( out IEnumerable<PuzzleConfigClass> standard, out IEnumerable<PuzzleConfigClass> user )
		{
			List<PuzzleConfigClass> resultStandard = new List<PuzzleConfigClass>();
			List<PuzzleConfigClass> resultUser = new List<PuzzleConfigClass>();

			List<string> files = new List<string>();
			string[] userFiles = { };
			string existingCurrentDirectory = System.Environment.CurrentDirectory;
			try
			{
				// We reset the current directory, because we want the paths relative to the puzzle dir.
				System.Environment.CurrentDirectory = StandardPaths.ConfigDir;
				files.AddRange( Directory.GetFiles( "puzzles", "*.xml", SearchOption.AllDirectories ) );
				userFiles = Directory.GetFiles( "user", "*.xml", SearchOption.AllDirectories );
			}
			catch { }
			finally
			{
				System.Environment.CurrentDirectory = existingCurrentDirectory;
			}

			foreach( string file in files )
			{
				PuzzleConfigClass configClass = PuzzleConfigClass.Load( file );
				resultStandard.Add( configClass );
			}

			foreach( string file in userFiles )
			{
				PuzzleConfigClass configClass = PuzzleConfigClass.Load( file );
				resultUser.Add( configClass );
			}

			standard = resultStandard;
			user = resultUser;
		}
	}
}
