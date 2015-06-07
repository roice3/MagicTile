namespace R3.Geometry
{
	using System.Collections.Generic;
	using System.Linq;
	using R3.Core;

	/// <summary>
	/// This class is used to generate the really exotic H3 honeycombs,
	/// the {4,4,4}, {3,6,3}, and {6,3,6}.
	/// </summary>
	public class H3Supp
	{
		/// <summary>
		/// Our approach will be:
		/// (1) Generate a portion of one cell.
		/// (2) Reflect all facets in the central facet, to get things filled-in inside the central facet.  (Trim small edges here?)
		/// (3) Copy this region around the plane, and go back to step (2) if density is not high enough.
		/// (4) Map to Ball, trimming edges that become too small.
		/// NOTE: All verts are on the boundary, so we can reflect around 
		//		  in circles on the plane at infinity, rather than spheres.
		/// </summary>
		public static void GenerateExotic( EHoneycomb honeycomb, H3.Settings settings )
		{
			settings.AngularThickness = 0.17;

			Tiling tiling;
			Tile baseTile;
			GetAssociatedTiling( honeycomb, out tiling, out baseTile );

			List<H3.Cell.Edge> edges = new List<H3.Cell.Edge>();
			foreach( Segment seg in baseTile.Boundary.Segments )
				edges.Add( new H3.Cell.Edge( seg.P1, seg.P2 ) );

			settings.Position = Polytope.Projection.FaceCentered;
			double scale = 1;
			Vector3D offset = new Vector3D();
			if( settings.Position == Polytope.Projection.FaceCentered )
			{
				scale = FaceCenteredScale( baseTile.VertexCircle );
				offset = new Vector3D();
			}
			else if( settings.Position == Polytope.Projection.EdgeCentered )
			{
				scale = EdgeCenteredScale( baseTile );
				offset = baseTile.Boundary.Segments[0].Midpoint;
			}

			int iterations = m_params.Iterations;
			for( int i=0; i<iterations; i++ )
				edges = DoOneStep( edges, tiling, baseTile.VertexCircle );
			edges = CopyAndProject( edges, tiling, scale, offset );

			if( m_params.RemoveDangling )
			{
				Dictionary<H3.Cell.Edge,int> edgeDict = edges.ToDictionary( e => e, e => 1 );
				H3.RemoveDanglingEdgesRecursive( edgeDict );
				edges = edgeDict.Keys.ToList();
			}

			string outputFileName = H3.m_baseDir + Honeycomb.String( honeycomb, false );
			System.IO.File.Delete( outputFileName );

			if( m_params.Output == H3.Output.STL )
			{
				outputFileName += ".stl";

				// Now mesh the edges.
				Shapeways mesh = new Shapeways();
				foreach( H3.Cell.Edge edge in edges )
				{
					// Append to the file vs. writing out all at once because I was running out of memory otherwise.
					mesh = new Shapeways();
					int div;
					H3Models.Ball.LODThin( edge.Start, edge.End, out div );
					mesh.Div = div;
					H3.Util.AddToMeshInternal( mesh, edge.Start, edge.End );
					mesh.Mesh.Scale( settings.Scale );
					STL.AppendMeshToSTL( mesh.Mesh, outputFileName );
				}
			}
			else
			{
				outputFileName += ".pov";
				PovRay.WriteH3Edges( new PovRay.Parameters() 
					{ 
						AngularThickness = settings.AngularThickness,
						Halfspace = settings.Halfspace,
						ThinEdges = settings.ThinEdges,
					}, 
					edges.ToArray(), outputFileName );
			}
		}

		private class Params
		{
			public Params() { Setup(); }
			public int Iterations;
			public int MaxTiles;
			public double UhsCutoff;
			public double BallCutoff;
			public bool RemoveDangling;
			public H3.Output Output = H3.Output.POVRay;

			private void Setup()
			{
				//if( Output == H3.Output.STL )
				if( true )
				{
					Iterations = 2;
					MaxTiles = 500;
					UhsCutoff = 0.002;
					BallCutoff = 0.0158;
					RemoveDangling = true;
					//UhsCutoff = 0.02;
					//BallCutoff = 0.015;
					BallCutoff = 0.03;	// 363
				}
				else
				{
					Iterations = 10;
					MaxTiles = 750;
					UhsCutoff = 0.001;
					BallCutoff = 0.01;
					RemoveDangling = false;
				}
			}
		}
		private static Params m_params = new Params();

		public static bool IsExotic( EHoneycomb honeycomb )
		{
			return
				honeycomb == EHoneycomb.H444 ||
				honeycomb == EHoneycomb.H636 ||
				honeycomb == EHoneycomb.H363;
		}

		private static void GetPQ( EHoneycomb honeycomb, out int p, out int q )
		{
			int r;
			Honeycomb.PQR( honeycomb, out p, out q, out r );
		}

		private static void GetAssociatedTiling( EHoneycomb honeycomb, out Tiling tiling, out Tile baseTile )
		{
			int p, q;
			GetPQ( honeycomb, out p, out q );
			TilingConfig tilingConfig = new TilingConfig( p, q, maxTiles: m_params.MaxTiles );
			tiling = new Tiling();
			tiling.Generate( tilingConfig );

			baseTile = Tiling.CreateBaseTile( tilingConfig );
		}

		/// <summary>
		/// Helper to do one step of reflections.
		/// Returns a new list of region edges.
		/// </summary>
		private static List<H3.Cell.Edge> DoOneStep( List<H3.Cell.Edge> regionEdges, Tiling tiling, Circle region )
		{
			HashSet<H3.Cell.Edge> newEdges = new HashSet<H3.Cell.Edge>( new H3.Cell.EdgeEqualityComparer() );
			foreach( Tile tile in tiling.Tiles )
			{
				foreach( H3.Cell.Edge edge in regionEdges )
				{
					H3.Cell.Edge toAdd = null;
					if( !Tolerance.Zero( tile.Center.Abs() ) )
					{
						// Translate
						// The isometry is necessary for the 363, but seems to mess up 636
						Vector3D start = tile.Isometry.Apply( edge.Start );
						Vector3D end = tile.Isometry.Apply( edge.End );
						//Vector3D start = edge.Start + tile.Center;
						//Vector3D end = edge.End + tile.Center;

						// Reflect
						start = region.ReflectPoint( start );
						end = region.ReflectPoint( end );

						toAdd = new H3.Cell.Edge( start, end );
					}
					else
						toAdd = edge;

					if( EdgeOkUHS( toAdd, region ) )
						newEdges.Add( toAdd );
				}
			}

			return newEdges.ToList();
		}

		private static bool EdgeOkUHS( H3.Cell.Edge edge, Circle region )
		{
			if( Tolerance.GreaterThan( edge.Start.Abs(), region.Radius ) ||
				Tolerance.GreaterThan( edge.End.Abs(), region.Radius ) )
				return false;

			return EdgeOk( edge, m_params.UhsCutoff );
		}

		private static bool EdgeOkBall( H3.Cell.Edge edge )
		{
			return EdgeOk( edge, m_params.BallCutoff );
		}

		private static bool EdgeOk( H3.Cell.Edge edge, double cutoff )
		{
			return edge.Start.Dist( edge.End ) > cutoff;
		}

		private static List<H3.Cell.Edge> CopyAndProject( List<H3.Cell.Edge> regionEdges, Tiling tiling, double scale, Vector3D offset )
		{
			HashSet<H3.Cell.Edge> newEdges = new HashSet<H3.Cell.Edge>( new H3.Cell.EdgeEqualityComparer() );
			//foreach( Tile tile in tiling.Tiles )	// Needed for doing full ball (rather than just half of it)
			Tile tile = tiling.Tiles.First();
			{
				foreach( H3.Cell.Edge edge in regionEdges )
				{
					// Translation
					// The isometry is necessary for the 363, but seems to mess up 636
					Vector3D start = tile.Isometry.Apply( edge.Start ) + offset;
					Vector3D end = tile.Isometry.Apply( edge.End ) + offset;
					//Vector3D start = edge.Start + tile.Center + offset;
					//Vector3D end = edge.End + tile.Center + offset;

					// Scaling
					start *= scale;
					end *= scale;

					// Projections
					start = H3Models.UHSToBall( start );
					end = H3Models.UHSToBall( end );

					H3.Cell.Edge transformed = new H3.Cell.Edge( start, end );
					if( EdgeOkBall( transformed ) )
						newEdges.Add( transformed );
				}
			}

			return newEdges.ToList();
		}

		private static double FaceCenteredScale( Circle vertexCircle )
		{
			// The radius is the height of the face in UHS.
			// We need to scale this to be at (0,0,1)
			return 1.0 / vertexCircle.Radius;
		}

		private static double EdgeCenteredScale( Tile baseTile )
		{
			Segment seg = baseTile.Boundary.Segments[0];
			return 1.0 / ( seg.Length / 2 );
		}
	}
}
