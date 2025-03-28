namespace MagicTile
{
	using R3.Core;
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
		/// For a systolic puzzle, twisting will be based on a pair of pants.
		/// null by default.
		/// </summary>
		public Pants Pants { get; set; }

		/// <summary>
		/// Easy check for systolic mode.
		/// </summary>
		private bool Systolic { get { return Pants != null; } }

		/// <summary>
		/// Easy check for earthquake mode.
		/// </summary>
		private bool Earthquake { get { return Pants != null && Circles.Length == 3; } }

		private CircleNE[] CirclesForSlice( int slice )
		{
			int dirSeg = SliceMask.SliceToDirSeg( slice );
			switch( dirSeg )
			{
				// I wish the indices matched here the circles better.
				case 1:
					return Circles.Skip( 2 ).Take( 2 ).ToArray();
				case 3:
					return Circles.Skip( 4 ).Take( 2 ).ToArray();
				case 5:
					return Circles.Skip( 0 ).Take( 2 ).ToArray();
				case -1:
					break;
				default:
					System.Diagnostics.Debug.Assert( false );
					break;
			}

			return new CircleNE[] { };
		}

		/// <summary>
		/// Grabs all the circles the apply to a given slicemask.
		/// </summary>
		public CircleNE[] CirclesForSliceMask( int mask )
		{
			// For earthquake puzzles, return all 3 circles for now.
			if( Earthquake )
				return Circles;

			// For systolic puzzles, return the relevant slice.
			// NOTE: I did consider all 3 edges of the pants twisting every time too.
			//		 That could be yet a different kind of puzzle (there are tradeoffs)
			if( Systolic )
			{
				int slice = SliceMask.MaskToSlice( mask );
				return CirclesForSlice( slice );
			}

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
		/// The number of slices, excluding antipodal slices.
		/// This is here to help with the GAP script output functionality.
		/// </summary>
		public int NumSlicesNoOpp { get; set; }

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
			// We need the full list for earthquake.
			if( Earthquake )
				return AffectedStickers;

			List<StickerList> result = new List<StickerList>();

			// Systolic handling.
			// This may change in the future if we want to support systolics with multiple slices
			// (at this point the "slices" are representing the 3 different directions.
			if( Systolic )
			{
				int sliceInQuotesButReallyDirection = SliceMask.MaskToSlice( mask );
				result.Add( AffectedStickers[sliceInQuotesButReallyDirection-1] );
				return result;
			}

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
			if( Earthquake )
			{
				if( Pants.TestCircle.HasVertexInside( cell.Boundary ) )
					return true;

				for( int i=0; i<3; i++ )
				{
					CircleNE c = Pants.TestCircle.Clone();
					c.Reflect( Pants.Hexagon.Segments[i * 2] );
					if( c.HasVertexInside( cell.Boundary ) )
						return true;
				}

				return false;
			}

			if( Systolic )
			{
				foreach( CircleNE circleNE in this.Circles )
				{
					if( circleNE.Intersects( cell.Boundary ) )
						return true;
				}

				return false;
			}

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

			if( Earthquake )
			{
				Vector3D cen = sticker.Poly.Center;
				if( Pants.IsPointInsideOptimized( cen ) )
					AffectedStickers[0].Add( sticker );
				return;
			}

			System.Func<CircleNE, Vector3D, bool> isInside = ( c, t ) => sphericalPuzzle ?
				 CircleNE.IsPointInsideNE( c, t ) : CircleNE.IsPointInsideFast( c, t );

			if( Systolic )
			{
				// Systolic puzzles are special.
				isInside = ( c, t ) => CircleNE.isPointInsideHypercycle( c, t );

				// Only 1 pair-of-pants supported to start.
				System.Diagnostics.Debug.Assert( Circles.Length == 6 );
				for( int slice=1; slice<=3; slice++ )
				{
					var circles = CirclesForSlice( slice );
					CircleNE c1 = circles[0];
					CircleNE c2 = circles[1];

					// If we make the circle centers the pants hexagon center and calc the non-euclidean one (slow), we may need to reverse the sense of this..
					if( isInside( c1, sticker.Poly.Center ) && !isInside( c2, sticker.Poly.Center ) )
					//if( !isInside( c1, sticker.Poly.Center ) && isInside( c2, sticker.Poly.Center ) )
					{
						AffectedStickers[slice-1].Add( sticker );
					}
				}

				return;
			}

			// Slices are ordered by depth.
			// We cycle from the inner slice outward.
			for( int slice=0; slice<this.Circles.Length; slice++ )
			{
				if( isInside( Circles[slice], sticker.Poly.Center ) )
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

		public Mobius MobiusForTwist( PuzzleConfig config, SingleTwist twist, double rotation, bool useEarthquakeTwistData = false )
		{
			Geometry g = config.Geometry;

			Mobius mobius = new Mobius();
			if( config.Systolic )
			{
				int hexSeg = useEarthquakeTwistData ?
					SliceMask.MaskToDirSeg( twist.SliceMaskEarthquake ) :
					SliceMask.MaskToDirSeg( twist.SliceMask );
				//System.Diagnostics.Trace.WriteLine( "Slicemask: " + twist.SliceMask + "\tHexseg:" + hexSeg );

				Vector3D p1 = this.Pants.Hexagon.Segments[hexSeg].P2;
				Vector3D p2 = Pants.Hexagon.Segments[hexSeg].P1;
				if( Pants.Isometry.Reflected )
					R3.Core.Utils.SwapPoints( ref p1, ref p2 );
				System.Diagnostics.Debug.Assert( !Reverse );

				// Earthquake puzzles twist twice as far.
				if( !config.Earthquake )
					rotation /= 2;

				mobius.Geodesic( Geometry.Hyperbolic, p1, p2, rotation );
			}
			else
			{
				mobius.Elliptic( g, Center, Reverse ? rotation * -1 : rotation );
			}
			return mobius;
		}
	}
}
