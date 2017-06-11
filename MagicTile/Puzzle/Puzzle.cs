namespace MagicTile
{
	using MagicTile.Utils;
	using R3.Core;
	using R3.Drawing;
	using R3.Geometry;
	using R3.Math;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Drawing;
	using System.Linq;
	using System.Numerics;
	using System.Threading.Tasks;

	public interface IStatusCallback
	{
		void Status( string message );
		bool Cancelled { get; }
	}

	public class Puzzle
	{
		public Puzzle()
		{
			m_masters = new List<Cell>();
			m_slaves = new Dictionary<Cell, List<Cell>>();
			IRPCells = new Cell[] {};
			m_stateCalcCells = new List<Cell>();
			m_stateCalcCellSet = new HashSet<Cell>();
			AllTwistData = new List<IdentifiedTwistData>();
			Config = new PuzzleConfig();
			TextureHelper = new TextureHelper();
			MacroList = new MacroList();
		}

		/// <summary>
		/// The puzzle configuration for this puzzle.
		/// </summary>
		public PuzzleConfig Config { get; set; }

		/// <summary>
		/// A description of the topology.
		/// </summary>
		public string Topology { get; set; }

		/// <summary>
		/// Access to the master cells for this puzzle.
		/// </summary>
		public List<Cell> MasterCells { get { return m_masters; } }
		private List<Cell> m_masters;

		/// <summary>
		/// Access to the slave cells for a master cell.
		/// </summary>
		public IEnumerable<Cell> SlaveCells( Cell master )
		{
			List<Cell> slaves;
			if( !m_slaves.TryGetValue( master, out slaves ) )
				return new Cell[] {};
			return slaves;
		}
		private Dictionary<Cell, List<Cell>> m_slaves;

		/// <summary>
		/// Access to all cells.
		/// </summary>
		public IEnumerable<Cell> AllCells
		{
			get
			{
				foreach( Cell master in this.MasterCells )
				{
					yield return master;
					foreach( Cell slave in this.SlaveCells( master ) )
						yield return slave;
				}
			}
		}

		/// <summary>
		/// Access to all the slave cells in the puzzle.
		/// </summary>
		public IEnumerable<Cell> AllSlaveCells
		{
			get
			{
				foreach( Cell master in this.MasterCells )
				foreach( Cell slave in this.SlaveCells( master ) )
					yield return slave;
			}
		}

		public bool IsStateCalcCell( Cell cell )
		{
			return m_stateCalcCellSet.Contains( cell );
		}

		/// <summary>
		/// Access to the cells needed to render on a rolled up surface.
		/// </summary>
		public Cell[] SurfaceRenderingCells { get; private set; }

		/// <summary>
		/// The cells for an associated IRP, if we have one.
		/// </summary>
		public Cell[] IRPCells { get; private set; }

		/// <summary>
		/// Access to IRP translations.
		/// </summary>
		public IEnumerable<Translation> IRPTranslations
		{
			get
			{
				if( m_translations == null )
					return new Translation[] { };
				return m_translations;
			}
		}
		private Translation[] m_translations;

		/// <summary>
		/// The main list of all twist data.
		/// </summary>
		public List<IdentifiedTwistData> AllTwistData { get; private set; }

		/// <summary>
		/// Access to the puzzle state.
		/// </summary>
		public State State { get; set; }

		/// <summary>
		/// Our twist history.
		/// </summary>
		public TwistHistory TwistHistory { get; set; }

		/// <summary>
		/// Macros for this puzzle.
		/// </summary>
		public MacroList MacroList { get; set; }

		/// <summary>
		/// Whether or not we have surface config.
		/// </summary>
		public bool HasSurfaceConfig
		{
			get
			{
				return
					Config.SurfaceConfig != null &&
					Config.SurfaceConfig.Configured;
			}
		}

		/// <summary>
		/// Whether or not we have an associated skew polyhedron.
		/// </summary>
		public bool HasSkew { get; set; }

		/// <summary>
		/// Whether or not we can only be shown as a skew polyhedron.
		/// </summary>
		public bool OnlySkew { get; set; }

		/// <summary>
		/// Clear out our internal caches.
		/// </summary>
		private void Clear()
		{
			m_masters.Clear();
			m_slaves.Clear();
			this.IRPCells = new Cell[] {};
			m_translations = null;
			m_stateCalcCells.Clear();
			AllTwistData.Clear();
			m_twistDataNearTree.Reset( NearTree.GtoM( this.Config.Geometry ) );
			m_cellNearTree.Reset( NearTree.GtoM( this.Config.Geometry ) );
			this.MacroList.Clear();
			this.Topology = string.Empty;
		}

		private static void StatusOrCancel( IStatusCallback callback, string message )
		{
			if( callback.Cancelled )
				throw new OperationCanceledException( "Puzzle building cancelled." );

			callback.Status( message );
		}

		public Tiling GenTiling( int num )
		{
			TilingConfig tilingConfig = new TilingConfig( this.Config.P, this.Config.Q, num );
			tilingConfig.Shrink = this.Config.TileShrink;
			Tiling tiling = new Tiling();
			tiling.Generate( tilingConfig );
			return tiling;
		}

		/// <summary>
		/// Build the puzzle.
		/// </summary>
		public void Build( IStatusCallback callback )
		{
			this.Clear();

			// Generate a tiling for use with this puzzle.
			StatusOrCancel( callback, "creating underlying tiling..." );
			Tiling tiling = GenTiling( this.Config.NumTiles );
			Tile template = tiling.Tiles.First();

			// This will track all the tiles we've turned into cells.
			Dictionary<Vector3D, Cell> completed = new Dictionary<Vector3D, Cell>();

			StatusOrCancel( callback, "precalculating identification isometries..." );
			PuzzleIdentifications identifications = PrecalcIdentificationIsometries( tiling, template );

			// Add in all of our cells.
			if( callback.Cancelled )
				return;
			callback.Status( "adding in cells..." );
			foreach( Tile t in MasterCandidates( tiling ) )
			{
				// Have we already done this tile?
				if( completed.ContainsKey( t.Center ) )
					continue;

				// Add it (this will add all the slaves too).
				AddMaster( t, tiling, identifications, completed );
			}

			StatusOrCancel( callback, "analyzing topology..." );
			TopologyAnalyzer topology = new TopologyAnalyzer( this, template );
			topology.Analyze();
			this.Topology = topology.ToString();

			StatusOrCancel( callback, "marking cells for state calcs..." );
			TwistData[] templateTwistDataArray = TemplateTwistData( template ).ToArray();
			MarkCellsForStateCalcs( tiling, completed, templateTwistDataArray, topology );

			// Slice up the template tile.
			StatusOrCancel( callback, "slicing up template tile..." );
			List<Polygon> tStickers = SliceUpTemplate( template, templateTwistDataArray );

			StatusOrCancel( callback, "adding in stickers..." );
			if( this.Config.Geometry == Geometry.Spherical )
			{
				// ZZZ - this is only here because we're still drawing spherical directly (without textures).
				foreach( Cell cell in completed.Values )
					AddStickersToCell( cell, tStickers );
			}
			else
			{
				foreach( Cell cell in m_stateCalcCells )
					AddStickersToCell( cell, tStickers );
			}

			StatusOrCancel( callback, "setting up texture coordinates..." );
			SetupTextureCoords();

			StatusOrCancel( callback, "preparing twisting..." );
			SetupTwistDataForFullPuzzle( tiling, topology, templateTwistDataArray );
			SetupCellNearTree( completed );

			StatusOrCancel( callback, "preparing surface data..." );
			PrepareSurfaceData( callback );

			StatusOrCancel( callback, "loading and preparing irp data..." );
			try 
			{ 
				LoadIRP( callback ); 
			}
			catch( System.Exception ex )
			{
 				StatusOrCancel( callback, ex.Message + "\n" + ex.StackTrace );
			}

			//TraceGraph();

			if (tStickers.Count == 1)
			{
				StatusOrCancel(callback, "populating neighbors...");
				PopulateNeighbors();
			}

			this.State = new State( this.MasterCells.Count, tStickers.Count );
			this.TwistHistory = new TwistHistory();

			int count = this.MasterCells.Count;
			foreach( Cell master in this.MasterCells )
				count += this.SlaveCells( master ).Count();

			Debug.Assert( tiling.Tiles.Count() == completed.Count() );
			callback.Status( "Number of colors:" + this.MasterCells.Count );
			callback.Status( "Number of tiles:" + tiling.Tiles.Count() );
			callback.Status( "Number of cells:" + count );
			callback.Status( "Number of stickers per cell:" + tStickers.Count );
		}

		private IEnumerable<Tile> MasterCandidates( Tiling tiling )
		{
			IEnumerable<Tile> masterCandidates = tiling.Tiles;

			// Hack to make the fundamental region look better for some Klein bottle puzzles.
			// (to match Mathologer video).
			if( Config.Geometry == Geometry.Euclidean )
			{
				if( Config.P == 4 && Config.Q == 4 && Config.ExpectedNumColors == 4 )
				{
					masterCandidates = tiling.Tiles.Where( ( t, index ) => index < 3 || index == 5 ).ToArray();
				}

				if( Config.P == 6 && Config.Q == 3 && Config.ExpectedNumColors == 9 )
				{
					masterCandidates = tiling.Tiles.Where( ( t, index ) => index < 8 || index == 15 ).ToArray();
				}
			}
	
			return masterCandidates;
		}

		private CircleNE SetupTemplateSlicingCircle( Tile template, Distance d, Mobius m )
		{
			CircleNE circle = new CircleNE();
			circle.Center = circle.CenterNE = template.Center;

			double radiusInGeometry = d.Dist( this.Config.P, this.Config.Q );
			switch( this.Config.Geometry )
			{
				case Geometry.Spherical:
					circle.Radius = Spherical2D.s2eNorm( radiusInGeometry );
					break;
				case Geometry.Euclidean:
					circle.Radius = radiusInGeometry;
					break;
				case Geometry.Hyperbolic:
					circle.Radius = DonHatch.h2eNorm( radiusInGeometry );
					break;

			}

			circle.Transform( m );
			return circle;
		}

		private CircleNE[] SetupTemplateSlicingCircles( Tile template, Vector3D center, IEnumerable<Distance> distances )
		{
			Mobius m = new Mobius();
			m.Isometry( this.Config.Geometry, 0, center );
			List<CircleNE> slicers = new List<CircleNE>();
			foreach( Distance d in distances )
				slicers.Add( SetupTemplateSlicingCircle( template, d, m ) );
			return slicers.ToArray();
		}

		private IEnumerable<TwistData> TemplateTwistData( Tile template )
		{
			List<TwistData> result = new List<TwistData>();
			if( this.Config.SlicingCircles == null )
				return result;

			SlicingCircles circles = this.Config.SlicingCircles;

			List<Distance> faceCentered = circles.FaceCentered;
			if( circles.FaceTwisting )
			{
				TwistData twistData = new TwistData();
				twistData.TwistType = ElementType.Face;
				twistData.Center = template.Center;
				twistData.Order = this.Config.P;
				twistData.Circles = SetupTemplateSlicingCircles( template, twistData.Center, faceCentered );				
				result.Add( twistData );
			}

			foreach( Segment s in template.Boundary.Segments )
			{
				List<Distance> edgeCentered = circles.EdgeCentered;
				if( circles.EdgeTwisting )
				{
					TwistData twistData = new TwistData();
					twistData.TwistType = ElementType.Edge;
					twistData.Center = s.Midpoint;
					twistData.Order = 2;
					twistData.Circles = SetupTemplateSlicingCircles( template, twistData.Center, edgeCentered );
					result.Add( twistData );
				}

				List<Distance> vertexCentered = circles.VertexCentered;
				if( circles.VertexTwisting )
				{
					TwistData twistData = new TwistData();
					twistData.TwistType = ElementType.Vertex;
					twistData.Center = s.P1;
					twistData.Order = this.Config.Q;
					twistData.Circles = SetupTemplateSlicingCircles( template, twistData.Center, vertexCentered );
					result.Add( twistData );
				}

				// Long way to go to make this general.
				if( this.Config.Earthquake )
				{
					TwistData twistData = new TwistData();
					twistData.TwistType = ElementType.Vertex;
					twistData.Center = s.P1;
					twistData.Order = 3;		// The only use here is in controlling twist speed.
					twistData.NumSlices = 3;	// We'll use the slices as a way to mark the 3 directions.

					Mobius m = new Mobius();
					m.Isometry( Geometry.Hyperbolic, Euclidean2D.AngleToCounterClock( new Vector3D( 1, 0 ), s.P1 ), new Vector3D() );

					twistData.Pants = new Pants();
					twistData.Pants.SetupHexagonForKQ();
					if( Pants.TemplateHex == null )
						Pants.TemplateHex = twistData.Pants.Hexagon.Clone();
					twistData.Pants.Transform( m );
					twistData.Circles = Pants.SystolesForKQ().Select( c => { c.Transform( m ); return c; } ).ToArray();

					result.Add( twistData );
				}
			}

			return result;
		}

		/// <summary>
		/// Returns all the circles we want to use to slice up a template cell.
		/// </summary>
		private IEnumerable<CircleNE> Slicers( Tile template, IEnumerable<TwistData> templateSlicers )
		{
			HashSet<CircleNE> complete = new HashSet<CircleNE>( new CircleNE_EqualityComparer() );
			List<Tile> templateTile = new List<Tile>();
			templateTile.Add( template );

			foreach( TwistData twistData in templateSlicers )
			foreach( CircleNE slicingCircle in twistData.Circles )
			{
				// Use all edge and vertex incident tiles.
				// ZZZ - It's easy to imagine needing to grab more than this in the future.
				foreach( Tile t in templateTile.Concat( template.EdgeIncidences.Concat( template.VertexIndicences ) ) )
				{
					CircleNE result = slicingCircle.Clone();
					result.Transform( t.Isometry );

					if( complete.Contains( result ) )
						continue;

					complete.Add( result );
					yield return result;
				}
			}
		}

		// ZZZ - move all this puzzle slicing code to a separate file.
		private List<Polygon> SliceUpTemplate( Tile template, TwistData[] templateTwistDataArray )
		{
			List<CircleNE> slicers = Slicers( template, templateTwistDataArray ).ToList();
			List<Polygon> slicees = new List<Polygon>(), sliced = null;
			slicees.Add( template.Drawn );
			SliceRecursive( slicees, slicers, ref sliced );
			
			// ZZZ - Hacky to special case this,
			//		 but I'm not sure how the general solution will go.
			if( this.Config.Earthquake )
			{
				slicers.Clear();
				for( int i=0; i<7; i++ )
				{
					Mobius m = new Mobius();
					m.Elliptic( Geometry.Hyperbolic, new Complex(), Math.PI * 2 * i / 7 );
					CircleNE c = new CircleNE() { P1 = new Vector3D(), P2 = new Vector3D( 1, 0 ), Radius = double.PositiveInfinity, CenterNE = new Vector3D( 0, 0.5 ) };
					c.Transform( m );
					slicers.Add( c );
				}

				List<Polygon> result = new List<Polygon>();
				result.Add( sliced[4] );
				slicees = sliced.Except( result ).ToList();
				List<Polygon> temp = new List<Polygon>();
				SliceRecursive( slicees, slicers, ref temp );
				result.AddRange( temp );
				return result;
			}

			// Some slicing is complicated enough that stickers have zero area.  Remove such stickers.
			sliced = sliced.Where( p => !Tolerance.Zero( p.SignedArea ) ).ToList();

			return sliced;
		}

		private void SliceRecursive( List<Polygon> slicees, List<CircleNE> slicers, ref List<Polygon> sliced )
		{
			// We're done if we've used up all the slicing circles.
			if( 0 == slicers.Count )
			{
				sliced = slicees;
				return;
			}

			// Use the next circle to slice it all up.
			int lastIndex = slicers.Count - 1;
			CircleNE slicer = slicers[lastIndex];
			List<Polygon> tempSliced = new List<Polygon>();
			foreach( Polygon slicee in slicees )
			{
				List<Polygon> tempSliced2;
				if( Config.Earthquake )
					Slicer.SlicePolygonWithHyperbolicGeodesic( slicee, slicer, this.Config.SlicingCircles.Thickness, out tempSliced2 );
				else
					Slicer.SlicePolygon( slicee, slicer, this.Config.Geometry, this.Config.SlicingCircles.Thickness, out tempSliced2 );
				tempSliced.AddRange( tempSliced2 );
			}

			// On to the next level...
			slicers.RemoveAt( lastIndex );
			SliceRecursive( tempSliced, slicers, ref sliced );
		}

		// ZZZ - clean up naming (confusing with classes in PuzzleConfig.cs).
		private class PuzzleIdentification
		{
			public bool UseMirrored { get; set; }	// ZZZ - move to list class below?
			public Isometry Unmirrored { get; set; }
			public Isometry Mirrored { get; set; }

			public IEnumerable<Isometry> Isometries
			{
				get
				{
					List<Isometry> useMe = new List<Isometry>();
					if( UseMirrored )
					{
						useMe.Add( Unmirrored );
						useMe.Add( Mirrored );
					}
					else
					{
						useMe.Add( Unmirrored );
					}
					return useMe;
				}
			}
		}
		private class PuzzleIdentifications : List<PuzzleIdentification> {}

		private PuzzleIdentifications CalcIsometriesFromRelations( Tiling tiling, Tile template )
		{
			PuzzleIdentifications result = new PuzzleIdentifications();

			// Fundamental triangle definition.
			Segment seg = template.Boundary.Segments[0];
			Vector3D
				p1 = new Vector3D(),
				p2 = seg.Midpoint,
				p3 = seg.P1;
			Vector3D[] source = new Vector3D[] { p1, p2, p3 };

			Circle[] mirrors = new Circle[]
			{
				new Circle( p1, p2 ),
				new Circle( p1, p3 ),
				seg.Circle
			};

			Mobius id = Mobius.Identity();
			MobiusEqualityComparer comparer = new MobiusEqualityComparer();

			// Apply the reflections and get the isometries.
			var relations = GroupPresentation.ReadRelations( Config.GroupRelations );
			HashSet<Mobius> relationTransforms = new HashSet<Mobius>( comparer );
			foreach( int[] reflections in relations )
			{
				Vector3D p1r = p1, p2r = p2, p3r = p3;
				foreach( int reflection in reflections )
				{
					ReflectInMirror( mirrors, reflection, ref p1r );
					ReflectInMirror( mirrors, reflection, ref p2r );
					ReflectInMirror( mirrors, reflection, ref p3r );
				}

				Mobius m = new Mobius();
				m.MapPoints( p1r, p2r, p3r, p1, p2, p3 );
				if( !comparer.Equals( m, id ) )
					relationTransforms.Add( m );
			}

			//
			// We need to add in conjugations of the relations as well
			//

			Func<Vector3D, int, int, int, Vector3D> rot = ( v, n, m1, m2 ) =>
			{
				for( int i = 0; i < n; i++ )
				{
					v = mirrors[m1].ReflectPoint( v );
					v = mirrors[m2].ReflectPoint( v );
				}
				return v;
			};

			int max = Config.P * Config.ExpectedNumColors * 2;
			while( relationTransforms.Count < max )
			{
				for( int p = 0; p < Config.P; p++ )
					AddConjugations( relationTransforms, source, v => rot( v, p, 0, 1 ), max );
				/*for( int q = 0; q < Config.Q; q++ )
					AddConjugations( relationTransforms, source, v => rot( v, q, 1, 2 ), max );
				for( int r = 0; r < 2; r++ )
					AddConjugations( relationTransforms, source, v => rot( v, r, 0, 2 ), max );
				for( int i = 0; i < 3; i++ )*/
					AddConjugations( relationTransforms, source, v => mirrors[2].ReflectPoint( v ), max );
			}

			foreach( Mobius m in relationTransforms )
				result.Add( SetupIdent( m ) );

			return result;
		}

		private void AddConjugations( HashSet<Mobius> relationTransforms, Vector3D[] source, Func<Vector3D,Vector3D> transform, int max )
		{
			HashSet<Mobius> conjugations = new HashSet<Mobius>( relationTransforms, new MobiusEqualityComparer() );

			Vector3D
				p1 = source[0],
				p2 = source[1],
				p3 = source[2];

			foreach( Mobius m in relationTransforms )
			{
				Vector3D[] points = new Vector3D[] {
					p1, p2, p3,
					m.Apply( p1 ), m.Apply( p2 ), m.Apply( p3 ) };

				for( int i = 0; i < points.Length; i++ )
					points[i] = transform( points[i] );

				Mobius m2 = new Mobius();
				m2.MapPoints( points[0], points[1], points[2], points[3], points[4], points[5] );
				conjugations.Add( m2 );

				if( conjugations.Count >= max )
					break;
			}

			relationTransforms.UnionWith( conjugations );
		}

		private PuzzleIdentification SetupIdent( Mobius m )
		{
			Isometry isometry = new Isometry( m, null );
			PuzzleIdentification ident = new PuzzleIdentification();
			ident.UseMirrored = false;
			ident.Unmirrored = isometry;
			return ident;
		}

		private void ReflectInMirror( Circle[] mirrors, int index, ref Vector3D point )
		{
			point = mirrors[index].ReflectPoint( point );
		}

		private bool UsingRelations
		{
			get { return !string.IsNullOrEmpty( Config.GroupRelations ); }
		}

		/// <summary>
		/// This is here for puzzle building performance.  We pre-calculate the array of isometries to apply once, 
		/// vs. reflecting everywhere (which is *much* more expensive).
		/// </summary>
		private PuzzleIdentifications PrecalcIdentificationIsometries( Tiling tiling, Tile template )
		{
			if( UsingRelations )
				return CalcIsometriesFromRelations( tiling, template );

			PuzzleIdentifications result = new PuzzleIdentifications();
			IdentificationList identificationList = Config.Identifications;
			if( identificationList == null || identificationList.Count == 0 )
				return result;

			foreach( Identification identification in identificationList )
			{
				List<int> initialEdges = identification.InitialEdges;
				if( initialEdges == null || initialEdges.Count == 0 )
				{
					initialEdges = new List<int>();
					for( int s = 0; s < template.Boundary.Segments.Count; s++ )
						initialEdges.Add( s );
				}

				// We'll have two edge sets, one for identifications, and one for their mirrors.
				// ZZZ - we don't always need to fill out the mirrors.
				List<List<int>> edgeSetList = new List<List<int>>();
				edgeSetList.Add( identification.Edges );
				{
					List<int> mirroredEdgeSet = new List<int>();
					foreach( int edge in identification.Edges )
					{
						int mirroredEdge = this.Config.P - edge;
						mirroredEdgeSet.Add( mirroredEdge );
					}
					edgeSetList.Add( mirroredEdgeSet );
				}

				foreach( int init in initialEdges )
				{
					int initialEdge = init;	// So we can change during mirroring below if necessary.

					// Now cycle through the edge sets.
					PuzzleIdentification ident = new PuzzleIdentification();
					ident.UseMirrored = identification.UseMirroredEdgeSet;
					for( int i=0; i<edgeSetList.Count; i++ )
					{
						List<int> edgeSet = edgeSetList[i];
						bool mirror = i == 1;
						if( mirror && 0 != initialEdge )
							initialEdge = this.Config.P - initialEdge;

						Polygon boundary = template.Boundary.Clone();
						Segment segment = boundary.Segments[initialEdge];

						if( identification.InPlaceReflection )
						{
							Segment reflect = Segment.Line( new Vector3D(), segment.Midpoint );
							boundary.Reflect( reflect );
						}

						boundary.Reflect( segment );

						// Do all the configured reflections.
						int sIndex = initialEdge;
						bool even = boundary.Orientation;
						foreach( int offsetIndex in edgeSet )
						{
							if( even )
								sIndex += offsetIndex;
							else
								sIndex -= offsetIndex;
							even = !even;

							if( sIndex < 0 )
								sIndex += boundary.Segments.Count;
							if( sIndex >= boundary.Segments.Count )
								sIndex -= boundary.Segments.Count;

							boundary.Reflect( boundary.Segments[sIndex] );
						}

						if( identification.EndRotation != 0 )
						{
							double angle = identification.EndRotation * 2 * System.Math.PI / this.Config.P;
							if( mirror )
								angle *= -1;
							Mobius rotate = new Mobius();
							if( IsSpherical && Infinity.IsInfinite( boundary.Center ) )	// Was required for hemi-puzzles.
								rotate.Elliptic( this.Config.Geometry, new Vector3D(), -angle );
							else
								rotate.Elliptic( this.Config.Geometry, boundary.Center, angle );
							boundary.Transform( rotate );
						}

						Isometry isometry = new Isometry();
						isometry.CalculateFromTwoPolygons( template, boundary, this.Config.Geometry );
						isometry = isometry.Inverse();

						if( mirror )
							ident.Mirrored = isometry;
						else
							ident.Unmirrored = isometry;
					}

					result.Add( ident );
				}
			}

			return result;
		}

		private void PopulateNeighbors()
		{
			foreach (var master in m_masters)
			{
				foreach (var neighbor in AllCells)
				{
					var hasCommonEdge = master.Boundary.EdgeMidpoints
						.Any(masterEdgeMidPoint => neighbor.Boundary.EdgeMidpoints
							.Any(cellEdgeMidPoint => masterEdgeMidPoint == cellEdgeMidPoint));
					if (hasCommonEdge)
					{
						master.Neighbors.Add(neighbor.MasterOrSelf);
						neighbor.MasterOrSelf.Neighbors.Add(master);
					}
				}
			}
		}

		private void AddMaster( Tile tile, Tiling tiling, PuzzleIdentifications identifications, Dictionary<Vector3D, Cell> completed )
		{
			Cell master = SetupCell( tiling.Tiles.First(), tile.Boundary, completed );
			master.IndexOfMaster = m_masters.Count;

			// Paranoia.
			if( 0 != this.Config.ExpectedNumColors &&
				master.IndexOfMaster >= this.Config.ExpectedNumColors )
			{
				//Debug.Assert( false );
				// It will already have an invalid index.
				master.IndexOfMaster = -1;
				return;
			}

			m_masters.Add( master );

			// This is to help with recentering on puzzles constructed via group relations.
			// We need to recurse deeper for some of them, but just for the slaves of the central tile.
			Tile template = tiling.Tiles.First();
			TilingPositions positions = null;
			if( master.IndexOfMaster == 0 && UsingRelations )
			{
				tiling = null;
				positions = new TilingPositions();
				positions.Build( new TilingConfig( Config.P, Config.Q, maxTiles: Config.NumTiles * 5 ) );
			}

			// Now add all the slaves for this master.
			List<Cell> parents = new List<Cell>();
			parents.Add( master );
			AddSlavesRecursive( master, parents, tiling, positions, template, identifications, completed );
		}

		private void AddSlavesRecursive( Cell master, List<Cell> parents, Tiling tiling, TilingPositions positions, Tile template,
			PuzzleIdentifications identifications, Dictionary<Vector3D, Cell> completed )
		{
			// Are we done?
			if( parents.Count == 0 || identifications.Count == 0 )
				return;

			// To track the cells we add at this level.
			List<Cell> added = new List<Cell>();

			foreach( Cell parent in parents )
			foreach( PuzzleIdentification identification in identifications )
			foreach( Isometry identIsometry in identification.Isometries )
			{
				Cell slave = ApplyOneIsometry( master, parent, identIsometry, tiling, positions, template, completed );
				if( slave != null )
					added.Add( slave );
			};

			AddSlavesRecursive( master, added, tiling, positions, template, identifications, completed );
		}

		private Cell ApplyOneIsometry( Cell master, Cell parent, Isometry identIsometry, Tiling tiling, TilingPositions positions, Tile template,
			Dictionary<Vector3D, Cell> completed )
		{
			// Conjugate to get the identification relative to this parent.
			// NOTE: When we don't conjugate, some cells near the boundary are missed being identified.
			//		 I got around that by configuring the number of colors in the puzzle, and never adding more than that expected amount.
			//		 That was maybe a good thing to do anyway.
			// But conjugating was causing me lots of headaches because, e.g. it was causing extraneous mirroring/rotations
			// in puzzles like the Klein bottle, which don't have symmetrical identifications.  So I took it out for now.
			// NOTE: Later I tried conjugating for spherical puzzles, but that just produced bad puzzles (copies would have different colors adjacent).
			//		 So I think this is right.
			//Isometry conjugated = parent.Isometry.Inverse() * identIsometry * parent.Isometry;
			Isometry conjugated = identIsometry;

			// We can use the conjugates when using relations, because those are regular maps.
			//if( UsingRelations )
			//	conjugated = parent.Isometry.Inverse() * identIsometry * parent.Isometry;

			Vector3D newCenter = parent.VertexCircle.CenterNE;
			newCenter = conjugated.ApplyInfiniteSafe( newCenter );

			// ZZZ - Hack for spherical.  Some centers were projecting to very large values rather than DNE.
			if( Infinity.IsInfinite( newCenter ) )
				newCenter = Infinity.InfinityVector2D;

			// In the tiling?
			Tile tile;
			if( tiling != null && !tiling.TilePositions.TryGetValue( newCenter, out tile ) )
			{
				return null;
			}
			if( positions != null && !positions.Positions.Contains( newCenter ) )
			{
				return null;
			}

			// Already done this one?
			if( completed.ContainsKey( newCenter ) )
				return null;

			// New! Add it.
			Polygon boundary = parent.Boundary.Clone();
			boundary.Transform( conjugated );
			Cell slave = SetupCell( template, boundary, completed );
			AddSlave( master, slave );
			return slave;
		}

		private void AddSlave( Cell master, Cell slave )
		{
			// Go ahead and set this.
			slave.Master = master;
			slave.IndexOfMaster = master.IndexOfMaster;

			List<Cell> slaves;
			if( !m_slaves.TryGetValue( master, out slaves ) )
			{
				slaves = new List<Cell>();
				m_slaves[master] = slaves;
			}

			slaves.Add( slave );
		}

		/// <summary>
		/// Does initial creation/setup of a cell.
		/// This will also mark this tile as completed.
		/// NOTE: The passed in boundary should be based on applied identifications (not based on a Tile in the tiling)!
		/// </summary>
		private Cell SetupCell( Tile home, Polygon boundary, Dictionary<Vector3D, Cell> completed )
		{
			Cell cell = new Cell( boundary, boundary.CircumCircle );
			
			// This has to be recalculated, because it may not be the same as the tiling isometries.
			//cell.Isometry = t.Isometry;
			cell.Isometry.CalculateFromTwoPolygons( home, boundary, this.Config.Geometry );
			completed[boundary.Center] = cell;
			return cell;
		}

		/// <summary>
		/// Adds all the stickers to a cell based on template cell stickers.
		/// NOTE: As a memory optimization, we only want to do this for cells 
		///		  involved in state calcs.
		/// </summary>
		private void AddStickersToCell( Cell cell, List<Polygon> tStickers )
		{
			Isometry inv = cell.Isometry.Inverse();
			for( int i=0; i<tStickers.Count; i++ )
			{
				Polygon transformed = tStickers[i].Clone();
				transformed.Transform( inv );
				Sticker sticker = new Sticker( cell.IndexOfMaster, i, transformed );
				cell.Stickers.Add( sticker );
			}
		}

		/// <summary>
		/// This will calculate texture coordinates and vertices for all our cells.
		/// </summary>
		private void SetupTextureCoords()
		{
			Cell firstMaster = MasterCells.First();
			Vector3D[] templateTextureCoords = TextureHelper.TextureCoords( firstMaster.Boundary, this.Config.Geometry );

			for( int i = 0; i < MasterCells.Count; i++ )
			{
				Cell master = MasterCells[i];
				Vector3D[] masterTextureCoords = Isometry.TransformVertices( templateTextureCoords, master.Isometry.Inverse() );

				// Actually pre-calculate final values? That would require some knowledge from PuzzleRenderer class though,
				// i.e. the scaled "factor" of the texture.
				master.TextureCoords = masterTextureCoords;	
				master.TextureVertices = masterTextureCoords;

				foreach( Cell slave in SlaveCells( master ) )
				{
					// NOTE: We don't set the slave TextureCoords, since we can just use the ones in the master directly.
					slave.TextureVertices = Isometry.TransformVertices( templateTextureCoords, slave.Isometry.Inverse() );
				}
			}

			TextureHelper.SetupElementIndices( firstMaster.Boundary );
		}

		/// <summary>
		/// A texture helper for this puzzle.
		/// </summary>
		internal TextureHelper TextureHelper { get; set; }

		private static TwistData TransformedTwistDataForCell( Cell cell, TwistData untransformed, bool reverse )
		{
			Isometry isometry = cell.Isometry.Inverse();
			Vector3D newCenter = isometry.Apply( untransformed.Center );

			TwistData transformedTwistData = new TwistData();
			transformedTwistData.TwistType = untransformed.TwistType;
			transformedTwistData.Center = newCenter;	// NOTE: Can't use InfinitySafe method here! We need this center to stay accurate, for future transformations.
			transformedTwistData.Order = untransformed.Order;
			transformedTwistData.Reverse = reverse;
			transformedTwistData.NumSlices = untransformed.NumSlices;
			if( untransformed.Pants != null )
			{
				transformedTwistData.Pants = untransformed.Pants.Clone();
				transformedTwistData.Pants.Transform( isometry );
			}
			List<CircleNE> transformedCircles = new List<CircleNE>();
			foreach( CircleNE circleNE in untransformed.Circles )
			{
				CircleNE copy = circleNE.Clone();
				copy.Transform( isometry );
				transformedCircles.Add( copy );
			}
			transformedTwistData.Circles = transformedCircles.ToArray();

			return transformedTwistData;
		}

		/// <summary>
		/// A version dependent helper.  This is the current, good way to setup twist data.
		/// </summary>
		private void SetupTwistDataForCell_VersionCurrent( TopologyAnalyzer topology, Cell cell, TwistData templateTwistData, bool reverse,
			Dictionary<Vector3D, TwistData> twistDataMap, List<IdentifiedTwistData> collections )
		{
			TwistData transformedTwistData = TransformedTwistDataForCell( cell, templateTwistData, reverse );

			// Already have this one?
			Vector3D centerSafe = InfinitySafe( transformedTwistData.Center );
			if( twistDataMap.ContainsKey( centerSafe ) )
				return;

			int index = topology.GetLogicalElementIndex( templateTwistData.TwistType, centerSafe );
			if( -1 == index )
			{
				Debug.Assert( false );
				return;
			}

			IdentifiedTwistData collection = collections[index];
			collection.TwistDataForDrawing.Add( transformedTwistData );
			if( m_stateCalcCells.Contains( cell ) )
				collection.TwistDataForStateCalcs.Add( transformedTwistData );

			transformedTwistData.IdentifiedTwistData = collection;
			twistDataMap[centerSafe] = transformedTwistData;		
		}

		/// <summary>
		/// A version dependent helper.  This is the current, good way to setup twist data.
		/// </summary>
		private void SetupTwistDataForFullPuzzle_VersionCurrent( Tiling tiling, TopologyAnalyzer topology, 
			TwistData[] templateTwistDataArray, Dictionary<Vector3D, TwistData> twistDataMap  )
		{
			// Create collections.
			// We'll create them for all elements, then remove unused ones at the end.
			List<IdentifiedTwistData> collections = new List<IdentifiedTwistData>();
			int count = topology.F + topology.E + topology.V;
			for( int i=0; i<count; i++ )
				collections.Add( new IdentifiedTwistData() );

			foreach( Cell master in this.MasterCells )
			foreach( TwistData twistData in templateTwistDataArray )
			{
				bool reverse = false;
				SetupTwistDataForCell_VersionCurrent( topology, master, twistData, reverse, twistDataMap, collections );
				foreach( Cell slave in SlaveCells( master ) )
				{
					reverse = master.Reflected ^ slave.Reflected;
					SetupTwistDataForCell_VersionCurrent( topology, slave, twistData, reverse, twistDataMap, collections );
				}
			}

			// Remove empty ones, assign indices, and save to puzzle.
			collections = collections.Where( i => i.TwistDataForDrawing.Count > 0 ).ToList();
			for( int i=0; i<collections.Count; i++ )
			{
				collections[i].Index = i;
				this.AllTwistData.Add( collections[i] );
			}
		}

		/// <summary>
		/// Here for backward compatibility.
		/// </summary>
		private void SetupTwistDataForCell_PreviewVersion( Cell cell, TwistData templateTwistData, bool reverse,
			Dictionary<Vector3D, TwistData> twistDataMap, IdentifiedTwistData collection )
		{
			TwistData transformedTwistData = TransformedTwistDataForCell( cell, templateTwistData, reverse );
			collection.TwistDataForDrawing.Add( transformedTwistData );
			if( m_stateCalcCells.Contains( cell ) )
				collection.TwistDataForStateCalcs.Add( transformedTwistData );

			transformedTwistData.IdentifiedTwistData = collection;
			twistDataMap[InfinitySafe( transformedTwistData.Center )] = transformedTwistData;
		}

		/// <summary>
		/// Here for backward compatibility.
		/// </summary>
		private void SetupTwistDataForFullPuzzle_PreviewVersion( Tiling tiling, TwistData[] templateTwistDataArray,
			Dictionary<Vector3D, TwistData> twistDataMap )
		{
			// Map to help track what is done.
			Dictionary<Vector3D, bool> usedMasterLocation = new Dictionary<Vector3D, bool>();

			foreach( Cell master in this.MasterCells )
			foreach( TwistData twistData in templateTwistDataArray )
			{
				// Avoid duplicate vertex/edge circles in our list.
				Vector3D centerNE = twistData.Center;
				centerNE = master.Isometry.Inverse().Apply( centerNE );
				if( usedMasterLocation.ContainsKey( centerNE ) )
					continue;
				usedMasterLocation[centerNE] = true;

				IdentifiedTwistData collection = new IdentifiedTwistData();
				collection.Index = this.AllTwistData.Count;
				this.AllTwistData.Add( collection );

				bool reverse = false;
				SetupTwistDataForCell_PreviewVersion( master, twistData, false, twistDataMap, collection );
				foreach( Cell slave in SlaveCells( master ) )
				{
					reverse = master.Reflected ^ slave.Reflected;
					SetupTwistDataForCell_PreviewVersion( slave, twistData, reverse, twistDataMap, collection );
				}
			}
		}

		/// <summary>
		/// Sets up the slicing circles and twisting on the full puzzle.
		/// </summary>
		private void SetupTwistDataForFullPuzzle( Tiling tiling, TopologyAnalyzer topology, TwistData[] templateTwistDataArray )
		{
			// In case this has been built before.
			m_twistDataNearTree.Reset( NearTree.GtoM( this.Config.Geometry ) );

			// This is a performance optimization, to avoid doing calculation that results in loops below.
			bool isSpherical = this.IsSpherical;

			// Map to help track what is done.
			Dictionary<Vector3D, TwistData> twistDataMap = new Dictionary<Vector3D, TwistData>();
			if( Config.Version == Loader.VersionPreview )
				SetupTwistDataForFullPuzzle_PreviewVersion( tiling, templateTwistDataArray, twistDataMap );
			else if( Config.Version == Loader.VersionCurrent )
				SetupTwistDataForFullPuzzle_VersionCurrent( tiling, topology, templateTwistDataArray, twistDataMap );

			// Add opposite twisting circles for spherical puzzles.
			AddOppTwisters( twistDataMap );

			// Mark affected master cells and stickers.
			// For spherical, we need to do this for all twist data (because we draw everything instead of using textures).
			// For euclidean/hyperbolic, we only need to do this for the subset of twist data which will be used for state calcs.
			foreach( IdentifiedTwistData collection in this.AllTwistData )
			{
				List<TwistData> collectionTwistData = isSpherical ? 
					collection.TwistDataForDrawing : 
					collection.TwistDataForStateCalcs;
				Parallel.For( 0, collectionTwistData.Count, i =>
				{
					TwistData twistData = collectionTwistData[i];

					// Mark all the affected master cells.
					foreach( Cell master in MasterCells )
						twistData.WillAffectMaster( master, isSpherical );

					// Mark all the affected stickers.
					// Again, for spherical, we have to do more than just the stateCalcCells.
					IEnumerable<Cell> cells = isSpherical ? this.AllCells : m_stateCalcCells;
					foreach( Cell cell in cells )
					foreach( Sticker sticker in cell.Stickers )
						twistData.WillAffectSticker( sticker, isSpherical );
				} );
			}

			// Optimization.
			// We only need to keep around twist data for state calcs that touches masters.  
			foreach( IdentifiedTwistData collection in this.AllTwistData )
			{
				collection.TwistDataForStateCalcs = 
					collection.TwistDataForStateCalcs.Where( 
						td => td.AffectedMasterCells != null &&
							  td.AffectedMasterCells.Count > 0 ).ToList();
			}

			// Now build the near tree.
			foreach( KeyValuePair<Vector3D, TwistData> keyValuePair in twistDataMap )
			{
				// NearTree doesn't like NaN or +Inf
				Vector3D center = InfinitySafe( keyValuePair.Key );

				NearTreeObject nearTreeObject = new NearTreeObject();
				nearTreeObject.ID = keyValuePair.Value;
				nearTreeObject.Location = center;
				m_twistDataNearTree.InsertObject( nearTreeObject );
			}
		}

		/// <summary>
		/// For spherical puzzles, this will append antipodal twist data (if it exists).
		/// </summary>
		private void AddOppTwisters( Dictionary<Vector3D, TwistData> twistDataMap )
		{
			foreach( TwistData td in twistDataMap.Values )
			{
				td.NumSlicesNoOpp = td.Circles.Length;
				if( !IsSpherical )
					continue;

				CircleNE firstCircle = td.Circles.First();
				Vector3D antipode = firstCircle.ReflectPoint( td.Center );
				antipode = InfinitySafe( antipode );

				TwistData anti;
				if( !twistDataMap.TryGetValue( antipode, out anti ) )
				{
					// For spherical puzzles, we need to set things to have one more layer.
					// (to support the slice beyond last circle).
					td.NumSlices = td.Circles.Length + 1;
					continue;
				}

				// Puzzles with non-regular colorings can be really weird with slicing,
				// e.g. the {3,5} 8C.  We just won't allow slicing if the antipodal-twist has any
				// identified twists that are not it or us (This will still allow hemi-puzzles to have slices).
				//
				// Another weirdness encountered with the {3,4} 4CA...  The identified antipodal twist had the
				// same twisting orientation, which makes the orientation of a slice-2 twist undefined.  
				// We need to avoid that situation too.
				bool allowed = true;
				foreach( TwistData identified in anti.IdentifiedTwistData.TwistDataForDrawing )
				{
					// It
					if( identified == anti )
						continue;

					// Us
					if( InfinitySafe( identified.Center ) == InfinitySafe( td.Center ) )
					{
						// As noted above, we must also have opposite orientation to be allowed.
						if( anti.Reverse ^ td.Reverse )
							continue;
					}

					allowed = false;
					break;
				}
				if( !allowed )
					continue;

				// Add opposite twist data.
				List<CircleNE> list = td.Circles.ToList();
				foreach( CircleNE opp in anti.Circles )
				{
					CircleNE clone = opp.Clone();
					clone.CenterNE = firstCircle.CenterNE;
					list.Add( clone );
				}

				// ZZZ - move this block to a function and call for all twist data (for safety).
				{
					// Sort
					// We transform all the circles to the origin, then sort by their radius.
					Mobius toOrigin = new Mobius();
					toOrigin.Isometry( Geometry.Spherical, 0, -firstCircle.CenterNE );
					list = list.OrderBy( c =>
						{
							CircleNE clone = c.Clone();
							clone.Transform( toOrigin );
							return clone.Radius;
						} ).ToList();

					// Remove dupes.
					// The equality comparer wasn't working for lines if I didn't normalize them first.
					// I don't really want to do that inside the comparer, since it edits the objects.
					foreach( CircleNE c in list )
						if( c.IsLine )
							c.NormalizeLine();
					td.Circles = list.Distinct( new CircleNE_EqualityComparer() ).ToArray();
				}

				// For spherical puzzles, we need to set things to have one more layer.
				// (to support the slice beyond last circle).
				td.NumSlices = td.Circles.Length + 1;
			}
		}

		private void SetupCellNearTree( Dictionary<Vector3D, Cell> cellMap )
		{
			m_cellNearTree.Reset( NearTree.GtoM( this.Config.Geometry ) );

			/* Interestingly, building this way led to infinite recursion during lookups (when not running through the debugger).
			 * Maybe the neartree likes the vectors added in a particular order?
			 * Maybe the points of tiny cells get too close together, so putting in a dictionary first weeds some out?
			 * I don't know.
			 * 
			foreach( Cell master in this.MasterCells )
			{
				NearTreeObject nearTreeObject = new NearTreeObject();
				nearTreeObject.ID = master;
				nearTreeObject.Location = master.Center;
				m_cellNearTree.InsertObject( nearTreeObject );

				foreach( Cell slave in this.SlaveCells( master ) )
				{
					nearTreeObject.ID = slave;
					nearTreeObject.Location = slave.Center;
					m_cellNearTree.InsertObject( nearTreeObject );
				}
			}*/

			// Now build the near tree.
			foreach( KeyValuePair<Vector3D, Cell> keyValuePair in cellMap )
			{
				// Ignore the orphaned cells in cellMap. They are not reachable from the masters list
				if(keyValuePair.Value.IndexOfMaster == -1) continue;
				// NearTree doesn't like NaN or +Inf
				Vector3D center = InfinitySafe( keyValuePair.Key );

				NearTreeObject nearTreeObject = new NearTreeObject();
				nearTreeObject.ID = keyValuePair.Value;
				nearTreeObject.Location = center;
				m_cellNearTree.InsertObject( nearTreeObject );
			}
		}

		/// <summary>
		/// Find the closest cocentric slicing circles to a location.
		/// NOTE: The location should not be transformed by any mouse movements.
		/// </summary>
		internal TwistData ClosestTwistingCircles( Vector3D location )
		{
			NearTreeObject nearTreeObject;
			bool found = m_twistDataNearTree.FindNearestNeighbor( out nearTreeObject, location, double.MaxValue );
			if( !found )
				return null;

			TwistData result = (TwistData)nearTreeObject.ID;
			return result;
		}

		/// <summary>
		/// Find the closest cell to a location.
		/// NOTE: The location should not be transformed by any mouse movements.
		/// </summary>
		internal Cell ClosestCell( Vector3D location )
		{
			NearTreeObject nearTreeObject;
			bool found = m_cellNearTree.FindNearestNeighbor( out nearTreeObject, location, double.MaxValue );
			if( !found )
				return null;

			Cell result = (Cell)nearTreeObject.ID;
			return result;
		}

		private NearTree m_twistDataNearTree = new NearTree();
		private NearTree m_cellNearTree = new NearTree();

		/// <summary>
		/// Marks all the cells we'll need to use for state calcs.
		/// </summary>
		private void MarkCellsForStateCalcs( Tiling tiling, Dictionary<Vector3D, Cell> cells, 
			TwistData[] templateTwistDataArray, TopologyAnalyzer topology )
		{
			m_stateCalcCells.Clear();
			m_stateCalcCellSet.Clear();
			List<Cell> result = new List<Cell>();
			result.AddRange( this.MasterCells );

			// Special handlinng for earthquake.
			// ZZZ - I wonder if this should be the approach in the normal case too (cycling through 
			//		 masters first and slaves second), but fear changing the existing behavior.
			//		 After all, any slave twist will also result in some master twist.
			//		 This might speed up code below, and get rid of the complexity of the "hotTwists" code.
			HashSet<Vector3D> complete = new HashSet<Vector3D>();
			if( this.Config.Earthquake )
			{
				// Get all twist data attached to master cells.
				List<TwistData> toCheck = new List<TwistData>();
				foreach( TwistData twistData in templateTwistDataArray )
				foreach( Cell master in this.MasterCells )
				{
					bool dummy = false;
					TwistData transformed = TransformedTwistDataForCell( master, twistData, dummy );
					if( complete.Contains( transformed.Center ) )
						continue;
					complete.Add( transformed.Center );
					toCheck.Add( transformed );
				}

				Cell[] allSlaves = AllSlaveCells.ToArray();
				Parallel.For( 0, allSlaves.Length, i =>
				{
					Cell slave = allSlaves[i];
					foreach( TwistData td in toCheck )
					{
						if( td.WillAffectCell( slave, this.IsSpherical ) )
						{
							result.Add( slave );
							break;
						}
					}
				} );

				m_stateCalcCells = result.Distinct().ToList();
				m_stateCalcCellSet = new HashSet<Cell>( m_stateCalcCells );
				return;
			}

			List<TwistData> hotTwists = new List<TwistData>();
			foreach( TwistData twistData in templateTwistDataArray )
			foreach( Cell slave in this.AllSlaveCells )
			{
				bool dummy = false;
				TwistData transformed = TransformedTwistDataForCell( slave, twistData, dummy );
				
				if( complete.Contains( transformed.Center ) )
					continue;
				complete.Add( transformed.Center );

				foreach( Cell master in this.MasterCells )
				{
					if( transformed.WillAffectCell( master, this.IsSpherical ) )
					{
						result.Add( slave );
						hotTwists.Add( transformed );
						break;
					}
				}
			}

			// Any slaves this twist data touches also have the potential to affect this master.
			// ZZZ - This ends up adding extraneous cells on {7,3}, assuming only 1/7th turn twists.
			//		 If we allowed 3/7th turn twists, it wouldn't be extraneous though.
			// ZZZ - Nested foreach loops are really slow here.
			Parallel.ForEach( AllSlaveCells, slave =>
			{
				foreach( TwistData td in hotTwists )
					if( td.WillAffectCell( slave, this.IsSpherical ) )
					{
						result.Add( slave );
						break;
					}
			} );

			// We have to special case things for IRP puzzles with no slicing,
			// since StateCalc cells will then not include all cells adjacent to masters.
			// (We rely on StateCalc cells when building up the IRP geometry).
			// In this block, we just grab masters + incident cells.
			// NOTE: This is what we initially did for all puzzles, but it was not enough for {3,7} puzzles.
			if( this.HasValidIRPConfig && this.MasterCells.Count == result.Count )
			{
				foreach( Cell master in this.MasterCells )
				{
					// ZZZ - could throw.  But what would I do in that case anyway?
					Tile masterTile = tiling.TilePositions[master.Center];

					List<Tile> masterTileAsList = new List<Tile>();
					masterTileAsList.Add( masterTile );
					foreach( Tile t in masterTileAsList.Concat( masterTile.EdgeIncidences.Concat( masterTile.VertexIndicences ) ) )
					{
						Cell c = cells[t.Center];
						result.Add( c );
					}
				}
			}

			m_stateCalcCells = result.Distinct().ToList();
			m_stateCalcCellSet = new HashSet<Cell>( m_stateCalcCells );
		}

		private List<Cell> m_stateCalcCells;
		private HashSet<Cell> m_stateCalcCellSet;

		private void PrepareSurfaceData( IStatusCallback callback )
		{
			ClearSurfaceVars();
			if( !this.HasSurfaceConfig )
				return;

			SurfaceConfig sc = Config.SurfaceConfig;
			Geometry g = Geometry.Euclidean;
			if( sc.Surface == Surface.Sphere || sc.Surface == Surface.Boys )
			{
				// Build a polygon with arc segments that will make a circle.
				Segment seg = Segment.Arc( 
					new Vector3D( 1, 0 ), 
					new Vector3D( Math.Sqrt( 0.5 ), Math.Sqrt( 0.5 ) ), 
					new Vector3D( 0, 1 ) );
				List<Segment> segs = new List<Segment>();
				for( int i=0; i<4; i++ )
				{
					Mobius m = new Mobius();
					m.Isometry( Geometry.Euclidean, Math.PI * i / 2, new Complex() );
					Segment clone = seg.Clone();
					clone.Transform( m );
					segs.Add( clone );
				}

				SurfacePoly = new Polygon();
				SurfacePoly.Segments = segs;
				SurfaceRenderingCells = AllCells.ToArray();
				g = Geometry.Spherical; // This will make the texture verts get calculated correctly.
			}
			else
			{
				int p = Config.P;
				int q = Config.Q;
				Vector3D b1 = new Vector3D( sc.Basis1X.Dist( p, q ), sc.Basis1Y.Dist( p, q ) );
				Vector3D b2 = new Vector3D( sc.Basis2X.Dist( p, q ), sc.Basis2Y.Dist( p, q ) );

				// Mark the cells we need to render the surface.
				SurfacePoly = Polygon.FromPoints( new Vector3D[] { new Vector3D(), b1, b1 + b2, b2 } );
				SurfaceRenderingCells = AllCells.Where( c => SurfacePoly.Intersects( c.Boundary ) ).ToArray();
			}

			// Setup texture coords.
			int lod = 6;
			SurfaceTextureCoords = TextureHelper.TextureCoords( SurfacePoly, g, (int)Math.Pow( 2.0, lod ) );
			SurfaceElementIndices = TextureHelper.CalcElementIndices( SurfacePoly, lod )[lod];

			// For each triangle of the surface, cache the closest twisting data.
			List<TwistData> elementTwistData1 = new List<TwistData>();
			List<TwistData> elementTwistData2 = new List<TwistData>();
			int[] elements = SurfaceElementIndices;
			for( int i = 0; i < elements.Length; i++ )
			{
				if( i % 3 != 0 )
					continue;

				Vector3D p1 = SurfaceTextureCoords[elements[i + 0]];
				Vector3D p2 = SurfaceTextureCoords[elements[i + 1]];
				Vector3D p3 = SurfaceTextureCoords[elements[i + 2]];
				Vector3D avg = (p1 + p2 + p3) / 3;

				TwistData td = ClosestTwistingCircles( avg );
				elementTwistData1.Add( td );

				// We have two patches for the sphere.
				if( Config.SurfaceConfig.Surface == Surface.Sphere )
				{
					Mobius m = new Mobius();
					m.Elliptic( Geometry.Spherical, Complex.ImaginaryOne, Math.PI );
					avg = m.Apply( avg );

					td = ClosestTwistingCircles( avg );
					elementTwistData2.Add( td );
				}
			}
			SurfaceElementTwistData1 = elementTwistData1.ToArray();
			SurfaceElementTwistData2 = elementTwistData2.ToArray();
		}

		private void ClearSurfaceVars()
		{
			SurfacePoly = null;
			SurfaceTextureCoords = null;
			SurfaceElementIndices = null;
			SurfaceElementTwistData1 = null;
			SurfaceElementTwistData2 = null;
		}

		public Polygon SurfacePoly { get; private set; }

		public Vector3D[] SurfaceTextureCoords { get; private set; }
		public int[] SurfaceElementIndices { get; private set; }
		public TwistData[] SurfaceElementTwistData1 { get; private set; }
		public TwistData[] SurfaceElementTwistData2 { get; private set; }

		/// <summary>
		/// Whether we have a valid IRP data file configured.
		/// </summary>
		public bool HasValidIRPConfig 
		{
			get
			{
				IRPConfig irpConfig = this.Config.IRPConfig;
				if( irpConfig == null || string.IsNullOrEmpty( irpConfig.DataFile ) )
					return false;
				return true;
			}
		}

		public bool HasValidSkewConfig
		{
			get
			{
				Skew4DConfig skewConfig = this.Config.Skew4DConfig;
				return skewConfig != null;
			}
		}

		private Polygon[] BuildSkewGeometry()
		{
			switch( this.Config.Skew4DConfig.Polytope )
			{
				case Polytope.Duoprism:
				{
					int num = (int)Math.Sqrt( this.Config.ExpectedNumColors );
					return SkewPolyhedron.BuildDuoprism( num );
				}
				case Polytope.Runcinated5Cell:
					return SkewPolyhedron.BuildRuncinated5Cell();
				case Polytope.Bitruncated5Cell:
					return SkewPolyhedron.BuildBitruncated5Cell();
			}

			throw new System.Exception( "Unknown skew polytope." );
		}

		private void LoadIRP( IStatusCallback callback )
		{
			this.HasSkew = this.OnlySkew = false;
			if( !this.HasValidIRPConfig && !this.HasValidSkewConfig )
				return;

			callback.Status( string.Format( "\tValid IRP Config = {0}, Valid Skew Config = {1}", this.HasValidIRPConfig, this.HasValidSkewConfig ) );

			IRPConfig irpConfig = null;
			VRMLInfo info = null;

			Polygon[] polygons = null;
			if( this.HasValidIRPConfig )
			{
				irpConfig = this.Config.IRPConfig;
				string path = System.IO.Path.Combine( Utils.StandardPaths.IrpDir, irpConfig.DataFile );
				info = VRML.LoadIRP( path );
				polygons = info.Polygons;
				callback.Status( "\tGeometry loaded." );
			}
			else if( this.HasValidSkewConfig )
			{
				polygons = BuildSkewGeometry();
				callback.Status( "\tGeometry built." );
			}

			List<Cell> irps = new List<Cell>();
			foreach( Polygon poly in polygons )
			{
				CircleNE dummy = new CircleNE();
				Cell cell = new Cell( poly, dummy );
				irps.Add( cell );
			}

			// ZZZ - Taking this out for now.
			//if( irps.Count != this.MasterCells.Count || irps.Count == 0 )
			//	throw new Exception( string.Format( "IRP had {0} cells, but the puzzle has {1}", irps.Count, this.MasterCells.Count ) );

			//
			// Now we need to assign the IRP cells to the correct master cells.
			// We also need to get them into the correct positions (reflection + rotation),
			// so that they fit together in the same manner as our Master cells.
			//

			Cell irp = irps[0];
			if( this.HasValidIRPConfig )
			{
				irp = irps[irpConfig.FirstTile];
				if( irpConfig.Reflect )
					irp.Boundary.Reverse();
				if( irpConfig.Rotate > 0 )
					irp.Boundary.Cycle( irpConfig.Rotate );
			}

			irp.IndexOfMaster = 0;
			CreatePickInfo( irp );

			// We use a lack of identifications to mean this IRP is for viewing only!
			// There is no good coloring for the associated hyperbolic tiling.
			if( this.Config.Identifications == null || this.Config.Identifications.Count == 0 )
			{
				this.OnlySkew = true;
				int index = 0;
				for( int i = 0; i < irps.Count; i++ )
				{
					irps[i].IndexOfMaster = index;
					index++;	// We do this because of the weird 256-tile {3,8}b.
					if( index >= this.MasterCells.Count )
						index = 0;
				}
				callback.Status( "\tFor viewing only!" );
			}
			else
			{
				// We have a good hyperbolic coloring, so make the IRP tiling match up with it.
				List<Cell> working = new List<Cell>();
				working.Add( irp );
				AlterIRPRecursive( working, m_stateCalcCells, irps );
			}
			IRPCells = irps.ToArray();
			callback.Status( "\tSkew tiles matched to underlying tiling." );

			foreach( Cell irpCell in irps )
			{
				if( irpCell.IndexOfMaster == -1 )
				{
					Debug.Assert( false );
					callback.Status( "\tWarning! Unassigned IRP tile!" );
					irpCell.IndexOfMaster = 0;
				}

				Cell master = this.MasterCells[irpCell.IndexOfMaster];
				int irpSides = irpCell.Boundary.Segments.Count;
				int masterSides = master.Boundary.Segments.Count;
				if( irpSides != masterSides )
					throw new Exception( string.Format( "IRP had a cell with {0} sides, but the underlying hyperbolic tiling has cells with {1} sides.",
						irpSides, masterSides ) );
			}

			// NOTE: No slaves when we are 4D skew.
			if( this.HasValidIRPConfig )
			{
				AddIRPSlaves( info );
				callback.Status( "\tAdded slave tiles for IRP." );
			}

			// NOTE: We need to do this last because we've been altering the geometry above.
			foreach( Cell c in IRPCells )
			{
				if( c == null )
					continue;

				c.TextureVertices = TextureHelper.TextureCoords( c.Boundary, Geometry.Euclidean );

				// We need to subdivide pick info for 4D skew polyhedra.
				if( this.HasValidSkewConfig )
					foreach( PickInfo pi in c.PickInfo )
						pi.Subdivide();

				// We need to push this info into our IRP cells 
				// (it is used when determining twisting directions)
				c.Isometry = MasterCells[c.IndexOfMaster].Isometry;
			}
			callback.Status( "\tCalculated texture coordinates." );

			this.HasSkew = true;
		}

		private class IncidenceData
		{
			public IncidenceData( Cell c, int e, Cell i, int ie, bool r )
			{
				Cell = c;
				Edge = e;
				Incident = i;
				IncidentEdge = ie;
				Reflected = r;
			}
			public Cell Cell { get; set; }
			public int Edge { get; set; }

			public Cell Incident { get; set; }
			public int IncidentEdge { get; set; }
			public bool Reflected { get; set; }
		}

		private List<IncidenceData> GrabIncidenceData( Cell cell, List<Cell> allCells, bool poincare )
		{
			List<IncidenceData> result = new List<IncidenceData>();

			for( int e = 0; e < cell.Boundary.Segments.Count; e++ )
			{
				Segment seg = cell.Boundary.Segments[e];
				Vector3D mid = seg.Midpoint;

				foreach( Cell compare in allCells )
				{
					// The same? (or logically the same)
					// ZZZ - The latter check really isn't necessary.
					if( compare == cell || ( poincare && cell.IndexOfMaster == compare.IndexOfMaster ) )
						continue;

					for( int ie = 0; ie<compare.Boundary.Segments.Count; ie++ )
					{
						Segment segCompare = compare.Boundary.Segments[ie];
						if( segCompare.Midpoint == mid )
						{
							bool reflected = seg.P1 == segCompare.P1;	// ZZZ - may be problematic for non-orientable topologies (?)

							IncidenceData info = new IncidenceData( cell, e, compare, ie, reflected );
							result.Add( info );
							break;
						}
					}
				}
			}

			return result;
		}

		private void AlterIRPRecursive( List<Cell> working, List<Cell> stateCells, List<Cell> irps )
		{
			if( working.Count == 0 )
				return;

			// NOTE: the 'working' irps passed in should already be altered, and in it's final position!

			List<Cell> altered = new List<Cell>();

			foreach( Cell irp in working )
			{
				Cell master = this.MasterCells[irp.IndexOfMaster];

				// Grab incidence info.
				List<IncidenceData> masterIncidences = GrabIncidenceData( master, stateCells, poincare: true );
				List<IncidenceData> irpIncidences = GrabIncidenceData( irp, irps, poincare: false );
			
				// Try to update all the adjacent irp cells.
				foreach( IncidenceData irpIncidence in irpIncidences )
				{
					// Already done this one?
					Cell incidentIRP = irpIncidence.Incident;
					if( incidentIRP.IndexOfMaster != -1 )
						continue;

					// Find the corresponding master incidence data.
					IncidenceData masterIncidence = masterIncidences.Find( i => i.Edge == irpIncidence.Edge );
					if( masterIncidence == null )
					{
						// It should exist.
						Debug.Assert( false );
						continue;
					}

					List<Cell> test = irps.Where( c => c.IndexOfMaster == masterIncidence.Incident.IndexOfMaster ).ToList();
					if( test.Count > 0 )
					{
						// We don't want this to happen.
						Debug.Assert( false );
						continue;
					}

					// Reflect if needed.
					int incidentIRPEdge = irpIncidence.IncidentEdge;
					if( irpIncidence.Reflected ^ masterIncidence.Reflected )
					{
						incidentIRP.Boundary.Reverse();	// This mirrors about the 0th vertex.
						incidentIRPEdge = ( incidentIRP.Boundary.NumSides - 1 ) - incidentIRPEdge;
					}

					// Rotate if needed.
					int numRotates = incidentIRPEdge - masterIncidence.IncidentEdge;
					if( numRotates < 0 )
						numRotates += incidentIRP.Boundary.NumSides;
					incidentIRP.Boundary.Cycle( numRotates );

					// Set the cell index.
					incidentIRP.IndexOfMaster = masterIncidence.Incident.IndexOfMaster;

					CreatePickInfo( incidentIRP );

					altered.Add( incidentIRP );
				}
			}

			AlterIRPRecursive( altered, stateCells, irps );
		}

		private void CreatePickInfo( Cell irpCell )
		{
			if( irpCell.IndexOfMaster == -1 )
				throw new System.Exception( "wtf, yo?" );
			Cell master = this.MasterCells[irpCell.IndexOfMaster];

			Polygon boundary = irpCell.Boundary.Clone();
			List<PickInfo> pickData = new List<PickInfo>();

			SlicingCircles circles = this.Config.SlicingCircles;
			if( circles == null )
			{
				// Do nothing.	
			}
			else if( circles.FaceTwisting )
			{
				TwistData twistData = ClosestTwistingCircles( master.Boundary.Center );
				PickInfo info = new PickInfo( boundary, twistData );
				info.Color = Color.White;
				pickData.Add( info );
			}
			else if( circles.EdgeTwisting )
			{
				for( int s=0; s<boundary.NumSides; s++ )
				{
					TwistData twistData = ClosestTwistingCircles( master.Boundary.Segments[s].Midpoint );

					Segment seg = boundary.Segments[s];
					PickInfo info = new PickInfo(
						Polygon.FromPoints( new Vector3D[] { boundary.Center, seg.P1, seg.P2 } ),
						twistData );
					info.Color = Color.Blue;
					pickData.Add( info );
				}
			}
			else if( circles.VertexTwisting )
			{
				for( int s = 0; s < boundary.NumSides; s++ )
				{
					TwistData twistData = ClosestTwistingCircles( master.Boundary.Segments[s].P1 );

					Segment seg1 = boundary.Segments[s == 0 ? boundary.NumSides - 1 : s - 1];
					Segment seg2 = boundary.Segments[s];
					PickInfo info = new PickInfo(
						Polygon.FromPoints( new Vector3D[] { boundary.Center, seg1.Midpoint, seg2.P1, seg2.Midpoint } ),
						twistData );
					info.Color = Color.Red;
					pickData.Add( info );
				}
			}

			// ZZZ - We're not supporting puzzles with multiple types of twisting right now.
			/*
			Polygon boundary = irpCell.Boundary.Clone();
			boundary.Scale( 0.5 );
			 
			List<PickInfo> pickData = new List<PickInfo>();
			PickInfo info = new PickInfo( boundary, null );
			info.Color = Color.White;
			pickData.Add( info );
			
			for( int s=0; s<irpCell.Boundary.NumSides; s++ )
			{
				int idx1 = s == 0 ? irpCell.Boundary.NumSides - 1 : s - 1;
				int idx2 = s;

				Segment s1Outer = irpCell.Boundary.Segments[idx1];
				Segment s2Outer = irpCell.Boundary.Segments[idx2];
				Segment sInner = boundary.Segments[idx2];

				Vector3D[] p1Outer = s1Outer.Subdivide( 4 );
				Vector3D[] p2Outer = s2Outer.Subdivide( 4 );

				PickInfo edge = new PickInfo(
					Polygon.FromPoints( new Vector3D[] { sInner.P1, p2Outer[1], p2Outer[3], sInner.P2 } ),
					null );
				edge.Color = Color.Blue;

				PickInfo corner = new PickInfo(
					Polygon.FromPoints( new Vector3D[] { sInner.P1, p1Outer[3], p2Outer[0], p2Outer[1] } ),
					null );
				corner.Color = Color.Red;

				pickData.Add( edge );
				pickData.Add( corner );
			}
			 */

			irpCell.PickInfo = pickData.ToArray();
		}

		public struct Translation
		{
			public Translation( short x, short y, short z, Vector3D trans )
			{
				m_x = x;
				m_y = y;
				m_z = z;
				m_translation = trans;
			}
			public short m_x;
			public short m_y;
			public short m_z;
			public Vector3D m_translation;
		}

		private void AddIRPSlaves( VRMLInfo info )
		{
			double dx1 = info.DX.X;
			double dx2 = info.DX.Y;
			double dx3 = info.DX.Z;
			double dy1 = info.DY.X;
			double dy2 = info.DY.Y;
			double dy3 = info.DY.Z;
			double dz1 = info.DZ.X;
			double dz2 = info.DZ.Y;
			double dz3 = info.DZ.Z;

			int max = 31;
			int nx = max;
			int ny = max;
			int nz = max;

			List<Translation> translations = new List<Translation>();

			////////////////////////////////////////////////////////////////////////////////
			// This block taken as directly as possible from Vladamir's vec_viewer_proto.wrl

			var x0 = -((nx-1)*dx1 + (ny-1)*dx2 + (nz-1)*dx3) / 2; 
			var y0 = -((nx-1)*dy1 + (ny-1)*dy2 + (nz-1)*dy3) / 2;
			var z0 = -((nx-1)*dz1 + (ny-1)*dz2 + (nz-1)*dz3) / 2;

			var ix = 0;
			var iy = 0;
			var iz = 0;
			double x = 0;
			double y = 0;
			double z = 0;

			for( iz = 0; iz < nz; iz++){
				for( iy = 0; iy < ny; iy++){
					for( ix = 0; ix < nx; ix++){
						x = x0 + ix*dx1 + iy*dx2 + iz*dx3;
						y = y0 + ix*dy1 + iy*dy2 + iz*dy3;
						z = z0 + ix*dz1 + iy*dz2 + iz*dz3;
						translations.Add( 
							new Translation(
								(short)Math.Abs( ix - (max-1)/2 ), 
								(short)Math.Abs( iy - (max-1)/2 ), 
								(short)Math.Abs( iz - (max-1)/2 ),
								new Vector3D( x, y, z ) ) );
					}
				}
			}

			////////////////////////////////////////////////////////////////////////////////

			m_translations = translations.ToArray();
		}

		private void TraceGraph()
		{
			Trace.WriteLine( "START" );
			foreach( Cell master in this.MasterCells )
			{
				List<IncidenceData> masterIncidences = GrabIncidenceData( master, m_stateCalcCells, poincare: true );
				foreach( IncidenceData incident in masterIncidences )
				{
					Trace.WriteLine( string.Format( "master{0};master{1}", master.IndexOfMaster, incident.Incident.IndexOfMaster ) );
				}
			}
			Trace.WriteLine( "END" );
		}

		public bool IsSpherical
		{
			get
			{
				return this.Config.Geometry == Geometry.Spherical;
			}
		}

		/// <summary>
		/// For things that don't like NaN, +Inf, or extremely large values.
		/// </summary>
		public Vector3D InfinitySafe( Vector3D input )
		{
			if( !IsSpherical )
				return input;

			return Infinity.InfinitySafe( input );
		}

		/// <summary>
		/// Update the state based on a twist.
		/// </summary>
		public void UpdateState( SingleTwist twist )
		{
			UpdateState( Config, State, twist );
		}

		/// <summary>
		/// Update the state based on a twist.
		/// </summary>
		/// <returns>A map of just the updated stickers (old sticker mapped to new)</returns>
		public static Dictionary<Sticker,Sticker> UpdateState( PuzzleConfig config, State state, SingleTwist twist )
		{
			// Maps from old sticker position to sticker hash.
			Dictionary<Vector3D, Sticker> oldMap = new Dictionary<Vector3D, Sticker>();

			// Maps from sticker to new sticker position.
			Dictionary<Sticker, Vector3D> newMap = new Dictionary<Sticker, Vector3D>();

			bool earthquake = config.Earthquake;
			IdentifiedTwistData identifiedTwistData = twist.IdentifiedTwistData;
			double rotation = earthquake ? 1.0 : twist.Magnitude;
			if( !twist.LeftClick )
				rotation *= -1;

			bool isSpherical = config.Geometry == Geometry.Spherical;
			int count = 0;
			foreach( TwistData twistData in twist.StateCalcTD )
			{
				count++;
				Mobius mobius = twistData.MobiusForTwist( config.Geometry, twist, rotation,
					earthquake, count > identifiedTwistData.TwistDataForStateCalcs.Count );

				foreach( List<Sticker> list in twistData.AffectedStickersForSliceMask( twist.SliceMask ) )
				foreach( Sticker sticker in list )
				{
					// Old map
					Vector3D center = sticker.Poly.Center;
					if( isSpherical && Infinity.IsInfinite( center ) )
						center = Infinity.InfinityVector;
					oldMap[center] = sticker;

					// New map
					Vector3D transformed;
					if( isSpherical && Infinity.IsInfinite( center ) )
						transformed = mobius.ApplyToInfinite();
					else
						transformed = mobius.Apply( center );
					if( isSpherical && Infinity.IsInfinite( transformed ) )
						transformed = Infinity.InfinityVector;
					newMap[sticker] = transformed;
				}
			}

			Dictionary<Sticker, Sticker> updated = new Dictionary<Sticker, Sticker>();
			foreach( KeyValuePair<Sticker, Vector3D> kvp in newMap )
			{
				Sticker sticker2;
				if( !oldMap.TryGetValue( kvp.Value, out sticker2 ) )
				{
					// XXX - This currenty happens with moves on the boundary of the puzzle for p>=6,
					// and for points at infinity.
					//assert( false );
					continue;
				}
				
				Sticker sticker1 = kvp.Key;
				
				// Sticker has moved from sticker1 -> sticker2.
				state.SetStickerColorIndex( sticker2.CellIndex, sticker2.StickerIndex,
					state.GetStickerColorIndex( sticker1.CellIndex, sticker1.StickerIndex ) );

				if( sticker1.CellIndex == sticker2.CellIndex &&
					sticker1.StickerIndex == sticker2.StickerIndex )
					continue;
				updated[sticker1] = sticker2;
			}

			state.CommitChanges();
			return updated;
		}
	}
}
