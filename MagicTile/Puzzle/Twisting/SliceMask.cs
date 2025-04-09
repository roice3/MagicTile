namespace MagicTile
{
	using System.Collections.Generic;

	public static class SliceMask
	{
		public static int SLICEMASK_1 = 0x0001;
		public static int SLICEMASK_2 = 0x0002;
		public static int SLICEMASK_3 = 0x0004;
		public static int SLICEMASK_4 = 0x0008;
		public static int SLICEMASK_5 = 0x0010;
		public static int SLICEMASK_6 = 0x0020;
		public static int SLICEMASK_7 = 0x0040;
		public static int SLICEMASK_8 = 0x0080;
		public static int SLICEMASK_9 = 0x0100;
		public static int SLICEMASK_10 = 0x0200;

		public static int SliceToMask( int slice )
		{
			switch( slice )
			{
				case 1:
					return SLICEMASK_1;
				case 2:
					return SLICEMASK_2;
				case 3:
					return SLICEMASK_3;
				case 4:
					return SLICEMASK_4;
				case 5:
					return SLICEMASK_5;
				case 6:
					return SLICEMASK_6;
				case 7:
					return SLICEMASK_7;
				case 8:
					return SLICEMASK_8;
				case 9:
					return SLICEMASK_9;
				case 10:
					return SLICEMASK_10;
				default:
					return 0;
			}
		}

		/// <summary>
		/// This is for systolic puzzles.
		/// </summary>
		public static int DirSegToMask( int dirSeg )
		{
			switch( dirSeg )
			{
				case 1:
					return SLICEMASK_1;
				case 3:
					return SLICEMASK_2;
				case 5:
					return SLICEMASK_3;
				default:
					return 0;
			}
		}

		public static int SliceToDirSeg( int slice )
		{
			switch( slice )
			{
				case 1:
					return 1;
				case 2:
					return 3;
				case 3:
					return 5;
				default:
					return 0;
			}
		}

		public static int MaskToDirSeg( int mask )
		{
			int slice = MaskToSlice( mask );
			return SliceToDirSeg( slice );
		}

		/// <summary>
		/// This is only meant to be used with one slice (on systolic puzzles).
		/// If it is called with multiple slices, just the first will be returned.
		/// </summary>
		public static int MaskToSlice( int mask )
		{
			int[] slices = MaskToSlices( mask );
			if( slices.Length >= 1 )
				return slices[0];
			
			// Default to the first slice.
			return 1;
		}

		public static int[] MaskToSlices( int mask )
		{
			List<int> result = new List<int>();
			for( int i=0; i<=10; i++ )
				if( ( SliceToMask( i ) & mask ) != 0 )
					result.Add( i );

			return result.ToArray();
		}
	}
}
