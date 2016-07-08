namespace R3.Geometry
{
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Math = System.Math;

	/// <summary>
	/// A class to play around with H3 honecombs
	/// </summary>
	public class H3
	{
		/// <summary>
		/// We can track a cell by its ideal vertices.
		/// We'll work with these in the plane.
		/// </summary>
		public class Cell
		{
			public Cell( Facet[] facets ) : this( -1, facets )
			{
			}

			public Cell( int p, Facet[] facets )
			{
				P = p;
				Facets = facets;
				Depths = new int[4];
			}
			
			public int P; // Number of edges in polygon
			public Facet[] Facets;
			public Vector3D Center;

			// Not necessary.
			public Mesh Mesh;

			/// <summary>
			/// Used to track recursing depth of reflections across various mirrors.
			/// </summary>
			public int[] Depths;

			public bool IdealVerts
			{
				get
				{
					if( this.Verts.Count() == 0 )
						return false;
					return Tolerance.Equal( this.Verts.First().MagSquared(), 1 );
				}
			}

			public void AppendAllEdges( HashSet<Edge> edges )
			{
				foreach( Facet f in Facets )
					f.AppendAllEdges( edges );
			}

			/// <summary>
			/// In Ball model.
			/// </summary>
			public void CalcCenterFromFacets()
			{
				Vector3D center = new Vector3D();
				foreach( Sphere s in this.Facets.Select( f => f.Sphere ) )
				{
					if( s.IsPlane )
						continue;

					Vector3D sCenter = s.Center;
					double abs = s.Center.Abs();
					sCenter.Normalize();
					sCenter *= abs - s.Radius;
					center += sCenter;
				}

				center /= this.Facets.Length;
				this.Center = center;
			}

			public class Facet
			{
				public Facet( Vector3D[] verts ) { Verts = verts; }
				public Facet( Sphere sphere ) { Sphere = sphere; }

				/// <summary>
				/// The facet vertices.
				/// May live on the plane, in the ball model, etc. as needed.
				/// </summary>
				public Vector3D[] Verts;

				/// <summary>
				/// This is an alternate way to track facets, and *required* 
				/// for Lorentzian honeycombs, because all the vertices are hyperideal.
				/// It is expected that these are always in the Ball model.
				/// </summary>
				public Sphere Sphere { get; set; }
				private Vector3D CenterInBall 
				{ 
					get
					{
						if( Infinity.IsInfinite( Sphere.Radius ) )
							return new Vector3D();

						// Calcs based on orthogonal circles.
						// http://mathworld.wolfram.com/OrthogonalCircles.html
						double d = Math.Sqrt( 1 + Sphere.Radius * Sphere.Radius );
						Vector3D center = Sphere.Center;
						center.Normalize();
						center *= ( d - Sphere.Radius );
						return center;
					}
				}

				public void CalcSphereFromVerts( Geometry g )
				{
					switch( g )
					{
						case Geometry.Spherical:

							Sphere = new Sphere();
							if( Verts.Where( v => v == new Vector3D() ).Count() > 0 )	// XXX - not general, I'm so lazy.
							{
								Vector3D[] nonZero = Verts.Where( v => v != new Vector3D() ).ToArray();
								Sphere.Radius = double.PositiveInfinity;
								Sphere.Center = nonZero[0].Cross( nonZero[1] );
							}
							else
							{
								// The sphere intersects the unit-sphere at a unit-circle (orthogonal to the facet center direction).
								Vector3D direction = new Vector3D();
								foreach( Vector3D v in Verts )
									direction += v;
								direction /= Verts.Length;
								direction.Normalize();

								Vector3D p1 = Euclidean3D.ProjectOntoPlane( direction, new Vector3D(), Verts[0] );
								p1.Normalize();

								Circle3D c = new Circle3D( p1, Verts[0], -p1 );
								Sphere.Center = c.Center;
								Sphere.Radius = c.Radius;
							}

							break;

						case Geometry.Euclidean:

							Sphere = new Sphere();
							Sphere.Radius = double.PositiveInfinity;
							Vector3D v1 = Verts[0], v2 = Verts[1], v3 = Verts[2];
							Sphere.Center = ( v2 - v1 ).Cross( v3 - v1 );
							Sphere.Offset = Euclidean3D.ProjectOntoPlane( Sphere.Center, v1, new Vector3D() );
							break;

						case Geometry.Hyperbolic:
							Sphere = H3Models.Ball.OrthogonalSphereInterior( Verts[0], Verts[1], Verts[2] );
							break;
					}
				}
				
				public Facet Clone()
				{
					Facet newFacet = new Facet( Verts == null ? null : (Vector3D[])Verts.Clone() );
					newFacet.Sphere = Sphere == null ? null : Sphere.Clone();
					return newFacet;
				}

				public void Reflect( Sphere sphere )
				{
					if( Verts != null )
					{
						for( int i=0; i<Verts.Length; i++ )
							Verts[i] = sphere.ReflectPoint( Verts[i] );
					}

					if( Sphere != null )
						Sphere.Reflect( sphere );
				}

				public Vector3D ID
				{
					get
					{
						Vector3D result = new Vector3D();
						if( Verts != null )
						{
							foreach( Vector3D v in Verts )
								result += v;
							return result;
						}

						if( Sphere != null )
							return Sphere.ID;

						throw new System.ArgumentException();
					}
				}

				public void AppendAllEdges( HashSet<Edge> edges )
				{
					// We can only do this if we have vertices.
					if( Verts == null )
						return;

					for( int i=0; i<Verts.Length; i++ )
					{
						int idx1 = i;
						int idx2 = i == Verts.Length - 1 ? 0 : i + 1;
						edges.Add( new Edge( Verts[idx1], Verts[idx2] ) );
					}
				}
			}

			public class Edge
			{
				public Edge( Vector3D v1, Vector3D v2, bool order = true )
				{
					// Keep things "ordered", so we can easily compare edges.
					if( order )
					{
						Vector3D[] orderedVerts = new Vector3D[] { v1, v2 };
						orderedVerts = orderedVerts.OrderBy( v => v, new Vector3DComparer() ).ToArray();
						Start = orderedVerts[0];
						End = orderedVerts[1];
					}
					else
					{
						Start = v1;
						End = v2;
					}

					Depths = new int[4];
				}

				public Vector3D Start;
				public Vector3D End;

				// The reason we use a vector here is so the components 
				// can be interpreted in different color schemes (HLS, RGB, etc.)
				public Vector3D Color;

				/// <summary>
				/// Used to track recursing depth of reflections across various mirrors.
				/// </summary>
				public int[] Depths;

				public Edge Clone()
				{
					return (Edge)MemberwiseClone();
				}

				public Vector3D ID
				{
					get
					{
						return Start + End;
					}
				}

				public Vector3D Opp( Vector3D v )
				{
					return v == Start ? End : Start;
				}

				public void Write( StreamWriter sw, int level )
				{
					sw.WriteLine( string.Format( "{0},{1},{2}", level, Start.ToStringXYZOnly(), End.ToStringXYZOnly() ) );
				}

				public void CopyDepthsFrom( Edge e )
				{
					Depths = (int[])e.Depths.Clone();
				}
			}

			public class EdgeEqualityComparer : IEqualityComparer<Edge>
			{
				public bool Equals( Edge e1, Edge e2 )
				{
					return
						e1.Start.Compare( e2.Start, m_tolerance ) &&
						e1.End.Compare( e2.End, m_tolerance );
				}

				public int GetHashCode( Edge e )
				{
					return e.Start.GetHashCode() ^ e.End.GetHashCode();
				}

				private double m_tolerance = 0.0001;
			}

			public bool HasVerts
			{
				get
				{
					foreach( Facet f in Facets )
						if( f.Verts == null )
							return false;
					return true;
				}
			}

			public IEnumerable<Vector3D> Verts
			{
				get
				{
					foreach( Facet facet in Facets )
					{
						if( facet.Verts != null )
							foreach( Vector3D v in facet.Verts )
								yield return v;
					}
				}
			}

			public Vector3D ID
			{
				get 
				{
					Vector3D result = new Vector3D();
					//foreach( Vector3D v in Verts )
						//result += Sterographic.PlaneToSphereSafe( v );	// XXX - what about when not working in plane.

					if( HasVerts )
					{
						foreach( Vector3D v in Verts )
							result += v;
					}
					else
					{
						// Intentionally just use the center.
					}
					result += Center;
					return result;
				}
			}

			public Cell Clone()
			{
				List<Facet> newFacets = new List<Facet>();
				foreach( Facet facet in Facets )
					newFacets.Add( facet.Clone() );

				Cell clone = new Cell( P, newFacets.ToArray() );
				clone.Center = Center;
				clone.Depths = (int[])Depths.Clone();

				if( Mesh != null )
					clone.Mesh = Mesh.Clone();

				return clone;
			}

			/// <summary>
			/// Moves our vertices from the plane to the sphere.
			/// </summary>
			public void ToSphere()
			{
				foreach( Facet facet in Facets )
					for( int i=0; i<P; i++ )
						facet.Verts[i] = Sterographic.PlaneToSphereSafe( facet.Verts[i] );
			}

			/// <summary>
			/// Scales cell to circumsphere.
			/// NOTE: We should already be on a sphere, not the plane.
			/// </summary>
			public void ScaleToCircumSphere( double r )
			{
				foreach( Facet facet in Facets )
					for( int i=0; i<P; i++ )
					{
						Vector3D axis = facet.Verts[i];
						axis.Normalize();
						facet.Verts[i] = axis * r;
					}
			}

			/// <summary>
			/// Apply a Mobius transformation to us (meaning of Mobius transform is on boundary of UHS model).
			/// </summary>
			public void ApplyMobius( Mobius m )
			{
				foreach( Facet facet in Facets )
					for( int i = 0; i < P; i++ )
						facet.Verts[i] = H3Models.Ball.ApplyMobius( m, facet.Verts[i] );
				Center = H3Models.Ball.ApplyMobius( m, Center );
			}

			public void Reflect( Sphere sphere )
			{
				foreach( Facet facet in Facets )
					facet.Reflect( sphere );
				Center = sphere.ReflectPoint( Center );

				if( this.Mesh != null )
				{
					for( int i=0; i<Mesh.Triangles.Count; i++ )
					{
						Mesh.Triangle tri = Mesh.Triangles[i];
						tri.a = sphere.ReflectPoint( tri.a );
						tri.b = sphere.ReflectPoint( tri.b );
						tri.c = sphere.ReflectPoint( tri.c );
						Mesh.Triangles[i] = tri;
					}
				}
			}
		}

		public static class Util
		{
			public static void AddEdges( Cell cell, int level, Dictionary<Cell.Edge,int> completedEdges )
			{
				foreach( Cell.Facet facet in cell.Facets )
				{
					for( int i=0; i<cell.P; i++ )
					{	
						int i1 = i;
						int i2 = i == cell.P - 1 ? 0 : i + 1;

						Cell.Edge edge = new Cell.Edge( facet.Verts[i1], facet.Verts[i2] );
						if( completedEdges.ContainsKey( edge ) )
							continue;

						completedEdges.Add( edge, level );
					}
				}
			}

			public static void AddToMeshInternal( Shapeways mesh, Vector3D _v1, Vector3D _v2 )
			{
				// Move to ball.
				//Vector3D v1 = Sterographic.PlaneToSphereSafe( _v1 );
				//Vector3D v2 = Sterographic.PlaneToSphereSafe( _v2 );
				Vector3D v1 = _v1, v2 = _v2;

				System.Func<Vector3D, double> sizeFunc = SizeFuncConst;
				if( m_settings.Halfspace )
				{
					Vector3D[] points = H3Models.UHS.GeodesicPoints( _v1, _v2 );
					if( !m_settings.ThinEdges )
						sizeFunc = v => H3Models.UHS.SizeFunc( v, m_settings.AngularThickness );
					mesh.AddCurve( points, sizeFunc );
				}
				else
				{
					Vector3D[] points = H3Models.Ball.GeodesicPoints( v1, v2 );
					if( !m_settings.ThinEdges )
						sizeFunc = v => H3Models.Ball.SizeFunc( v, m_settings.AngularThickness );
					mesh.AddCurve( points, sizeFunc );
				}
			}

			/// <summary>
			/// Used to help figure out how thin our wires are going to get.
			/// </summary>
			internal static void LogMinSize( Dictionary<Cell.Edge, int> edges )
			{
				double max = 0;
				foreach( Cell.Edge e in edges.Keys )
				{
					max = Math.Max( max, e.Start.Abs() );
					max = Math.Max( max, e.End.Abs() );
				}

				Vector3D v = new Vector3D( 0, 0, max );
				double radius = H3Models.Ball.SizeFunc( v, m_settings.AngularThickness );
				double thickness = radius * 2 * m_settings.Scale;
				System.Console.WriteLine( string.Format( "location = {0}, thickness = {1}", max, thickness ) );

				if( thickness < 0.87 )
					System.Console.WriteLine( "WARNING!!!!!!! Edge thickness will be too small for shapeways.  You will be REJECTED." );
			}

			private static void AddCoordSpheres( Shapeways mesh )
			{
				mesh.AddSphere( new Vector3D( 1.5, 0, 0 ), 0.1 );
				mesh.AddSphere( new Vector3D( 0, 1.5, 0 ), 0.2 );
				mesh.AddSphere( new Vector3D( 0, 0, 1.5 ), 0.3 );
			}
		}

		public enum Output
		{
			STL,
			POVRay
		}

		public class Settings
		{
			public double Scale = 50;	// 10cm ball by default.

			public bool Halfspace = false;
			public int MaxCells = 150000;

			// Ball Model
			public double Ball_MaxLength = 3;
			//public double Ball_MinLength = 0.075;
			public double Ball_MinLength = 0.15;
			//public double Ball_MinLength = 0.05;
			//private static double Ball_MinLength = 0.45;	// lamp
			public double Ball_Cutoff = 0.95;

			// UHS
			//public double UHS_MinEdgeLength = .09;
			//public double UHS_MaxBounds = 6.5;
			public double UHS_MinEdgeLength = 0.03;
			public double UHS_MaxBounds = 2;
			public double UHS_Horocycle = 0.25;

			// Bananas
			public bool ThinEdges = false;
			public double AngularThickness = 0.06;	// an angle (the slope of the banana)
			//public double AngularThickness = 0.04;
			//public double AngularThickness = 0.25;

			// Position and associated Mobius to apply
			public Polytope.Projection Position = Polytope.Projection.CellCentered;
			public Mobius Mobius = Mobius.Identity();

			public Output Output = Output.POVRay;
		}

		public static Settings m_settings = new Settings();
		//public static string m_baseDir = @"C:\Users\hrn\Documents\roice\povray\H3\";
		public static string m_baseDir = @".\";

		public static void GenHoneycombs()
		{
			//GenHoneycomb( Honeycomb.H434 );

			//GenHoneycomb( Honeycomb.H435 );
			//GenHoneycomb( Honeycomb.H534 );
			//GenHoneycomb( Honeycomb.H535 );
			//GenHoneycomb( Honeycomb.H353 );

			//GenHoneycomb( Honeycomb.H336 );
			//GenHoneycomb( Honeycomb.H436 );
			//GenHoneycomb( Honeycomb.H536 );
			//GenHoneycomb( Honeycomb.H344 );

			//GenHoneycomb( Honeycomb.H636 );
			//GenHoneycomb( Honeycomb.H444 );
			//GenHoneycomb( Honeycomb.H363 );

			GenHoneycomb( EHoneycomb.H33I );

			//H3Fundamental.Generate( Honeycomb.H435, m_settings );
		}

		private static int[] m_levelsToKeep = new int[] { 1, 2, 3, 4, 5 };
		private static double[] m_rangeToKeep = new double[] { 0.9, 0.96 };
		private static bool CheckRange( Vector3D v )
		{
			double abs = v.Abs();
			return
				abs > m_rangeToKeep[0] &&
				abs < m_rangeToKeep[1];
		}

		private static void SetupShapewaysSettings( Settings settings, EHoneycomb honeycomb )
		{
			switch( honeycomb )
			{
				case EHoneycomb.H336:
				case EHoneycomb.H344:
				{
					break;
				}
				case EHoneycomb.H436:
				{
					//settings.Ball_MinLength = 0.08;
					settings.Ball_MinLength = 0.08;
					break;
				}
				case EHoneycomb.H536:
				{
					//settings.Ball_MinLength = 0.05;
					settings.Ball_MinLength = 0.03;
					break;
				}
			}
		}

		private static void SetupSettings( EHoneycomb honeycomb )
		{
			switch( honeycomb )
			{
				case EHoneycomb.H435:
				{
					m_settings.Ball_Cutoff = 0.95;
					//m_settings.AngularThickness = 0.163;

					// Sandstone
					m_settings.Position = Polytope.Projection.VertexCentered;
					m_settings.Ball_Cutoff = 0.88;
					m_settings.AngularThickness = 0.18;

					// Paper
					m_settings.Position = Polytope.Projection.CellCentered;
					m_settings.Ball_Cutoff = 0.96;
					m_settings.AngularThickness = 0.08;

					break;
				}
				case EHoneycomb.H534:
				{
					m_settings.Ball_Cutoff = m_settings.ThinEdges ? 0.984 : 0.95;
					//m_settings.Ball_MaxLength = 1;
					//m_settings.Ball_MinLength = 0.05;

					// Sandstone
					//m_settings.Position = Polytope.Projection.CellCentered;
					//m_settings.Ball_Cutoff = 0.88;
					//m_settings.AngularThickness = 0.18;

					// Laser Crystal
					m_settings.AngularThickness = 0.1;
					m_settings.Ball_Cutoff = 0.98;
					m_settings.Output = Output.STL;

					break;
				}
				case EHoneycomb.H535:
				{
					//m_settings.Ball_Cutoff = m_settings.ThinEdges ? 0.993 : 0.96;
					//m_settings.Postion = Position.VertexCentered;
					m_settings.ThinEdges = true;
					m_settings.Ball_Cutoff = 0.98;
					break;
				}
				case EHoneycomb.H353:
				{
					bool big = true;
					if( big )
					{
						m_settings.Scale = 65;
						m_settings.Ball_Cutoff = 0.96;
						m_settings.AngularThickness = 0.165;
					}
					else
					{
						// Defaults
					}
					break;
				}
				case EHoneycomb.H336:
				{
					/* Settings for the AMS visual insight picture.
					m_settings.Halfspace = true;
					m_settings.UHS_MinEdgeLength = 0.025;
					m_settings.AngularThickness = 0.075;
					 */

					break;
				}
				case EHoneycomb.H337:
				{
					m_settings.Scale = 75;
					//m_settings.AngularThickness = 0.202;
					m_settings.AngularThickness = 0.1;
					break;
				}
				case EHoneycomb.H436:
				{
					break;
				}
				case EHoneycomb.H536:
				{
					m_settings.Ball_MinLength = 0.02;
					break;
				}
				case EHoneycomb.H344:
				{
					m_settings.Ball_MinLength = 0.05;
					break;
				}
				case EHoneycomb.H33I:
				{
					m_settings.AngularThickness = .17;
					break;
				}
			}

			//if( m_settings.Output == Output.POVRay )
			if( false )
			{
				// Wiki
				m_settings.Ball_MinLength = 0.0018;	// 534
				m_settings.Ball_MinLength = 0.005;	// 344
				m_settings.Ball_MinLength = 0.003; // others

				m_settings.Ball_MinLength = 0.001; // duals
				//m_settings.MaxCells = 300000;
				//m_settings.MaxCells = 100000;
				m_settings.MaxCells = 500000;
				//m_settings.Position = Polytope.Projection.EdgeCentered;

				// Finite
				//m_settings.Position = Polytope.Projection.CellCentered;
				//m_settings.Ball_Cutoff = 0.997;
				//m_settings.Ball_Cutoff = 0.999;	//535
			}
		}

		/// <summary>
		/// Naming is from Coxeter's table.
		/// Side lengths of fundamental right angled triangle.
		/// We'll use these to calc isometries to change the canonical position.
		/// 2 * phi = edge length
		/// chi = circumradius
		/// psi = inradius
		/// </summary>
		public static void HoneycombData( EHoneycomb honeycomb, out double phi, out double chi, out double psi )
		{
			phi = chi = psi = -1;
			chi = Honeycomb.CircumRadius( honeycomb );
			psi = Honeycomb.InRadius( honeycomb );
			phi = Honeycomb.EdgeLength( honeycomb ) / 2;
		}

		public static void SetupCentering( EHoneycomb honeycomb, Settings settings, double phi, double chi, double psi, ref Polytope.Projection projection )
		{
			settings.Mobius = Mobius.Identity();
			switch( settings.Position )
			{
				case Polytope.Projection.CellCentered:
					//m_settings.Mobius = Mobius.Scale( 5 );	// {6,3,3}, {4,3,3} cell-centered (sort of)
					break;
				case Polytope.Projection.FaceCentered:

					if( psi != -1 )
						settings.Mobius = Mobius.Scale( H3Models.UHS.ToE( psi ) );

					break;
				case Polytope.Projection.EdgeCentered:

					if( psi != -1 )
					{
						settings.Mobius = Mobius.Scale( H3Models.UHS.ToE( Honeycomb.MidRadius( honeycomb ) ) );
						projection = Polytope.Projection.EdgeCentered;
					}
					else
					{
						// This only works for the finite cells.

						// We can get the scaling distance we need from the analogue of the
						// pythagorean theorm for a hyperbolic right triangle.
						// The hypotenuse is the circumradius (chi), and the other side is the 1/2 edge length (phi).
						// http://en.wikipedia.org/wiki/Pythagorean_theorem#Hyperbolic_geometry
						double tempScale = DonHatch.acosh( Math.Cosh( chi ) / Math.Cosh( phi ) );
						settings.Mobius = Mobius.Scale( H3Models.UHS.ToE( tempScale ) );

						projection = Polytope.Projection.VertexCentered;
					}

					break;
				case Polytope.Projection.VertexCentered:

					if( chi != -1 )
					{
						settings.Mobius = Mobius.Scale( H3Models.UHS.ToE( chi ) );
						projection = Polytope.Projection.VertexCentered;
					}

					break;
			}
		}

		public static void GenHoneycomb( EHoneycomb honeycomb )
		{
			if( honeycomb == EHoneycomb.H434 )
			{
				Euclidean.GenEuclidean();
				return;
			}

			if( H3Supp.IsExotic( honeycomb ) )
			{
				H3Supp.GenerateExotic( honeycomb, m_settings );
				return;
			}

			SetupSettings( honeycomb );
			//SetupShapewaysSettings( m_settings, honeycomb );

			Tiling tiling = CellTilingForHoneycomb( honeycomb );

			double cellScale = Honeycomb.CircumRadius( honeycomb );
			if( Infinity.IsInfinite( cellScale ) )
				cellScale = 1;
			else
				cellScale = DonHatch.h2eNorm( cellScale );

			Shapeways mesh = new Shapeways();
			bool finite = cellScale != 1;
			if( finite )
			{
				GenHoneycombInternal( mesh, tiling, honeycomb, cellScale );
			}
			else
			{
				GenHoneycombInternal( mesh, tiling, honeycomb, cellScale );

				mesh = new Shapeways();
				//GenDualHoneycombInternal( mesh, tiling, honeycomb );
			}
		}

		public static Tiling CellTilingForHoneycomb( EHoneycomb honeycomb )
		{
			int p, q, r;
			Honeycomb.PQR( honeycomb, out p, out q, out r );

			// Get data we need to generate the honeycomb.
			Polytope.Projection projection = Polytope.Projection.FaceCentered;
			double phi, chi, psi;
			HoneycombData( honeycomb, out phi, out chi, out psi );

			SetupCentering( honeycomb, m_settings, phi, chi, psi, ref projection );

			Tiling tiling = new Tiling();
			TilingConfig config = new TilingConfig( p, q );
			tiling.GenerateInternal( config, projection );
			return tiling;
		}

		public static Cell.Facet[] GenFacets( Tiling tiling )
		{
			List<Cell.Facet> facets = new List<Cell.Facet>();
			foreach( Tile tile in tiling.Tiles )
			{
				List<Vector3D> points = new List<Vector3D>();
				foreach( Segment seg in tile.Boundary.Segments )
					points.Add( seg.P1 );
				facets.Add( new Cell.Facet( points.ToArray() ) );
			}

			return facets.ToArray();
		}

		public static Cell.Facet[] GenFacetSpheres( Tiling tiling, double inRadius )
		{
			List<Cell.Facet> facets = new List<Cell.Facet>();
			foreach( Tile tile in tiling.Tiles )
			{
				Vector3D facetClosestToOrigin = tile.Boundary.Center;
				facetClosestToOrigin = Sterographic.PlaneToSphereSafe( facetClosestToOrigin );
				facetClosestToOrigin.Normalize();
				facetClosestToOrigin *= DonHatch.h2eNorm( inRadius );
				Sphere facetSphere = H3Models.Ball.OrthogonalSphereInterior( facetClosestToOrigin );
				facets.Add( new Cell.Facet( facetSphere ) );
			}

			return facets.ToArray();
		}

		/// <summary>
		/// Generates H3 honeycombs with cells having a finite number of facets.
		/// </summary>
		private static void GenHoneycombInternal( Shapeways mesh, Tiling tiling, EHoneycomb honeycomb, double cellScale )
		{
			string honeycombString = Honeycomb.String( honeycomb, false );

			int p, q, r;
			Honeycomb.PQR( honeycomb, out p, out q, out r );

			double inRadius = Honeycomb.InRadius( honeycomb );
			Cell.Facet[] facets = GenFacetSpheres( tiling, inRadius );
			Cell first = new Cell( p, facets );

			/*Cell first = new Cell( p, GenFacets( tiling ) );
			first.ToSphere();	// Work in ball model.
			first.ScaleToCircumSphere( cellScale );
			first.ApplyMobius( m_settings.Mobius );*/

			// This is for getting endpoints of cylinders for hollowing.
			bool printVerts = false;
			if( printVerts )
			{
				foreach( Cell.Facet facet in first.Facets )
				{
					Vector3D v = facet.Verts.First();
					v *= m_settings.Scale;
					System.Diagnostics.Trace.WriteLine( string.Format( "Vert: {0}", v ) );
				}
			}

			// Recurse.
			int level = 1;
			HashSet<Vector3D> completedCellCenters = new HashSet<Vector3D>();
			completedCellCenters.Add( first.ID );
			Dictionary<Cell.Edge, int> completedEdges = new Dictionary<Cell.Edge, int>( new Cell.EdgeEqualityComparer() );	// Values are recursion level, so we can save this out.
			HashSet<Sphere> completedFacets = new HashSet<Sphere>( facets.Select( f => f.Sphere ) );
			//if( CellOk( first ) )
				//Util.AddEdges( first, level, completedEdges );
			List<Cell> starting = new List<Cell>();
			starting.Add( first );
			List<Cell> completedCells = new List<Cell>();
			completedCells.Add( first );

			ReflectRecursive( level, starting, completedCellCenters, completedEdges, completedCells, completedFacets );

			bool finite = cellScale != 1;
			completedEdges.Clear();
			SaveToFile( honeycombString, completedEdges, finite );
			PovRay.AppendFacets( completedCells.ToArray(), m_baseDir + honeycombString + ".pov" );
		}

		public static void SaveToFile( string honeycombString, Cell.Edge[] edges, bool finite, bool append = false )
		{
			SaveToFile( honeycombString, edges.ToDictionary( e => e, e => 1 ), finite, append );
		}

		public static void SaveToFile( string honeycombString, Dictionary<Cell.Edge, int> edges, bool finite, bool append = false )
		{
			//H3.Util.LogMinSize( edges );

			if( m_settings.Output == Output.STL )
				MeshEdges( honeycombString, edges, finite );
			else
				PovRay.WriteH3Edges( new PovRay.Parameters() 
					{ 
						AngularThickness = m_settings.AngularThickness,
						Halfspace = m_settings.Halfspace,
						//Halfspace = true,
						ThinEdges = m_settings.ThinEdges,
					},
					edges.Keys.ToArray(), m_baseDir + honeycombString + ".pov", append ); 
		}

		public static void AppendFacets( string honeycombString, H3.Cell[] cells )
		{
			PovRay.AppendFacets( cells, m_baseDir + honeycombString + ".pov" );
		}

		public static void MeshEdges( string honeycombString, Dictionary<Cell.Edge, int> edges, bool finite )
		{
			Shapeways mesh = new Shapeways();
			if( finite )
			{
				//AddBananas( mesh, edges );
				AddSpheres( mesh, edges );
			}
			else
			{
				foreach( KeyValuePair<Cell.Edge, int> kvp in edges )
					Util.AddToMeshInternal( mesh, kvp.Key.Start, kvp.Key.End );
			}

			mesh.Mesh.Scale( m_settings.Scale );
			//SaveOutEdges( edges, m_baseDir + honeycombString + ".txt" );
			STL.SaveMeshToSTL( mesh.Mesh, m_baseDir + honeycombString + ".stl" );
		}

		/// <summary>
		/// This method is for honeycombs having Euclidean tilings as cells.
		/// Since we can't generate the full cells, these are easier to generate as duals to the other honeycombs.
		/// </summary>
		private static void GenDualHoneycombInternal( Shapeways mesh, Tiling tiling, EHoneycomb honeycomb )
		{
			string honeycombString = Honeycomb.String( honeycomb, true );

			int p, q, r;
			Honeycomb.PQR( honeycomb, out p, out q, out r );

			double inRadius = Honeycomb.InRadius( honeycomb );
			Cell.Facet[] facets = GenFacetSpheres( tiling, inRadius );
			Cell first = new Cell( p, facets );

			// We are already working in the ball model.
			//first = new Cell( p, GenFacets( tiling ) );
			//first.ToSphere();
			//first.ApplyMobius( m_settings.Mobius ); Done in recursive code below.

			// Recurse.
			HashSet<Vector3D> completedCellCenters = new HashSet<Vector3D>();
			Dictionary<Cell.Edge, int> completedEdges = new Dictionary<Cell.Edge, int>( new Cell.EdgeEqualityComparer() );	// Values are recursion level, so we can save this out.
			HashSet<Sphere> completedFacets = new HashSet<Sphere>( facets.Select( f => f.Sphere ) );
			List<Cell> completedCells = new List<Cell>();

			List<Cell> starting = new List<Cell>();
			starting.Add( first );
			completedCellCenters.Add( first.Center );
			completedCells.Add( first );
			int level = 0;
			ReflectRecursiveDual( level, starting, completedCellCenters, completedCells, completedEdges, completedFacets );

			// Can't do this on i33!
			//RemoveDanglingEdgesRecursive( completedEdges );

			//SaveToFile( honeycombString, completedEdges, finite: true );
			PovRay.AppendFacets( completedFacets.ToArray(), m_baseDir + honeycombString + ".pov" );
			//PovRay.AppendFacets( completedCells.ToArray(), m_baseDir + honeycombString + ".pov" );
		}

		public static void RemoveDanglingEdgesRecursive( Dictionary<Cell.Edge, int> edges )
		{
			List<Cell.Edge> needRemoval = new List<Cell.Edge>();

			// Info we'll need to remove dangling edges.
			Dictionary<Vector3D, int> vertexCounts = new Dictionary<Vector3D, int>();
			foreach( Cell.Edge edge in edges.Keys )
			{
				CheckAndAdd( vertexCounts, edge.Start );
				CheckAndAdd( vertexCounts, edge.End );
			}

			foreach( KeyValuePair<Cell.Edge, int> kvp in edges )
			{
				Cell.Edge e = kvp.Key;
				if( vertexCounts[e.Start] == 1 ||
					vertexCounts[e.End] == 1 )
				{
					needRemoval.Add( e );
				}
			}

			if( needRemoval.Count > 0 )
			{
				foreach( Cell.Edge edge in needRemoval )
					edges.Remove( edge );
				RemoveDanglingEdgesRecursive( edges );
			}
		}

		/// <summary>
		/// This will add spheres at incomplete edges.
		/// </summary>
		private static void AddSpheres( Shapeways mesh, Dictionary<Cell.Edge, int> edges )
		{
			Dictionary<Vector3D, int> vertexCounts = new Dictionary<Vector3D, int>();
			foreach( KeyValuePair<Cell.Edge, int> kvp in edges )
			{
				//if( !m_levelsToKeep.Contains( kvp.Value ) )
				//	continue;

				Cell.Edge e = kvp.Key;
				CheckAndAdd( vertexCounts, e.Start );
				CheckAndAdd( vertexCounts, e.End );
			}

			foreach( KeyValuePair<Vector3D, int> kvp in vertexCounts )
			{
				if( kvp.Value < 4 ) // XXX - not always this.
				{
					/*
					Vector3D center = kvp.Key;
					if( m_settings.Halfspace )
						center = H3Models.BallToUHS( center );
					double radius = m_settings.Halfspace ?
						center.Z * Math.Tan( m_settings.Ball_BaseThickness ) : SizeFuncBall( center );
					mesh.AddSphere( center, radius );
					 */

					if( m_settings.ThinEdges )
						mesh.AddSphere( kvp.Key, SizeFuncConst( new Vector3D() ) );
					else
						H3Sphere.AddSphere( mesh, kvp.Key, m_settings );
				}
			}
		}

		/// <summary>
		/// This will add in all our bananas.
		/// </summary>
		private static void AddBananas( Shapeways mesh, Dictionary<Cell.Edge, int> edges )
		{
			foreach( KeyValuePair<Cell.Edge, int> kvp in edges )
			{
				//if( !m_levelsToKeep.Contains( kvp.Value ) )
				//	continue;

				Cell.Edge e = kvp.Key;
				//if( CheckRange( e.Start ) &&
				//	CheckRange( e.End ) )
				//	continue;

				if( m_settings.ThinEdges )
				{
					/*Vector3D[] points = m_settings.Halfspace ?
						H3Models.UHS.GeodesicPoints( e.Start, e.End ) :
						H3Models.Ball.GeodesicPoints( e.Start, e.End );
					mesh.AddCurve( points, SizeFuncConst );*/

					Vector3D center, normal;
					double radius, angleTot;
					int div = 10;
					if( m_settings.Halfspace )
						H3Models.UHS.Geodesic( e.Start, e.End, out center, out radius, out normal, out angleTot );
					else
					{
						H3Models.Ball.Geodesic( e.Start, e.End, out center, out radius, out normal, out angleTot );
						H3Models.Ball.LODThin( e.Start, e.End, out div );
					}
					//mesh.AddArc( center, radius, e.Start, normal, angleTot, div, SizeFuncConst );
					H3.Util.AddToMeshInternal( mesh, e.Start, e.End );
				}
				else
					Banana.AddBanana( mesh, e.Start, e.End, m_settings );

			}
		}

		private static void CheckAndAdd( Dictionary<Vector3D, int> vertexCounts, Vector3D v )
		{
			int count;
			if( vertexCounts.TryGetValue( v, out count ) )
				count++;
			else
				count = 1;

			vertexCounts[v] = count;
		}

		private static void SaveOutEdges( Dictionary<Cell.Edge, int> edges, string fileName )
		{
			using( StreamWriter sw = File.CreateText( fileName ) )
			{
				foreach( KeyValuePair<Cell.Edge, int> kvp in edges )
					kvp.Key.Write( sw, kvp.Value );
			}
		}

		/// <summary>
		/// This method works for honeycombs having cells with a finite number of facets.
		/// The cell vertices may be ideal or finite.
		/// Calculations are carried out in the ball model.
		/// </summary>
		private static void ReflectRecursive( int level, List<Cell> cells, HashSet<Vector3D> completedCellCenters, Dictionary<Cell.Edge, int> completedEdges,
			List<Cell> completedCells, HashSet<Sphere> facets )
		{
			// Breadth first recursion.

			if( 0 == cells.Count )
				return;

			if( level > 2 )
				return;

			level++;

			List<Cell> reflected = new List<Cell>();

			foreach( Cell cell in cells )
			{
				bool idealVerts = cell.IdealVerts;
				List<Sphere> facetSpheres = new List<Sphere>();
				foreach( Cell.Facet facet in cell.Facets )
				{
					facetSpheres.Add( facet.Sphere );
					/*
					if( idealVerts )
						facetSpheres.Add( H3Models.Ball.OrthogonalSphere( facet.Verts[0], facet.Verts[1], facet.Verts[2] ) );
					else
						facetSpheres.Add( H3Models.Ball.OrthogonalSphereInterior( facet.Verts[0], facet.Verts[1], facet.Verts[2] ) );*/
				}

				foreach( Sphere facetSphere in facetSpheres )
				{
					if( completedCellCenters.Count > m_settings.MaxCells )
						return;

					Cell newCell = cell.Clone();
					newCell.Reflect( facetSphere );
					if( completedCellCenters.Contains( newCell.ID ) ||
						!CellOk( newCell ) )
						continue;

					//if( CellOk( newCell ) )
						//Util.AddEdges( newCell, level, completedEdges );
					reflected.Add( newCell );
					completedCellCenters.Add( newCell.ID );
					completedCells.Add( newCell );
					foreach( Cell.Facet facet in newCell.Facets )
						facets.Add( facet.Sphere );
				}
			}

			ReflectRecursive( level, reflected, completedCellCenters, completedEdges, completedCells, facets );
		}

		private static bool CellOk( Cell cell )
		{
			return true;

			bool idealVerts = cell.IdealVerts;

			// ZZZ - maybe criterion should be total perimeter?
			foreach( Cell.Facet facet in cell.Facets )
			{
				for( int i = 0; i < cell.P; i++ )
				{
					int i1 = i;
					int i2 = i == cell.P - 1 ? 0 : i + 1;
					Vector3D v1 = facet.Verts[i1];
					Vector3D v2 = facet.Verts[i2];

					// Handle the cutoff differently when we are in the ball model,
					// and the cell vertices are finite.
					if( !m_settings.Halfspace && !idealVerts )
					{
						double cutoff = m_settings.Ball_Cutoff;
						if( v1.Abs() > cutoff || v2.Abs() > cutoff )
							return false;
					}
					else
					{
						if( m_settings.Halfspace )
						{
							v1 = H3Models.BallToUHS( v1 );
							v2 = H3Models.BallToUHS( v2 );
							if( ((v1.Z > m_settings.UHS_Horocycle && v2.Z < m_settings.UHS_Horocycle) ||
								 (v2.Z > m_settings.UHS_Horocycle && v1.Z < m_settings.UHS_Horocycle)) &&
								v1.Abs() < m_settings.UHS_MaxBounds &&
								v2.Abs() < m_settings.UHS_MaxBounds )
								return true;
						}
						else
						{
							double dist = v1.Dist( v2 );
							if( dist < m_settings.Ball_MinLength || dist > m_settings.Ball_MaxLength )
								return false;
						}
					}
				}
			}

			return true;
			//return false;	// for some UHS stuff, we reverse this.
		}

		/// <summary>
		/// This is to generate tilings with infinite faceted cells (dual to the input cells).
		/// It works natively in the Ball model.
		/// </summary>c
		private static void ReflectRecursiveDual( int level, List<Cell> cells, HashSet<Vector3D> completedCellCenters, 
			List<Cell> completedCells, Dictionary<Cell.Edge, int> completedEdges, HashSet<Sphere> facets )
		{
			// Breadth first recursion.

			if( 0 == cells.Count )
				return;

			level++;

			List<Cell> reflected = new List<Cell>();

			foreach( Cell cell in cells )
			{
				List<Sphere> facetSpheres = new List<Sphere>();
				foreach( Cell.Facet facet in cell.Facets )
				{
					facetSpheres.Add( facet.Sphere );
					//facetSpheres.Add( H3Models.Ball.OrthogonalSphere( facet.Verts[0], facet.Verts[1], facet.Verts[2] ) );
				}

				foreach( Sphere facetSphere in facetSpheres )
				{
					if( completedCellCenters.Count > m_settings.MaxCells )
						return;

					Cell newCell = cell.Clone();
					newCell.Reflect( facetSphere );

					Vector3D start = H3Models.Ball.ApplyMobius( m_settings.Mobius, cell.Center );
					Vector3D end = H3Models.Ball.ApplyMobius( m_settings.Mobius, newCell.Center );

					Cell.Edge edge = new Cell.Edge( start, end );
					if( completedEdges.ContainsKey( edge ) )
						continue;

					// This check was making things not work.
					//if( completedCellCenters.Contains( newCell.Center ) )
					//	continue;

					if( !EdgeOk( edge ) )
						continue;
					
					reflected.Add( newCell );
					completedCells.Add( newCell );
					completedCellCenters.Add( newCell.Center );
					completedEdges.Add( edge, level );
					foreach( Cell.Facet facet in newCell.Facets )
						facets.Add( facet.Sphere );
				}
			}

			ReflectRecursiveDual( level, reflected, completedCellCenters, completedCells, completedEdges, facets );
		}

		private static bool EdgeOk( Cell.Edge edge )
		{
			double minEdgeLength = m_settings.UHS_MinEdgeLength;
			double maxBounds = m_settings.UHS_MaxBounds;

			if( m_settings.Halfspace )
			{
				Vector3D start = H3Models.BallToUHS( edge.Start );
				Vector3D end = H3Models.BallToUHS( edge.End );
				if( start.Dist( end ) < minEdgeLength )
					return false;

				if( start.Abs() > maxBounds ||
					end.Abs() > maxBounds )
					return false;

				return true;
			}
			else
			{
				//return edge.Start.Dist( edge.End ) > minEdgeLength*.5;

				// Calculated by size testing.  This is the point where the thickness will be .025 inches, assuming we'll scale the ball by a factor of 2.
				// This pushes the limits of Shapeways.
				//double thresh = 0.93;	// {6,3,3}
				//double thresh = 0.972;	// {7,3,3}
				//double thresh = 0.94;
				//double thresh = 0.984;
				//thresh = 0.9975;	// XXX - put in settings.
				double thresh = 0.999;
				return 
					edge.Start.Abs() < thresh &&
					edge.End.Abs() < thresh;
			}
		}

		public static double SizeFuncConst( Vector3D v )
		{
			return H3Models.SizeFuncConst( v, m_settings.Scale );
		}
	}
}
