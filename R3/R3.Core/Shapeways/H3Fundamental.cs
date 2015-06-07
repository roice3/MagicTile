namespace R3.Geometry
{
	using R3.Core;
	using System.Collections.Generic;
	using System.Linq;
	using Math = System.Math;

	/// <summary>
	/// This class generates H3 honeycombs via a fundamental region.
	/// </summary>
	public class H3Fundamental
	{
		/// <summary>
		/// Class for the fundamental tetrahedron.
		/// </summary>
		public class Tet
		{
			public Tet( Vector3D center, Vector3D face, Vector3D edge, Vector3D vertex )
			{
				Verts[0] = center;
				Verts[1] = face;
				Verts[2] = edge;
				Verts[3] = vertex;
				CalcFaces();
			}

			// The order of these 4 vertices will be 
			// Center,Face,Edge,Vertex of the parent cell.
			public Vector3D[] Verts = new Vector3D[4];

			public Sphere[] Faces
			{
				get { return m_faces; }
			}
			private Sphere[] m_faces;

			private void CalcFaces()
			{
				m_faces = new Sphere[]
					{
						// Orientation is important! CCW from outside
						// XXX - Broken for tets with ideal verts.
						H3Models.Ball.OrthogonalSphereInterior( Verts[0], Verts[1], Verts[2] ),
						H3Models.Ball.OrthogonalSphereInterior( Verts[0], Verts[3], Verts[1] ),
						H3Models.Ball.OrthogonalSphereInterior( Verts[0], Verts[2], Verts[3] ),
						H3Models.Ball.OrthogonalSphereInterior( Verts[1], Verts[3], Verts[2] )
					};
			}

			public Vector3D ID
			{
				get
				{
					Vector3D result = new Vector3D();
					foreach( Vector3D v in Verts )
						result += v;
					return result;
				}
			}

			public Tet Clone()
			{
				return new Tet( Verts[0], Verts[1], Verts[2], Verts[3] );
			}

			public void Reflect( Sphere sphere )
			{
				for( int i=0; i<4; i++ )
					Verts[i] = sphere.ReflectPoint( Verts[i] );
				CalcFaces();
			}
		}

		public class TetEqualityComparer : IEqualityComparer<Tet>
		{
			public bool Equals( Tet t1, Tet t2 )
			{
				return t1.ID.Compare( t2.ID, m_tolerance );
			}

			public int GetHashCode( Tet t )
			{
				return t.ID.GetHashCode();
			}

			private double m_tolerance = 0.0001;
		}

		public static void Generate( EHoneycomb honeycomb, H3.Settings settings )
		{
			// XXX - Block the same as in H3.  Share code better.
			H3.Cell template = null;
			{
				int p, q, r;
				Honeycomb.PQR( honeycomb, out p, out q, out r );

				// Get data we need to generate the honeycomb.
				Polytope.Projection projection = Polytope.Projection.FaceCentered;
				double phi, chi, psi;
				H3.HoneycombData( honeycomb, out phi, out chi, out psi );

				H3.SetupCentering( honeycomb, settings, phi, chi, psi, ref projection );

				Tiling tiling = new Tiling();
				TilingConfig config = new TilingConfig( p, q );
				tiling.GenerateInternal( config, projection );

				H3.Cell first = new H3.Cell( p, H3.GenFacets( tiling ) );
				first.ToSphere();	// Work in ball model.
				first.ScaleToCircumSphere( 1.0 );
				first.ApplyMobius( settings.Mobius );

				template = first;
			}

			// Center
			Vector3D center = template.Center;

			// Face
			H3.Cell.Facet facet = template.Facets[0];
			Sphere s = H3Models.Ball.OrthogonalSphereInterior( facet.Verts[0], facet.Verts[1], facet.Verts[2] );
			Vector3D face = s.Center;
			face.Normalize();
			face *= DistOriginToOrthogonalSphere( s.Radius );

			// Edge
			Circle3D c;
			H3Models.Ball.OrthogonalCircleInterior( facet.Verts[0], facet.Verts[1], out c );
			Vector3D edge = c.Center;
			edge.Normalize();
			edge *= DistOriginToOrthogonalSphere( c.Radius );

			// Vertex
			Vector3D vertex = facet.Verts[0];

			Tet fundamental = new Tet( center, face, edge, vertex );

			// Recurse.
			int level = 1;
			Dictionary<Tet, int> completedTets = new Dictionary<Tet, int>( new TetEqualityComparer() );
			completedTets.Add( fundamental, level );
			List<Tet> tets = new List<Tet>();
			tets.Add( fundamental );
			ReflectRecursive( level, tets, completedTets, settings );

			Shapeways mesh = new Shapeways();
			foreach( KeyValuePair<Tet, int> kvp in completedTets )
			{
				if( Utils.Odd( kvp.Value ) )
					continue;

				Tet tet = kvp.Key;

				// XXX - really want sphere surfaces here.
				mesh.Mesh.Triangles.Add( new Mesh.Triangle( tet.Verts[0], tet.Verts[1], tet.Verts[2] ) );
				mesh.Mesh.Triangles.Add( new Mesh.Triangle( tet.Verts[0], tet.Verts[3], tet.Verts[1] ) );
				mesh.Mesh.Triangles.Add( new Mesh.Triangle( tet.Verts[0], tet.Verts[2], tet.Verts[3] ) );
				mesh.Mesh.Triangles.Add( new Mesh.Triangle( tet.Verts[1], tet.Verts[3], tet.Verts[2] ) );
			}

			mesh.Mesh.Scale( settings.Scale );
			STL.SaveMeshToSTL( mesh.Mesh, H3.m_baseDir + "fundamental" + ".stl" );
		}

		private static double DistOriginToOrthogonalSphere( double r )
		{
			// http://mathworld.wolfram.com/OrthogonalCircles.html
			double d = Math.Sqrt( 1 + r * r );
			return d - r;
		}

		private static void ReflectRecursive( int level, List<Tet> tets, Dictionary<Tet, int> completedTets, H3.Settings settings )
		{
			// Breadth first recursion.

			if( 0 == tets.Count )
				return;

			level++;

			List<Tet> reflected = new List<Tet>();

			foreach( Tet tet in tets )
			{
				foreach( Sphere facetSphere in tet.Faces )
				{
					if( facetSphere == null )
						throw new System.Exception( "Unexpected." );

					if( completedTets.Count > settings.MaxCells )
						return;

					Tet newTet = tet.Clone();
					newTet.Reflect( facetSphere );
					if( completedTets.Keys.Contains( newTet ) ||
						!TetOk( newTet ) )
						continue;

					reflected.Add( newTet );
					completedTets.Add( newTet, level );
				}
			}

			ReflectRecursive( level, reflected, completedTets, settings );
		}
		
		private static bool TetOk( Tet tet )
		{
			double cutoff = 0.95;
			foreach( Vector3D v in tet.Verts )
				if( Tolerance.GreaterThan( v.Z, 0 ) || 
					v.Abs() > cutoff )
					return false;

			return true;
		}
	}
}
