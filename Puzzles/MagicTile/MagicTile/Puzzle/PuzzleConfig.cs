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
	
	/// <summary>
	/// Solely here to pretty up persistence.
	/// </summary>
	[CollectionDataContract( Namespace = "", ItemName = "Identification" )]
	public class IdentificationList : List<Identification> { }

	[CollectionDataContract( Namespace = "", ItemName = "Distance" )]
	public class DistanceStringList : List<string> { }

	/// <summary>
	/// Holds the information we need to identify one cell with another.
	/// </summary>
	[DataContract( Namespace = "" )]
	public class Identification
	{
		public Identification()
		{
			InitialEdges = new List<int>();
			Edges = new List<int>();
			EndRotation = 0;
			UseMirroredEdgeSet = true;
		}

		public Identification( int[] edges, int rotation, bool useMirroredSet )
		{
			InitialEdges = new List<int>();
			Edges = edges.ToList();
			EndRotation = rotation;
			UseMirroredEdgeSet = useMirroredSet;
		}

		/// <summary>
		/// This is only here to pretty up the xml persistence.
		/// The InitialEdges variable below is what you want.  Move along.
		/// </summary>
		[DataMember]
		private string InitialEdgeSet
		{
			get
			{
				if( InitialEdges == null )
					return "";
				return Persistence.SaveArray<int>( InitialEdges.ToArray(), m_separator );
			}
			set
			{
				InitialEdges = Persistence.LoadArray<int>( value, m_separator ).ToList();
			}
		}

		/// <summary>
		/// This is only here to pretty up the xml persistence.
		/// The Edges variable below is what you want.  Move along.
		/// </summary>
		[DataMember]
		private string EdgeSet
		{
			get
			{
				return Persistence.SaveArray<int>( Edges.ToArray(), m_separator );
			}
			set
			{
				Edges = Persistence.LoadArray<int>( value, m_separator ).ToList();
			}
		}

		private const char m_separator = ':';

		/// <summary>
		/// The initial list of edges to reflect across.  If this is not filled out, it will default to all of them.
		/// </summary>
		public List<int> InitialEdges { get; set; }

		/// <summary>
		/// A list of subsequent edges to reflect across to go from one cell to another.
		/// </summary>
		public List<int> Edges { get; set; }

		/// <summary>
		/// If true, we will first do an in-place reflection from the cell to itself.
		/// Reflection will happen before reflecting across initial edges.
		/// Reflection will be such that the initial edge to be reflected across will remain in place.
		/// Most tilings won't use this, but it will allow us to make certain coloring patterns 
		/// that require an even number of reflections, but are otherwise impossible, e.g. a {4,4} orientable 9-color.
		/// It also allows a different config for the {8,3} 12-color.
		/// </summary>
		[DataMember]
		public bool InPlaceReflection { get; set; }

		/// <summary>
		/// CCW Symmetry rotation to be applied to polygon after edge reflections.
		/// The meaning depends on the polygon.
		/// (e.g. 1 means 1/3rd turn of a triangle, 1/7th turn of a heptagon)
		/// </summary>
		[DataMember]
		public int EndRotation { get; set; }

		/// <summary>
		/// If this is true, we'll use a mirrored list of edges as well.
		/// (Mirrored relative to the midpoint of the first segment.)
		/// </summary>
		[DataMember]
		public bool UseMirroredEdgeSet { get; set; }
	}

	/// <summary>
	/// Allows configuration of distances in terms of (2,p,q) triangle edges.
	/// This is nice for specifying distances in simple fractions of those edge lengths.
	/// NOTE: The distance represented is in the respective geometry (i.e. may not be Euclidean).
	/// </summary>
	[DataContract( Namespace = "" )]
	public class Distance
	{
		public Distance() { }
		public Distance( double p, double q, double r, double d )
		{
			P = p;
			Q = q;
			R = r;
			D = d;
		}

		/// <summary>
		/// A multiplier for the triangle edge opposite the PI/P angle.
		/// </summary>
		[DataMember]
		public double P { get; set; }

		/// <summary>
		/// A multiplier for the triangle edge opposite the PI/Q angle.
		/// </summary>
		[DataMember]
		public double Q { get; set; }

		/// <summary>
		/// A multiplier for the triangle edge opposite the PI/2 angle.
		/// </summary>
		[DataMember]
		public double R { get; set; }

		/// <summary>
		/// An straight up distance, if that is convenient.
		/// </summary>
		[DataMember]
		public double D { get; set; }

		/// <summary>
		/// Return the distance we represent for a particular {p,q} tiling.
		/// </summary>
		public double Dist( int p, int q )
		{
			return 
				this.P * Geometry2D.GetTrianglePSide( p, q ) +
				this.Q * Geometry2D.GetTriangleQSide( p, q ) +
				this.R * Geometry2D.GetTriangleHypotenuse( p, q ) + 
				this.D;
		}

		public string SaveToStringShort()
		{
			if( D == 0 )
				return string.Format( CultureInfo.InvariantCulture, "{0}:{1}:{2}", P, Q, R );
			else
				return SaveToString();
		}

		public string SaveToString()
		{
			return string.Format( CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}", P, Q, R, D );
		}

		public void LoadFromString( string saved )
		{
			string[] split = saved.Split( ':' );
			if( split.Length != 4 )
			{
				Debug.Assert( false );
				return;
			}

			P = double.Parse( split[0], CultureInfo.InvariantCulture );
			Q = double.Parse( split[1], CultureInfo.InvariantCulture );
			R = double.Parse( split[2], CultureInfo.InvariantCulture );
			D = double.Parse( split[3], CultureInfo.InvariantCulture );
		}
	}

	/// <summary>
	/// Class to hold information to configure slicing circles.
	/// </summary>
	[DataContract( Namespace = "" )]
	public class SlicingCircles
	{
		public SlicingCircles()
		{
			FaceCentered = new List<Distance>();
			EdgeCentered = new List<Distance>();
			VertexCentered = new List<Distance>();
			Thickness = 0.01;
		}

		/// <summary>
		/// The face-centered slicing circles.
		/// </summary>
		[DataMember( Name = "FaceCentered" )]
		private DistanceStringList FaceCenteredPersist 
		{
			get { return SaveList( FaceCentered );  }
			set 
			{
				FaceCentered = new List<Distance>(); 
				LoadList( FaceCentered, value ); 
			}
		}

		/// <summary>
		/// The edge-centered slicing circles.
		/// </summary>
		[DataMember( Name = "EdgeCentered" )]
		private DistanceStringList EdgeCenteredPersist
		{
			get { return SaveList( EdgeCentered ); }
			set 
			{
				EdgeCentered = new List<Distance>(); 
				LoadList( EdgeCentered, value ); 
			}
		}

		/// <summary>
		/// The vertex-centered slicing circles.
		/// </summary>
		[DataMember( Name = "VertexCentered" )]
		private DistanceStringList VertexCenteredPersist
		{
			get { return SaveList( VertexCentered ); }
			set 
			{ 
				VertexCentered = new List<Distance>(); 
				LoadList( VertexCentered, value ); 
			}
		}
		
		// ZZZ - Way to use generic IPersistable type in the Persistence class?
		private DistanceStringList SaveList( List<Distance> list )
		{
			if( list == null )
				return new DistanceStringList();
			DistanceStringList result = new DistanceStringList();
			foreach( Distance d in list )
				result.Add( d.SaveToString() );
			return result;
		}

		private void LoadList( List<Distance> list, DistanceStringList saved )
		{
			foreach( string s in saved )
			{
				Distance d = new Distance();
				d.LoadFromString( s );
				list.Add( d );
			}
		}

		/// <summary>
		/// The face-centered slicing circles.
		/// </summary>
		public List<Distance> FaceCentered { get; set; }

		/// <summary>
		/// The edge-centered slicing circles.
		/// </summary>
		public List<Distance> EdgeCentered { get; set; }

		/// <summary>
		/// The vertex-centered slicing circles.
		/// </summary>
		public List<Distance> VertexCentered { get; set; }

		/// <summary>
		/// The thickness of our slicing circles, in the respective geometry.
		/// I suppose this could be configured per slicing circle, but that seems excessive.
		/// </summary>
		[DataMember]
		public double Thickness { get; set; }

		public bool FaceTwisting
		{
			get
			{
				return
					this.FaceCentered != null &&
					this.FaceCentered.Count > 0;
			}
		}

		public bool EdgeTwisting
		{
			get
			{
				return
					this.EdgeCentered != null &&
					this.EdgeCentered.Count > 0;
			}
		}

		public bool VertexTwisting
		{
			get
			{
				return
					this.VertexCentered != null &&
					this.VertexCentered.Count > 0;
			}
		}

		public bool Sliced
		{
			get
			{
				return FaceTwisting || EdgeTwisting || VertexTwisting;
			}
		}
	}

	/// <summary>
	/// Configuration required for loading an IRP to associate with the puzzle.
	/// </summary>
	[DataContract( Namespace = "" )]
	public class IRPConfig
	{
		/// <summary>
		/// The file containing the IRP data.
		/// </summary>
		[DataMember]
		public string DataFile { get; set; }

		/// <summary>
		/// The index of the IRP tile which will be mapped to the 0th MagicTile tile.
		/// </summary>
		[DataMember]
		public int FirstTile { get; set; }

		/// <summary>
		/// Whether to reflect the first IRP tile (across a line from the center to the first vertex).
		/// </summary>
		[DataMember]
		public bool Reflect { get; set; }

		/// <summary>
		/// CCW rotation to apply to the first IRP tile. This will be applied after the reflection.
		/// </summary>
		[DataMember]
		public int Rotate { get; set; }
	}

	public enum Polytope
	{
		Duoprism,
		Runcinated5Cell,
		Bitruncated5Cell,
	}

	/// <summary>
	/// Configuration required for generating regular 4D skew polyedra data associate with the puzzle.
	/// </summary>
	[DataContract( Namespace = "" )]
	public class Skew4DConfig
	{
		/// <summary>
		/// The polytope associated with the regular skew polyhedron.
		/// </summary>
		[DataMember]
		public Polytope Polytope { get; set; }
	}

	/// <summary>
	/// Main puzzleConfig class.
	/// </summary>
	[DataContract( Namespace = "" )]
	public class PuzzleConfig
	{
		public PuzzleConfig()
		{
			SetupDefaultConfig();
		}

		private void SetupDefaultConfig()
		{
			this.ID = "Puzzle.{7,3}.Classic";	// Needs to be coordinated with {7,3} config.
			this.DisplayName = "{7,3} Classic";
			P = 7;
			Q = 3;
			Identifications = new IdentificationList
			{ 
				new Identification( new int[] {3,3,3}, 0, useMirroredSet: true )
			};
			ExpectedNumColors = 24;
			NumTiles = 5000;

			SlicingCircles = new SlicingCircles();

			bool earthquake = false;
			if( !earthquake )
				SlicingCircles.FaceCentered.Add( new Distance( 2.0 / 3, 0, 1.0, 0 ) );
			Earthquake = earthquake;
			TileShrink = 0.94;

			Version = Loader.VersionCurrent;
		}

		/// <summary>
		/// We don't save this out.  Setting it is handled by the loader.
		/// </summary>
		public string Version { get; set; }

		/// <summary>
		/// A unique identifier for this puzzle.
		/// </summary>
		[DataMember]
		public string ID { get; set; }

		/// <summary>
		/// To show in menus and title bar.
		/// </summary>
		[DataMember]
		public string DisplayName { get; set; }

		/// <summary>
		/// A subset of the display name we put in the menus and tree.
		/// </summary>
		public string MenuName { get; set; }

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
		/// Slicing circles.
		/// These may be face, edge, or vertex centered.
		/// (That or twist order needs to be specified (2,p,q))
		/// Only need to list for the single generating tile.
		[DataMember]
		public SlicingCircles SlicingCircles { get; set; }

		/// <summary>
		/// Whether or not we are an Earthquake puzzle (based on systolic pants decomposition).
		/// </summary>
		public bool Earthquake { get; set; }

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

		public bool EdgeOrVertexTwisting
		{
			get
			{
				return SlicingCircles.EdgeTwisting || SlicingCircles.VertexTwisting;
			}
		}

		/// <summary>
		/// NOTE: filePath is relative to the base puzzle directory.
		/// </summary>
		public static void Save( PuzzleConfig config, string filePath )
		{
			try
			{
				string fullPath = Path.GetFullPath( Path.Combine( StandardPaths.ConfigDir, filePath ) );
				Directory.CreateDirectory( Path.GetDirectoryName( fullPath ) );
				DataContractHelper.SaveToXml( config, fullPath );
			}
			catch( System.Exception e )
			{
				string message = string.Format( "Failed to save puzzle config.\n{0}", e.Message );
				Log.Error( message );
			}
		}

		/// <summary>
		/// NOTE: filePath is relative to the base puzzle directory.
		/// </summary>
		public static PuzzleConfig Load( string filePath )
		{
			try
			{
				string fullPath = Path.GetFullPath( Path.Combine( StandardPaths.ConfigDir, filePath ) );
				return (PuzzleConfig)DataContractHelper.LoadFromXml( typeof( PuzzleConfig ), fullPath );
			}
			catch( System.Exception e )
			{
				string message = string.Format( "Failed to load puzzle config.\n{0}", e.Message );
				Log.Error( message );

				return new PuzzleConfig();
			}
		}
	}
}
