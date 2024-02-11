namespace R3.Geometry
{
	using R3.Math;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Numerics;

	public class Tile
	{
		public Tile()
		{
			Isometry = new Isometry();
			EdgeIncidences = new List<Tile>();
			VertexIndicences = new List<Tile>();
		}

		public Tile( Polygon boundary, Polygon drawn, Geometry geometry )
			: this()
		{
			Boundary = boundary;
			Drawn = drawn;
			Geometry = geometry;

			// Make the vertex circle.
			VertexCircle = boundary.CircumCircle;

			// ZZZ - we shouldn't do this here (I did it for the slicing study page).
			//VertexCircle.Radius = 1.0;

			//
			// Below are experimentations with different vertex circle sizes.
			//

			//VertexCircle.Radius *= (1+1.0/9);

			// cuts adjacent cells at midpoint
			// Math.Sqrt(63)/6 for {3,6}
			// (1 + 1.0/5) for {3,7} 
			// (1 + 1.0/9) for {3,8}
			// (1 + 1.0/20) for {3,9}

			// cuts at 1/3rd
			// 2/Math.Sqrt(3) for {3,6}
		}

		public Polygon Boundary { get; set; }
		public Polygon Drawn { get; set; }
		public CircleNE VertexCircle { get; set; }
		public Geometry Geometry { get; set; }

		/// <summary>
		/// The center of this tile.
		/// </summary>
		public Vector3D Center
		{
			get { return Boundary.Center; }
		}

		/// <summary>
		/// This is the isometry that will take us back to the parent tile.
		/// NOTE: This is not internally updated during transformation,
		///		  or copied during a clone.  It is meant to be set once at tiling
		///		  generation time.
		/// </summary>
		public Isometry Isometry { get; set; }

		/// <summary>
		/// Used to track edge-adjacent tiles in a tiling.
		/// </summary>
		public List<Tile> EdgeIncidences { get; set; }

		/// <summary>
		/// Used to track vertex-adjacent tiles in a tiling.
		/// </summary>
		public List<Tile> VertexIndicences { get; set; }

		public Tile Clone()
		{
			Tile newTile = new Tile();
			newTile.Boundary = Boundary.Clone();
			newTile.Drawn = Drawn.Clone();
			newTile.VertexCircle = VertexCircle.Clone();
			newTile.Geometry = Geometry;
			return newTile;
		}

		public void Reflect( Segment s )
		{
			Boundary.Reflect( s );
			//Drawn.Reflect( s );
			VertexCircle.Reflect( s );
		}

		/// <summary>
		/// Apply a Mobius transform to us.
		/// </summary>
		public void Transform( Mobius m )
		{
			Boundary.Transform( m );
			//Drawn.Transform( m );
			VertexCircle.Transform( m );
		}

		public void Transform( Isometry i )
		{
			Boundary.Transform( i );
			//Drawn.Transform( i );
			VertexCircle.Transform( i );
		}

		/// <summary>
		/// Helper method to see if we have points projected to infinity.
		/// </summary>
		public bool HasPointsProjectedToInfinity
		{
			get
			{
				// This can only happen in spherical case.
				if( this.Geometry != Geometry.Spherical )
					return false;

				if( Infinity.IsInfinite( this.Boundary.Center ) )
					return true;

				// We also need to check the edges.
				foreach( Segment s in this.Boundary.Segments )
					if( Infinity.IsInfinite( s.P1 ) ||
						Infinity.IsInfinite( s.P2 ) )
						return true;

				return false;
			}
		}

		/// <summary>
		/// ZZZ - needs to be part of performance setting?
		/// Returns true if the tile should be included after a Mobius transformation will be applied.
		/// If the tile is not included, this method avoids applying the Mobius transform to the entire tile.
		/// </summary>
		public bool IncludeAfterMobius( Mobius m )
		{
			switch( this.Geometry )
			{
				// Spherical tilings are finite, so we can always include everything.
				case Geometry.Spherical:
					return true;

				case Geometry.Euclidean:
					return true;	// We'll let the number of tiles specified in the tiling control this..

				case Geometry.Hyperbolic:
				{
					//Polygon poly = Boundary.Clone();
					//poly.Transform( m );

					//bool use = (poly.Length > 0.01);

					// ZZZ - DANGER! Some transforms can cause this to lead to stackoverflow (the ones that scale the tiling up).
					//bool use = ( poly.Length > 0.01 ) && ( poly.Center.Abs() < 10 );
					//bool use = ( poly.Center.Abs() < 0.9 );	// Only disk

					CircleNE c = VertexCircle;
					bool use = c.CenterNE.Abs() < 0.99999;

					/*List<Vector3D> points = poly.GetEdgePoints();
					double maxdist = points.Max( point => point.Abs() );
					bool use = maxdist < 0.97;*/

					return use;
				}
			}

			Debug.Assert( false );
			return false;
		}

		/// <summary>
		/// A correct implementation of shrink tile.
		/// hmmmm, is "scaling" even well defined in non-E geometries? Am I really looking for an equidistant curve?
		/// Sadly, even if I figure out what is best, I fear changing out usage of the incorrect one below in MagicTile,
		/// because of the possibility of breaking existing puzzles.
		/// </summary>
		internal static void ShrinkTileCorrect( ref Tile tile, double shrinkFactor )
		{
			System.Func<Vector3D,double,Vector3D> scaleFunc = null;
			switch( tile.Geometry )
			{
				case Geometry.Euclidean:
				{
					scaleFunc = ( v, s ) => v * s;
					break;
				}
				case Geometry.Spherical:
				{
					scaleFunc = ( v, s ) =>
						{
							// Move to spherical norm, scale, then move back to euclidean.
							double scale = Spherical2D.s2eNorm( ( Spherical2D.e2sNorm( v.Abs() ) * s ) );
							v.Normalize();
							return v * scale;
						};
					break;
				}
				case Geometry.Hyperbolic:
				{
					throw new System.NotImplementedException();
				}
			}
		}

		/// <summary>
		/// This will trim back the tile using an equidistant curve.
		/// It assumes the tile is at the origin.
		/// </summary>
		internal static void ShrinkTile( ref Tile tile, double shrinkFactor )
		{
			// This code is not correct in non-Euclidean cases!
			// But it works reasonable well for small shrink factors.
			// For example, you can easily use this function to grow a hyperbolic tile beyond the disk.
			Mobius m = new Mobius();
			m.Hyperbolic( tile.Geometry, new Vector3D(), shrinkFactor );
			tile.Drawn.Transform( m );
			return;

			/*
			// ZZZ
			// Wow, all the work I did below was subsumed by 4 code lines above!
			// I can't bring myself to delete it yet.

			switch( tile.Geometry )
			{
				case Geometry.Spherical:
				{
					List<Tile> clipped = new List<Tile>();
					clipped.Add( tile );

					Polygon original = tile.Drawn.Clone();
					foreach( Segment seg in original.Segments )
					{
						Debug.Assert( seg.Type == SegmentType.Arc );

						if( true )
						{
							// Unproject to sphere.
							Vector3D p1 = Spherical2D.PlaneToSphere( seg.P1 );
							Vector3D p2 = Spherical2D.PlaneToSphere( seg.P2 );

							// Get the poles of the GC, and project them to the plane.
							Vector3D pole1, pole2;
							Spherical2D.GreatCirclePole( p1, p2, out pole1, out pole2 );
							pole1 = Spherical2D.SphereToPlane( pole1 );
							pole2 = Spherical2D.SphereToPlane( pole2 );

							// Go hyperbolic, dude.
							double scale = 1.065;	// ZZZ - needs to be configurable.
							Complex fixedPlus = pole1;
							Mobius hyperbolic = new Mobius();
							hyperbolic.Hyperbolic( tile.Geometry, fixedPlus, scale );
							Vector3D newP1 = hyperbolic.Apply( seg.P1 );
							Vector3D newMid = hyperbolic.Apply( seg.Midpoint );
							Vector3D newP2 = hyperbolic.Apply( seg.P2 );

							Circle trimmingCircle = new Circle();
							trimmingCircle.From3Points( newP1, newMid, newP2 );

							Slicer.Clip( ref clipped, trimmingCircle, true );
						}
						else
						{
							// I think this block has logic flaws, but strangely it seems to work,
							// so I'm leaving it in commented out for posterity.

							Vector3D p1 = seg.P1;
							Vector3D mid = seg.Midpoint;
							Vector3D p2 = seg.P2;

							//double offset = .1;
							double factor = .9;
							double f1 = Spherical2D.s2eNorm( (Spherical2D.e2sNorm( p1.Abs() ) * factor) );
							double f2 = Spherical2D.s2eNorm( (Spherical2D.e2sNorm( mid.Abs() ) * factor) );
							double f3 = Spherical2D.s2eNorm( (Spherical2D.e2sNorm( p2.Abs() ) * factor) );
							p1.Normalize();
							mid.Normalize();
							p2.Normalize();
							p1 *= f1;
							mid *= f2;
							p2 *= f3;

							Circle trimmingCircle = new Circle();
							trimmingCircle.From3Points( p1, mid, p2 );

							Slicer.Clip( ref clipped, trimmingCircle, true );
						}
					}

					Debug.Assert( clipped.Count == 1 );
					tile = clipped[0];
					return;
				}
				case Geometry.Euclidean:
				{
					double scale = .95;

					Mobius hyperbolic = new Mobius();
					hyperbolic.Hyperbolic( tile.Geometry, new Vector3D(), scale );

					tile.Drawn.Transform( hyperbolic );

					return;
				}
				case Geometry.Hyperbolic:
				{
					List<Tile> clipped = new List<Tile>();
					clipped.Add( tile );

					Circle infinity = new Circle();
					infinity.Radius = 1.0;

					Polygon original = tile.Drawn.Clone();
					foreach( Segment seg in original.Segments )
					{
						Debug.Assert( seg.Type == SegmentType.Arc );
						Circle segCircle = seg.GetCircle();

						// Get the intersection points with the disk at infinity.
						Vector3D p1, p2;
						int count = Euclidean2D.IntersectionCircleCircle( infinity, segCircle, out p1, out p2 );
						Debug.Assert( count == 2 );

						Vector3D mid = seg.Midpoint;
						//mid *= 0.75;	// ZZZ - needs to be configurable.
							
						double offset = .03;
						double f1 = DonHatch.h2eNorm( DonHatch.e2hNorm( mid.Abs() ) - offset );
						mid.Normalize();
						mid *= f1;
							
						Circle trimmingCircle = new Circle();
						trimmingCircle.From3Points( p1, mid, p2 );

						Slicer.Clip( ref clipped, trimmingCircle, false );
					}

					Debug.Assert( clipped.Count == 1 );
					tile = clipped[0];
					return;
				}
			}
			*/
		}
	}
}
