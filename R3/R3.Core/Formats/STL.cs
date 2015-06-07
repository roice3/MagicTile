namespace R3.Core
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	using R3.Geometry;
	using System.IO;

	public class STL
	{
		public static void SaveMeshToSTL( Mesh mesh, String fileName ) 
		{
			using( StreamWriter sw = File.CreateText( fileName ) )
				WriteInternal( mesh, sw );
		}

		public static void AppendMeshToSTL( Mesh mesh, String fileName )
		{
			using( StreamWriter sw = File.AppendText( fileName ) )
				WriteInternal( mesh, sw );
		}

		private static void WriteInternal( Mesh mesh, StreamWriter sw )
		{
			sw.WriteLine( "solid" );
			foreach( Mesh.Triangle tri in mesh.Triangles )
				WriteTriangle( sw, tri );
			sw.WriteLine( "endsolid" );
			sw.Close();
		}

		private static void WriteTriangle( StreamWriter sw, Mesh.Triangle tri )
		{
			Vector3D v1 = tri.b - tri.a;
			Vector3D v2 = tri.c - tri.a;

			// See http://en.wikipedia.org/wiki/STL_format#The_Facet_Normal
			// about how to do the normals correctly.
			//Vector3D n = v1.Cross( v2 );
			Vector3D n = new Vector3D( 0, 0, 1 );
			n.Normalize();

			sw.WriteLine( "  facet normal {0:e6} {1:e6} {2:e6}", n.X, n.Y, n.Z );
			sw.WriteLine( "    outer loop" );
			sw.WriteLine( "      vertex {0:e6} {1:e6} {2:e6}", tri.a.X, tri.a.Y, tri.a.Z );
			sw.WriteLine( "      vertex {0:e6} {1:e6} {2:e6}", tri.b.X, tri.b.Y, tri.b.Z );
			sw.WriteLine( "      vertex {0:e6} {1:e6} {2:e6}", tri.c.X, tri.c.Y, tri.c.Z );
			sw.WriteLine( "    endloop" );
			sw.WriteLine( "  endfacet" );

			/*
				"  facet normal {0:e6} {1:e6} {2:e6}" -f $n
				"    outer loop"
				"      vertex {0:e6} {1:e6} {2:e6}" -f $v1
				"      vertex {0:e6} {1:e6} {2:e6}" -f $v2
				"      vertex {0:e6} {1:e6} {2:e6}" -f $v3
				"    endloop"
				"  endfacet"

			  facet normal 0.000000e+000 0.000000e+000 -1.000000e+000
				outer loop
				  vertex 4.500000e-001 4.500000e-001 4.500000e-001
				  vertex 4.500000e-001 7.500000e-001 4.500000e-001
				  vertex 7.500000e-001 7.500000e-001 4.500000e-001
				endloop
			  endfacet
			*/
		}
	}
}
