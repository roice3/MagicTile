namespace R3.Core
{
	using R3.Geometry;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;

	public class VRMLInfo
	{
		// ZZZ - This should be a map of exposed fields, not strongly typed to IRP values here.
		public Vector3D DX { get; set; }
		public Vector3D DY { get; set; }
		public Vector3D DZ { get; set; }
		public Polygon[] Polygons { get; set; }
	}

	public class VRML
	{
		/// <summary>
		/// Load IRP data from a vrml file.
		/// </summary>
		public static VRMLInfo LoadIRP( string path )
		{
			try
			{
				System.Console.WriteLine( string.Format( "Reading VRML file {0}", path ) );
				string[] lines = File.ReadAllLines( path );
				return LoadInternal( lines );
			}
			catch( System.Exception e )
			{
				System.Console.WriteLine( e.Message );
				return null;
			}
		}

		private static VRMLInfo LoadInternal( string[] lines )
		{
			Vector3D[] points = null;
			VRMLInfo result = new VRMLInfo();
			Vector3D dx = new Vector3D(), dy = new Vector3D(), dz = new Vector3D();

			string pointStart = vrml_point + vrml_arrayStart;
			string polyStart = vrml_coordIndex + vrml_arrayStart;

			int current = 0;
			while( current < lines.Length && result.Polygons == null )
			{
				string cleaned = CleanupLine( lines[current] );

				if( cleaned.StartsWith( vrml_field ) )
					LoadField( cleaned, ref dx, ref dy, ref dz );

				if( cleaned.EndsWith( pointStart ) )
					points = LoadPoints( lines, ref current );

				if( cleaned.EndsWith( polyStart ) )
				{
					// Once we load this, we're done.
					result.Polygons = LoadPolygons( points, lines, ref current );
					break;
				}

				current++;
			}

			result.DX = dx;
			result.DY = dy;
			result.DZ = dz;
			return result;
		}

		private static void LoadField( string line, ref Vector3D dx, ref Vector3D dy, ref Vector3D dz )
		{
			string[] split = SplitLine( line );
			if( split.Length != 4 )
				throw new System.Exception( "ExposedField not in expected format." );

			string var = split[2];
			double val = double.Parse( split[3], CultureInfo.InvariantCulture );
			if( var == "dx1" )
				dx.X = val;
			else if( var == "dx2" )
				dx.Y = val;
			else if( var == "dx3" )
				dx.Z = val;
			else if( var == "dy1" )
				dy.X = val;
			else if( var == "dy2" )
				dy.Y = val;
			else if( var == "dy3" )
				dy.Z = val;
			else if( var == "dz1" )
				dz.X = val;
			else if( var == "dz2" )
				dz.Y = val;
			else if( var == "dz3" )
				dz.Z = val;
		}

		private static Vector3D[] LoadPoints( string[] lines, ref int index )
		{
			List<Vector3D> result = new List<Vector3D>();

			while( true )
			{
				index++;
				if( index >= lines.Length )
					throw new System.Exception( "Point array was never closed." );

				string cleaned = CleanupLine( lines[index] );
				if( string.IsNullOrEmpty( cleaned ) )
					continue;

				string[] split = SplitLine( cleaned );
				if( split.Length == 0 )
					continue;

				// Allow the array end to be on the same line as a point definition.
				bool pointValid = false;
				if( 3 == split.Length || ( 4 == split.Length && split[3].Contains( vrml_arrayEnd ) ) ) 
				{
					Vector3D newPoint = new Vector3D(
						double.Parse( split[0], CultureInfo.InvariantCulture ),
						double.Parse( split[1], CultureInfo.InvariantCulture ),
						double.Parse( split[2], CultureInfo.InvariantCulture ) );
					result.Add( newPoint );
					pointValid = true;
				}

				if( cleaned.Contains( vrml_arrayEnd ) )	
					break;

				// We should be valid or have ended.
				if( !pointValid )
					throw new System.Exception( string.Format( "Point coordinates at line {0} not in readable format.", index ) );
			}

			return result.ToArray();
		}

		private static Polygon[] LoadPolygons( Vector3D[] points, string[] lines, ref int index )
		{
			List<Polygon> result = new List<Polygon>();
			if( points == null )
				throw new System.Exception( "VRML: point array must be loaded before coordIndex array." );

			// One polygon for each line.
			while( true )
			{
				index++;
				if( index >= lines.Length )
					throw new System.Exception( "CoordIndex array was never closed." );

				string cleaned = CleanupLine( lines[index] );
				if( string.IsNullOrEmpty( cleaned ) )
					continue;

				string[] split = SplitLine( cleaned );
				if( split.Length == 0 )
					continue;

				List<Vector3D> polyPoints = new List<Vector3D>();
				bool done = false;
				for( int i = 0; i < split.Length; i++ )
				{
					// Allow the array end to be on the same line as a point definition.
					if( split[i].Contains( vrml_arrayEnd ) )
					{
						done = true;
						break;
					}

					int idx = int.Parse( split[i] );
					if( idx != -1 )	// This looks to be used to delimit the end of a poly definition.
						polyPoints.Add( points[idx] );
				}

				if( polyPoints.Count > 0 )
				{
					Polygon poly = new Polygon();
					poly.CreateEuclidean( polyPoints.ToArray() );
					result.Add( poly );
				}

				if( done )
					break;
			}

			return result.ToArray();
		}

		/// <summary>
		/// This will exclude comments and remove whitespace.
		/// </summary>
		private static string CleanupLine( string line )
		{
			int comment = line.IndexOf( '#' );
			if( comment != -1 )
				line = line.Substring( 0, comment );
			return line.Trim();
		}

		private static string[] SplitLine( string line )
		{
			return line.Split( new char[] { ' ', ',', '\t' }, System.StringSplitOptions.RemoveEmptyEntries );
		}

		public static void AppendShape( string path, string texFile, Vector3D[] points, short[] elements, Vector3D[] textureCoords, bool reverse, bool skipMiddle )
		{
			using( StreamWriter sw = File.AppendText( path ) )
			{
				sw.Write( 
					"Shape { \r\n" +
					"  appearance Appearance { \r\n" +
					"	texture ImageTexture { \r\n" +
					"	  url \"" + texFile + "\" \r\n" +
					"	} \r\n" +
					"  } \r\n" +
					"  geometry IndexedFaceSet { \r\n" );

				WriteElements( sw, elements, reverse, skipMiddle ); 
				WritePoints( sw, points );
				WriteTexCoords( sw, textureCoords );

				sw.Write(
					"  } \r\n" +
					"} " );
			}
		}

		public static void AppendShape( string path, string texFile, Vector3D[] points, short[] elements, System.Drawing.Color color, bool reverse, bool skipMiddle )
		{
			using( StreamWriter sw = File.AppendText( path ) )
			{
				float r = (float)color.R / 255;
				float g = (float)color.G / 255;
				float b = (float)color.B / 255;

				sw.Write(
					"Shape { \r\n" +
					"  appearance Appearance { \r\n" +
					"	material Material { \r\n" +
					"	  diffuseColor " + r + ", " + g + ", " + b + " \r\n" +
					"	} \r\n" +
					"  } \r\n" +
					"  geometry IndexedFaceSet { \r\n" );

				WriteElements( sw, elements, reverse, skipMiddle );
				WritePoints( sw, points );

				sw.Write(
					"  } \r\n" +
					"} " );
			}
		}

		private static void WriteElements( StreamWriter sw, short[] elements, bool reverse, bool skipMiddle )
		{
			sw.Write(
				"    coordIndex [ \r\n" );

			for( int i=0; i<elements.Length/3; i++ )
			{
				int idx1 = i * 3;
				int idx2 = i * 3 + 1;
				int idx3 = i * 3 + 2;
				if( reverse )
					Utils.Swap( ref idx1, ref idx2 );

				//if( skipMiddle && i % 256 >= 192 )
				if( skipMiddle && i % 1024 >= 624 )
					continue;

				sw.WriteLine( string.Format( "{0}, {1}, {2}, -1", elements[idx1], elements[idx2], elements[idx3] ) );
			}

			sw.Write(
				"    ] \r\n" );
		}

		private static void WritePoints( StreamWriter sw, Vector3D[] points )
		{
			sw.Write(
				"    coord Coordinate { \r\n" +
				"      point [ \r\n" );

			foreach( Vector3D v in points )
				sw.WriteLine( string.Format( "{0} {1} {2},", v.X, v.Y, v.Z ) );

			sw.Write(
				"      ] \r\n" +
				"    } \r\n" );
		}

		private static void WriteTexCoords( StreamWriter sw, Vector3D[] textureCoords )
		{
			sw.Write(
				"    texCoord TextureCoordinate { \r\n" +
				"      point [ \r\n" );

			foreach( Vector3D v in textureCoords )
				sw.WriteLine( string.Format( "{0} {1},", v.X, v.Y ) );

			sw.Write(
				"      ] \r\n" +
				"    } \r\n" );
		}

		// VRML formatting
		private static string vrml_point = "point";
		private static string vrml_coordIndex = "coordIndex";
		private static char vrml_arrayStart = '[';
		private static char vrml_arrayEnd = ']';
		private static string vrml_field = "exposedField";
	}
}
