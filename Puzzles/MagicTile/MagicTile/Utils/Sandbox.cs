namespace MagicTile.Utils
{
	using R3.Core;
	using R3.Geometry;
	using System.Collections.Generic;
	using System.IO;

	internal static class Sandbox
	{
		/// <summary>
		/// For playing around.
		/// </summary>
		public static void Run()
		{
			//PolytopeTest();
			//SliceTest();
			//PuzzleSaveTest();
			//VrmlLoadTest();
		}

		private static void PolytopeTest()
		{
			SkewPolyhedron.BuildBitruncated5Cell();
		}

		private static void SliceTest()
		{
			Polygon p = new Polygon();
			p.CreateRegular( 7, 3 );

			Circle c = p.CircumCircle;
			c.Radius *= .9;

			List<Polygon> output;
			Slicer.SlicePolygon( p, c, out output );
		}

		private static void PuzzleSaveTest()
		{
			PuzzleConfigClass test = new PuzzleConfigClass();
			test.P = 7;
			test.Q = 3;
			test.ClassDisplayName = "{7,3}";
			test.ClassID = "{7,3}";

			test.Identifications = new IdentificationList
			{ 
				new Identification( new int[] {3,3,3}, 0, useMirroredSet: true )
			};

			PuzzleSpecific specific = new PuzzleSpecific();
			specific.SlicingCircles = new SlicingCircles();
			specific.DisplayName = "booger";
			PuzzleSpecificList list = new PuzzleSpecificList();
			list.Add( specific );
			test.PuzzleSpecificList = list;

			PuzzleConfigClass.Save( test, "user/testConfig.xml" );
		}

		private static void VrmlLoadTest()
		{
			string path = Path.Combine( StandardPaths.ConfigDir, "IRP/5_5_cell.wrl" );
			VRML.LoadIRP( path );
		}
	}
}
