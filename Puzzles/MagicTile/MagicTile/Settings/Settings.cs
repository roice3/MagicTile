namespace MagicTile
{
	using MagicTile.Utils;
	using System.ComponentModel;
	using System.Drawing.Design;
	using System.Runtime.Serialization;
	using R3.Geometry;
	using R3.UI;

	using Color = System.Drawing.Color;

	/// <summary>
	/// Repeating this here, because I don't want to expose 
	/// everything in the HyperbolicModel enumeration.
	/// </summary>
	public enum HModel
	{
		Poincare,
		Klein
	}

	[DataContract( Namespace = "" )]
	public class Settings
	{
		public Settings()
		{
			SetDefaults();
		}

		public void SetDefaults()
		{
			SetupDefaultColors();

			RotationRate = 0.5;
			Gliding = 0.5;
			ShowOnlyFundamental = false;
			HighlightTwistingCircles = true;
			ShowTextureTriangles = false;
			EnableTextureMipmaps = true;
			EnableAntialiasing = true;
			ShowAsSkew = true;
			XLevels = YLevels = ZLevels = 3;
			ConstrainToHypersphere = true;
			EnableLighting = true;
			AmbientLighting = 0.4;
			EnableStereo = false;
			StereoType = EStereoType.CrossEyed;
			StereoSeparation = 1;
			FocalLength = 1;
			SphericalModel = SphericalModel.Sterographic;
			HyperbolicModel = HModel.Poincare;
		}

		public static void Save( Settings settings )
		{
			try
			{
				DataContractHelper.SaveToXml( settings, StandardPaths.SettingsFile );
			}
			catch( System.Exception e )
			{
				string message = string.Format( "Failed to save settings.\n{0}", e.Message );
				Log.Error( message );
			}
		}

		public static Settings Load()
		{
			try
			{
				return (Settings)DataContractHelper.LoadFromXml( typeof( Settings ), StandardPaths.SettingsFile );
			}
			catch( System.Exception e )
			{
				string message = string.Format( "Failed to load settings.\n{0}", e.Message );
				Log.Error( message );

				return new Settings();
			}
		}

		public double RotationStep( int twistOrder )
		{
			// NOTE: The weird .123 is intentional because there is an issue
			//		 where certain rotations can project lines through infinity
			//		 at divisible rotation values.  This helps avoid that until
			//		 I do a proper fix in the projection.
			double rotationStep = this.RotationRate * 40;
			rotationStep += 1.123456789;
			rotationStep /= twistOrder;

			// Make extremely large for disco ball mode.
			if( this.RotationRate == 1 )
				rotationStep = 200;

			return rotationStep;
		}

		[DataMember]
		[DisplayName( "Rotation Rate" )]
		[Description( "Controls the animation rate of twists.  Range is 0 to 1, and you can set to 1 for instantaneous twisting." )]
		[Category( "Behavior" )]
		[Range( 0, 1 )]
		[Editor( typeof( TrackBarValueEditor<double> ), typeof( UITypeEditor ) )]
		public double RotationRate { get; set; }

		[DataMember]
		[DisplayName( "Show Only Fundamental" )]
		[Description( "If true, only the fundamental set of cells will be shown, and not their orbits (applies only to infinite tilings)." )]
		[Category( "Behavior" )]
		public bool ShowOnlyFundamental { get; set; }

		[DataMember]
		[DisplayName( "Highlight Twisting Circles" )]
		[Description( "Whether or not to highlight the twisting circles." )]
		[Category( "Behavior" )]
		public bool HighlightTwistingCircles { get; set; }

		[DataMember]
		[DisplayName( "Spherical Model" )]
		[Description( "The projection model used to display spherical geometry." )]
		[Category( "Behavior" )]
		public SphericalModel SphericalModel { get; set; }

		[DataMember]
		[DisplayName( "Hyperbolic Model" )]
		[Description( "The projection model used to display hyperbolic geometry." )]
		[Category( "Behavior" )]
		public HModel HyperbolicModel { get; set; }

		[DataMember]
		[DisplayName( "Surface Display" )]
		[Description( "BETA! Controls rendering the puzzle on a compact surface. " +
			"This is an experimental feature and will only work with some puzzles. The default (false) designates rendering the universal cover. " +
			"A true value might show puzzles on various rolled up surfaces. " +
			"I reserve the right to break this in future versions to make this feature more general. Consider it fragile and spotty!" )]
		[Category( "Behavior" )]
		public bool SurfaceDisplay { get; set; }

		[DataMember]
		[DisplayName( "Gliding" )]
		[Description( "Controls the amount of auto-gliding.  Moving the slider to the right increases glide, and " +
			"moving to the left causes the gliding to be more dampened." )]
		[Category( "Behavior" )]
		[Range( 0, 1 )]
		[Editor( typeof( TrackBarValueEditor<double> ), typeof( UITypeEditor ) )]
		public double Gliding { get; set; }

		[DataMember]
		[DisplayName( "Show Texture Triangles" )]
		[Description( "Display the triangles used for texturing." )]
		[Category( "Debug" )]
		public bool ShowTextureTriangles { get; set; }

		[DataMember]
		[DisplayName( "Enable Texture Mipmaps" )]
		[Description( "Controls if mipmaps are generated for textures." )]
		[Category( "Debug" )]
		public bool EnableTextureMipmaps { get; set; }

		[DataMember]
		[DisplayName( "Enable Antialiasing" )]
		[Description( "Controls if multisample antialiasing is enabled." )]
		[Category( "Debug" )]
		public bool EnableAntialiasing { get; set; }

		[DataMember]
		[DisplayName( "Show State Calc Tiles" )]
		[Description( "Display just the tiles that are used for state calculations." )]
		[Category( "Debug" )]
		public bool ShowStateCalcCells { get; set; }

		[DataMember]
		[DisplayName( "Show as Skew" )]
		[Description( "Controls rendering the puzzle as a regular skew polyhedron (IRP or finite 4D). " +
			"This setting is ignored if the puzzle does not have an associated skew polyhedron, or if the tiling is configured to only display as skew." )]
		[Category( "Skew Polyhedra (IRP and 4D)" )]
		public bool ShowAsSkew { get; set; }

		/// <summary>
		/// Used to limit the values we allow for IRP levels.
		/// </summary>
		public class IRPLevelsConverter : Int32Converter
		{
			public override bool GetStandardValuesSupported( ITypeDescriptorContext context )
			{
				return true;
			}

			public override StandardValuesCollection GetStandardValues( ITypeDescriptorContext context )
			{
				uint[] vals = new uint[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31 };
				return new StandardValuesCollection( vals );
			}

			public override bool GetStandardValuesExclusive( ITypeDescriptorContext context )
			{
				return true;
			}
		}

		[DataMember]
		[DisplayName( "IRP X Levels" )]
		[Description( "The number of levels to draw in the x direction. x and X are shortcut keys for decrementing and incrementing this value." )]
		[Category( "Skew Polyhedra (IRP and 4D)" )]
		[TypeConverter( typeof( IRPLevelsConverter ) )]
		public int XLevels { get; set; }

		[DataMember]
		[DisplayName( "IRP Y Levels" )]
		[Description( "The number of levels to draw in the y direction. y and Y are shortcut keys for decrementing and incrementing this value." )]
		[Category( "Skew Polyhedra (IRP and 4D)" )]
		[TypeConverter( typeof( IRPLevelsConverter ) )]
		public int YLevels { get; set; }

		[DataMember]
		[DisplayName( "IRP Z Levels" )]
		[Description( "The number of levels to draw in the z direction. z and Z are shortcut keys for decrementing and incrementing this value." )]
		[Category( "Skew Polyhedra (IRP and 4D)" )]
		[TypeConverter( typeof( IRPLevelsConverter ) )]
		public int ZLevels { get; set; }

		public void ClampLevels()
		{
			if( XLevels < 1 )
				XLevels = 1;
			if( XLevels > 31 )
				XLevels = 31;
			if( YLevels < 1 )
				YLevels = 1;
			if( YLevels > 31 )
				YLevels = 31;
			if( ZLevels < 1 )
				ZLevels = 1;
			if( ZLevels > 31 )
				ZLevels = 31;
		}

		[DataMember]
		[DisplayName( "4D Constrain to Hypersphere" )]
		[Description( "If true, our skew polyhedron will live in S3 rather than R4." )]
		[Category( "Skew Polyhedra (IRP and 4D)" )]
		public bool ConstrainToHypersphere { get; set; }

		[DataMember]
		[DisplayName( "Enable Lighting" )]
		[Description( "Enable lighting on IRP models." )]
		[Category( "Lighting" )]
		public bool EnableLighting { get; set; }

		[DataMember]
		[DisplayName( "Ambient lighting" )]
		[Description( "Controls the amount of ambient lighting. Range is 0 to 1." )]
		[Category( "Lighting" )]
		[Range( 0, 1 )]
		[Editor( typeof( TrackBarValueEditor<double> ), typeof( UITypeEditor ) )]
		public double AmbientLighting { get; set; }

		public enum EStereoType
		{
			CrossEyed,
			WallEyed,
		}

		[DataMember]
		[DisplayName( "Enable Stereo" )]
		[Description( "Display free-view stereo. This currently only applies to IRP puzzles." )]
		[Category( "Stereo" )]
		public bool EnableStereo { get; set; }

		[DataMember]
		[DisplayName( "Stereo Type" )]
		[Description( "The type of stereo to display." )]
		[Category( "Stereo" )]
		public EStereoType StereoType { get; set; }

		[DataMember]
		[DisplayName( "Stereo Separation" )]
		[Description( "The offset amount applied to each image of the stereo pair. " +
			"Move the slider to the left for less separation, or to the right for more. " +
			"At the default value of 1, the separation is 1/30th of the focal length, which is usually a comfortable amount." )]
		[Category( "Stereo" )]
		[Range( 0, 4 )]
		[Editor( typeof( TrackBarValueEditor<double> ), typeof( UITypeEditor ) )]
		public double StereoSeparation { get; set; }

		// NOTE: This member is a multiplier applied to the focal length in calculations.
		[DataMember]
		[DisplayName( "Focal Length" )]
		[Description( "This can be used to control the depth of the object in the screen. " +
			"Move the slider to the right to increase the focal length (make the object appear closer), and to the left to decrease the focal length (make the object appear further). " +
			"At a value of 2, the projection plane is at the origin, in which case half the object is in front of the screen and half behind." )]
		[Category( "Stereo" )]
		[Range( 0, 4 )]
		[Editor( typeof( TrackBarValueEditor<double> ), typeof( UITypeEditor ) )]
		public double FocalLength { get; set; }

		[OnDeserializing]
		private void SetValuesOnDeserializing( StreamingContext context )
		{
			// These defaults will get used when the settings have not already been persisted.
			this.Gliding = 0.5;
			this.EnableTextureMipmaps = true;
			this.EnableAntialiasing = true;
			this.ShowAsSkew = true;
			this.XLevels = this.YLevels = this.ZLevels = 3;
			this.ConstrainToHypersphere = true;
			this.EnableLighting = true;
			this.AmbientLighting = 0.4;
			this.StereoSeparation = 1;
			this.FocalLength = 1;
			this.ColorBg = Color.DarkGray;
			this.ColorTileEdges = Color.Black;
		}

		[DataMember]
		[DisplayName( "Twisting Circles" )]
		[Description( "The color of twisting circles." )]
		[Category( "Coloring" )]
		public Color ColorTwistingCircles { get; set; }

		[DataMember]
		[DisplayName( "Background" )]
		[Description( "Background Color" )]
		[Category( "Coloring" )]
		public Color ColorBg { get; set; }

		[DataMember]
		[DisplayName( "Tile Edges" )]
		[Description( "The color of tile edges (the small gaps between tiles), as well as slices." )]
		[Category( "Coloring" )]
		public Color ColorTileEdges { get; set; }

		[DataMember]
		[DisplayName( "Color 1" )]
		[Category( "Coloring" )]
		public Color Color1 { get; set; }

		[DataMember]
		[DisplayName( "Color 2" )]
		[Category( "Coloring" )]
		public Color Color2 { get; set; }

		[DataMember]
		[DisplayName( "Color 3" )]
		[Category( "Coloring" )]
		public Color Color3 { get; set; }

		[DataMember]
		[DisplayName( "Color 4" )]
		[Category( "Coloring" )]
		public Color Color4 { get; set; }

		[DataMember]
		[DisplayName( "Color 5" )]
		[Category( "Coloring" )]
		public Color Color5 { get; set; }

		[DataMember]
		[DisplayName( "Color 6" )]
		[Category( "Coloring" )]
		public Color Color6 { get; set; }

		[DataMember]
		[DisplayName( "Color 7" )]
		[Category( "Coloring" )]
		public Color Color7 { get; set; }

		[DataMember]
		[DisplayName( "Color 8" )]
		[Category( "Coloring" )]
		public Color Color8 { get; set; }

		[DataMember]
		[DisplayName( "Color 9" )]
		[Category( "Coloring" )]
		public Color Color9 { get; set; }

		[DataMember]
		[DisplayName( "Color 10" )]
		[Category( "Coloring" )]
		public Color Color10 { get; set; }

		[DataMember]
		[DisplayName( "Color 11" )]
		[Category( "Coloring" )]
		public Color Color11 { get; set; }

		[DataMember]
		[DisplayName( "Color 12" )]
		[Category( "Coloring" )]
		public Color Color12 { get; set; }

		[DataMember]
		[DisplayName( "Color 13" )]
		[Category( "Coloring" )]
		public Color Color13 { get; set; }

		[DataMember]
		[DisplayName( "Color 14" )]
		[Category( "Coloring" )]
		public Color Color14 { get; set; }

		[DataMember]
		[DisplayName( "Color 15" )]
		[Category( "Coloring" )]
		public Color Color15 { get; set; }

		[DataMember]
		[DisplayName( "Color 16" )]
		[Category( "Coloring" )]
		public Color Color16 { get; set; }

		[DataMember]
		[DisplayName( "Color 17" )]
		[Category( "Coloring" )]
		public Color Color17 { get; set; }

		[DataMember]
		[DisplayName( "Color 18" )]
		[Category( "Coloring" )]
		public Color Color18 { get; set; }

		[DataMember]
		[DisplayName( "Color 19" )]
		[Category( "Coloring" )]
		public Color Color19 { get; set; }

		[DataMember]
		[DisplayName( "Color 20" )]
		[Category( "Coloring" )]
		public Color Color20 { get; set; }

		[DataMember]
		[DisplayName( "Color 21" )]
		[Category( "Coloring" )]
		public Color Color21 { get; set; }

		[DataMember]
		[DisplayName( "Color 22" )]
		[Category( "Coloring" )]
		public Color Color22 { get; set; }

		[DataMember]
		[DisplayName( "Color 23" )]
		[Category( "Coloring" )]
		public Color Color23 { get; set; }

		[DataMember]
		[DisplayName( "Color 24" )]
		[Category( "Coloring" )]
		public Color Color24 { get; set; }

		[DataMember]
		[DisplayName( "Color 25" )]
		[Category( "Coloring" )]
		public Color Color25 { get; set; }

		[DataMember]
		[DisplayName( "Color 26" )]
		[Category( "Coloring" )]
		public Color Color26 { get; set; }

		[DataMember]
		[DisplayName( "Color 27" )]
		[Category( "Coloring" )]
		public Color Color27 { get; set; }

		[DataMember]
		[DisplayName( "Color 28" )]
		[Category( "Coloring" )]
		public Color Color28 { get; set; }

		[DataMember]
		[DisplayName( "Color 29" )]
		[Category( "Coloring" )]
		public Color Color29 { get; set; }

		[DataMember]
		[DisplayName( "Color 30" )]
		[Category( "Coloring" )]
		public Color Color30 { get; set; }

		[DataMember]
		[DisplayName( "Color 31" )]
		[Category( "Coloring" )]
		public Color Color31 { get; set; }

		[DataMember]
		[DisplayName( "Color 32" )]
		[Category( "Coloring" )]
		public Color Color32 { get; set; }

		[DataMember]
		[DisplayName( "Color 33" )]
		[Category( "Coloring" )]
		public Color Color33 { get; set; }

		[DataMember]
		[DisplayName( "Color 34" )]
		[Category( "Coloring" )]
		public Color Color34 { get; set; }

		[DataMember]
		[DisplayName( "Color 35" )]
		[Category( "Coloring" )]
		public Color Color35 { get; set; }

		[DataMember]
		[DisplayName( "Color 36" )]
		[Category( "Coloring" )]
		public Color Color36 { get; set; }

		[DataMember]
		[DisplayName( "Color 37" )]
		[Category( "Coloring" )]
		public Color Color37 { get; set; }

		[DataMember]
		[DisplayName( "Color 38" )]
		[Category( "Coloring" )]
		public Color Color38 { get; set; }

		[DataMember]
		[DisplayName( "Color 39" )]
		[Category( "Coloring" )]
		public Color Color39 { get; set; }

		[DataMember]
		[DisplayName( "Color 40" )]
		[Category( "Coloring" )]
		public Color Color40 { get; set; }

		[DataMember]
		[DisplayName( "Color 41" )]
		[Category( "Coloring" )]
		public Color Color41 { get; set; }

		[DataMember]
		[DisplayName( "Color 42" )]
		[Category( "Coloring" )]
		public Color Color42 { get; set; }

		[DataMember]
		[DisplayName( "Color 43" )]
		[Category( "Coloring" )]
		public Color Color43 { get; set; }

		[DataMember]
		[DisplayName( "Color 44" )]
		[Category( "Coloring" )]
		public Color Color44 { get; set; }

		[DataMember]
		[DisplayName( "Color 45" )]
		[Category( "Coloring" )]
		public Color Color45 { get; set; }

		[DataMember]
		[DisplayName( "Color 46" )]
		[Category( "Coloring" )]
		public Color Color46 { get; set; }

		[DataMember]
		[DisplayName( "Color 47" )]
		[Category( "Coloring" )]
		public Color Color47 { get; set; }

		[DataMember]
		[DisplayName( "Color 48" )]
		[Category( "Coloring" )]
		public Color Color48 { get; set; }

		[DataMember]
		[DisplayName( "Color 49" )]
		[Category( "Coloring" )]
		public Color Color49 { get; set; }

		[DataMember]
		[DisplayName( "Color 50" )]
		[Category( "Coloring" )]
		public Color Color50 { get; set; }

		[DataMember]
		[DisplayName( "Color 51" )]
		[Category( "Coloring" )]
		public Color Color51 { get; set; }

		[DataMember]
		[DisplayName( "Color 52" )]
		[Category( "Coloring" )]
		public Color Color52 { get; set; }

		[DataMember]
		[DisplayName( "Color 53" )]
		[Category( "Coloring" )]
		public Color Color53 { get; set; }

		[DataMember]
		[DisplayName( "Color 54" )]
		[Category( "Coloring" )]
		public Color Color54 { get; set; }

		[DataMember]
		[DisplayName( "Color 55" )]
		[Category( "Coloring" )]
		public Color Color55 { get; set; }

		[DataMember]
		[DisplayName( "Color 56" )]
		[Category( "Coloring" )]
		public Color Color56 { get; set; }

		private void SetupDefaultColors()
		{
			ColorTwistingCircles = Color.OrangeRed;
			ColorBg = Color.DarkGray;
			ColorTileEdges = Color.Black;
			Color1 = Color.White;
			Color2 = Color.Green;
			Color3 = Color.Blue;
			Color4 = Color.Yellow;
			Color5 = Color.Red;
			Color6 = Color.FromArgb( 255, 128, 0 );
			Color7 = Color.Cyan;
			Color8 = Color.Purple;
			Color9 = Color.Silver;
			Color10 = Color.Maroon;
			Color11 = Color.HotPink;
			Color12 = Color.SlateBlue;
			Color13 = Color.Olive;
			Color14 = Color.Tomato;
			Color15 = Color.DeepSkyBlue;
			Color16 = Color.MediumSpringGreen;
			Color17 = Color.LimeGreen;
			Color18 = Color.Gold;
			Color19 = Color.MediumVioletRed;
			Color20 = Color.FromArgb( 64, 64, 64 );
			Color21 = Color.Navy;
			Color22 = Color.Sienna;
			Color23 = Color.Chartreuse;
			Color24 = Color.Teal;
			Color25 = Color.FromArgb( 64, 0, 64 );
			Color26 = Color.Salmon;
			Color27 = Color.Aqua;
			Color28 = Color.LightSkyBlue;
			Color29 = Color.OrangeRed;
			Color30 = Color.FromArgb( 192, 192, 0 );
			Color31 = Color.RosyBrown;
			Color32 = Color.Tan;
			Color33 = Color.MintCream;
			Color34 = Color.BlueViolet;
			Color35 = Color.CadetBlue;
			Color36 = Color.DeepPink;
			Color37 = Color.Chocolate;
			Color38 = Color.Coral;
			Color39 = Color.CornflowerBlue;
			Color40 = Color.Khaki;
			Color41 = Color.Crimson;
			Color42 = Color.DarkGoldenrod;
			Color43 = Color.DarkGray;
			Color44 = Color.DarkGreen;
			Color45 = Color.DarkKhaki;
			Color46 = Color.DarkMagenta;
			Color47 = Color.DarkOliveGreen;
			Color48 = Color.DarkOrange;
			Color49 = Color.DarkOrchid;
			Color50 = Color.DarkRed;
			Color51 = Color.FromArgb( 255, 192, 255 );
			Color52 = Color.DarkSeaGreen;
			Color53 = Color.DarkSlateBlue;
			Color54 = Color.DarkSlateGray;
			Color55 = Color.DarkTurquoise;
			Color56 = Color.DarkViolet;
		}
	}
}
