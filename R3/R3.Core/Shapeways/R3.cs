namespace R3.Geometry
{
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Math = System.Math;

	// NOTE: Wanted to name this class R3 (parallel to H3/S3), but namespace problems happened.
	internal class Euclidean
	{
		public static void GenEuclidean()
		{
			Shapeways mesh = new Shapeways();
			HashSet<H3.Cell.Edge> completed = new HashSet<H3.Cell.Edge>();

			int count = 5;
			for( int i = -count; i < count; i++ )
				for( int j = -count; j < count; j++ )
					for( int k = -count; k < count; k++ )
					{
						// Offset
						double io = i + 0.5;
						double jo = j + 0.5;
						double ko = k + 0.5;

						// Do every edge emanating from this point.
						AddEuclideanEdge( mesh, completed, new Vector3D( io, jo, ko ), new Vector3D( io + 1, jo, ko ) );
						AddEuclideanEdge( mesh, completed, new Vector3D( io, jo, ko ), new Vector3D( io - 1, jo, ko ) );
						AddEuclideanEdge( mesh, completed, new Vector3D( io, jo, ko ), new Vector3D( io, jo + 1, ko ) );
						AddEuclideanEdge( mesh, completed, new Vector3D( io, jo, ko ), new Vector3D( io, jo - 1, ko ) );
						AddEuclideanEdge( mesh, completed, new Vector3D( io, jo, ko ), new Vector3D( io, jo, ko + 1 ) );
						AddEuclideanEdge( mesh, completed, new Vector3D( io, jo, ko ), new Vector3D( io, jo, ko - 1 ) );
					}

			STL.SaveMeshToSTL( mesh.Mesh, "d:\\temp\\434.stl" );
		}

		private static void AddEuclideanEdge( Shapeways mesh, HashSet<H3.Cell.Edge> completed, Vector3D start, Vector3D end )
		{
			H3.Cell.Edge edge = new H3.Cell.Edge( start, end );
			if( completed.Contains( edge ) )
				return;

			Shapeways tempMesh = new Shapeways();
			Segment seg = Segment.Line( start, end );

			int div = 20 - (int)(start.Abs() * 4);
			if( div < 1 )
				div = 1;

			tempMesh.AddCurve( seg.Subdivide( div ), .05 );
			Transform( tempMesh.Mesh );

			mesh.Mesh.Triangles.AddRange( tempMesh.Mesh.Triangles );
			completed.Add( edge );
		}

		private static void Transform( Mesh mesh )
		{
			Sphere sphere = new Sphere();
			sphere.Radius = 0.1;
			for( int i = 0; i < mesh.Triangles.Count; i++ )
			{
				mesh.Triangles[i] = new Mesh.Triangle(
					sphere.ReflectPoint( mesh.Triangles[i].a ),
					sphere.ReflectPoint( mesh.Triangles[i].b ),
					sphere.ReflectPoint( mesh.Triangles[i].c ) );
			}
		}
	}
}
