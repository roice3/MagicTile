namespace R3.Drawing
{
	using R3.Geometry;
	using R3.Math;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using Math = System.Math;

	public class TextureHelper
	{
		public TextureHelper()
		{
			int levels = 3;
			m_maxSubdivisions = NumDiv( levels );
		}

		public TextureHelper( int levels )
		{
			m_maxSubdivisions = NumDiv( levels );
		}

		private static int NumDiv( int levels )
		{
			return (int)Math.Pow( 2, levels );
		}

		/// <summary>
		/// Stores the triangle element indices for different levels of detail.
		/// There are 4 entries in the list, and the first entry will have the least detail.
		/// The arrays specify indices into the texture coords, and represent triangle elements.
		/// </summary>
		public List<int[]> ElementIndices { get; set; }

		/// <summary>
		/// Sets up our list of element indices
		/// </summary>
		public void SetupElementIndices( Polygon poly )
		{
			ElementIndices = CalcElementIndices( poly, 3 );
		}

		public static List<int[]> CalcElementIndices( Polygon poly, int levels )
		{
			//int numBaseTriangles = poly.Segments.Count == 3 ? 1 : poly.Segments.Count;	// For geodesic saddles.
			int numBaseTriangles = poly.Segments.Count;

			int numDiv = NumDiv( levels );
			List<int[]> result = new List<int[]>();
			for( int i = 0; Math.Pow( 2, i ) <= numDiv; i++ )
				result.Add( TextureElements( numBaseTriangles, i, numDiv ) );
			return result;
		}

		private int m_maxSubdivisions = 8;	// Must be a power of 2.


		///////////////////////////////////////////////////////////////// PLAYING AROUND WITH GEODESIC SADDLES

		private static Vector3D[] CalcPointsUsingTwoSegments( Segment seg1, Segment seg2, int divisions, Geometry g )
		{
			List<Vector3D> points = new List<Vector3D>();
			Vector3D[] s1 = SubdivideSegmentInGeometry( seg1.P1, seg1.P2, divisions, g );
			Vector3D[] s2 = SubdivideSegmentInGeometry( seg2.P2, seg2.P1, divisions, g );
			for( int i = 0; i < divisions; i++ )
				points.AddRange( SubdivideSegmentInGeometry( s1[i], s2[i], divisions - i, g ) );

			points.Add( seg1.P2 );
			return points.ToArray();
		}

		private static Vector3D[] CalcPointsUsingRecursion( int level, Vector3D p1, Vector3D p2, Vector3D p3, Geometry g )
		{
			List<Vector3D> result = new List<Vector3D>();
			result.Add( p1 );
			result.Add( p2 );
			result.Add( p3 );

			if( level >= 3 )
				return result.ToArray();

			level++;

			int divisions = 2;
			Vector3D[] list1 = SubdivideSegmentInGeometry( p1, p2, divisions, g );
			Vector3D[] list2 = SubdivideSegmentInGeometry( p2, p3, divisions, g );
			Vector3D[] list3 = SubdivideSegmentInGeometry( p3, p1, divisions, g );

			result.AddRange( CalcPointsUsingRecursion( level, list1[0], list1[1], list3[1], g ) );
			result.AddRange( CalcPointsUsingRecursion( level, list1[1], list1[2], list2[1], g ) );
			result.AddRange( CalcPointsUsingRecursion( level, list1[1], list2[1], list3[1], g ) );
			result.AddRange( CalcPointsUsingRecursion( level, list2[1], list2[2], list3[1], g ) );

			return result.Distinct().ToArray();
		}

		private static Vector3D[] CalcViaProjections( Vector3D p1, Vector3D p2, Vector3D p3, int divisions, Geometry g )
		{
			Vector3D h1 = Sterographic.PlaneToHyperboloid( p1 );
			Vector3D h2 = Sterographic.PlaneToHyperboloid( p2 );
			Vector3D h3 = Sterographic.PlaneToHyperboloid( p3 );

			List<Vector3D> temp = new List<Vector3D>();
			Segment seg1 = Segment.Line( h1, h2 );
			Segment seg2 = Segment.Line( h3, h2 );
			Vector3D[] s1 = seg1.Subdivide( divisions );
			Vector3D[] s2 = seg2.Subdivide( divisions );
			for( int i = 0; i < divisions; i++ )
			{
				Segment seg = Segment.Line( s1[i], s2[i] );
				temp.AddRange( seg.Subdivide( divisions - i ) );
			}
			temp.Add( h2 );

			List<Vector3D> result = new List<Vector3D>();
			foreach( Vector3D v in temp )
			{
				Vector3D copy = v;
				Sterographic.NormalizeToHyperboloid( ref copy );
				result.Add( Sterographic.HyperboloidToPlane( copy ) );
			}
			return result.ToArray();
		}

		private static Vector3D FindClosestPoint( Vector3D v, Vector3D[] list )
		{
			Vector3D result = new Vector3D();

			double dist = double.MaxValue;
			foreach( Vector3D t in list )
			{
				double abs = ( v - t ).Abs();
				if( abs < dist )
				{
					dist = abs;
					result = t;
				}
			}

			return result;
		}

		/////////////////////////////////////////////////////////////////

		/// <summary>
		/// Helper to generate a set of texture coordinates.
		/// </summary>
		public static Vector3D[] TextureCoords( Polygon poly, Geometry g, int maxDiv = 8 )
		{
			int divisions = maxDiv;

			List<Vector3D> points = new List<Vector3D>();
			if( 0 == poly.Segments.Count )
				return points.ToArray();

			// ZZZ - Should we do this different handling of triangles?
			// I think no, this was just for investigating "geodesic saddles".
			bool doGeodesicSaddles = false;
			if( 3 == poly.Segments.Count && doGeodesicSaddles )
			{
				Vector3D[] t1 = CalcPointsUsingTwoSegments( poly.Segments[0], poly.Segments[1], divisions, g );
				Vector3D[] t2 = CalcPointsUsingTwoSegments( poly.Segments[1], poly.Segments[2], divisions, g );
				Vector3D[] t3 = CalcPointsUsingTwoSegments( poly.Segments[2], poly.Segments[0], divisions, g );

				Vector3D[] r = CalcPointsUsingRecursion( 0, poly.Segments[0].P1, poly.Segments[1].P1, poly.Segments[2].P1, g );
				Vector3D[] proj = CalcViaProjections( poly.Segments[0].P1, poly.Segments[1].P1, poly.Segments[2].P1, divisions, g );

				foreach( Vector3D v1 in t1 )
				{
					Vector3D v2 = FindClosestPoint( v1, t2 );
					Vector3D v3 = FindClosestPoint( v1, t3 );
					Vector3D add = ( v1 + v2 + v3 ) / 3;
					//Vector3D add = FindClosestPoint( v1, r );
					//Vector3D add = v1;
					points.Add( add );
				}

				return proj;
			}
			else
			{
				// We make a triangle lattice for each segment.
				// Think of the segment and the poly center making one big triangle,
				// which is subdivided into smaller triangles.
				foreach( Segment s in poly.Segments )
				{
					Vector3D[] s1 = SubdivideSegmentInGeometry( s.P1, poly.Center, divisions, g );
					Vector3D[] s2 = SubdivideSegmentInGeometry( s.P2, poly.Center, divisions, g );
					for( int i = 0; i < divisions; i++ )
						points.AddRange( SubdivideSegmentInGeometry( s1[i], s2[i], divisions - i, g ) );

					points.Add( poly.Center );
				}
			}

			return points.ToArray();
		}

		/// <summary>
		/// Subdivides a segment from p1->p2 with the two endpoints not on the origin, in the respective geometry.
		/// </summary>
		private static Vector3D[] SubdivideSegmentInGeometry( Vector3D p1, Vector3D p2, int divisions, Geometry g )
		{
			// Handle this specially, so we can keep things 3D if needed.
			if( g == Geometry.Euclidean )
			{
				Segment seg = Segment.Line( p1, p2 );
				return seg.Subdivide( divisions );
			}

			Mobius p1ToOrigin = new Mobius();
			p1ToOrigin.Isometry( g, 0, -p1 );
			Mobius inverse = p1ToOrigin.Inverse();

			Vector3D newP2 = p1ToOrigin.Apply( p2 );
			Segment radial = Segment.Line( new Vector3D(), newP2 );
			Vector3D[] temp = SubdivideRadialInGeometry( radial, divisions, g );

			List<Vector3D> result = new List<Vector3D>();
			foreach( Vector3D v in temp )
				result.Add( inverse.Apply( v ) );

			return result.ToArray();
		}

		/// <summary>
		/// Equally subdivides a segment with a startpoint at the origin, in the respective geometry.
		/// </summary>
		private static Vector3D[] SubdivideRadialInGeometry( Segment radial, int divisions, Geometry g )
		{
			List<Vector3D> result = new List<Vector3D>();
			if( radial.Type != SegmentType.Line )
			{
				Debug.Assert( false );
				return result.ToArray();
			}

			switch( g )
			{
				case Geometry.Spherical:
				{
					double eLength = radial.Length;
					double sLength = Spherical2D.e2sNorm( eLength );
					double divLength = sLength / divisions;

					for( int i = 0; i <= divisions; i++ )
					{
						double temp = Spherical2D.s2eNorm( divLength * i );
						result.Add( radial.P2 * temp / eLength );
					}

					break;
				}
				case Geometry.Euclidean:
					return radial.Subdivide( divisions );

				case Geometry.Hyperbolic:
				{
					double eLength = radial.Length;
					double hLength = DonHatch.e2hNorm( eLength );
					double divLength = hLength / divisions;

					for( int i = 0; i <= divisions; i++ )
					{
						double temp = DonHatch.h2eNorm( divLength * i );
						result.Add( radial.P2 * temp / eLength );
					}

					break;
				}
			}

			return result.ToArray();
		}

		/// <summary>
		/// Returns the sum of all the integers up to and including n.
		/// </summary>
		private static int TriangularNumber( int n )
		{
			return n * (n + 1) / 2;
		}

		/// <summary>
		/// Grabs an array of indices into the coordinate array for TextureCoords.
		/// The array represents individual triangles (each set of 3 is one triangle).
		/// </summary>
		public static int[] TextureElements( int numBaseTriangles, int LOD, int maxDiv = 8 )
		{
			int divisions = maxDiv;
			int stride = divisions / (int)Math.Pow( 2, LOD );

			// 9 + 8 + 7 + 6 + 5 + 4 + 3 + 2 + 1
			int numVertsPerSegment = TriangularNumber( divisions + 1 );

			List<int> result = new List<int>();
			int offset = 0;
			for( int count = 0; count < numBaseTriangles; count++ )
			{
				// Make the triangles.
				int start1 = offset, start2 = offset;
				for( int i = 0; i < divisions; i += stride )
				{
					start1 = start2;

					int temp = divisions - i + 1;
					for( int j = 0; j < stride; j++ )
					{
						start2 += temp;
						temp--;
					}

					for( int j = 0; j < divisions - i; j += stride )
					{
						int idx1 = start1 + j;
						int idx2 = start1 + j + stride;
						int idx3 = start2 + j;

						result.Add( idx1 );
						result.Add( idx2 );
						result.Add( idx3 );
					}

					for( int j = 0; j < divisions - i - stride; j += stride )
					{
						int idx1 = start2 + j;
						int idx2 = start1 + j + stride;
						int idx3 = start2 + j + stride;

						result.Add( idx1 );
						result.Add( idx2 );
						result.Add( idx3 );
					}
				}

				offset += numVertsPerSegment;
			}

			return result.ToArray();
		}
	}
}
