namespace R3.Drawing
{
	using R3.Geometry;

	/// <summary>
	/// Class to help converting from Image/Canvas coords <-> absolute coords.
	/// </summary>
	public class ImageSpace
	{
		/// <summary>
		/// Takes in width/height of an Image/Canvas.
		/// </summary>
		public ImageSpace( double width, double height )
		{
			m_width = width;
			m_height = height;
		}
		private double m_width;
		private double m_height;

		public double XMin { get; set; }
		public double XMax { get; set; }
		public double YMin { get; set; }
		public double YMax { get; set; }

		/// <summary>
		/// Returns a screen width from an absolute width.
		/// </summary>
		public double Width( double width )
		{
			double percent = width / ( XMax - XMin );
			return ( percent * m_width );
		}

		/// <summary>
		/// Returns a screen height from an absolute height.
		/// </summary>
		public double Height( double height )
		{
			double percent = height / ( YMax - YMin );
			return ( percent * m_height );
		}

		/// <summary>
		/// Returns a screen pixel from an absolute location.
		/// NOTE: We don't return a 'Point' because it is different in Forms/Silverlight.
		/// </summary>
		public Vector3D Pixel( Vector3D point )
		{
			double xPercent = ( point.X - XMin ) / ( XMax - XMin );
			double yPercent = ( point.Y - YMin ) / ( YMax - YMin );
			double x = ( xPercent * m_width );
			double y = m_height - ( yPercent * m_height );
			return new Vector3D( x, y, 0 );
		}

		/// <summary>
		/// Returns an absolute location from a screen pixel.
		/// NOTE: We don't take in a 'Point' because it is different in Forms/Silverlight.
		/// </summary>
		public Vector3D Point( Vector3D Pixel )
		{
			return new Vector3D(
				XMin + ( Pixel.X / m_width ) * ( XMax - XMin ),
				YMax - ( Pixel.Y / m_height ) * ( YMax - YMin ),
				0 );
		}

		/// <summary>
		/// VectorNDs are assumed to be 4D.
		/// </summary>
		public VectorND Pixel( VectorND point )
		{
			Vector3D result = Pixel( new Vector3D( point.X[0], point.X[1], point.X[2] ) );
			return new VectorND( new double[] { result.X, result.Y, result.Z, 0 } );
		}
	}
}
