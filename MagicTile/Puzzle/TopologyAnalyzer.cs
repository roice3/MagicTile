namespace MagicTile
{
	using System.Collections.Generic;
	using System.Linq;
	using R3.Geometry;

	/// <summary>
	/// A data structure for all elements of a given type (face/edge/vertex).
	/// There is one item in the list for every logical point.
	/// Each item (a HashSet) contains all copies of the logical point.
	/// </summary>
	using ElementList = System.Collections.Generic.List<
		System.Collections.Generic.HashSet<R3.Geometry.Vector3D>>;

	/// <summary>
	/// Class to calculate the number of faces/edges/vertices of our puzzle.
	/// This is an easy problem for "regular" colorings,
	/// but we also want to support the stranger colorings, and so we must
	/// traverse all the tiles in the puzzle, looking for identifications.
	/// 
	/// The data structures built up by this class will later help us to 
	/// setup all the twist data. (Initially, we were doing that 
	/// incorrectly for the irregular colorings.)
	/// </summary>
	public class TopologyAnalyzer
	{
		public TopologyAnalyzer( Puzzle puzzle, Tile template )
		{
			m_puzzle = puzzle;
			m_template = template;
		}

		private readonly Puzzle m_puzzle;
		private readonly Tile m_template;

		public override string ToString()
		{
			return string.Format( "F={0}, E={1}, V={2}, χ={3}", F, E, V, EulerCharacteristic );
		}

		/// <summary>
		/// The number of faces.
		/// </summary>
		public int F { get; private set; }

		/// <summary>
		/// The number of edges.
		/// </summary>
		public int E { get; private set; }

		/// <summary>
		/// The number of vertices.
		/// </summary>
		public int V { get; private set; }

		/// <summary>
		/// The Euler Characteristic.
		/// </summary>
		public int EulerCharacteristic
		{
			get
			{
				return V - E + F;
			}
		}

		/// <summary>
		/// Returns the index of the logical element associated with a point, or -1 if there isn't one.
		/// Indices are not reused between element types.
		/// This is to help with setting up twisting.
		/// We need to be able to pass in a Vector3D, and have this tell us which element that corresponds to.
		/// </summary>
		public int GetLogicalElementIndex( ElementType type, Vector3D point )
		{
			HashSet<Vector3D> identified = FindIdentifiedPoints( type, point );
			if( identified == null )
				return -1;

			ElementList list = GrabPositionListForType( type );
			int result = list.FindIndex( i => i == identified );

			if( type == ElementType.Edge )
				result += F;
			if( type == ElementType.Vertex )
				result += F + E;
			return result;
		}

		/// <summary>
		/// Data structure containing all face points.
		/// </summary>
		private ElementList m_allFaces;

		/// <summary>
		/// Data structure containing all edge points.
		/// </summary>
		private ElementList m_allEdges;

		/// <summary>
		/// Data structure containing all vertex points.
		/// </summary>
		private ElementList m_allVertices;

		/// <summary>
		/// Main work method.
		/// </summary>
		public void Analyze()
		{
			m_allFaces = new ElementList();
			m_allEdges = new ElementList();
			m_allVertices = new ElementList();

			//
			// Faces (the only easy element, since the canonical
			// points are guaranteed to only be in one place).
			//
			foreach( Cell master in m_puzzle.MasterCells )
			{
				HashSet<Vector3D> identified = new HashSet<Vector3D>();
				identified.Add( m_puzzle.InfinitySafe( master.Center ) );
				foreach( Cell slave in m_puzzle.SlaveCells( master ) )
					identified.Add( m_puzzle.InfinitySafe( slave.Center ) );
				m_allFaces.Add( identified );
			}

			// Edges and Vertices.
			AnalyzeElement( ElementType.Edge );
			AnalyzeElement( ElementType.Vertex );

			F = m_allFaces.Count;
			E = m_allEdges.Count;
			V = m_allVertices.Count;
		}

		/// <summary>
		/// A method to fill our position list for an element type.  Meant to be used on edges and vertices.
		/// Here's how this works:
		///   Starting from a master point, we find all the identified slave points and put them in our set.
		///   Think of this as a tree of points.  While building these sets of identified points, multiple trees 
		///   may end up intersecting.  In that case, we need to combine the two trees (HashSets) into one, 
		///   since all the points from both are now identified with each other.
		/// 
		/// It's hard to explain why all this is necessary, but the {8,4} 10C puzzle drove the development of this.
		/// Picture a set of master tiles only.  A logical point might physically be in multiple places, if it
		/// is on the edge of the fundamental region.  Furthermore, one of these master points on the edge and their 
		/// slaves might only cover a subset of all the points needing identification.
		/// </summary>
		private void AnalyzeElement( ElementType type )
		{
			HashSet<Vector3D> complete = new HashSet<Vector3D>();

			foreach( Cell master in m_puzzle.MasterCells )
			for( int i=0; i<master.Boundary.Segments.Count; i++ )
			{
				Vector3D masterPoint = RetrieveSegmentPoint( type, master, i );

				// For this master point, start a new set of identified elements,
				// but only if we are not part of a previous set!
				HashSet<Vector3D> identified = null;
				if( complete.Contains( masterPoint ) )
					identified = FindIdentifiedPoints( type, masterPoint );
				else
				{
					identified = new HashSet<Vector3D>();
					ElementList allElements = GrabPositionListForType( type );
					allElements.Add( identified );

					identified.Add( masterPoint );
					complete.Add( masterPoint );
				}

				foreach( Cell slave in m_puzzle.SlaveCells( master ) )
				{
					Vector3D slavePoint = RetrieveSegmentPoint( type, slave, i );

					HashSet<Vector3D> identifiedCompare = null;
					if( complete.Contains( slavePoint ) )
					{
						// We're already in the complete set.
						// It could be because another identification set added us.
						identifiedCompare = FindIdentifiedPoints( type, slavePoint );
						if( identified != identifiedCompare )
						{
							// We were already in another identification set, 
							// and need to merge with it.
							MergeSets( type, identified, identifiedCompare );
						}

						continue;
					}
						
					identified.Add( slavePoint );
					complete.Add( slavePoint );
				}
			}
		}

		private Vector3D RetrieveSegmentPoint( ElementType type, Cell cell, int segIndex )
		{
			if( type == ElementType.Edge )
			{
				// NOTE: We must transform, and not return the segment midpoint.
				//		 This is because the Euclidean midpoint of a transformed segment
				//		 is not the same as the transform of the midpoint.
				Segment seg = m_template.Boundary.Segments[segIndex];
				Vector3D transformed = cell.IsometryInverse.Apply( seg.Midpoint );
				return m_puzzle.InfinitySafe( transformed );
			}
			if( type == ElementType.Vertex )
			{
				Segment seg = cell.Boundary.Segments[segIndex];
				return m_puzzle.InfinitySafe( seg.P1 );
			}

			throw new System.ArgumentException();
		}

		private void MergeSets( ElementType type, HashSet<Vector3D> set1, HashSet<Vector3D> set2 )
		{
			// Add set2 to set1, then remove set2.
			foreach( Vector3D v in set2 )
				set1.Add( v );

			ElementList allElements = GrabPositionListForType( type );
			allElements.Remove( set2 );
		}

		/// <summary>
		/// This will find the set of identified points for some input point.
		/// Returns null if no identified points are found.
		/// </summary>
		private HashSet<Vector3D> FindIdentifiedPoints( ElementType elementType, Vector3D point )
		{
			ElementList allElements = GrabPositionListForType( elementType );
			foreach( HashSet<Vector3D> set in allElements )
			{
				if( set.Contains( point ) )
					return set;
			}

			return null;
		}

		private ElementList GrabPositionListForType( ElementType elementType )
		{
			switch( elementType )
			{
				case ElementType.Face:
					return m_allFaces;
				case ElementType.Edge:
					return m_allEdges;
				case ElementType.Vertex:
					return m_allVertices;
			}

			throw new System.ArgumentException();
		}
	}
}
