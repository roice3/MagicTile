namespace R3.Geometry
{
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using Math = System.Math;
	using R3.Core;
	using R3.Math;

	public static class Polytope
	{
		// The various projections we can do on a polytope.
		public enum Projection
		{
			CellCentered,
			FaceCentered,
			EdgeCentered,
			VertexCentered
		}
	}

	public static class SkewPolyhedron
	{
		public static Polygon[] BuildDuoprism( int num )
		{
			double angleInc = 2 * Math.PI / num;

			// Torus in two directions.
			List<Polygon> polys = new List<Polygon>();
			double angle1 = angleInc / 2;
			for( int i=0; i<num; i++ )
			{
				double angle2 = angleInc / 2;
				for( int j=0; j<num; j++ )
				{
					List<Vector3D> polyPoints = new List<Vector3D>();

					polyPoints.Add( new Vector3D(
						Math.Cos( angle2 ),
						Math.Sin( angle2 ),
						Math.Cos( angle1 ),
						Math.Sin( angle1 ) ) );

					polyPoints.Add( new Vector3D(
						Math.Cos( angle2 ),
						Math.Sin( angle2 ),
						Math.Cos( angle1 + angleInc ),
						Math.Sin( angle1 + angleInc ) ) );

					polyPoints.Add( new Vector3D(
						Math.Cos( angle2 + angleInc ),
						Math.Sin( angle2 + angleInc ),
						Math.Cos( angle1 + angleInc ),
						Math.Sin( angle1 + angleInc ) ) );

					polyPoints.Add( new Vector3D(
						Math.Cos( angle2 + angleInc ),
						Math.Sin( angle2 + angleInc ),
						Math.Cos( angle1 ),
						Math.Sin( angle1 ) ) );

					Polygon poly = new Polygon();
					poly.CreateEuclidean( polyPoints.ToArray() );
					polys.Add( poly );

					angle2 += angleInc;
				}
				angle1 += angleInc;
			}

			// Nice starting orientation.
			Matrix4D m1 = Matrix4D.MatrixToRotateinCoordinatePlane( Math.PI / 8, 0, 2 );
			Matrix4D m2 = Matrix4D.MatrixToRotateinCoordinatePlane( -Math.PI / 4, 1, 2 );
			foreach( Polygon poly in polys )
			{
				poly.Rotate( m1 );
				poly.Rotate( m2 );
			}

			return polys.ToArray();
		}

		public static Polygon[] BuildBitruncated5Cell()
		{
			double a = 5 / Math.Sqrt( 10 );
			double b = 1 / Math.Sqrt( 6 );
			double c = 1 / Math.Sqrt( 3 );
			double d = 3 / Math.Sqrt( 3 );

			// http://eusebeia.dyndns.org/4d/bitrunc5cell
			/* 
			* ±(0, 4/√6, 4/√3, 0)
			* ±(0, 4/√6, −2/√3, ±2)
			* ±(5/√10, 1/√6, 4/√3, 0)
			* ±(5/√10, 1/√6, −2/√3, ±2)
			* ±(5/√10, 5/√6, 2/√3, 0)
			* ±(5/√10, 5/√6, −1/√3, ±1)
			* ±(5/√10, −3/√6, 0, ±2)
			* ±(5/√10, −3/√6, ±3/√3, ±1)
			 */

			Vector3D[] coords = new Vector3D[] 
			{
				new Vector3D( 0, 4*b, 4*c, 0 ),
				new Vector3D( 0, 4*b, 4*c, 0 ) * -1,
				
				new Vector3D( 0, 4*b, -2*c, 2 ),
				new Vector3D( 0, 4*b, -2*c, -2 ),
				new Vector3D( 0, 4*b, -2*c, 2 ) * -1,
				new Vector3D( 0, 4*b, -2*c, -2 ) * -1,

				new Vector3D( a, b, 4*c, 0 ),
				new Vector3D( a, b, 4*c, 0 ) * -1,

				new Vector3D( a, b, -2*c, 2 ),
				new Vector3D( a, b, -2*c, -2 ),
				new Vector3D( a, b, -2*c, 2 ) * -1,
				new Vector3D( a, b, -2*c, -2 ) * -1,

				new Vector3D( a, 5*b, 2*c, 0 ), 
				new Vector3D( a, 5*b, 2*c, 0 ) * -1,

				new Vector3D( a, 5*b, -c, 1 ), 
				new Vector3D( a, 5*b, -c, -1 ), 
				new Vector3D( a, 5*b, -c, 1 ) * -1, 
				new Vector3D( a, 5*b, -c, -1 ) * -1, 

				new Vector3D( a, -3*b, 0, 2 ),
				new Vector3D( a, -3*b, 0, -2 ),
				new Vector3D( a, -3*b, 0, 2 ) * -1,
				new Vector3D( a, -3*b, 0, -2 ) * -1,

				new Vector3D( a, -3*b, d, 1 ),
				new Vector3D( a, -3*b, -d, 1 ),
				new Vector3D( a, -3*b, d, -1 ),
				new Vector3D( a, -3*b, -d, -1 ),
				new Vector3D( a, -3*b, d, 1 ) * -1,
				new Vector3D( a, -3*b, -d, 1 ) * -1,
				new Vector3D( a, -3*b, d, -1 ) * -1,
				new Vector3D( a, -3*b, -d, -1 ) * -1,
			};

			Polygon[] result = LookForPolys( coords, 2.0, 6 );

			// Nice starting orientation.
			Matrix4D m = new Matrix4D();
			m.Data = new double[][] {
				new double[] {  0.23, -0.72,  0.60,  0.26 },
				new double[] { -0.02,  0.38,  0.72, -0.59 },
				new double[] {  0.97,  0.22, -0.11, -0.03 },
				new double[] {  0.06, -0.54, -0.34, -0.77 } };
			m = Matrix4D.GramSchmidt( m );
			foreach( Polygon poly in result )
				poly.Rotate( m );

			return result;
		}

		public static Polygon[] BuildRuncinated5Cell()
		{
			// http://eusebeia.dyndns.org/4d/runci5cell

			Vector3D[] coords = new Vector3D[] 
			{
				new Vector3D( 0, 0, 0, 2 ), 
				new Vector3D( 0, 0, 0, -2 ),
				new Vector3D( 0, 0,  3/Math.Sqrt(3),  1 ),
				new Vector3D( 0, 0, -3/Math.Sqrt(3),  1 ),
				new Vector3D( 0, 0, -3/Math.Sqrt(3), -1 ),
				new Vector3D( 0, 0,  3/Math.Sqrt(3), -1 ),
				new Vector3D( 0, 4/Math.Sqrt(6), -2/Math.Sqrt(3), 0 ),
				new Vector3D( 0, 4/Math.Sqrt(6), -2/Math.Sqrt(3), 0 ) * -1,
				new Vector3D( 0, 4/Math.Sqrt(6),  1/Math.Sqrt(3),  1 ),
				new Vector3D( 0, 4/Math.Sqrt(6),  1/Math.Sqrt(3), -1 ),
				new Vector3D( 0, 4/Math.Sqrt(6),  1/Math.Sqrt(3),  1 ) * -1,
				new Vector3D( 0, 4/Math.Sqrt(6),  1/Math.Sqrt(3), -1 ) * -1,
				new Vector3D( 5/Math.Sqrt(10), -3/Math.Sqrt(6), 0, 0 ),
				new Vector3D( 5/Math.Sqrt(10), -3/Math.Sqrt(6), 0, 0 ) * -1,
				new Vector3D( 5/Math.Sqrt(10),  1/Math.Sqrt(6), -2/Math.Sqrt(3), 0 ),
				new Vector3D( 5/Math.Sqrt(10),  1/Math.Sqrt(6), -2/Math.Sqrt(3), 0 ) * -1,
				new Vector3D( 5/Math.Sqrt(10),  1/Math.Sqrt(6),  1/Math.Sqrt(3),  1 ),
				new Vector3D( 5/Math.Sqrt(10),  1/Math.Sqrt(6),  1/Math.Sqrt(3), -1 ),
				new Vector3D( 5/Math.Sqrt(10),  1/Math.Sqrt(6),  1/Math.Sqrt(3),  1 ) * -1,
				new Vector3D( 5/Math.Sqrt(10),  1/Math.Sqrt(6),  1/Math.Sqrt(3), -1 ) * -1
			};

			Polygon[] result = LookForPolys( coords, 2.0, 4 );

			// Nice starting orientation.
			Matrix4D m = new Matrix4D();
			m.Data = new double[][] {
				new double[] {  0.62, -0.66, -0.07, -0.41 },
				new double[] {  0.68,  0.22,  0.30,  0.64 },
				new double[] { -0.08,  0.04,  0.93, -0.36 },
				new double[] { -0.37, -0.72,  0.21,  0.54 } };
			m = Matrix4D.GramSchmidt( m );
			foreach( Polygon poly in result )
				poly.Rotate( m );

			return result;
		}

		private static Polygon[] LookForPolys( Vector3D[] coords, double edgeLength, int p )
		{
			Dictionary<int, List<Edge>> lookup = new Dictionary<int, List<Edge>>();
			for( int i=0; i<coords.Length; i++ )
				lookup[i] = new List<Edge>();

			// First find all the edges.
			List<Edge> allEdges = new List<Edge>();
			for( int i=0; i<coords.Length; i++ )
			for( int j=i+1; j<coords.Length; j++ )
			{
				if( Tolerance.Equal( coords[i].Dist( coords[j] ), edgeLength ) )
				{
					Edge e = new Edge( i, j );
					allEdges.Add( e );
					lookup[i].Add( e );
					lookup[j].Add( e );
				}
			}

			// Find all cycles of length p.
			List<List<int>> cycles = new List<List<int>>();
			for( int i=0; i<coords.Length; i++ )
				cycles.Add( new List<int>( new int[] { i } ) );
			cycles = FindCyclesRecursive( cycles, p, lookup );

			// Find the distinct ones.
			foreach( List<int> cycle in cycles )
			{
				// Don't include the start vertex.
				// This is important for the Distinct check below.
				cycle.RemoveAt( cycle.Count - 1 );
			}
			cycles = cycles.Distinct( new CycleEqualityComparer() ).ToList();

			// Now turn into polygons.
			List<Polygon> result = new List<Polygon>();
			foreach( List<int> cycle in cycles )
			{
				List<Vector3D> points = new List<Vector3D>();
				foreach( int i in cycle )
					points.Add( coords[i] );

				// Normalize vertices to hypersphere.
				for( int i=0; i<points.Count; i++ )
				{
					Vector3D normalized = points[i];
					normalized.Normalize();
					points[i] = normalized;
				}

				Polygon poly = Polygon.FromPoints( points.ToArray() );

				// Only add if coplanar.
				// Assume our polygons are regular and even-sized, 
				// in which case we can do a hackish check here.
				// ZZZ - improve hack.
				if( points.Count > 3 )
				{
					bool coplanar = true;
					double toCenter = points[0].Dist( poly.Center );
					if( !Tolerance.Equal( points[p/2].Dist( points[0] ), toCenter*2 ) )
						coplanar = false;

					if( !coplanar )
						continue;
				}

				result.Add( poly );
			}

			return result.ToArray();
		}

		public class CycleEqualityComparer : IEqualityComparer<List<int>>
		{
			public bool Equals( List<int> c1, List<int> c2 )
			{
				if( c1.Count != c2.Count )
					return false;

				int[] sorted1 = c1.OrderBy( i => i ).ToArray();
				int[] sorted2 = c2.OrderBy( i => i ).ToArray();

				for( int i=0; i<sorted1.Length; i++ )
					if( sorted1[i] != sorted2[i] )
						return false;

				return true;
			}

			public int GetHashCode( List<int> cycle )
			{
				int hCode = 0;
				foreach( int idx in cycle )
					hCode = hCode ^ idx.GetHashCode();
				return hCode.GetHashCode();
			}
		}

		/// <summary>
		/// This might end up being useful if we need to optimize.
		/// http://mathoverflow.net/questions/67960/cycle-of-length-4-in-an-undirected-graph
		/// </summary>
		private static List<List<int>> FindCyclesRecursive( List<List<int>> cycles, int cycleLength, Dictionary<int, List<Edge>> lookup )
		{
			if( cycles[0].Count-1 == cycleLength )
			{
				// Return the ones where we ended where we started.
				List<List<int>> result = cycles.Where( c => c.First() == c.Last() ).ToList();
				return result;
			}

			List<List<int>> newCycles = new List<List<int>>();
			foreach( List<int> cycle in cycles )
			{
				int last = cycle.Last();
				foreach( Edge newEdge in lookup[last] )
				{
					int next = newEdge.Opposite( last );
					if( cycle.Count != cycleLength && cycle.Contains( next ) )
						continue;

					List<int> newCycle = new List<int>( cycle );
					newCycle.Add( next );
					newCycles.Add( newCycle );
				}
			}

			return FindCyclesRecursive( newCycles, cycleLength, lookup );
		}
	}
}
