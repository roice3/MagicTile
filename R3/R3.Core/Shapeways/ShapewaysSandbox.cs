namespace R3.Geometry
{
	using R3.Core;
	using R3.Drawing;
	using System.Collections.Generic;
	using System.Linq;
	using Math = System.Math;

	public static class ShapewaysSandbox
	{
		public static void GenPolyhedron()
		{
			Tiling tiling;
			int p = 3;
			int q = 6;
			GetAssociatedTiling( p, q, 5000, out tiling );

			double overallScale = 12.5;	// 2.5 cm = 1 in diameter

			Shapeways mesh = new Shapeways();
			foreach( Tile tile in tiling.Tiles )
			foreach( Segment seg in tile.Boundary.Segments )
			{
				double tilingScale = 0.75;
				seg.Scale( new Vector3D(), tilingScale );

				Vector3D v1 = Sterographic.PlaneToSphereSafe( seg.P1 );
				Vector3D v2 = Sterographic.PlaneToSphereSafe( seg.P2 );
				//if( v1.Dist( v2 ) < 0.01 )
				//	continue;
				if( SphericalCoords.CartesianToSpherical( v1 ).Y < Math.PI / 12 &&
					SphericalCoords.CartesianToSpherical( v2 ).Y < Math.PI / 12 )
					continue;

				double dist = v1.Dist( v2 );
				int divisions = 2 + (int)( dist * 20 );

				Vector3D[] points = seg.Subdivide( divisions );
				points = points.Select( v => Sterographic.PlaneToSphereSafe( v ) ).ToArray();
				mesh.AddCurve( points, v => SizeFunc( v, overallScale ) );
			}

			mesh.Mesh.Scale( overallScale );

			string outputFileName = @"d:\temp\" + p + q + ".stl";
			STL.SaveMeshToSTL( mesh.Mesh, outputFileName );
		}

		private static void GetAssociatedTiling( int p, int q, int maxTiles, out Tiling tiling )
		{
			TilingConfig tilingConfig = new TilingConfig( p, q, maxTiles: maxTiles );
			tiling = new Tiling();
			tiling.GenerateInternal( tilingConfig, p == 6 ? Polytope.Projection.VertexCentered : Polytope.Projection.FaceCentered );
		}

		private static double SizeFunc( Vector3D v, double overallScale )
		{
			//return .6 / 2 / overallScale;

			// Silver min wall is 0.6
			// Silver min wire is 0.8 (supported) or 1.0 (unsupported).

			double min = 0.55 / 2;
			double max = 1.5 / 2;
			
			// for caps
			//double min = 0.71 / 2;
			//double max = 0.5 / 2;	// 36
			//double max = 0.35 / 2; // 63

			Vector3D s = SphericalCoords.CartesianToSpherical( v );
			double angle = s.Y / Math.PI;		// angle 0 to 1
			double result = min + ( max - min ) * angle;
			return result / overallScale;
		}

		public static void GenCapWithHole()
		{
			Shapeways mesh = new Shapeways();

			double overallScale = 12.5;	// 2.5 cm = 1 in diameter

			// Make hole 2 mm
			double startAngle = Math.Asin( 2.0 / 2 / overallScale );
			double endAngle = Math.PI / 8;	// Slightly larger than hole above.

			int div = 75;
			double angleInc = (endAngle - startAngle) / div;
			for( int i=0; i<div; i++ )
			{
				double angle1 = startAngle + angleInc * i;
				double angle2 = startAngle + angleInc * (i+1);
				Vector3D s1 = new Vector3D( 0, 0, 1 ), s2 = new Vector3D( 0, 0, 1 );
				s1.RotateAboutAxis( new Vector3D( 0, 1, 0 ), angle1 );
				s2.RotateAboutAxis( new Vector3D( 0, 1, 0 ), angle2 );

				Vector3D p1 = s1 * (1 + SizeFunc( s1, overallScale ));
				Vector3D p2 = s2 * (1 + SizeFunc( s2, overallScale ));
				Vector3D n1 = s1 * (1 - SizeFunc( s1, overallScale ));
				Vector3D n2 = s2 * (1 - SizeFunc( s2, overallScale ));

				List<Vector3D> pointsP1 = new List<Vector3D>();
				List<Vector3D> pointsP2 = new List<Vector3D>();
				List<Vector3D> pointsN1 = new List<Vector3D>();
				List<Vector3D> pointsN2 = new List<Vector3D>();
				for( int j=0; j<div; j++ )
				{
					pointsP1.Add( p1 );
					pointsP2.Add( p2 );
					pointsN1.Add( n1 );
					pointsN2.Add( n2 );
					p1.RotateAboutAxis( new Vector3D( 0, 0, 1 ), 2 * Math.PI / div );
					p2.RotateAboutAxis( new Vector3D( 0, 0, 1 ), 2 * Math.PI / div );
					n1.RotateAboutAxis( new Vector3D( 0, 0, 1 ), 2 * Math.PI / div );
					n2.RotateAboutAxis( new Vector3D( 0, 0, 1 ), 2 * Math.PI / div );
				}

				mesh.AddSegment( pointsP1.ToArray(), pointsP2.ToArray() );
				mesh.AddSegment( pointsN2.ToArray(), pointsN1.ToArray() );
				if( i == 0 )
					mesh.AddSegment( pointsN1.ToArray(), pointsP1.ToArray() );
				if( i == div - 1 )
					mesh.AddSegment( pointsP2.ToArray(), pointsN2.ToArray() );
			}

			mesh.Mesh.Scale( overallScale );

			string outputFileName = @"d:\temp\cap_with_hole.stl";
			STL.SaveMeshToSTL( mesh.Mesh, outputFileName );
		}

		public static void GenCap()
		{
			double overallScale = 12.5;	// 2.5 cm = 1 in diameter

			Vector3D start = new Vector3D( 0, 0, 1 );
			start.RotateAboutAxis( new Vector3D( 0, 1, 0 ), Math.PI / 11.5 );	// Slightly larger than hole above.

			List<Vector3D> circlePoints = new List<Vector3D>();
			int div = 30;
			for( int i=0; i<div; i++ )
			{
				circlePoints.Add( start );
				start.RotateAboutAxis( new Vector3D( 0, 0, 1 ), 2 * Math.PI / div );
			}

			Polygon poly = Polygon.FromPoints( circlePoints.ToArray() );
			Vector3D[] texCoords = TextureHelper.TextureCoords( poly, Geometry.Euclidean );
			int[] elements = TextureHelper.TextureElements( poly.NumSides, LOD: 3 );
			
			Shapeways mesh = new Shapeways();
			for( int i = 0; i < elements.Length / 3; i++ )
			{
				int idx1 = i * 3;
				int idx2 = i * 3 + 1;
				int idx3 = i * 3 + 2;
				Vector3D v1 = texCoords[elements[idx1]];
				Vector3D v2 = texCoords[elements[idx2]];
				Vector3D v3 = texCoords[elements[idx3]];
				v1.Normalize();
				v2.Normalize();
				v3.Normalize();

				Vector3D v1p = v1 * ( 1 + SizeFunc( v1, overallScale ) );
				Vector3D v2p = v2 * ( 1 + SizeFunc( v2, overallScale ) );
				Vector3D v3p = v3 * ( 1 + SizeFunc( v3, overallScale ) );
				Vector3D v1n = v1 * ( 1 - SizeFunc( v1, overallScale ) );
				Vector3D v2n = v2 * ( 1 - SizeFunc( v2, overallScale ) );
				Vector3D v3n = v3 * ( 1 - SizeFunc( v3, overallScale ) );

				mesh.Mesh.Triangles.Add( new Mesh.Triangle( v1p, v2p, v3p ) );
				mesh.Mesh.Triangles.Add( new Mesh.Triangle( v1n, v3n, v2n ) );

				// To make it manifold.
				// 64 elements per seg.
				int relativeToSeg = i % 64;
				if( relativeToSeg < 8 )
				{
					mesh.Mesh.Triangles.Add( new Mesh.Triangle( v1p, v1n, v2p ) );
					mesh.Mesh.Triangles.Add( new Mesh.Triangle( v2p, v1n, v2n ) );
				}
			}

			mesh.Mesh.Scale( overallScale );

			string outputFileName = @"d:\temp\cap.stl";
			STL.SaveMeshToSTL( mesh.Mesh, outputFileName );
		}
	}
}
