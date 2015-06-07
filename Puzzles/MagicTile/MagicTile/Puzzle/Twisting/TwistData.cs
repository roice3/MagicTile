namespace MagicTile
{
	using R3.Geometry;
	using R3.Math;
	using System.Collections.Generic;
	using System.Linq;

	using StickerList = System.Collections.Generic.List<Sticker>;

	/// <summary>
	/// This is used to collate together TwistData which is identified with each other.
	/// </summary>
	public class IdentifiedTwistData
	{
		public IdentifiedTwistData()
		{
			TwistDataForDrawing = new List<TwistData>();
			TwistDataForStateCalcs = new List<TwistData>();
		}

		/// <summary>
		/// This is the index of ourselves in the main list of all twist data in the puzzle.
		/// </summary>
		public int Index { get; set; }

		public List<TwistData> TwistDataForDrawing { get; set; }
		public List<TwistData> TwistDataForStateCalcs { get; set; }

		public int Order
		{
			get
			{
				return TwistDataForStateCalcs.First().Order;
			}
		}

		public Cell[] AffectedMasterCells
		{
			get
			{
				if( m_affectedMasterCells != null )
					return m_affectedMasterCells;

				List<Cell> result = new List<Cell>();
				foreach( TwistData twistData in this.TwistDataForStateCalcs )
				foreach( Cell master in twistData.AffectedMasterCells.Keys )
					result.Add( master );

				m_affectedMasterCells = result.Distinct().ToArray();
				return m_affectedMasterCells;
			}
		}
		private Cell[] m_affectedMasterCells;

		public void StartTwist( int slicemask, bool sphericalPuzzle )
		{
			SetTwistingOnStickers( slicemask, sphericalPuzzle, twisting: true );
		}

		public void EndTwist( int slicemask, bool sphericalPuzzle )
		{
			SetTwistingOnStickers( slicemask, sphericalPuzzle, twisting: false );
		}

		private void SetTwistingOnStickers( int slicemask, bool sphericalPuzzle, bool twisting )
		{
			List<TwistData> collection = sphericalPuzzle ?
				this.TwistDataForDrawing :
				this.TwistDataForStateCalcs;

			foreach( TwistData twistData in collection )
			foreach( StickerList stickerList in twistData.AffectedStickersForSliceMask( slicemask ) )
			foreach( Sticker sticker in stickerList )
				sticker.Twisting = twisting;
		}
	}

	public enum ElementType
	{
		Face,
		Edge,
		Vertex
	}

	/// <summary>
	/// Class to hold all data we need cached to perform a twist.
	/// </summary>
	public class TwistData
	{
		/// <summary>
		/// The type of twist.
		/// </summary>
		public ElementType TwistType { get; set; }

		/// <summary>
		/// The (non-euclidean) center of the twist.
		/// </summary>
		public Vector3D Center { get; set; }

		/// <summary>
		/// The order of the twist.
		/// </summary>
		public int Order { get; set; }
		
		/// <summary>
		/// Whether we need to reverse the twist (for non-orientable puzzles).
		/// </summary>
		public bool Reverse { get; set; }

		/// <summary>
		/// The cocentric slicing circles that can be involved in the twist.
		/// </summary>
		public CircleNE[] Circles { get; set; }

		/// <summary>
		/// Grabs all the circles the apply to a given slicemask.
		/// </summary>
		public CircleNE[] CirclesForSliceMask( int mask )
		{
			int count = this.Circles.Length;
			List<int> indexes = new List<int>();
			foreach( int slice in SliceMask.MaskToSlices( mask ) )
			{
				if( slice > NumSlices )
					continue;

				int index1 = slice - 2;
				if( index1 >= 0 && index1 < count )
					indexes.Add( index1 );

				int index2 = slice - 1;
				if( index2 >= 0 && index2 < count )
					indexes.Add( index2 );
			}

			indexes = indexes.Distinct().ToList();
			indexes.Sort();
			List<CircleNE> result = new List<CircleNE>();
			foreach( int i in indexes )
				result.Add( this.Circles[i] );
			return result.ToArray();
		}

		/// <summary>
		/// The number of slices, which may not be the same as the number of circles.
		/// </summary>
		public int NumSlices 
		{ 
			get 
			{
				// Unless we explicitely set this (which we do for spherical puzzles),
				// it is just the number of circles.
				if( -1 == m_numSlices )
					return this.Circles.Length;
				return m_numSlices;
			}
			set
			{
				m_numSlices = value;
			}
		}
		private int m_numSlices = -1;

		/// <summary>
		/// Affected Stickers.  The index of the outer list is the slice.
		/// </summary>
		private List<StickerList> AffectedStickers { get; set; }

		/// <summary>
		/// The affected stickers for a particular slice mask.
		/// There is one sticker list per slice.
		/// </summary>
		public List<StickerList> AffectedStickersForSliceMask( int mask )
		{
			List<StickerList> result = new List<StickerList>();
			foreach( int slice in SliceMask.MaskToSlices( mask ) )
			{
				if( slice > AffectedStickers.Count )
					continue;
				result.Add( AffectedStickers[slice-1] );
			}
			return result;
		}

		/// <summary>
		/// A list of master cells that should get invalidated when this twist happens.
		/// </summary>
		public Dictionary<Cell, bool> AffectedMasterCells { get; set; }

		/// <summary>
		/// A reference to all the other twist data associated with us.
		/// </summary>
		public IdentifiedTwistData IdentifiedTwistData { get; set; }

		/// <summary>
		/// Helper to check if we will affect a cell.
		/// </summary>
		public bool WillAffectCell( Cell cell, bool sphericalPuzzle )
		{
			foreach( CircleNE circleNE in this.Circles )
			{
				bool inside = sphericalPuzzle ?
					circleNE.IsPointInsideNE( cell.Center ) :
					circleNE.IsPointInsideFast( cell.Center );
				if( inside ||
					circleNE.Intersects( cell.Boundary ) )
					return true;
			}

			return false;
		}

		/// <summary>
		/// Helper to check if we will affect a master, and cache in our list if so.
		/// </summary>
		public void WillAffectMaster( Cell master, bool sphericalPuzzle )
		{
			if( AffectedMasterCells == null )
				AffectedMasterCells = new Dictionary<Cell, bool>();

			if( WillAffectCell( master, sphericalPuzzle ) )
				AffectedMasterCells[master] = true;
		}

		/// <summary>
		/// Helper to check if we will affect a sticker, and cache in our list if so.
		/// </summary>
		public void WillAffectSticker( Sticker sticker, bool sphericalPuzzle )
		{
			if( AffectedStickers == null )
			{
				AffectedStickers = new List<StickerList>();
				for( int slice=0; slice<this.NumSlices; slice++ )
					AffectedStickers.Add( new List<Sticker>() );
			}

			// Slices are ordered by depth.
			// We cycle from the inner slice outward.
			for( int slice=0; slice<this.Circles.Length; slice++ )
			{
				CircleNE circle = this.Circles[slice];
				bool isInside = sphericalPuzzle ?
					circle.IsPointInsideNE( sticker.Poly.Center ) :
					circle.IsPointInsideFast( sticker.Poly.Center );
				if( isInside )
				{
					AffectedStickers[slice].Add( sticker );
					return;
				}
			}

			// If we made it here for spherical puzzles, we are in the last slice.
			// Second check was needed for {3,5} 8C
			if( sphericalPuzzle && 
				( this.NumSlices != this.Circles.Length ) )
				AffectedStickers[this.NumSlices-1].Add( sticker );
		}
	}
}
