namespace R3.Geometry
{
	using R3.Drawing;
	using R3.Math;
	using System.Collections.Generic;
	using System.Linq;
	using System.Diagnostics;

	public class Mesh
	{
		public Mesh()
		{
			Triangles = new List<Triangle>();
		}

		public struct Triangle
		{
			public Triangle( Vector3D _a, Vector3D _b, Vector3D _c ) { a = _a; b = _b; c = _c; color = new Vector3D(1,1,1); }
			public Vector3D a;
			public Vector3D b;
			public Vector3D c;

			// The reason we use a vector here is so the components 
			// can be interpreted in different color schemes (HLS, RGB, etc.)
			public Vector3D color;

			public Vector3D Normal
			{
				get
				{
					return (b - a).Cross( c - a );
				}
			}
		}

		public List<Triangle> Triangles { get; set; }

		public void BuildIndexes( out Vector3D[] verts, out Vector3D[] normals, out List<int[]> faces )
		{
			Dictionary<Vector3D,int> vertMap = new Dictionary<Vector3D,int>();
			Dictionary<Vector3D, List<Triangle>> triMap = new Dictionary<Vector3D, List<Triangle>>();

			int current = 0;
			foreach( Triangle tri in Triangles )
			{
				int idx;
				if( !vertMap.TryGetValue( tri.a, out idx ) )
					vertMap[tri.a] = current++;
				if( !vertMap.TryGetValue( tri.b, out idx ) )
					vertMap[tri.b] = current++;
				if( !vertMap.TryGetValue( tri.c, out idx ) )
					vertMap[tri.c] = current++;

				List<Triangle> list;
				if( !triMap.TryGetValue( tri.a, out list ) )
					triMap[tri.a] = list = new List<Triangle>();
				list.Add( tri );
				if( !triMap.TryGetValue( tri.b, out list ) )
					triMap[tri.b] = list = new List<Triangle>();
				list.Add( tri );
				if( !triMap.TryGetValue( tri.c, out list ) )
					triMap[tri.c] = list = new List<Triangle>();
				list.Add( tri );
			}

			List<Vector3D> _verts = new List<Vector3D>();
			List<Vector3D> _normals = new List<Vector3D>();
			foreach( var kvp in vertMap )
			{
				Vector3D v = kvp.Key;
				_verts.Add( v );

				Vector3D normal = new Vector3D();
				List<Triangle> tris = triMap[v];
				foreach( Triangle tri in tris )
					normal += tri.Normal;
				normal /= tris.Count;
				_normals.Add( normal );
			}
			verts = _verts.ToArray();
			normals = _normals.ToArray();

			faces = new List<int[]>();
			foreach( Triangle tri in Triangles )
				faces.Add( new int[] { vertMap[tri.a], vertMap[tri.b], vertMap[tri.c] } );
		}

		public Mesh Clone()
		{
			Mesh clone = new Mesh();
			clone.Triangles = Triangles.Select( t => t ).ToList();
			return clone;
		}

		/// <summary>
		/// Scale our mesh (useful for shapeways models)
		/// </summary>
		public void Scale( double scale )
		{
			for( int i=0; i<Triangles.Count; i++ )
			{
				Triangles[i] = new Mesh.Triangle(
					Triangles[i].a * scale,
					Triangles[i].b * scale,
					Triangles[i].c * scale );
			}
		}

		public void Rotate( double angle )
		{
			for( int i = 0; i < Triangles.Count; i++ )
			{
				Vector3D a = Triangles[i].a;
				Vector3D b = Triangles[i].b;
				Vector3D c = Triangles[i].c;
				a.RotateXY( angle );
				b.RotateXY( angle );
				c.RotateXY( angle );

				Triangles[i] = new Mesh.Triangle( a, b, c );
			}
		}

		/// <summary>
		/// Make an edge mesh of a regular tiling.
		/// </summary>
		public static Mesh MakeEdgeMesh( int p, int q )
		{
			Mesh mesh = new Mesh();

			int maxTiles = 400;

			Tiling tiling = new Tiling();
			TilingConfig config = new TilingConfig( p, q, maxTiles: maxTiles );
			config.Shrink = 0.6;
			tiling.GenerateInternal( config );

			TilingConfig boundaryConfig = new TilingConfig( 14, 7, maxTiles: 1 );
			boundaryConfig.Shrink = 1.01;
			Tile boundary = Tiling.CreateBaseTile( boundaryConfig );

			AddSymmetryTriangles( mesh, tiling, boundary.Drawn );
			//AddSymmetryTriangles( mesh, tiling, null );
			return mesh;

			HashSet<Vector3D> completed = new HashSet<Vector3D>();
			int count = 0;
			foreach( Tile tile in tiling.Tiles )
			{
				MeshEdges( mesh, tile, completed, null );
				count++;
				if( count >= maxTiles )
					break;
			}

			return mesh;
		}

		private static int m_divisions = 75;

		private static Vector3D Shrink( Vector3D p, Vector3D centroid )
		{
			Vector3D temp = p - centroid;
			temp.Normalize();
			p += temp * 0.001;
			return p;
		}

