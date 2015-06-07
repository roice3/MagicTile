namespace R3.Core
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using R3.Geometry;

	public class PovRay
	{
		public class Parameters
		{
			public double Scale = 1;
			public bool Halfspace = false;
			public bool ThinEdges = false;
			public double AngularThickness = 0.13;
		}

		/// <summary>
		/// Make a povray file for all the edges of an H3 model.
		/// Input edge locations are expected to live in the ball model.
		/// </summary>
		public static void WriteH3Edges( Parameters parameters, H3.Cell.Edge[] edges, string fileName, bool append = false )
		{
			if( append )
			{
				using( StreamWriter sw = File.AppendText( fileName ) )
				{
					foreach( H3.Cell.Edge edge in edges )
						sw.WriteLine( H3Edge( parameters, edge ) );
				}
			}
			else
			{
				using( StreamWriter sw = File.CreateText( fileName ) )
				{
					foreach( H3.Cell.Edge edge in edges )
						sw.WriteLine( H3Edge( parameters, edge ) );
				}
			}
		}

		private static string H3Edge( Parameters parameters, H3.Cell.Edge edge )
		{
			Vector3D v1 = edge.Start, v2 = edge.End;

			Vector3D[] points = null;
			Func<Vector3D, Sphere> sizeFunc = v => new Sphere() { Center = v, Radius = H3Models.SizeFuncConst( v, parameters.Scale ) };

			if( parameters.Halfspace )
			{
				points = H3Models.UHS.GeodesicPoints( v1, v2 );
				if( !parameters.ThinEdges )
					sizeFunc = v => 
					{
						// XXX, inexact
						return new Sphere() { Center = v, Radius = H3Models.UHS.SizeFunc( v, parameters.AngularThickness ) };
					};
			}
			else
			{
				points = H3Models.Ball.GeodesicPoints( v1, v2 );
				if( !parameters.ThinEdges )
					sizeFunc = v =>
					{
						Vector3D c;
						double r;
						H3Models.Ball.DupinCyclideSphere( v, parameters.AngularThickness/2, out c, out r );
						return new Sphere() { Center = c, Radius = r };
						//return new Sphere() { Center = v, Radius = H3Models.Ball.SizeFunc( v, parameters.AngularThickness ) }; // inexact
					};
			}

			return H3EdgeSphereSweep( points, sizeFunc );
		}

		private static string H3EdgeSphereSweep( Vector3D[] points, Func<Vector3D,Sphere> sphereFunc )
		{
			if( points.Length < 2 )
				throw new System.ArgumentException();

			// For the cubic spline, repeat the first and last points.
			List<Vector3D> appended = new List<Vector3D>();
			appended.Add( points.First() );
			appended.AddRange( points );
			appended.Add( points.Last() );
	
			Func<Vector3D,string> formatVecAndSize = v =>
			{
				Sphere s = sphereFunc( v );
				return string.Format( "<{0:G6},{1:G6},{2:G6}>,{3:G6}", s.Center.X, s.Center.Y, s.Center.Z, s.Radius );
			};

			// Use b_spline http://bugs.povray.org/task/81

			string formattedPoints = string.Join( ",", appended.Select( formatVecAndSize ).ToArray() );
			return string.Format( "sphere_sweep {{ b_spline {0}, {1} texture {{tex}} }}", points.Length + 2, formattedPoints );
		}

		/// <summary>
		/// Append facets to the end of an existing povray file.
		/// </summary>
		public static void AppendFacets( Sphere[] facets, string fileName )
		{
			using( StreamWriter sw = File.AppendText( fileName ) )
			{
				foreach( Sphere sphere in facets )
					sw.WriteLine( H3Facet( sphere ) );
			}
		}

		private static string H3Facet( Sphere sphere )
		{
			if( sphere.IsPlane )
			{
				Vector3D offsetOnNormal = Euclidean2D.ProjectOntoLine( sphere.Offset, new Vector3D(), sphere.Normal );
				return string.Format( "plane {{ {0}, {1:G6} material {{ sphereMat }} clipped_by {{ ball }} }}",
					FormatVec( sphere.Normal ), offsetOnNormal.Abs() );
			}
			else
			{
				return string.Format( "sphere {{ {0}, {1:G6} material {{ sphereMat }} clipped_by {{ ball }} }}",
					FormatVec( sphere.Center ), sphere.Radius );
			}
		}

		/// <summary>
		/// An alternative version for facets that require extra clipping.
		/// </summary>
		public static void AppendFacets( H3.Cell[] cells, string fileName )
		{
			HashSet<Sphere> completed = new HashSet<Sphere>();
			using( StreamWriter sw = File.AppendText( fileName ) )
			{
				foreach( H3.Cell cell in cells )
					sw.WriteLine( H3Facet( cell, completed ) );
			}
		}

		private static string H3Facet( H3.Cell cell, HashSet<Sphere> completed )
		{
			StringBuilder sb = new StringBuilder();

			foreach( H3.Cell.Facet facet in cell.Facets )
			{
				if( completed.Contains( facet.Sphere ) )
					continue;

				bool invert1 = !facet.Sphere.IsPointInside( cell.Center );
				sb.Append( string.Format( "sphere {{ {0}, {1:G6}{2} material {{ sphereMat }} clipped_by {{ ball }}",
					FormatVec( facet.Sphere.Center ), facet.Sphere.Radius, invert1 ? " inverse" : string.Empty ) );

				H3.Cell.Facet[] others = cell.Facets.Except( new H3.Cell.Facet[] { facet } ).ToArray();
				foreach( H3.Cell.Facet otherFacet in others )
				{
					bool invert = !otherFacet.Sphere.IsPointInside( cell.Center );
					sb.Append( string.Format( " clipped_by {{ {0} }}", FormatSphereNoMaterial( otherFacet.Sphere, invert ) ) );
				}

				sb.AppendLine( " }" );

				completed.Add( facet.Sphere );
			}

			return sb.ToString();
		}

		/// <summary>
		/// A version for the fundamental simplex.
		/// </summary>
		public static void CreateSimplex( Sphere[] facets, string fileName )
		{
			using( StreamWriter sw = File.CreateText( fileName ) )
			{
				sw.WriteLine( SimplexFacets( facets ) );
			}
		}

		private static string SimplexFacets( Sphere[] facets )
		{
			StringBuilder sb = new StringBuilder();

			foreach( Sphere facet in facets )
			{
				//bool invert1 = !facet.IsPointInside( cell.Center );
				bool invert1 = false;
				sb.Append( string.Format( "{0} material {{ sphereMat }} clipped_by {{ ball }}",
					FormatSphereNoMaterial( facet, invert1, false ) ) );

				Sphere[] others = facets.Except( new Sphere[] { facet } ).ToArray();
				foreach( Sphere otherFacet in others )
				{
					//bool invert = !otherFacet.IsPointInside( cell.Center );
					bool invert = true;
					sb.Append( string.Format( " clipped_by {{ {0} }}", FormatSphereNoMaterial( otherFacet, invert ) ) );
				}

				sb.AppendLine( " }" );
			}

			return sb.ToString();
		}

		private static string FormatSphereNoMaterial( Sphere sphere, bool invert, bool includeClosingBracket = true )
		{
			if( sphere.IsPlane )
			{
				Vector3D offsetOnNormal = Euclidean2D.ProjectOntoLine( sphere.Offset, new Vector3D(), sphere.Normal );
				return string.Format( "plane {{ {0}, {1:G6}{2} {3}",
					FormatVec( sphere.Normal ), offsetOnNormal.Abs(), invert ? " inverse" : string.Empty, 
					includeClosingBracket ? "}" : string.Empty );
			}
			else
			{
				return string.Format( "sphere {{ {0}, {1:G6}{2} {3}",
					FormatVec( sphere.Center ), sphere.Radius, invert ? " inverse" : string.Empty,
					includeClosingBracket ? "}" : string.Empty );
			}
		}

		private static string FormatVec( Vector3D v )
		{
			return string.Format( "<{0:G6},{1:G6},{2:G6}>", v.X, v.Y, v.Z );
		}
	}
}
