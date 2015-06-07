namespace MagicTile
{
	using R3.Geometry;

	public class Sticker
	{
		public Sticker( int cellIndex, int stickerIndex, Polygon poly )
		{
			this.CellIndex = cellIndex;
			this.StickerIndex = stickerIndex;
			this.Poly = poly;
		}

		/// <summary>
		/// The index of the (master) cell this sticker is associated with.
		/// </summary>
		public int CellIndex { get; private set; }

		/// <summary>
		/// The index of this sticker in the parent cell.
		/// </summary>
		public int StickerIndex { get; private set; }

		/// <summary>
		/// The polygon for this sticker.
		/// </summary>
		public Polygon Poly { get; private set; }

		/// <summary>
		/// Use to track if this sticker is affected during twisting.
		/// </summary>
		public bool Twisting { get; set; }
	}
}
