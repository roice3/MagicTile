namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	/// <summary>
	/// Information we need for a tiling.
	/// </summary>
	public struct TilingConfig
	{
		public TilingConfig( int p, int q, int maxTiles ) : this() 
		{
			SetupConfig( p, q, maxTiles );
		}

		public TilingConfig( int p, int q ) : this()
		{
			if( Geometry2D.GetGeometry( p, q ) != Geometry.Spherical )
				throw new System.ArgumentException();

			SetupConfig( p, q, PlatonicSolids.NumFacets( p, q ) );
		}

		private void SetupConfig( int p, int q, int maxTiles )
		{
			P = p;
			Q = q;
			m.Unity();
			MaxTiles = maxTiles;
			Shrink = 1.0;
		}

		public int P { get; set; }
		public int Q { get; set; }

		/// <summary>
		/// The induced geometry.
		/// </summary>
		public Geometry Geometry { get { return Geometry2D.GetGeometry( this.P, this.Q ); } }

		/// <summary>
		/// A Mobius transformation to apply while creating the tiling.
		/// </summary>
		public Mobius M { get { return m; } set { m = value; } }
		private Mobius m;

		/// <summary>
		/// The max number of tiles to include in the tiling.
		/// </summary>
		public int MaxTiles { get; set; }

		/// <summary>
		/// A shrinkage to apply to the drawn portion of a tile.
		/// Default is 1.0 (no shrinkage).
		/// </summary>
		public double Shrink { get; set; }

		/// <summary>
		/// Returns a Mobius transform that can be used to create a dual {q,p} tiling.
		/// This Mobius transform will center the tiling on a vertex.
		/// </summary>
		public Mobius VertexCenteredMobius()
		{
			return VertexCenteredMobius( this.P, this.Q );
		}

		public static Mobius VertexCenteredMobius( int p, int q )
		{
			double angle = Math.PI / q;
			if( Utils.Even( q ) )
				angle *= 2;
			Vector3D offset = new Vector3D( -1 * Geometry2D.GetNormalizedCircumRadius( p, q ), 0, 0 );
			offset.RotateXY( angle );
			Mobius m = new Mobius();
			m.Isometry( Geometry2D.GetGeometry( p, q ), angle, offset.ToComplex() );
			return m;
		}

		/// <summary>
		/// This Mobius transform will center the tiling on an edge.
		/// </summary>
		public Mobius EdgeMobius()
		{
			Geometry g = Geometry2D.GetGeometry( this.P, this.Q );

			Polygon poly = new Polygon();
			poly.CreateRegular( this.P, this.Q );
			Segment seg = poly.Segments[0];
			Vector3D offset = seg.Midpoint;

			double angle = Math.PI / this.P;
			offset.RotateXY( -angle );

			Mobius m = new Mobius();
			m.Isometry( g, -angle, -offset );
			return m;
		}
	}

	public class TilingPositions
	{
		public HashSet<Vector3D> Positions;

		public void Build( TilingConfig config )
		{
			Positions = new HashSet<Vector3D>();
			Tile tile = Tiling.CreateBaseTile( config );

			// Fundamental triangle definition.
			Segment seg = tile.Boundary.Segments[0];
			Vector3D
				p1 = new Vector3D(),
				p2 = seg.Midpoint,
				p3 = seg.P1;

			Circle[] mirrors = new Circle[]
			{
				new Circle( p1, p2 ),
				new Circle( p1, p3 ),
				seg.Circle
			};

			HashSet<Vector3D> starting = new HashSet<Vector3D>( new Vector3D[] { new Vector3D() } );
			BuildInternalRecursive( mirrors, config, starting, Positions );
		}

		private void BuildInternalRecursive( Circle[] mirrors, TilingConfig config, HashSet<Vector3D> starting, HashSet<Vector3D> positions )
		{
			if( starting.Count == 0 )
				return;

			Geometry g = config.Geometry;

			HashSet<Vector3D> added = new HashSet<Vector3D>();
			foreach( Vector3D v in starting )
			{
				foreach( Circle c in mirrors )
				{
					Vector3D candidate = c.ReflectPoint( v );
					if( g == Geometry.Hyperbolic && candidate.Abs() > 0.9999 )	// ZZZ - Same magic number as in Tile.cs
						continue;

					if( positions.Add( candidate ) )
						added.Add( candidate );

					if( positions.Count > config.MaxTiles - 1 )
						return;
				}
			}

			BuildInternalRecursive( mirrors, config, added, positions );
		}
	}

	public class Tiling
	{
		public Tiling()
		{
			m_tiles = new List<Tile>();
			this.TilePositions = new Dictionary<Vector3D, Tile>();
		}

		/// <summary>
		/// The tiling configuration.
		/// </summary>
		public TilingConfig TilingConfig { get; set; }

		/// <summary>
		/// Our tiles.
		/// </summary>
		private List<Tile> m_tiles;

		/// <summary>
		/// A dictionary from tile centers to tiles.
		/// </summary>
		public Dictionary<Vector3D, Tile> TilePositions { get; set; }

		/// <summary>
		/// A static helper to generate two dual tilings.
		/// </summary>
		/// <remarks>{p,q} will have a vertex at the center.</remarks>
		/// <remarks>{q,p} will have its center at the center.</remarks>
		public static void MakeDualTilings( out Tiling tiling1, out Tiling tiling2, int p, int q )
		{
			tiling1 = new Tiling();
			tiling2 = new Tiling();

			int maxTiles = 2000;
			TilingConfig config1 = new TilingConfig( p, q, maxTiles );
			TilingConfig config2 = new TilingConfig( q, p, maxTiles );
			tiling1.GenerateInternal( config1, Polytope.Projection.FaceCentered );
			tiling2.GenerateInternal( config2, Polytope.Projection.VertexCentered );

			/*
			Circle c = new Circle();
			c.Radius = .9;
			tiling1.Clip( c );
			tiling2.Clip( c );
			*/
		}

		/// <summary>
		/// Generate ourselves from a tiling config.
		/// </summary>
		public void Generate( TilingConfig config )
		{
			GenerateInternal( config );
		}

		public void GenerateInternal( TilingConfig config, Polytope.Projection projection = Polytope.Projection.FaceCentered )
		{
			this.TilingConfig = config;

			// Create a base tile.
			Tile tile = CreateBaseTile( config );

			// Handle edge/vertex centered projections.
			if( projection == Polytope.Projection.VertexCentered )
			{
				Mobius mobius = config.VertexCenteredMobius();
				tile.Transform( mobius );
			}
			else if( projection == Polytope.Projection.EdgeCentered )
			{
				Mobius mobius = config.EdgeMobius();
				tile.Transform( mobius );
			}

			TransformAndAdd( tile );

			List<Tile> tiles = new List<Tile>();
			tiles.Add( tile );
			Dictionary<Vector3D,bool> completed = new Dictionary<Vector3D,bool>();
			completed[tile.Boundary.Center] = true;
			ReflectRecursive( tiles, completed );

			FillOutIsometries( tile, m_tiles, config.Geometry );
			FillOutIncidences();
		}

		/// <summary>
		/// This will fill out all the tiles with the isometry that will take them back to a home tile.
		/// </summary>
		private static void FillOutIsometries( Tile home, List<Tile> tiles, Geometry g )
		{
			foreach( Tile tile in tiles )
				tile.Isometry.CalculateFromTwoPolygons( home, tile, g );
		}

		/// <summary>
		/// I'm a little worried about memory of keeping these around being wasteful.
		/// </summary>
		public Dictionary<Vector3D, List<Tile>> EdgesIncidences {  get; private set; }
		public Dictionary<Vector3D, List<Tile>> VertexIncidences { get; private set; }

		/// <summary>
		/// Fill out all the incidence information.
		/// If performance became an issue, we could do some of this at tile generation time.
		/// </summary>
		private void FillOutIncidences()
		{
			Dictionary<Vector3D, List<Tile>> Edges = new Dictionary<Vector3D, List<Tile>>();
			Dictionary<Vector3D, List<Tile>> Vertices = new Dictionary<Vector3D, List<Tile>>();

			foreach( Tile t in m_tiles )
			{
				foreach( Vector3D edge in t.Boundary.EdgeMidpoints )
				{
					List<Tile> list;
					if( !Edges.TryGetValue( edge, out list ) )
					{
						list = new List<Tile>();
						Edges[edge] = list;
					}

					list.Add( t );
				}

				foreach( Vector3D vertex in t.Boundary.Vertices )
				{
					List<Tile> list;
					if( !Vertices.TryGetValue( vertex, out list ) )
					{
						list = new List<Tile>();
						Vertices[vertex] = list;
					}

					list.Add( t );
				}
			}

			foreach( List<Tile> list in Edges.Values )
				foreach( Tile t in list )
					t.EdgeIncidences.AddRange( list );

			foreach( List<Tile> list in Vertices.Values )
				foreach( Tile t in list )
					t.VertexIndicences.AddRange( list );

			foreach( Tile t in m_tiles )
			{
				// Remove duplicates and ourselves from lists.
				t.EdgeIncidences = t.EdgeIncidences.Distinct().Except( new Tile[] { t } ).ToList();
				t.VertexIndicences = t.VertexIndicences.Distinct().Except( new Tile[] { t } ).ToList();

				// Also, make sure we only track vertex incidences that do not have edge incidences too.
				t.VertexIndicences = t.VertexIndicences.Except( t.EdgeIncidences ).ToList();
			}

			EdgesIncidences = Edges;
			VertexIncidences = Vertices;
		}

		public static Tile CreateBaseTile( TilingConfig config )
		{
			Polygon boundary = new Polygon(), drawn = new Polygon();
			boundary.CreateRegular( config.P, config.Q );
			drawn = boundary.Clone();

			//boundary.CreateRegular( 3, 10 );
			//drawn.CreateRegular( 3, 8 );
			//boundary.CreateRegular( 3, 7 );
			//drawn = Heart();

			//for( int i=0; i<drawn.NumSides; i++ )
			//	drawn.Segments[i].Center *= 0.1;

			// Good combos:
			// ( 5, 5 ), ( 10, 10 )
			// ( 3, 10 ), ( 3, 9 )
			// ( 6, 4 ), ( 6, 8 )
			// ( 7, 3 ), ( 7, 9 )

			Tile tile = new Tile( boundary, drawn, config.Geometry );
			Tile.ShrinkTile( ref tile, config.Shrink );
			return tile;
		}

		/// <summary>
		/// Clips the tiling to the interior of a circle.
		/// </summary>
		public void Clip( Circle c, bool interior = true )
		{
			Slicer.Clip( ref m_tiles, c, interior );
		}

		/// <summary>
		/// Will clone the tile, transform it and add it to our tiling.
		/// </summary>
		private bool TransformAndAdd( Tile tile )
		{
			// Will we want to include it?
			if( !tile.IncludeAfterMobius( this.TilingConfig.M ) )
				return false;

			Tile clone = tile.Clone();
			clone.Transform( this.TilingConfig.M );
			m_tiles.Add( clone );
			this.TilePositions[clone.Boundary.Center] = clone;
			return true;
		}

		/// <summary>
		/// This will return whether we'll be a new tile after reflecting through a segment.
		/// This allows us to do the check without having to do all the work of reflecting the entire tile.
		/// </summary>
		public bool NewTileAfterReflect( Tile t, Segment s, Dictionary<Vector3D, bool> completed )
		{
			/* This was too slow!
			Polygon newPolyBoundary = t.Boundary.Clone();
			newPolyBoundary.Reflect( s );
			Vector3D testCenter = this.TilingConfig.M.Apply( newPolyBoundary.Center );*/

			CircleNE newVertexCircle = t.VertexCircle.Clone();
			newVertexCircle.Reflect( s );
			Vector3D testCenter = this.TilingConfig.M.Apply( newVertexCircle.CenterNE );

			return !completed.ContainsKey( testCenter );
		}

		private void ReflectRecursive( List<Tile> tiles, Dictionary<Vector3D,bool> completed ) 
		{
			// Breadth first recursion.

			if( 0 == tiles.Count )
				return;

			List<Tile> reflected = new List<Tile>();

			foreach( Tile tile in tiles )
			{
				// We don't want to reflect tiles living out at infinity.
				// Strange things happen, and we can still get the full tiling without doing this.
				if( tile.HasPointsProjectedToInfinity )
					continue;

				// Are we done?
				if( m_tiles.Count >= this.TilingConfig.MaxTiles )
					return;

				for( int s=0; s<tile.Boundary.NumSides; s++ )
				{
					Segment seg = tile.Boundary.Segments[s];
					if( !NewTileAfterReflect( tile, seg, completed ) )
						continue;

					Tile newBase = tile.Clone();
					newBase.Reflect( seg );
					if( TransformAndAdd( newBase ) )
					{
						Debug.Assert( !completed.ContainsKey( newBase.Boundary.Center ) );
						reflected.Add( newBase );
						completed[newBase.Boundary.Center] = true;
					}
				}
			}

			ReflectRecursive( reflected, completed );
		}

		/// <summary>
		/// The number of tiles.
		/// </summary>
		public int Count
		{
			get { return m_tiles.Count; }
		}

		/// <summary>
		/// Access to all the tiles.
		/// </summary>
		public IEnumerable<Tile> Tiles
		{
			get { return m_tiles; }
		}

		/// <summary>
		/// Retrieve all the polygons in this tiling that we want to draw.
		/// </summary>
		public IEnumerable<Polygon> Polygons 
		{
			get
			{
				return m_tiles.Select( t => t.Drawn );
			}
		}

		/// <summary>
		/// Retreive all the (non-Euclidean) vertex circles in this tiling.
		/// </summary>
		public IEnumerable<CircleNE> Circles
		{
			get
			{
				return m_tiles.Select( t => t.VertexCircle );
			}
		}
	}
}
