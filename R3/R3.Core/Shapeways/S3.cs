namespace R3.Geometry
{
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.Linq;
	using Math = System.Math;

	public class S3
	{
		private static void HopfFibration( Tiling tiling )
		{
			int segDivisions = 10;
			int circleDivisions = 125;
			Shapeways mesh = new Shapeways();

			HashSet<Vector3D> done = new HashSet<Vector3D>();
			foreach( Tile tile in tiling.Tiles )
				foreach( Segment seg in tile.Boundary.Segments )
				{
					if( done.Contains( seg.Midpoint ) )
						continue;

					// Subdivide the segment, and project points to S2.
					Vector3D[] points = seg.Subdivide( segDivisions ).Select( v => Spherical2D.PlaneToSphere( v ) ).ToArray();
					foreach( Vector3D point in points )
					{
						// Get the hopf circle and add to mesh.
						// http://en.wikipedia.org/wiki/Hopf_fibration#Explicit_formulae
						double a = point.X;
						double b = point.Y;
						double c = point.Z;
						double factor = 1 / ( Math.Sqrt( 1 + c ) );
						if( Tolerance.Equal( c, -1 ) )
							continue;

						List<Vector3D> circlePoints = new List<Vector3D>();
						double angleInc = 2 * Math.PI / circleDivisions;
						double angle = 0;
						for( int i = 0; i <= circleDivisions; i++ )
						{
							double sinTheta = Math.Sin( angle );
							double cosTheta = Math.Cos( angle );
							circlePoints.Add( new Vector3D(
								( 1 + c ) * cosTheta,
								a * sinTheta - b * cosTheta,
								a * cosTheta + b * sinTheta,
								( 1 + c ) * sinTheta ) );

							angle += angleInc;
						}

						bool shrink = false;
						ProjectAndAddS3Points( mesh, circlePoints.ToArray(), shrink );
					}

					done.Add( seg.Midpoint );
				}

			STL.SaveMeshToSTL( mesh.Mesh, @"D:\p4\R3\sample\out1.stl" );
		}

		private static void ShapewaysPolytopes()
		{
			VEF loader = new VEF();
			loader.Load( @"C:\Users\roice\Documents\projects\vZome\VefProjector\data\24cell-cellFirst.vef" );

			int divisions = 25;

			Shapeways mesh = new Shapeways();
			//int count = 0;
			foreach( Edge edge in loader.Edges )
			{
				Segment seg = Segment.Line(
					loader.Vertices[edge.V1].ConvertToReal(),
					loader.Vertices[edge.V2].ConvertToReal() );
				Vector3D[] points = seg.Subdivide( divisions );

				bool shrink = true;
				ProjectAndAddS3Points( mesh, points, shrink );

				//if( count++ > 10 )
				//	break;
			}

			STL.SaveMeshToSTL( mesh.Mesh, @"D:\p4\R3\sample\out1.stl" );
		}

		/// <summary>
		/// Helper to project points from S3 -> S2, then add an associated curve.
		/// </summary>
		private static void ProjectAndAddS3Points( Shapeways mesh, Vector3D[] pointsS3, bool shrink )
		{
			// Project to S3, then to R3.
			List<Vector3D> projected = new List<Vector3D>();
			foreach( Vector3D v in pointsS3 )
			{
				v.Normalize();
				Vector3D c = v.ProjectTo3DSafe( 1.0 );

				// Pull R3 into a smaller open disk.
				if( shrink )
				{
					double mag = Math.Atan( c.Abs() );
					c.Normalize();
					c *= mag;
				}

				projected.Add( c );
			}

			System.Func<Vector3D, double> sizeFunc = v =>
			{
				// Constant thickness.
				// return 0.08;

				double sphericalThickness = 0.002;

				double abs = v.Abs();
				if( shrink )
					abs = Math.Tan( abs );	// The unshrunk abs.

				// The thickness at this vector location.
				double result = Spherical2D.s2eNorm( Spherical2D.e2sNorm( abs ) + sphericalThickness ) - abs;

				if( shrink )
					result *= Math.Atan( abs ) / abs;	// shrink it back down.

				return result;
			};

			mesh.AddCurve( projected.ToArray(), sizeFunc );
		}

	}
}
