namespace R3.Drawing
{
	using OpenTK;
	using OpenTK.Graphics.OpenGL;
	using R3.Geometry;

	/// <summary>
	/// A helper class with OpenGL lighting utility methods.
	/// </summary>
	public static class Lighting
	{
		/// <summary>
		/// Sets up ambient light.
		/// </summary>
		public static void SetupAmbient( float ambient )
		{
			float[] ambient_light = { ambient, ambient, ambient, 1 };
			GL.LightModel( LightModelParameter.LightModelAmbient, ambient_light );
		}

		/// <summary>
		/// Sets up and enables a light and a given position.
		/// </summary>
		public static void SetupLightZero( Vector3D position, float ambient )
		{
			float[] ambient_light = { ambient, ambient, ambient, 1.0f };
			float[] spec = { 0.5f, 0.5f, 0.5f, 1.0f };
			float[] one = { 1.0f, 1.0f, 1.0f, 1.0f };

			GL.Light( LightName.Light0, LightParameter.Position, new float[] { (float)position.X, (float)position.Y, (float)position.Z } );
			GL.Light( LightName.Light0, LightParameter.Ambient, ambient_light );
			GL.Light( LightName.Light0, LightParameter.Diffuse, one );
			GL.Light( LightName.Light0, LightParameter.Specular, spec );
			//GL.Light( LightName.Light0, LightParameter.SpotExponent, 100 );	// For directional lights.

			GL.LightModel( LightModelParameter.LightModelTwoSide, 1 );
			GL.LightModel( LightModelParameter.LightModelLocalViewer, 1 );	// Needed for specular
			GL.LightModel( LightModelParameter.LightModelColorControl, 0x81FA );
			GL.Enable( EnableCap.Light0 );
		}

		public static void SetDefaultMaterial( float ambient )
		{
			float[] ambient_light = { ambient, ambient, ambient, 1.0f };
			float[] one = { 1.0f, 1.0f, 1.0f, 1.0f };
			float[] zero = { 0.0f, 0.0f, 0.0f, 1.0f };

			GL.Material( MaterialFace.FrontAndBack, MaterialParameter.Ambient, ambient_light );
			GL.Material( MaterialFace.FrontAndBack, MaterialParameter.Diffuse, one );
			GL.Material( MaterialFace.FrontAndBack, MaterialParameter.Specular, one );
			GL.Material( MaterialFace.FrontAndBack, MaterialParameter.Emission, new float[] { 0.1f, 0.1f, 0.1f, 1.0f } );
			GL.Material( MaterialFace.FrontAndBack, MaterialParameter.Shininess, 128 );
		}
	}
}