		private static void AddSymmetryTriangles( Mesh mesh, Tiling tiling, Polygon boundary )
		{
			// Assume template centered at the origin.
			Polygon template = tiling.Tiles.First().Boundary;
			List<Triangle> templateTris = new List<Triangle>();
			foreach( Segment seg in template.Segments )
			{
				int num = 1 + (int)(seg.Length * m_divisions);

				Vector3D a = new Vector3D();
				Vector3D b = seg.P1;
				Vector3D c = seg.Midpoint;
				Vector3D centroid = ( a + b + c ) / 3;

				Polygon poly = new Polygon();
				Segment segA = Segment.Line( new Vector3D(), seg.P1 );
				Segment segB = seg.Clone();
				segB.P2 = seg.Midpoint;
				Segment segC = Segment.Line( seg.Midpoint, new Vector3D() );
				poly.Segments.Add( segA );
				poly.Segments.Add( segB );
				poly.Segments.Add( segC );

				Vector3D[] coords = TextureHelper.TextureCoords( poly, Geometry.Hyperbolic );
				int[] elements = TextureHelper.TextureElements( 3, LOD: 3 );
				for( int i = 0; i < elements.Length / 3; i++ )
				{
					int idx1 = i * 3;
					int idx2 = i * 3 + 1;
					int idx3 = i * 3 + 2;
					Vector3D v1 = coords[elements[idx1]];
					Vector3D v2 = coords[elements[idx2]];
					Vector3D v3 = coords[elements[idx3]];
					templateTris.Add( new Triangle( v1, v2, v3 ) );
				}

				/*

				// Need to shrink a little, so we won't
				// get intersections among neighboring faces.
				a = Shrink( a, centroid );
				b = Shrink( b, centroid );
				c = Shrink( c, centroid );

				Vector3D[] list = seg.Subdivide( num * 2 );
				list[0] = b;
				list[list.Length / 2] = c;
				for( int i = 0; i < list.Length / 2; i++ )
					templateTris.Add( new Triangle( centroid, list[i], list[i + 1] ) );

				for( int i = num - 1; i >= 0; i-- )
					templateTris.Add( new Triangle( centroid, a + (c - a) * (i + 1) / num, a + (c - a) * i / num ) );

				for( int i = 0; i < num; i++ )
					templateTris.Add( new Triangle( centroid, a + (b - a) * i / num, a + (b - a) * (i + 1) / num ) );
				*/
			}

			foreach( Tile tile in tiling.Tiles )
			{
				Vector3D a = tile.Boundary.Segments[0].P1;
				Vector3D b = tile.Boundary.Segments[1].P1;
				Vector3D c = tile.Boundary.Segments[2].P1;

				Mobius m = new Mobius();
				if( tile.Isometry.Reflected )
					m.MapPoints( template.Segments[0].P1, template.Segments[1].P1, template.Segments[2].P1, c, b, a );
				else
					m.MapPoints( template.Segments[0].P1, template.Segments[1].P1, template.Segments[2].P1, a, b, c );

				foreach( Triangle tri in templateTris )
				{
					Triangle transformed = new Triangle(
						m.Apply( tri.a ),
						m.Apply( tri.b ),
						m.Apply( tri.c ) );
					CheckAndAdd( mesh, transformed, boundary );
				}
			}
		}

		private static void MeshEdges( Mesh mesh, Tile tile, HashSet<Vector3D> completed, Polygon boundary )
		{
			for( int i=0; i<tile.Boundary.Segments.Count; i++ )
			{
				Segment boundarySeg = tile.Boundary.Segments[i];
				Segment d1 = tile.Drawn.Segments[i];
				if( completed.Contains( boundarySeg.Midpoint ) )
					continue;

				// Find the incident segment.
				Segment seg2 = null, d2 = null;
				foreach( Tile incident in tile.EdgeIncidences )
				{
					for( int j=0; j<incident.Boundary.Segments.Count; j++ )
					{
						if( boundarySeg.Midpoint == incident.Boundary.Segments[j].Midpoint )
						{
							seg2 = incident.Boundary.Segments[j];
							d2 = incident.Drawn.Segments[j];
							break;
						}
					}

					if( seg2 != null )
						break;
				}

				// Found our incident edge?
				bool foundIncident = seg2 != null;
				if( !foundIncident )
					seg2 = d2 = boundarySeg;

				// Do the endpoints mismatch?
				if( boundarySeg.P1 != seg2.P1 )
				{
					Segment clone = d2.Clone();
					clone.Reverse();
					d2 = clone;
				}

				// Add the two vertices (careful of orientation).
				if( foundIncident )
				{
					CheckAndAdd( mesh, new Mesh.Triangle( boundarySeg.P1, d1.P1, d2.P1 ), boundary );
					CheckAndAdd( mesh, new Mesh.Triangle( boundarySeg.P2, d2.P2, d1.P2 ), boundary );
				}

				int num = 1 + (int)(d1.Length * m_divisions);
				Vector3D[] list1 = d1.Subdivide( num );
				Vector3D[] list2 = d2.Subdivide( num );
				for( int j=0; j<num; j++ )
				{
					CheckAndAdd( mesh, new Mesh.Triangle( list1[j], list1[j + 1], list2[j + 1] ), boundary );
					CheckAndAdd( mesh, new Mesh.Triangle( list2[j], list1[j], list2[j + 1] ), boundary );
				}

				completed.Add( boundarySeg.Midpoint );
			}
		}

		private static bool Check( Triangle tri, Polygon boundary )
		{
			if( boundary == null ||
				(boundary.IsPointInside( tri.a ) &&
				boundary.IsPointInside( tri.b ) &&
				boundary.IsPointInside( tri.c )) )
				return true;
			return false;
		}

		private static void CheckAndAdd( Mesh mesh, Triangle tri, Polygon boundary )
		{
			if( Check( tri, boundary ) )
				mesh.Triangles.Add( tri );
		}
	}
}
