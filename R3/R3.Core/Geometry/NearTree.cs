namespace R3.Geometry
{
	using R3.Math;
	using System.Collections.Generic;

	/*
	This code was adapted from an article and written by Larry Andrews
	in the November 2001 issue of C/C++ Users Journal.
	http://drdobbs.com/184401449

	It is a class for the nearest neighbor problem based on partition trees.

	NOTE:	This class is meant for static data sets, where a near tree can be
			built up once and then used for many lookups.  Time to build the tree
			is proportional to n*log(n).  Lookup times are proportional to log(n).
	*/

	public enum Metric
	{
		Spherical,
		Euclidean,
		Hyperbolic
		// GraphDist, etc. (anything that obeys the triangle inequality).
	}

	public class NearTreeObject
	{
		public object ID { get; set; }
		public Vector3D Location { get; set; }
	}

	public class NearTree
	{
		public NearTree()
		{
			Reset( Metric.Euclidean );
		}

		public NearTree( Metric m )
		{
			Reset( m );
		}

		public static Metric GtoM( Geometry g )
		{
			switch( g )
			{
				case Geometry.Spherical:
					return Metric.Spherical;
				case Geometry.Euclidean:
					return Metric.Euclidean;
				case Geometry.Hyperbolic:
					return Metric.Hyperbolic;
			}

			return Metric.Euclidean;
		}

		public void Reset( Metric m )
		{
			this.Metric = m;

			m_pLeftBranch = m_pRightBranch = null;
			m_pLeft = m_pRight = null;

			m_maxLeft = double.MinValue;
			m_maxRight = double.MinValue;
		}

		/// <summary>
		/// The distance metric to use.
		/// </summary>
		public Metric Metric { get; set; }

		// Left/right objects stored in this node.
		private NearTreeObject m_pLeft;
		private NearTreeObject m_pRight;

		// Longest distance from the left/right 
		// objects to anything below it in the tree.
		private double m_maxLeft;
		private double m_maxRight;

		// Tree descending from the left/right.
		private NearTree m_pLeftBranch;
		private NearTree m_pRightBranch;

		/// <summary>
		/// Inserts an object into the neartree.
		/// </summary>
		public void InsertObject( NearTreeObject nearTreeObject )
		{
			double tempRight = 0;
			double tempLeft = 0;
			if( m_pRight != null )
			{
				tempRight = Dist( nearTreeObject.Location, m_pRight.Location );
				tempLeft = Dist( nearTreeObject.Location, m_pLeft.Location );
			}

			if( m_pLeft == null )
				m_pLeft = nearTreeObject;
			else if( m_pRight == null )
				m_pRight = nearTreeObject;
			else if( tempLeft > tempRight )
			{
				if( m_pRightBranch == null )
					m_pRightBranch = new NearTree( this.Metric );

				// Note: that the next line assumes that m_maxRight
				// is negative for a new node.
				if( m_maxRight < tempRight )
					m_maxRight = tempRight;
				m_pRightBranch.InsertObject( nearTreeObject );
			}
			else
			{
				if( m_pLeftBranch == null )
					m_pLeftBranch = new NearTree( this.Metric );

				// Note: that the next line assumes that m_maxLeft
				// is negative for a new node.
				if( m_maxLeft < tempLeft )
					m_maxLeft = tempLeft;
				m_pLeftBranch.InsertObject( nearTreeObject );
			}
		}

		/// <summary>
		/// Finds the nearest neighbor to a location, and 
		/// withing a specified search radius (returns false if none found).
		/// </summary>
		public bool FindNearestNeighbor( out NearTreeObject closest, Vector3D location, double searchRadius )
		{
			closest = null;
			return FindNearestNeighborRecursive( ref closest, location, ref searchRadius );
		}

		/// <summary>
		/// Finds all the objects withing a certain radius of some location (returns false if none found).
		/// </summary>
		public bool FindCloseObjects( out NearTreeObject[] closeObjects, Vector3D location, double searchRadius )
		{
			List<NearTreeObject> result = new List<NearTreeObject>();
			bool found = 0 != FindCloseObjectsRecursive( ref result, location, searchRadius );
			closeObjects = result.ToArray();
			return found;
		}

		private bool FindNearestNeighborRecursive( ref NearTreeObject closest, Vector3D location, ref double searchRadius )
		{
			double tempRadius = 0;
			bool bRet = false;

			// First test each of the left and right positions to see
			// if one holds a point nearer than the nearest so far.
			if( m_pLeft != null ) 
			{
				tempRadius = Dist( location, m_pLeft.Location );
				if( tempRadius <= searchRadius )
				{
					searchRadius = tempRadius;
					closest = m_pLeft;
					bRet = true;
				}
			}
			if( m_pRight != null )
			{
				tempRadius = Dist( location, m_pRight.Location );
				if( tempRadius <= searchRadius )
				{
					searchRadius = tempRadius;
					closest = m_pRight;
					bRet = true;
				}
			}

			// Now we test to see if the branches below might hold an
			// object nearer than the best so far found. The triangle
			// rule is used to test whether it's even necessary to descend.
			if( m_pLeftBranch != null )
			{
				if( (searchRadius + m_maxLeft) >= Dist( location, m_pLeft.Location ) )
				{
					bRet |= m_pLeftBranch.FindNearestNeighborRecursive( ref closest, location, ref searchRadius );
				}
			}
			if( m_pRightBranch != null )
			{
				if( (searchRadius + m_maxRight) >= Dist( location, m_pRight.Location ) )
				{
					bRet |= m_pRightBranch.FindNearestNeighborRecursive( ref closest, location, ref searchRadius );
				}
			}

			return bRet;
		}

		private long FindCloseObjectsRecursive( ref List<NearTreeObject> closeObjects, Vector3D location, double searchRadius )
		{
			long lReturn = 0;

			// First test each of the left and right positions to see
			// if one holds a point nearer than the search radius.
			if( ( m_pLeft != null ) && ( Dist( location, m_pLeft.Location ) <= searchRadius ) )
			{
				closeObjects.Add( m_pLeft );
				lReturn++;
			}
			if( ( m_pRight != null ) && ( Dist( location, m_pRight.Location ) <= searchRadius ) )
			{
				closeObjects.Add( m_pRight );
				lReturn++;
			}

			//
			// Now we test to see if the branches below might hold an
			// object nearer than the search radius. The triangle rule
			// is used to test whether it's even necessary to descend.
			//
			if( ( m_pLeftBranch != null ) && ( ( searchRadius + m_maxLeft ) >= Dist( location, m_pLeft.Location ) ) )
			{
				lReturn += m_pLeftBranch.FindCloseObjectsRecursive( ref closeObjects, location, searchRadius );
			}
			if( ( m_pRightBranch != null ) && ( ( searchRadius + m_maxRight ) >= Dist( location, m_pRight.Location ) ) )
			{
				lReturn += m_pRightBranch.FindCloseObjectsRecursive( ref closeObjects, location, searchRadius );
			}

			return lReturn;
		}

		// Gets the distance between two points.
		private double Dist( Vector3D p1, Vector3D p2 )
		{
			switch( this.Metric )
			{
				case Metric.Spherical:
				{
					// ZZZ - Is it too expensive to build up a mobius every time?
					//		 I wonder if there is a better way.
					Mobius m = new Mobius();
					m.Isometry( Geometry.Spherical, 0, -p1 );
					Vector3D temp = m.Apply( p2 );
					return Spherical2D.e2sNorm( temp.Abs() );
				}
				case Metric.Euclidean:
				{
					return ( p2 - p1 ).Abs();
				}
				case Metric.Hyperbolic:
				{
					// ZZZ - Is it too expensive to build up a mobius every time?
					//		 I wonder if there is a better way.
					Mobius m = new Mobius();
					m.Isometry( Geometry.Hyperbolic, 0, -p1 );
					Vector3D temp = m.Apply( p2 );
					return DonHatch.e2hNorm( temp.Abs() );
				}
			}

			throw new System.NotImplementedException();
		}
	}
}
