namespace R3.Core
{
	using R3.Geometry;
	using Math = System.Math;
	using System.Drawing;

	public static class ColorUtil
	{
		// Takes Hue value as input, returns RGB vector.
		// Copied from POV-Ray
		public static Vector3D CH2RGB( double H )
		{
			double R = 0, G = 0, B = 0;
			if( H >= 0 && H < 120 )
			{
				R = (120 - H) / 60;
				G = (H - 0) / 60;
				B = 0;
			}
			else if( H >= 120 && H < 240 )
			{
				R = 0;
				G = (240 - H) / 60;
				B = (H - 120) / 60;
			}
			else if( H >= 240 && H <= 360 )
			{
				R = (H - 240) / 60;
				G = 0;
				B = (360 - H) / 60;
			}

			return new Vector3D(
				Math.Min( R, 1 ),
				Math.Min( G, 1 ),
				Math.Min( B, 1 ) );
		}

		// Copied from POV-Ray
		// Putting this here for speed. It was too expensive to do this at render time in POV-Ray.
		public static Vector3D CHSL2RGB( Vector3D hsl )
		{
			Vector3D ones = new Vector3D( 1, 1, 1 );

			double H = hsl.X;
			double S = hsl.Y;
			double L = hsl.Z;
			Vector3D SatRGB = CH2RGB( H );
			Vector3D Col = 2 * S * SatRGB + (1 - S) * ones;
			Vector3D rgb;
			if( L < 0.5 )
				rgb = L * Col;
			else
				rgb = (1 - L) * Col + (2 * L - 1) * ones;

			return rgb;
		}

		public static Color HslToRgb( Vector3D hsl )
		{
			Vector3D rgb = CHSL2RGB( hsl );
			rgb *= 255;
			return Color.FromArgb( 255, (int)rgb.X, (int)rgb.Y, (int)rgb.Z );
		}
	}
}
