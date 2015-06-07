namespace R3.Geometry
{
	using R3.Core;
	using R3.Math;
	using System.Collections;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Linq;
	using System.Numerics;
	using Math = System.Math;

	using TileVertex = System.Tuple<int, int>;

	/// <summary>
	/// A class to generate {3,3,r} tilings for r >= 6
	/// </summary>
	public class UltraInf
	{
		public UltraInf()
		{
			R = -1;
		}

		public int P { get { return 5; } }
		public int Q { get { return 3; } }
		public int R { get; set; }

		/// <summary>
		/// The radius of the Poincare Ball.
		/// </summary>
		private double RBall { get { return 1.0; } }

		private List<Tile> m_tiles;
		public Tile[] Tiles { get { return m_tiles.ToArray(); } }

		private List<Cell> m_cells;

		public CircleNE m_equator;
		public CircleNE Equator { get { return m_equator; } }

		//int m_cellCount = 200000;
		int m_cellCount = 200000;
		double m_shrink = 1;

		public Mobius m_animMobius;

		/// <summary>
		/// If set, this will be used for coloring.
		/// </summary>
		public Dictionary<Vector3D, Color> ColorMap { get; set; }

		private void Init()
		{
			m_tiles = new List<Tile>();
			m_equator = new CircleNE();
			m_neighborCircle = new CircleNE();
			m_cells = new List<Cell>();
		}

		/// <summary>
		/// anim should be between 0 and 1.
		/// </summary>
		public void Create( double anim )
		{
			Init();

			Tile[] cellTiles = FundamentalCell();

			//Transform( anim, cellTiles );
			int level = 1;
			Cell startingCell = new Cell( level, cellTiles );

			// Recurse.
			HashSet<Vector3D> completed = new HashSet<Vector3D>( new HighToleranceVectorEqualityComparer() );
			foreach( Tile t in cellTiles )
				completed.Add( t.VertexCircle.CenterNE );
			List<Cell> starting = new List<Cell>();
			starting.Add( startingCell );
			m_cells.Add( startingCell );
			ReflectRecursive( level, starting, completed );

			//Output();
		}

		public Tile[] FundamentalCell()
		{
			Tile template = TemplateTile();
			Tile.ShrinkTile( ref template, 0.9 );

			// Generate a cell tiling.
			TilingConfig tilingConfig = new TilingConfig( Q, P );
			Tiling poly = new Tiling();
			poly.Generate( tilingConfig );
			m_tiles = poly.Tiles.ToList();
			//SetupTransformCircle( tile );	// Call this before transforming.
			//SetupNeighborCircle( tile );	

			// Generate our cell.
			List<Tile> cellTiles = new List<Tile>();
			foreach( Tile t in poly.Tiles )
			{
				Tile temp = template.Clone();
				temp.Transform( t.Isometry.Inverse() );
				cellTiles.Add( temp );
			}

			return cellTiles.ToArray();
		}

		/// <summary>
		/// Outputs edges to an stl file.
		/// </summary>
		public void Output()
		{
			System.Func<Vector3D, Vector3D> p2s = v => Spherical2D.PlaneToSphere( v );
			System.Func<Vector3D, Vector3D> transform = v => H3Models.Ball.ApplyMobius( Mobius.Scale( 3 ), v );

			double min = double.MaxValue;
			Cell cell = m_cells.First();
			foreach( Vector3D v1 in cell.Tiles[0].Boundary.Vertices )
			foreach( Vector3D v2 in cell.Tiles[1].Boundary.Vertices )
				min = Math.Min( min, p2s( v1 ).Dist( p2s( v2 ) ) );

			// XXX - code below so ugly to read!

			Dictionary<TileVertex, TileVertex> vMap = new Dictionary<TileVertex, TileVertex>();
			for( int tile_i = 0; tile_i < cell.Tiles.Length; tile_i++ )
			for( int tile_j = tile_i+1; tile_j < cell.Tiles.Length; tile_j++ )
			{
				for( int vertex_i = 0; vertex_i < cell.Tiles[tile_i].Boundary.Vertices.Length; vertex_i++ )
				for( int vertex_j = 0; vertex_j < cell.Tiles[tile_j].Boundary.Vertices.Length; vertex_j++ )
				{
					Vector3D v1 = cell.Tiles[tile_i].Boundary.Vertices[vertex_i];
					Vector3D v2 = cell.Tiles[tile_j].Boundary.Vertices[vertex_j];

					if( Tolerance.Equal( p2s( v1 ).Dist( p2s( v2 ) ), min ) )
						vMap[new TileVertex( tile_i, vertex_i )] = new TileVertex( tile_j, vertex_j );
				}
			}

			HashSet<H3.Cell.Edge> edges = new HashSet<H3.Cell.Edge>( new H3.Cell.EdgeEqualityComparer() );
			foreach( Cell c in m_cells )
			foreach( KeyValuePair<TileVertex, TileVertex> kvp in vMap )
			{
				Vector3D v1 = transform( p2s( c.Tiles[kvp.Key.Item1].Boundary.Vertices[kvp.Key.Item2] ) );
				Vector3D v2 = transform( p2s( c.Tiles[kvp.Value.Item1].Boundary.Vertices[kvp.Value.Item2] ) );
				edges.Add( new H3.Cell.Edge( v1, v2 ) );
			}

			//H3.m_settings.ThinEdges = true;
			H3.SaveToFile( "ultrainf", edges.ToArray(), finite: false );
		}

		private void Transform( double anim, IEnumerable<Tile> tetTiles )
		{
			//TilingConfig config = new TilingConfig( 8, 3, 4 );	// Reproduces Tolerance issues with {3,3,7}, though not actually correct to be applying hyperbolic transforms anyway (only spherical).
			TilingConfig config = new TilingConfig( 3, 3, 1 );
			Mobius m = new Mobius();
			m = Mobius.Identity();

			// Invert
			Complex c1 = new Complex( 0, 1 );
			Complex c2 = new Complex( 1, 0 );
			Complex c3 = new Complex( 0, -0.999999999999 );	// - 1 doesn't work
			//m.MapPoints( c1, c2, c3, c3, c2, c1 );

			//Mobius m = config.DualMobius();
			//m.Isometry( Geometry.Spherical, 0, new Complex( 1.2345, -0.4321 ) );	// nice one
			//m.Isometry( Geometry.Spherical, 0, new Complex( 0, 0.148125 ) );		 // half plane

			// Animation.
			double p2 = DonHatch.e2hNorm( 0.6 );
			double p2Interp = DonHatch.h2eNorm( p2 * anim );
			//m.Isometry( Geometry.Spherical, 0, -p2Interp );
			m.Isometry( Geometry.Hyperbolic, 0, new Complex( -p2Interp, 0 ) );

			Mobius m2 = new Mobius();
			m2.Isometry( Geometry.Hyperbolic, 0, new Complex( -0.6, 0 ) );
			m2 = m_fixedCircleToStandardDisk.Inverse() * m2 * m_fixedCircleToStandardDisk;

			bool firstAnim = false;
			if( firstAnim )
			{
				m = m_fixedCircleToStandardDisk.Inverse() * m * m_fixedCircleToStandardDisk;
				m_animMobius = m;
			}
			else
			{
				m = m_neighborToStandardDisk.Inverse() * m * m_neighborToStandardDisk;
				m_animMobius = m2 * m;
			}
			m_animMobius.Normalize();

			foreach( Tile t in tetTiles )
				t.Transform( m_animMobius );
			foreach( Tile t in m_tiles )
				t.Transform( m_animMobius );
			m_equator.Transform( m_animMobius );
			m_neighborCircle.Transform( m_animMobius );
		}

		private Tile TemplateTile()
		{
			double inRadiusHyp = InRadius;
			double inRadiusEuclidean = DonHatch.h2eNorm( inRadiusHyp );
			double faceRadius = FaceRadius( inRadiusEuclidean );

			// Calc the midpoint, and project to plane.
			Vector3D midPoint = MidPoint( inRadiusEuclidean, faceRadius );
			midPoint.Z *= -1;
			midPoint = Sterographic.SphereToPlane( midPoint );
			//double midPointSpherical = MidPoint( inRadiusEuclidean, faceRadius );
			//double midPoint = Spherical2D.s2eNorm( midPointSpherical );

			// Create and scale based on our midpoint.
			Polygon poly = new Polygon();
			poly.CreateRegular( Q, R );
			double standardMidpointAbs = poly.Segments[0].Midpoint.Abs();
			m_shrink = midPoint.Abs() / standardMidpointAbs;
			poly.Scale( m_shrink );

			Matrix4D m = Matrix4D.MatrixToRotateinCoordinatePlane( -Math.PI/Q, 0, 1 );
			poly.Rotate( m );

			return new Tile( poly, poly.Clone(), Geometry.Hyperbolic );
		}

		private double PiOverNSafe( int n )
		{
			return n == -1 ? 0 : Math.PI / n;
		}

		/// <summary>
		/// Returns the in-radius of a cell (hyperbolic metric)
		/// </summary>
		private double InRadius
		{
			get
			{
				return DonHatch.acosh(
					Math.Sin( PiOverNSafe( P ) ) * Math.Cos( PiOverNSafe( R ) ) /
					Math.Sqrt( 1 - Math.Pow( Math.Cos( PiOverNSafe( P ) ), 2 ) - Math.Pow( Math.Cos( PiOverNSafe( Q ) ), 2 ) ) );
			}
		}

		/// <summary>
		/// Returns Face radius in ball model (Euclidean metric)
		/// </summary
		private double FaceRadius( double inRadiusEuclidean )
		{
			return (Math.Pow( RBall, 2 ) - Math.Pow( inRadiusEuclidean, 2 )) / (2 * inRadiusEuclidean);
		}

		/// <summary>
		/// Returns the midpoint of our polygon edge on the sphere.
		/// </summary>
		private Vector3D MidPoint( double inRadius, double faceRadius )
		{
			// Using info from:
			// http://en.wikipedia.org/wiki/Tetrahedron
			// http://eusebeia.dyndns.org/4d/tetrahedron

			// XXX - Should make this method just work in all {p,q,r} cases!

			// tet
			//double vertexToFace = Math.Acos( 1.0 / 3 );  // 338

			// icosa
			double polyCircumRadius = Math.Sin( 2 * Math.PI / 5 );
			double polyInRadius = Math.Sqrt( 3 ) / 12 * (3 + Math.Sqrt( 5 ));

			// cube
			//double polyCircumRadius = Math.Sqrt( 3 );
			//double polyInRadius = 1;

			double vertexToFace = Math.Acos( polyInRadius / polyCircumRadius );
			double angleTemp = Math.Acos( RBall / (inRadius + faceRadius) );

			double angleToRotate = (Math.PI - vertexToFace) - angleTemp;
			angleToRotate = vertexToFace - angleTemp;

			Vector3D zVec = new Vector3D( 0, 0, 1 );
			zVec.RotateAboutAxis( new Vector3D( 0, 1, 0 ), angleToRotate );
			return zVec;
		}

		/// <summary>
		/// Helper to calculate a mobius transform, given some point transformation
		/// that is a mobius.
		/// </summary>
		private static Mobius CalcMobius( System.Func<Vector3D, Vector3D> pointTransform )
		{
			double sqrt22 = Math.Sqrt( 2 ) / 2;
			Vector3D p1 = new Vector3D( 0, 1 );
			Vector3D p2 = new Vector3D( sqrt22, sqrt22 );
			Vector3D p3 = new Vector3D( 1, 0 );

			Vector3D e1 = pointTransform( p1 );
			Vector3D e2 = pointTransform( p2 );
			Vector3D e3 = pointTransform( p3 );

			Mobius m = new Mobius();
			m.MapPoints( e1, e2, e3,
				new Complex( 0, 1 ),
				new Complex( sqrt22, sqrt22 ),
				new Complex( 1, 0 ) );
			return m;
		}

		/// <summary>
		/// Setup a transform which will take a circle (one of the main 4 ones)
		/// to the standard disk location.
		/// </summary>
		private void SetupTransformCircle( Tile templateTriangle )
		{
			/*
			Polygon poly = m_tiles.First().Boundary;	// Tetrahedral tiling.

			double sqrt22 = Math.Sqrt( 2 ) / 2;

			Vector3D p1 = new Vector3D( 0, m_shrink );
			Vector3D p2 = new Vector3D( m_shrink * sqrt22, m_shrink * sqrt22 );
			Vector3D p3 = new Vector3D( m_shrink, 0 );

			Vector3D e1 = poly.Segments[1].ReflectPoint( p1 );
			Vector3D e2 = poly.Segments[1].ReflectPoint( p2 );
			Vector3D e3 = poly.Segments[1].ReflectPoint( p3 );

			m_standardDisk.MapPoints( e1, e2, e3, 
				new Complex( 0, 1 ),
				new Complex( sqrt22, sqrt22 ), 
				new Complex( 1, 0 ) );
			*/

			Polygon poly = m_tiles.First().Boundary;	// Tetrahedral tiling.
			System.Func<Vector3D, Vector3D> pointTransform = v =>
			{
				v *= m_shrink;
				return poly.Segments[1].ReflectPoint( v );
			};
			m_fixedCircleToStandardDisk = CalcMobius( pointTransform );
		}

		/// <summary>
		/// Setup a circle we'll use to color neighbors.
		/// </summary>
		private void SetupNeighborCircle( Tile templateTriangle )
		{
			Polygon poly = m_tiles.First().Boundary;	// Tetrahedral tiling.
			CircleNE circ = new CircleNE();
			circ.Radius = m_shrink;

			circ.Reflect( poly.Segments[2] );
			circ.Reflect( templateTriangle.Boundary.Segments[1] );

			Mobius m = new Mobius();
			m.Isometry( Geometry.Spherical, 0, -circ.CenterNE );
			circ.Transform( m );
			m_neighborCircle = new CircleNE();
			m_neighborCircle.Radius = 1.0 / circ.Radius;
			m_originalNeighborCircle = m_neighborCircle.Clone();

			System.Func<Vector3D, Vector3D> pointTransform = v =>
			{
				v *= m_neighborCircle.Radius;
				v = new Vector3D( v.Y, v.X );
				v.RotateXY( -Math.PI / 2 );
				return v;
			};
			m_neighborToStandardDisk = CalcMobius( pointTransform );
		}

		public Mobius m_fixedCircleToStandardDisk;
		public Mobius m_neighborToStandardDisk;
		private CircleNE m_neighborCircle;
		private CircleNE m_originalNeighborCircle;

		////////////////////////////////////////////////////////////////
		// Code below modeled after tiling class, but with tets instead of single tiles.

		internal class Cell
		{
			public Cell( int level, Tile[] tiles )
			{
				Level = level;
				Tiles = tiles;
			}

			public int Level { get; set; }
			public Tile[] Tiles { get; set; }

			public Cell Clone()
			{
				List<Tile> copied = new List<Tile>();
				foreach( Tile t in Tiles )
					copied.Add( t.Clone() );
				return new Cell( Level, copied.ToArray() );
			}

			public void Reflect( Segment seg )
			{
				foreach( Tile tile in this.Tiles )
				{
					tile.Reflect( seg );
					if( Infinity.IsInfinite( tile.Boundary.Center ) )
						tile.Boundary.Center = Infinity.LargeFiniteVector;
					if( Infinity.IsInfinite( tile.Drawn.Center ) )
						tile.Drawn.Center = Infinity.LargeFiniteVector;
				}
			}

			public IEnumerable<Segment> Segments
			{
				get
				{
					foreach( Tile t in this.Tiles )
					{
						// Don't use tiny triangles to recurse.
						//if( t.VertexCircle.Radius < 0.01 )
						if( t.VertexCircle.Radius < 0.008 )
							continue;

						if( t.Boundary.Segments.Any( s => s.Length < 0.15 ) )
							continue;

						foreach( Segment s in t.Boundary.Segments )
						{
							//if( s.Length < 0.15 )
							//	continue;
							//else
								yield return s;
						}
					}
				}
			}
		}

		public IEnumerable<Tile> CellTiles
		{
			get
			{
				foreach( Cell cell in m_cells )
					foreach( Tile tile in cell.Tiles )
						yield return tile;
			}
		}

		public class ColoredTile
		{
			public ColoredTile( Tile t, Color c ) { Tile = t; Color = c; }
			public Tile Tile { get; set; }
			public Color Color { get; set; }
		}

		public IEnumerable<ColoredTile> ColoredTiles
		{
			get
			{
				foreach( Cell cell in m_cells )
				{
					/*yield return new ColoredTile( tet.A, Color.Red );
					yield return new ColoredTile( tet.B, Color.Green );
					yield return new ColoredTile( tet.C, Color.Yellow );
					yield return new ColoredTile( tet.D, Color.Blue );*/

					int mag = cell.Level * 50;
					int r = mag, g = 0, b = 0;
					if( r > 255 )
					{
						r = 255;
						mag -= 255;
						g = mag;
					}
					if( g > 255 )
					{
						g = 255;
						mag -= 255;
						b = mag;
					}
					if( b > 255 )
						b = 255;
					//Color c = Color.FromArgb( 255, 255 - b < 75 ? 75 : 255 - b, 255 - r, 255 - g );	purple red
					Color c = Color.FromArgb( 255, 255 - b < 75 ? 75 : 255 - b, 255 - g, 255 - r );
					//Color c = Color.FromArgb( 255, r, b, g );
					c = Color.White;

					if( false )
					//if( !m_neighborCircle.IsPointInsideNE( tet.A.Center ) )
					{
						// Here is the point in the standard disk
						Vector3D p = m_animMobius.Inverse().Apply( cell.Tiles.First().Boundary.Center );
						p = m_originalNeighborCircle.ReflectPoint( p );
						p *= this.RBall / m_originalNeighborCircle.Radius;
						p.RotateXY( - Math.PI / 3 );

						if( this.ColorMap == null || !this.ColorMap.TryGetValue( p, out c ) )
							c = Color.Silver;

						//c = Color.Red;
					}

					foreach( Tile t in cell.Tiles )
						yield return new ColoredTile( t, c );
				}
			}
		}

		private bool NewTetAfterReflect( Cell cell, Segment s, HashSet<Vector3D> completed )
		{
			foreach( Tile tile in cell.Tiles )
			{
				CircleNE newVertexCircle = tile.VertexCircle.Clone();
				newVertexCircle.Reflect( s );

				if( completed.Contains( Infinity.InfinitySafe( newVertexCircle.CenterNE ) ) )
					return false;
			}

			return true;
		}

		private void ReflectRecursive( int level, List<Cell> cells, HashSet<Vector3D> completed )
		{
			level++;

			// Breadth first recursion.

			if( 0 == cells.Count )
				return;

			List<Cell> reflected = new List<Cell>();

			foreach( Cell cell in cells )
			{
				foreach( Segment seg in cell.Segments )
				{
					// Are we done?
					if( m_cells.Count >= m_cellCount )
						return;

					if( !NewTetAfterReflect( cell, seg, completed ) )
						continue;

					Cell newBase = cell.Clone();
					newBase.Level = level;
					newBase.Reflect( seg );
					m_cells.Add( newBase );
					reflected.Add( newBase );
					foreach( Tile tile in newBase.Tiles )
						completed.Add( Infinity.InfinitySafe( tile.VertexCircle.CenterNE ) );
				}
			}

			ReflectRecursive( level, reflected, completed );
		}

		/// <summary>
		/// ZZZ - move to a shared location (this is already copied)
		/// </summary>
		public class HighToleranceVectorEqualityComparer : IEqualityComparer<Vector3D>
		{
			public bool Equals( Vector3D v1, Vector3D v2 )
			{
				return
					v1.Compare( v2, m_tolerance );
			}

			public int GetHashCode( Vector3D v )
			{
				return v.GetHashCode( m_tolerance );
			}

			//private double m_tolerance = 0.00025;
			private double m_tolerance = 0.0005;
		}
	}
}
