namespace R3.Core
{
	using R3.Geometry;
	using System.Collections.Generic;
	using System.Linq;
	using System.Xml.Linq;

	public class SVG
	{
		public static void WritePolygons( string file, List<Polygon> polygons )
		{
			// Getting units right was troublesome.
			// See http://stackoverflow.com/questions/1346922/svg-and-dpi-absolute-units-and-user-units-inkscape-vs-firefox-vs-imagemagick/2306752#2306752

			XDocument xDocument = 
				new XDocument( new XElement( "svg",
					new XAttribute( "width", @"5in" ),
					new XAttribute( "height", @"5in" ),
					new XAttribute( "viewBox", @"0 0 5 5" ),
					new XElement( "g",
						new XAttribute( "style", @"fill:none;stroke:#0000FF;stroke-width:0.00005in" ),
						/*new XElement( "circle",
							new XAttribute( "cx", "0" ),
							new XAttribute( "cy", "0" ),
							new XAttribute( "r", "1" ) 
						),*/
						polygons.Select( poly => XPolygon( poly ) )
			)));

			xDocument.Save( file );
		}

		private static XElement XPolygon( Polygon poly )
		{
			// This makes the unit disk have a 1 inch radius.
			// The 72 / 25.4 is because Inkscape templates use pt for their main SVG units.
			const double scale = 1;//( 254 / 2 ) * ( 72 / 25.4 );

			string coords = "";
			for( int i=0; i<poly.Segments.Count; i++ )
			{
				Segment seg = poly.Segments[i];

				if( 0 == i )
					coords += "M " + FormatPoint( seg.P1, scale );

				if( seg.Type == SegmentType.Arc )
				{
					bool largeArc = seg.Angle > System.Math.PI;
					bool sweepDirection =  !seg.Clockwise;
					coords += "A " + FormatDouble( seg.Radius, scale ) + "," + FormatDouble( seg.Radius, scale ) + " 0 " + 
						FormatBool( largeArc ) + FormatBool( sweepDirection ) + FormatPoint( seg.P2, scale );
				}
				else
				{
					coords += "L " + FormatPoint( seg.P2, scale );
				}
			}

			return new XElement( "path",
				new XAttribute( "d", coords )
			);
		}

		private static string FormatBool( bool value )
		{
			return value ? "1 " : "0 ";
		}

		private static string FormatPoint( Vector3D p, double scale )
		{
			return FormatDouble( p.X, scale ) + "," + FormatDouble( p.Y, scale ) + " ";
		}

		private static string FormatDouble( double d, double scale )
		{
			return string.Format( "{0:F6}", d * scale );
		}
	}
}
