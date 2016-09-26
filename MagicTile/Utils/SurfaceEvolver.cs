namespace MagicTile.Utils
{
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using R3.Algorithm;
	using R3.Math;
	using R3.Geometry;
	using R3.Core;

	internal class SurfaceEvolver
	{
		public SurfaceEvolver( Puzzle p )
		{
			m_puzzle = p;
		}

		private Puzzle m_puzzle;

		internal struct SurfaceVertex
		{
			public SurfaceVertex( Vector3D v ) : this()
			{
				Vertex = v;
			}

			public int Index { get; set; }
			public Vector3D Vertex { get; set; }

			public string Write()
			{
				return string.Format( CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Index, Vertex.X, Vertex.Y, Vertex.Z );
			}

		}

		internal struct SurfaceEdge
		{
			public SurfaceEdge( int v1, int v2 ) : this()
			{
				V1 = v1;
				V2 = v2;
			}

			public int Index { get; set; }
			public int V1 { get; set; }
			public int V2 { get; set; }

			public string Write()
			{
				return string.Format( CultureInfo.InvariantCulture, "{0} {1} {2}", Index, V1, V2 );
			}

			public static bool Reversed( SurfaceEdge e1, SurfaceEdge e2 )
			{
				return e1.V1 == e2.V2 && e1.V2 == e2.V1;
			}
		}

		internal struct SurfaceFace
		{
			public SurfaceFace( int e1, int e2, int e3 ) : this()
			{
				E1 = e1; E2 = e2; E3 = e3;
			}

			public int Index { get; set; }
			public int E1 { get; set; }
			public int E2 { get; set; }
			public int E3 { get; set; }
			public bool Reverse { get; set; }

			public string Write()
			{
				return string.Format( CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Index, E1, E2, E3 );
			}
		}

		/// <summary>
		/// Doesn't compare the indices.
		/// </summary>
		private class SurfaceVertexEqualityComparer : IEqualityComparer<SurfaceVertex>
		{
			public SurfaceVertexEqualityComparer( Puzzle.Translation[] translations )
			{
				m_translations = translations;
			}

			private Puzzle.Translation[] m_translations;

			public bool Equals( SurfaceVertex v1, SurfaceVertex v2 )
			{
				double threshold = 0.001;
				if( v1.Vertex.Compare( v2.Vertex, threshold ) )
					return true;

				foreach( Puzzle.Translation trans in m_translations )
				{
					Vector3D offset = trans.m_translation;
					if( Tolerance.Zero( offset.Abs(), threshold ) )
						continue;

					Vector3D test = v1.Vertex + offset;
					if( test.Compare( v2.Vertex, threshold ) )
						return true;
				}

				return false;
			}

			public int GetHashCode( SurfaceVertex v )
			{
				return v.Vertex.GetHashCode();
			}
		}

		/// <summary>
		/// Doesn't compare the indices.
		/// Either direction is deemed the same.
		/// </summary>
		private class SurfaceEdgeEqualityComparer : IEqualityComparer<SurfaceEdge>
		{
			public bool Equals( SurfaceEdge e1, SurfaceEdge e2 )
			{
				return 
					(e1.V1 == e2.V1 && e1.V2 == e2.V2) ||
					(e1.V1 == e2.V2 && e1.V2 == e2.V1);
			}

			public int GetHashCode( SurfaceEdge e )
			{
				return e.V1.GetHashCode() ^ e.V2.GetHashCode();
			}
		}

		internal class Surface
		{
			public Surface()
			{			
				Vertices = new List<SurfaceVertex>();
				Edges = new List<SurfaceEdge>();
				Faces = new List<SurfaceFace>();
			}

			public List<SurfaceVertex> Vertices { get; set; }
			public List<SurfaceEdge> Edges { get; set; }
			public List<SurfaceFace> Faces { get; set; }

			public void Fill( Puzzle puzzle )
			{
				Vertices.Clear();
				Edges.Clear();
				Faces.Clear();

				int baseV = 1;
				int baseE = 1;
				int baseF = 1;
				foreach( Cell cell in puzzle.IRPCells )
				{
					Vector3D[] verts = cell.TextureVertices;
					for( int i = 0; i < verts.Length; i++ )
					{
						SurfaceVertex sv = new SurfaceVertex( verts[i] );
						sv.Index = baseV + i;
						Vertices.Add( sv );
					}

					const int lod = 3;
					int[] facets = TextureHelper.TextureElements( cell.Boundary.NumSides, lod );
					for( int i = 0; i < facets.Length; i += 3 )
					{
						SurfaceEdge e1 = new SurfaceEdge( baseV + facets[i], baseV + facets[i + 1] );
						SurfaceEdge e2 = new SurfaceEdge( baseV + facets[i + 1], baseV + facets[i + 2] );
						SurfaceEdge e3 = new SurfaceEdge( baseV + facets[i + 2], baseV + facets[i] );
						e1.Index = baseE + i;
						e2.Index = baseE + i + 1;
						e3.Index = baseE + i + 2;
						Edges.Add( e1 );
						Edges.Add( e2 );
						Edges.Add( e3 );

						SurfaceFace face = new SurfaceFace( e1.Index, e2.Index, e3.Index );
						face.Index = baseF + i / 3;
						face.Reverse = puzzle.MasterCells[ cell.IndexOfMaster ].Reflected;	// Reflected doesn't seem to be set on the IRP cell.
						Faces.Add( face );
					}

					baseV += verts.Length;
					baseE += facets.Length;
					baseF += facets.Length / 3;
				}
			}

			/// <summary>
			/// Maps original vertices to surface with dups removed.
			/// </summary>
			public Dictionary<int, int> m_vertexMap = new Dictionary<int, int>();

			public Surface RemoveDups( Puzzle puzzle )
			{
				// ZZZ - Clean up all the +1,-1s in this method.  They are here because SurfaceEvolver is 1-indexed, but it is ugly!

				Surface newSurface = new Surface();

				m_vertexMap.Clear();
				Dictionary<SurfaceVertex, int> vMap = new Dictionary<SurfaceVertex, int>( new SurfaceVertexEqualityComparer( puzzle.IRPTranslations.ToArray() ) );
				Dictionary<SurfaceEdge, int> eMap = new Dictionary<SurfaceEdge, int>( new SurfaceEdgeEqualityComparer() );

				// Remove dups by adding to a map.
				foreach( SurfaceVertex v in Vertices )
				{
					int index;
					if( !vMap.TryGetValue( v, out index ) )
					{
						index = newSurface.Vertices.Count + 1;
						SurfaceVertex newV = v;
						newV.Index = index;
						newSurface.Vertices.Add( newV );
						vMap[v] = index;
					}

					m_vertexMap[v.Index] = index;
				}

				// First map to new verts.
				List<SurfaceEdge> newEdges = new List<SurfaceEdge>();
				foreach( SurfaceEdge e in Edges )
				{
					SurfaceEdge newEdge = new SurfaceEdge( vMap[Vertices[e.V1-1]], vMap[Vertices[e.V2-1]] );
					newEdge.Index = newEdges.Count + 1;
					newEdges.Add( newEdge );
				}

				// Remove dups by adding to a map.
				foreach( SurfaceEdge e in newEdges )
				{
					int index;
					if( !eMap.TryGetValue( e, out index ) )
					{
						index = newSurface.Edges.Count + 1;
						SurfaceEdge newE = e;
						newE.Index = index;
						newSurface.Edges.Add( newE );
						eMap[e] = index;
					}
				}

				// Map to new edges.
				// We don't need to worry about duplicate faces showing up.
				foreach( SurfaceFace f in Faces )
				{
					int e1 = eMap[newEdges[f.E1 - 1]];
					int e2 = eMap[newEdges[f.E2 - 1]];
					int e3 = eMap[newEdges[f.E3 - 1]];

					if( SurfaceEdge.Reversed( newSurface.Edges[e1 - 1], newEdges[f.E1 - 1] ) )
						e1 *= -1;
					if( SurfaceEdge.Reversed( newSurface.Edges[e2 - 1], newEdges[f.E2 - 1] ) )
						e2 *= -1;
					if( SurfaceEdge.Reversed( newSurface.Edges[e3 - 1], newEdges[f.E3 - 1] ) )
						e3 *= -1;

					SurfaceFace newFace = new SurfaceFace( e1, e2, e3 );
					newFace.Index = newSurface.Faces.Count + 1;
					newFace.Reverse = f.Reverse;
					newSurface.Faces.Add( newFace );
				}

				newSurface.m_vertexMap = m_vertexMap;
				return newSurface;
			}

			public void Write()
			{
				using( StreamWriter sw = new StreamWriter( @"C:\Evolver\fe\roice\test.fe" ) )
				{
					sw.WriteLine( "vertices" );
					Vertices.ForEach( v => sw.WriteLine( v.Write() ) );
					sw.WriteLine( string.Empty );

					sw.WriteLine( "edges" );
					Edges.ForEach( e => sw.WriteLine( e.Write() ) );
					sw.WriteLine( string.Empty );

					sw.WriteLine( "faces" );
					Faces.ForEach( f => sw.WriteLine( f.Write() + " density 0" ) );
					//Faces.ForEach( f => sw.WriteLine( f.Write() ) );
					sw.WriteLine( string.Empty );

					sw.WriteLine( "bodies" );
					sw.Write( "1 " );	// Always have 1
					{
						string line = "";
						int count = 0;
						foreach( SurfaceFace face in Faces )
						{
							if( face.Reverse )
								line += "-" + face.Index;
							else
								line += face.Index;
							count++;

							if( count >= 10 )
							{
								sw.WriteLine( line + @" \" );
								line = "";
								count = 0;
							}
							else
								line += " ";
						}

						sw.Write( line );
						//sw.Write( line += "volume 1" );
					}

					sw.WriteLine( string.Empty );
				}
			}

			public void NormalizeVerts()
			{
				Vector3D centroid = new Vector3D();
				double max = 0;
				foreach( SurfaceVertex v in Vertices )
				{
					centroid += v.Vertex / Vertices.Count;
					if( v.Vertex.Abs() > max )
						max = v.Vertex.Abs();
				}
				for( int i=0; i<Vertices.Count; i++ )
				{
					SurfaceVertex v = Vertices[i];
					v.Vertex = ( v.Vertex - centroid ) / max;	// center and scale
					Vertices[i] = v;
				}
			}
		}

		private static void Relax( Surface surface )
		{
			System.Random rand = new System.Random();

			Graph g = new Graph();
			foreach( SurfaceVertex v in surface.Vertices )
			{
				Vector3D v3d = new Vector3D( v.Vertex.X, v.Vertex.Y, v.Vertex.Z );
				//Vector3D v3d = new Vector3D( rand.NextDouble(), rand.NextDouble(), rand.NextDouble() );
				//v3d.Normalize();
				g.Nodes.Add( new GraphNode( new VectorND( new double[] { v3d.X, v3d.Y, v3d.Z } ), new VectorND( dimension: 3 ) ) );
			}
			foreach( SurfaceEdge e in surface.Edges )
				g.AddEdge( new Edge( e.V1-1, e.V2-1 ) );	// more 0-indexed vs. 1-indexed stuff

			GraphRelaxation relaxer = new GraphRelaxation();
			relaxer.Graph = g;
			relaxer.EdgeAttraction = 0.1;
			relaxer.NodeRepulsion = 0;
			relaxer.Relax( 50 );

			int index = 0;
			foreach( GraphNode node in relaxer.Graph.Nodes )
			{
				SurfaceVertex sv = surface.Vertices[index];
				sv.Vertex = new Vector3D( node.Position.X[0], node.Position.X[1], node.Position.X[2] );
				surface.Vertices[index] = sv;
				index++;
			}
		}

		private void UpdatePuzzleVerts( Surface newSurface )
		{
			int baseV = 1;
			foreach( Cell cell in m_puzzle.IRPCells )
			{
				Vector3D[] verts = cell.TextureVertices;
				for( int i=0; i<verts.Length; i++ )
				{
					int originalIndex = baseV + i;
					int newIndex;
					if( !newSurface.m_vertexMap.TryGetValue( originalIndex, out newIndex ) )
						throw new System.Exception( "noooo!" );

					verts[i] = newSurface.Vertices[newIndex-1].Vertex;
				}

				baseV += verts.Length;
			}
		}

		public void WriteFile()
		{
			Surface surface = new Surface();
			surface.Fill( m_puzzle );
			surface = surface.RemoveDups( m_puzzle );
			Relax( surface );
			surface.NormalizeVerts();
			surface.Write();
			UpdatePuzzleVerts( surface );
		}
	}
}
