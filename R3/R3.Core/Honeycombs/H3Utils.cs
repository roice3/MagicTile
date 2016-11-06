namespace R3.Geometry
{
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.Linq;
	using System.Numerics;

	using Math = System.Math;

	// XXX - move to another file
	// http://en.wikipedia.org/wiki/Spherical_coordinate_system#Coordinate_system_conversions
	// theta is inclination (like latitude, but from 0 to pi)
	// phi is azimuth (like longitude)
	public class SphericalCoords
	{
		// x,y,z -> r,theta,phi
		public static Vector3D CartesianToSpherical( Vector3D v )
		{
			double r = v.Abs();
			if( Tolerance.Zero( r ) )
				return new Vector3D();

			return new Vector3D(
				r,
				Math.Acos( v.Z / r ),
				Math.Atan2( v.Y, v.X ) );
		}

		// r,theta,phi -> x,y,z
		public static Vector3D SphericalToCartesian( Vector3D v )
		{
			if( Tolerance.Zero( v.Abs() ) )
				return new Vector3D();

			return new Vector3D(
				v.X * Math.Sin( v.Y ) * Math.Cos( v.Z ),
				v.X * Math.Sin( v.Y ) * Math.Sin( v.Z ),
				v.X * Math.Cos( v.Y ) );
		}
	}

	public class H3Models
	{
		public static Vector3D BallToUHS( Vector3D v )
		{
			return TransformHelper( v, toUpperHalfPlane );
		}

		public static Vector3D UHSToBall( Vector3D v )
		{
			if( Infinity.IsInfinite( v ) )
				return new Vector3D( 0, 0, 1 );

			return TransformHelper( v, fromUpperHalfPlane );
		}

		/// <summary>
		/// NOTE! This should only be used if m is a transform that preserves the imaginary axis!
		/// </summary>
		public static Vector3D TransformHelper( Vector3D v, Mobius m )
		{
			Vector3D spherical = SphericalCoords.CartesianToSpherical( v );
			Complex c1 = Complex.FromPolarCoordinates( spherical.X, Math.PI/2 - spherical.Y );
			Complex c2 = m.Apply( c1 );

			/*
			if( c2.Phase > Math.PI / 2 || c2.Phase < -Math.PI / 2 )
			{
				// Happens when v is origin, and causes runtime problems in release.
				System.Diagnostics.Debugger.Break();
			}
			*/

			Vector3D s2 = new Vector3D( c2.Magnitude, Math.PI/2 - c2.Phase, spherical.Z );
			return SphericalCoords.SphericalToCartesian( s2 );
		}

		private static Mobius ToUpperHalfPlaneMobius()
		{
			Mobius m = new Mobius();
			m.UpperHalfPlane();
			return m;
		}

		private static Mobius FromUpperHalfPlaneMobius()
		{
			return ToUpperHalfPlaneMobius().Inverse();
		}

		private static Mobius toUpperHalfPlane = ToUpperHalfPlaneMobius();
		private static Mobius fromUpperHalfPlane = FromUpperHalfPlaneMobius();

		/// <summary>
		/// NOTE: s must be geodesic! (orthogonal to boundary).
		/// </summary>
		public static Sphere UHSToBall( Sphere s )
		{
			Vector3D center;
			double rad;
			if( s.IsPlane )
			{
				// Planes through the origin will stay unchanged.
				if( s.Offset == new Vector3D() )
					return s.Clone();

				// It must be vertical (because it is orthogonal).
				Vector3D b1 = H3Models.UHSToBall( Infinity.InfinityVector );
				Vector3D b2 = H3Models.UHSToBall( s.Offset );
				H3Models.Ball.OrthogonalCircle( b1, b2, out center, out rad );	// Safer to use OrthogonalSphere?
			}
			else
			{
				Vector3D temp;
				if( s.Center.IsOrigin )
				{
					temp = new Vector3D( s.Radius, 0, 0 );
				}
				else
				{
					temp = s.Center;
					temp.Normalize();
					temp *= s.Radius;
				}
				Vector3D centerUhs = s.Center;
				Vector3D b1 = H3Models.UHSToBall( centerUhs - temp );
				Vector3D b2 = H3Models.UHSToBall( centerUhs + temp );
				H3Models.Ball.OrthogonalCircle( b1, b2, out center, out rad );  // Safer to use OrthogonalSphere?

				// Did we project to a plane?
				if( Infinity.IsInfinite( rad ) )
				{
					temp.RotateXY( Math.PI / 2 );
					Vector3D b3 = H3Models.UHSToBall( centerUhs + temp );
					center = b1.Cross( b3 );
					rad = double.PositiveInfinity;
				}
			}

			return new Sphere()
			{
				Center = center,
				Radius = rad
			};
		}

		// XXX - Not general yet.
		// s does not have to be geodesic.
		public static Sphere BallToUHS( Sphere s )
		{
			// Get 4 points on the sphere.
			double rad = s.Radius;
			Vector3D[] spherePoints;
			if( s.IsPlane )
			{
				Vector3D perp = s.Normal.Perpendicular();
				Vector3D perp2 = perp;
				perp2.RotateAboutAxis( s.Normal, Math.PI / 2 );
				spherePoints = new Vector3D[]
				{
					s.Offset,
					s.Offset + perp,
					s.Offset - perp,
					s.Offset + perp2
				};
			}
			else
			{
				spherePoints = new Vector3D[]
				{
					s.Center + new Vector3D( rad, 0, 0 ),
					s.Center + new Vector3D( -rad, 0, 0 ),
					s.Center + new Vector3D( 0, rad, 0 ),
					s.Center + new Vector3D( 0, 0, rad )
				};
			}

			for( int i=0; i<4; i++ )
				spherePoints[i] = H3Models.BallToUHS( spherePoints[i] );

			return Sphere.From4Points( spherePoints[0], spherePoints[1], spherePoints[2], spherePoints[3] );

			/*
			Vector3D s1, s2, s3;
			H3Models.Ball.IdealPoints( s, out s1, out s2, out s3 );
			s1 = H3Models.BallToUHS( s1 );
			s2 = H3Models.BallToUHS( s2 );
			s3 = H3Models.BallToUHS( s3 );

			Circle3D c = new Circle3D( s1, s2, s3 );
			return new Sphere()
			{
				Center = c.Center,
				Radius = c.Radius
			};*/
		}

		public static Circle3D BallToUHS( Circle3D c )
		{
			Vector3D[] points = c.RepresentativePoints;
			for( int i=0; i<3; i++ )
				points[i] = H3Models.BallToUHS( points[i] );
			return new Circle3D( points[0], points[1], points[2] );
		}

		/// <summary>
		/// Transform a geodesic sphere in the ball model to the Klein model.
		/// Output will be a plane.
		/// </summary>
		public static Sphere BallToKlein( Sphere s )
		{
			// If we are already a plane, no transformation is needed.
			if( s.IsPlane )
				return s.Clone();

			Vector3D closest = Ball.ClosestToOrigin( s );
			Vector3D p1 = HyperbolicModels.PoincareToKlein( closest );

			// Ideal points are the same in the Klein/Poincare models, so grab
			// two more plane points from the ideal circle.
			Vector3D p2, p3, dummy;
			Ball.IdealPoints( s, out p2, out dummy, out p3 );

			Vector3D offset = p1;
			Vector3D normal = (p2 - p1).Cross( p3 - p1 );
			normal.Normalize();
			if( !s.Invert )
				normal *= -1;
			return Sphere.Plane( offset, normal );
		}

		/// <summary>
		/// This applies the same Mobius transform to all vertical planes through the z axis.
		/// NOTE: m must therefore be a mobius transform that keeps the imaginary axis constant!
		/// NOTE: s must be geodesic! (orthogonal to boundary).
		/// </summary>
		public static Sphere TransformInBall( Sphere s, Mobius m )
		{
			if( s.IsPlane )
			{
				// All planes in the ball go through the origin.
				if( !s.Offset.IsOrigin )
					throw new System.ArgumentException();

				// Vertical planes remain unchanged.
				if( Tolerance.Equal( s.Normal.Z, 0 ) )
					return s.Clone();

				// Other planes will become spheres.
				Vector3D pointOnSphere = s.Normal.Perpendicular();
				pointOnSphere.Normalize();
				Vector3D b1 = H3Models.TransformHelper( pointOnSphere, m );
				Vector3D b2 = H3Models.TransformHelper( -pointOnSphere, m );
				pointOnSphere.RotateAboutAxis( s.Normal, Math.PI / 2 );
				Vector3D b3 = H3Models.TransformHelper( pointOnSphere, m );
				return H3Models.Ball.OrthogonalSphere( b1, b2, b3 );
			}
			else
			{
				Vector3D s1, s2, s3;
				H3Models.Ball.IdealPoints( s, out s1, out s2, out s3 );

				// Transform the points.
				Vector3D b1 = H3Models.TransformHelper( s1, m );
				Vector3D b2 = H3Models.TransformHelper( s2, m );
				Vector3D b3 = H3Models.TransformHelper( s3, m );
				return H3Models.Ball.OrthogonalSphere( b1, b2, b3 );
			}
		}

		/// <summary>
		/// This applies the the Mobius transform to the plane at infinity.
		/// Any Mobius is acceptable.
		/// NOTE: s must be geodesic! (orthogonal to boundary).
		/// </summary>
		public static Sphere TransformInUHS( Sphere s, Mobius m )
		{
			Vector3D s1, s2, s3;
			if( s.IsPlane )
			{
				// It must be vertical (because it is orthogonal).
				Vector3D direction = s.Normal;
				direction.RotateXY( Math.PI / 2 );
				s1 = s.Offset;
				s2 = s1 + direction;
				s3 = s1 - direction;
			}
			else
			{
				Vector3D offset = new Vector3D( s.Radius, 0, 0 );
				s1 = s.Center + offset;
				s2 = s.Center - offset;
				s3 = offset;
				s3.RotateXY( Math.PI / 2 );
				s3 += s.Center;
			}

			Vector3D b1 = m.Apply( s1 );
			Vector3D b2 = m.Apply( s2 );
			Vector3D b3 = m.Apply( s3 );
			Circle3D boundaryCircle = new Circle3D( b1, b2, b3 );

			Vector3D cen = boundaryCircle.Center;
			Vector3D off = new Vector3D();
			if( Infinity.IsInfinite( boundaryCircle.Radius ) )
			{
				boundaryCircle.Radius = double.PositiveInfinity;
				Vector3D normal = b2 - b1;
				normal.Normalize();
				normal.RotateXY( -Math.PI / 2 );	// XXX - The direction isn't always correct.
				cen = normal;
				off = Euclidean2D.ProjectOntoLine( new Vector3D(), b1, b2 );
			}
			
			return new Sphere
			{
				Center = cen,
				Radius = boundaryCircle.Radius,
				Offset = off
			};
		}

		public static void TransformInBall2( Sphere s, Mobius m )
		{
			Sphere newSphere = TransformInBall( s, m );
			s.Center = newSphere.Center;
			s.Radius = newSphere.Radius;
			s.Offset = newSphere.Offset;
		}

		public static void TransformInUHS2( Sphere s, Mobius m )
		{
			Sphere newSphere = TransformInUHS( s, m );
			s.Center = newSphere.Center;
			s.Radius = newSphere.Radius;
			s.Offset = newSphere.Offset;
		}

		public static double SizeFuncConst( Vector3D v, double scale )
		{
			//return 0.01;	// 2mm diameter
			//return 0.02;

			return 3.0 /*mm*/ / 2 / scale;
		}

		public static class Ball
		{
			// The radius of our Poincare ball model.
			private static double m_pRadius = 1.0;

			/// <summary>
			/// Calculates the euclidean center/radius of a standard sphere transformed to the nonEuclidean point v.
			/// The standard sphere is the sphere at the origin having euclidean radius 'radiusEuclideanOrigin'.
			/// </summary>
			public static void DupinCyclideSphere( Vector3D v, double radiusEuclideanOrigin, 
				out Vector3D centerEuclidean, out double radiusEuclidean )
			{
				DupinCyclideSphere( v, radiusEuclideanOrigin, Geometry.Hyperbolic, out centerEuclidean, out radiusEuclidean );
				ApplyMinRadiusForWiki( ref radiusEuclidean );
				//ApplyMinRadiusForPrinting( ref radiusEuclidean );
			}

			/// <summary>
			/// Helper that works in all geometries.
			/// center: http://www.wolframalpha.com/input/?i=%28+%28+%28+r+%2B+p+%29+%2F+%28+1+-+r*p+%29+%29+%2B+%28+%28+-r+%2B+p+%29+%2F+%28+1+%2B+r*p+%29+%29++%29+%2F+2
			/// radius: http://www.wolframalpha.com/input/?i=%28+%28+%28+r+%2B+p+%29+%2F+%28+1+-+r*p+%29+%29+-+%28+%28+-r+%2B+p+%29+%2F+%28+1+%2B+r*p+%29+%29++%29+%2F+2
			/// </summary>
			public static void DupinCyclideSphere( Vector3D vNonEuclidean, double radiusEuclideanOrigin, Geometry g,
				out Vector3D centerEuclidean, out double radiusEuclidean )
			{
				if( g == Geometry.Euclidean )
				{
					centerEuclidean = vNonEuclidean;
					radiusEuclidean = radiusEuclideanOrigin;
					return;
				}

				double p = vNonEuclidean.Abs();
				if( !vNonEuclidean.Normalize() )
				{
					// We are at the origin.
					centerEuclidean = vNonEuclidean;
					radiusEuclidean = radiusEuclideanOrigin;
					return;
				}

				double r = radiusEuclideanOrigin;
				double numeratorCenter = g == Geometry.Hyperbolic ? ( 1 - r * r ) : ( 1 + r * r );
				double numeratorRadius = g == Geometry.Hyperbolic ? ( 1 - p * p ) : ( 1 + p * p );

				double center = p * numeratorCenter / ( 1 - p * p * r * r );
				radiusEuclidean = r * numeratorRadius / ( 1 - p * p * r * r );
				centerEuclidean = vNonEuclidean * center;

				/*
				// Alternate impl, in this case for spherical.
				Mobius m = new Mobius();
				m.Isometry( Geometry.Spherical, 0, p1 );
				Vector3D t1 = m.Apply( new Vector3D( mag, 0, 0 ) );
				Vector3D t2 = m.Apply( new Vector3D( -mag, 0, 0 ) );
				center = ( t1 + t2 ) / 2;
				radius = t1.Dist( t2 ) / 2; */
			}

			private static void ApplyMinRadiusForWiki( ref double radius )
			{
				radius = Math.Max( radius, 0.0001 );
				//radius = Math.Max( radius, 0.0005 );
				//radius = Math.Max( radius, 0.00001 );
			}

			private static void ApplyMinRadiusForPrinting( ref double radius )
			{
				radius = Math.Max( radius, (1.05 /*mm*/ / 2) / H3.m_settings.Scale );
			}

			/// <summary>
			/// A size function for the ball model.
			/// Returns a radius.
			/// </summary>
			public static double SizeFunc( Vector3D v, double angularThickness )
			{
				// Leverage the UHS function.
				Vector3D uhs = BallToUHS( v );
				double result = UHS.SizeFunc( uhs, angularThickness );
				uhs.X += result;
				Vector3D ball = UHSToBall( uhs );
				result = ( v - ball ).Abs();

				// Wiki images.
				result = Math.Max( result, 0.0005 );
				return result;

				// Shapeways.
				//result = Math.Max( result, ( 1.05 /*mm*/ / 2 ) / H3.m_settings.Scale );
				//return result;

				/* OLD WAY

				//return 0.0133333;
				//return Scale( 0.125 / 2 );

				double abs = v.Abs();

				// The thickness at this vector location.
				// Convert to H3, add thickness, convert back to E3, then subtract original vector.
				double result = DonHatch.h2eNorm( DonHatch.e2hNorm( abs ) + m_settings.Ball_BaseThickness ) - abs;

				// Keep us from being too small.
				//result = Math.Max( result, 0.05 );
				//result += 0.0075;
				return result;
				
				*/
			}

			/// <summary>
			/// Given 2 points on the surface of the ball, calculate the center and radius of the orthogonal circle.
			/// </summary>
			public static void OrthogonalCircle( Vector3D v1, Vector3D v2, out Vector3D center, out double radius )
			{
				// Picture at http://planetmath.org/OrthogonalCircles.html helpful for what I'm doing here.
				double sectorAngle = v1.AngleTo( v2 );
				if( Tolerance.Equal( sectorAngle, Math.PI ) )
				{
					center = Infinity.InfinityVector;
					radius = double.PositiveInfinity;
					return;
				}

				double distToCenter = m_pRadius / Math.Cos( sectorAngle / 2 );
				center = v1 + v2;
				center.Normalize();
				center *= distToCenter;

				radius = distToCenter * Math.Sin( sectorAngle / 2 );
			}

			public static Circle3D OrthogonalCircle( Vector3D v1, Vector3D v2 )
			{
				Vector3D center;
				double rad;
				OrthogonalCircle( v1, v2, out center, out rad );
				Vector3D normal = v1.Cross( v2 );
				return new Circle3D { Center = center, Normal = normal, Radius = rad };
			}

			/// <summary>
			/// Given 2 points in the interior of the ball, calculate the center and radius of the orthogonal circle.
			/// One point may optionally be on the boundary, but one shoudl be in the interior.
			/// If both points are on the boundary, we'll fall back on our other method.
			/// </summary>
			public static void OrthogonalCircleInterior( Vector3D v1, Vector3D v2, out Circle3D circle )
			{
				if( Tolerance.Equal( v1.Abs(), 1 ) &&
					Tolerance.Equal( v2.Abs(), 1 ) )
				{
					circle = OrthogonalCircle( v1, v2 );
					return;
				}
	
				// http://www.math.washington.edu/~king/coursedir/m445w06/ortho/01-07-ortho-to3.html
				// http://www.youtube.com/watch?v=Bkvo09KE1zo

				Vector3D interior = Tolerance.Equal( v1.Abs(), 1 ) ? v2 : v1;
				
				Sphere ball = new Sphere();
				Vector3D reflected = ball.ReflectPoint( interior );
				circle = new Circle3D( reflected, v1, v2 );
			}

			/// <summary>
			/// Find the sphere defined by 3 points on the unit sphere, and orthogonal to the unit sphere.
			/// Returns null if points are not on the unit sphere.
			/// </summary>
			public static Sphere OrthogonalSphere( Vector3D b1, Vector3D b2, Vector3D b3 )
			{
				Sphere unitSphere = new Sphere();
				if( !unitSphere.IsPointOn( b1 ) ||
					!unitSphere.IsPointOn( b2 ) ||
					!unitSphere.IsPointOn( b3 ) )
					return null;

				Circle3D c = new Circle3D( b1, b2, b3 );

				// Same impl as orthogonal circles now.
				Vector3D center;
				double radius;
				OrthogonalCircle( b1, b1 + ( c.Center - b1 ) * 2, out center, out radius );

				Sphere sphere = new Sphere();
				if( Infinity.IsInfinite( radius ) )
				{
					// Have the center act as a normal.
					sphere.Center = c.Normal;
					sphere.Radius = double.PositiveInfinity;
				}
				else
				{
					sphere.Center = center;
					sphere.Radius = radius;
				}
				return sphere;
			}

			/// <summary>
			/// Given a geodesic sphere, returns it's intersection with the boundary plane.
			/// </summary>
			public static Circle3D IdealCircle( Sphere s )
			{
				Vector3D s1, s2, s3;
				IdealPoints( s, out s1, out s2, out s3 );
				return new Circle3D( s1, s2, s3 );
			}

			/// <summary>
			/// Given a geodesic sphere, calculates 3 ideal points of the sphere.
			/// NOTE: s1 and s2 will be antipodal on the ideal circle.
			/// </summary>
			public static void IdealPoints( Sphere s, out Vector3D s1, out Vector3D s2, out Vector3D s3 )
			{
				// Get two points on the ball and sphere.
				// http://mathworld.wolfram.com/OrthogonalCircles.html
				// Orthogonal circles, plus some right angle stuff...
				double r = s.Radius;
				Vector3D direction = s.Center;
				Vector3D perp = direction.Perpendicular();
				direction.Normalize();
				s1 = direction;
				s2 = direction;

				double alpha = Math.Atan( r );
				s1.RotateAboutAxis( perp, alpha );
				s2.RotateAboutAxis( perp, -alpha );
				s3 = s1;
				s3.RotateAboutAxis( direction, Math.PI / 2 );
			}

			/// <summary>
			/// Find the sphere defined by 3 points in the interior of the unit sphere, and orthogonal to the unit sphere.
			/// </summary>
			public static Sphere OrthogonalSphereInterior( Vector3D c1, Vector3D c2, Vector3D c3 )
			{
				// Use circle points to find points on our boundary.
				Vector3D b1, b2, b3, dummy;
				GeodesicIdealEndpoints( c1, c2, out b1, out b2 );
				GeodesicIdealEndpoints( c3, c2, out b3, out dummy );

				return OrthogonalSphere( b1, b2, b3 );
			}

			/// <summary>
			/// Find an orthogonal sphere defined by a single interior point.
			/// This point is the unique point on the sphere that is furthest from the ball boundary.
			/// (equivalently, closest to the origin)
			/// </summary>
			public static Sphere OrthogonalSphereInterior( Vector3D v )
			{
				// r = radius of sphere
				// c = distance from origin to passed in point
				// http://www.wolframalpha.com/input/?i=%28c%2Br%29%5E2+%3D+1+%2B+r%5E2%2C+solve+for+r
				double c = v.Abs();
				double r = -(c * c - 1) / (2 * c);

				v.Normalize();
				return new Sphere()
				{
					Center = v * ( c + r ),
					Radius = r
				};
			}

			/// <summary>
			/// Given a geodesic sphere, find the point closest to the origin.
			/// </summary>
			public static Vector3D ClosestToOrigin( Sphere s )
			{
				return s.ProjectToSurface( new Vector3D() );
			}

			/// <summary>
			/// Given a geodesic circle, find the point closest to the origin.
			/// </summary>
			public static Vector3D ClosestToOrigin( Circle3D c )
			{
				Sphere s = new Sphere { Center = c.Center, Radius = c.Radius };
				return ClosestToOrigin( s );
			}

			/// <summary>
			/// Returns the hyperbolic distance between two points.
			/// </summary>
			public static double HDist( Vector3D u, Vector3D v )
			{
				double isometricInvariant = 2 * ( u - v ).MagSquared() / ( ( 1.0 - u.MagSquared() ) * ( 1.0 - v.MagSquared() ) );
				return DonHatch.acosh( 1 + isometricInvariant );
			}

			/// <summary>
			/// This is a 2D function for now.
			/// Given an input geodesic in the plane, returns an equidistant circle.
			/// Offset would be the offset at the origin.
			/// Works with all geometries.
			/// </summary>
			public static Circle EquidistantOffset( Geometry g, Segment seg, double offset )
			{
				Mobius m = new Mobius();
				Vector3D direction;
				if( seg.Type == SegmentType.Line )
				{
					direction = seg.P2 - seg.P1;
					direction.RotateXY( Math.PI / 2 );
				}
				else
				{
					direction = seg.Circle.Center;
				}

				direction.Normalize();
				m.Isometry( g, 0, direction * offset );

				System.Func<Vector3D, Vector3D> transform = v =>
				{
					if( Tolerance.Equal( v.Abs(), 1 ) )
						return v;	// XXX, not true in general, but code below goes haywire if this is true.

					Mobius m2 = new Mobius(), m3 = new Mobius();
					m2.Isometry( g, 0, -v );
					Vector3D p1_ = m2.Apply( seg.P1 );
					Vector3D p2_ = m2.Apply( seg.P2 );

					Vector3D direction2 = p2_ - p1_;
					direction2.RotateXY( Math.PI / 2 );
					direction2.Normalize();
					m3.Isometry( g, 0, direction2 * offset );

					Mobius final = m2.Inverse() * m3 * m2;	// This is correct, I think
					//Mobius final = m2.Inverse() * m * m2;
					//Mobius final = m;
					return final.Apply( v );
				};

				// Transform 3 points on segment.
				Vector3D p1 = transform( seg.P1 );
				Vector3D p2 = transform( seg.Midpoint );
				Vector3D p3 = transform( seg.P2 );

				return new Circle( p1, p2, p3 );
			}

			/// <summary>
			/// Calculate the hyperbolic midpoint of an edge.
			/// Only works for non-ideal edges at the moment.
			/// </summary>
			public static Vector3D Midpoint( H3.Cell.Edge edge )
			{
				// Special case if edge has endpoint on origin.
				// XXX - Really this should be special case anytime edge goes through origin.
				Vector3D e1 = edge.Start;
				Vector3D e2 = edge.End;
				if( e1.IsOrigin || e2.IsOrigin )
				{
					if( e2.IsOrigin )
						Utils.Swap<Vector3D>( ref e1, ref e2 );

					return HalfTo( e2 );
				}

				// No doubt there is a much better way, but
				// work in H2 slice transformed to xy plane, with e1 on x-axis.

				double angle = e1.AngleTo( e2 );	// always <= 180
				e1 = new Vector3D( e1.Abs(), 0 );
				e2 = new Vector3D( e2.Abs(), 0 );
				e2.RotateXY( angle );

				// Mobius that will move e1 to origin.
				Mobius m = new Mobius();
				m.Isometry( Geometry.Hyperbolic, 0, -e1 );
				e2 = m.Apply( e2 );

				Vector3D midOnPlane = HalfTo( e2 );
				midOnPlane= m.Inverse().Apply( midOnPlane );
				double midAngle = e1.AngleTo( midOnPlane );

				Vector3D mid = edge.Start;
				mid.RotateAboutAxis( edge.Start.Cross( edge.End ), midAngle );
				mid.Normalize( midOnPlane.Abs() );
				return mid;
			}

			private static Vector3D HalfTo( Vector3D v )
			{
				double distHyperbolic = DonHatch.e2hNorm( v.Abs() );
				double halfDistEuclidean = DonHatch.h2eNorm( distHyperbolic / 3 );
				Vector3D result = v;
				result.Normalize( halfDistEuclidean );
				return result;
			}

			/// <summary>
			/// Given two points (in the ball model), find the endpoints 
			/// of the associated geodesic that lie on the boundary.
			/// </summary>
			public static void GeodesicIdealEndpoints( Vector3D v1, Vector3D v2, out Vector3D b1, out Vector3D b2 )
			{
				// Leverage the UHS method.
				Vector3D v1_UHS = H3Models.BallToUHS( v1 );
				Vector3D v2_UHS = H3Models.BallToUHS( v2 );
				H3Models.UHS.GeodesicIdealEndpoints( v1_UHS, v2_UHS, out b1, out b2 );
				b1 = H3Models.UHSToBall( b1 );
				b2 = H3Models.UHSToBall( b2 );
			}

			public static void Geodesic( Vector3D v1, Vector3D v2, out Vector3D center, out double radius, out Vector3D normal, out double angleTot )
			{
				bool finite = !Tolerance.Equal( v1.MagSquared(), 1 ) || !Tolerance.Equal( v2.MagSquared(), 1 );
				if( finite )
				{
					Circle3D c;
					H3Models.Ball.OrthogonalCircleInterior( v1, v2, out c );
					center = c.Center;
					radius = c.Radius;
				}
				else
					H3Models.Ball.OrthogonalCircle( v1, v2, out center, out radius );
				Vector3D t1 = v1 - center;
				Vector3D t2 = v2 - center;
				t1.Normalize();	// This was necessary so that the cross product below didn't get too small.
				t2.Normalize();
				normal = ( t1 ).Cross( t2 );
				normal.Normalize();
				angleTot = ( t1 ).AngleTo( t2 );
			}

			/// <summary>
			/// Calculate points along a geodesic segment from v1 to v2.
			/// </summary>
			public static Vector3D[] GeodesicPoints( Vector3D v1, Vector3D v2 )
			{
				//int div = 20;
				int div = 36;
				//int div = 40; // Wiki
				//LODThin( v1, v2, out div );
				
				// Be smart about the number of divisions.
				Vector3D center, normal;
				double radius, angleTot;
				Geodesic( v1, v2, out center, out radius, out normal, out angleTot );
				double length = radius * angleTot;
				div = (int)(length * 57 / 2);

				// Keep in reasonable range.
				div = Math.Max( div, 11 );
				div = Math.Min( div, 57 );

				return GeodesicPoints( v1, v2, div );
			}

			/// <summary>
			/// Calculate points along a geodesic segment from v1 to v2.
			/// </summary>
			public static Vector3D[] GeodesicPoints( Vector3D v1, Vector3D v2, int div )
			{
				Vector3D center, normal;
				double radius, angleTot;
				Geodesic( v1, v2, out center, out radius, out normal, out angleTot );

				if( Infinity.IsInfinite( radius ) ||
					Tolerance.Zero( v1.Abs() ) || Tolerance.Zero( v2.Abs() ) )	// HACK! radius should be infinite, something wrong with geodesic func
				{
					Segment seg = Segment.Line( v1, v2 );
					return seg.Subdivide( div );
					//return new Vector3D[] { v1, v2 };
				}
				else
					return Shapeways.CalcArcPoints( center, radius, v1, normal, angleTot, div );
			}

			public static void LODThin( Vector3D e1, Vector3D e2, out int div )
			{
				int maxHit = 12;
				//Vector3D avg = ( e1 + e2 ) / 2;
				//int hit = (int)( avg.Abs() * maxHit );
				double dist = e1.Dist( e1 );
				int hit = (int)dist * 20 * maxHit;
				div = 20 - hit;
			}

			/// <summary>
			/// LOD
			/// </summary>
			public static void LOD_Finite( Vector3D e1, Vector3D e2, out int div1, out int div2, H3.Settings settings )
			{
				//if( settings.Halfspace )
				//	throw new System.NotImplementedException();

				int maxHit = 15;
				int hit = (int)( Math.Max( e1.Abs(), e2.Abs() ) * maxHit );
				div1 = 11;
				div2 = 30 - hit;

				/* lasercrystal
				int maxHit = 8;
				int hit = (int)( Math.Max( e1.Abs(), e2.Abs() ) * maxHit );
				div1 = 6;
				div2 = 20 - hit;*/
			}

			public static void LOD_Ideal( Vector3D e1, Vector3D e2, out int div1, out int div2, H3.Settings settings )
			{
				if( settings.Halfspace )
					throw new System.NotImplementedException();

				div1 = 13;
				div2 = (int)( 5 + Math.Sqrt( e1.Dist( e2 ) ) * 10 );
			}

			/// <summary>
			/// Helper to apply a Mobius to the ball model.
			/// Vector is taken to UHS, mobius applied, then taken back.
			/// </summary>
			public static Vector3D ApplyMobius( Mobius m, Vector3D v )
			{
				v = BallToUHS( v );
				v = m.ApplyToQuaternion( v );
				return UHSToBall( v );
			}
		}

		public static class UHS
		{
			/// <summary>
			/// Hyperbolic to Euclidean norm
			/// The output is a vertical distance from 0,0,0
			/// </summary>
			public static double ToE( double hNorm )
			{
				double eNorm = DonHatch.h2eNorm( hNorm );
				Vector3D uhs = H3Models.BallToUHS( new Vector3D( 0, 0, eNorm ) );
				return uhs.Z;
			}

			/// <summary>
			/// Euclidean to UHS norm
			/// </summary>
			public static double FromE( double eNorm )
			{
				throw new System.NotImplementedException();
			}

			/// <summary>
			/// Hyperbolic to Euclidean norml
			/// The output is a horizontal distance from 0,0,z
			/// </summary>
			public static double ToEHorizontal( double hNorm, double z )
			{
				// https://en.wikipedia.org/wiki/Poincar%C3%A9_half-plane_model
				double offset = Math.Sqrt( ( DonHatch.cosh( hNorm ) - 1 ) * 2 * z * z );
				return offset;
			}

			/// <summary>
			/// A size function for the UHS model.
			/// Returns a radius.
			/// </summary>
			public static double SizeFunc( Vector3D v, double angularThickness )
			{
				double size = v.Z * Math.Tan( angularThickness );
				//if( size == 0 )
				//	size = 0.001;

				return size;

				/* OLD WAY
				 			
				// http://en.wikipedia.org/wiki/Poincar%C3%A9_half-plane_model
				// http://www.wolframalpha.com/input/?i=d+%3D+acosh%28+1+%2B+%28+a+-+b+%29%5E2%2F%282*a*b%29+%29%2C+solve+for+a

				double d = m_settings.Ball_BaseThickness;
				double b = v.Z;
				double t = Math.Cosh( d );
				double a = b * t + Math.Sqrt( b * b * t * t - b * b );
				return Math.Abs( b - a );
				 
				*/
			}

			/// <summary>
			/// Given two points (in the UHS model), find the endpoints 
			/// of the associated geodesic that lie on the z=0 plane.
			/// </summary>
			public static void GeodesicIdealEndpoints( Vector3D v1, Vector3D v2, out Vector3D z1, out Vector3D z2 )
			{
				// We have to special case when geodesic is vertical (parallel to z axis).
				Vector3D diff = v2 - v1;
				Vector3D diffFlat = new Vector3D( diff.X, diff.Y );
				if( Tolerance.Zero( diffFlat.Abs() ) )	// Vertical
				{
					Vector3D basePoint = new Vector3D( v1.X, v1.Y );
					z1 = diff.Z > 0 ? basePoint : Infinity.InfinityVector;
					z2 = diff.Z < 0 ? basePoint : Infinity.InfinityVector;
				}
				else
				{
					if( Tolerance.Zero( v1.Z ) && Tolerance.Zero( v2.Z ) )
					{
						z1 = v1;
						z2 = v2;
						return;
					}

					// If one point is ideal, we need to not reflect that one!
					bool swapped = false;
					if( Tolerance.Zero( v1.Z ) )
					{
						Utils.SwapPoints( ref v1, ref v2 );
						swapped = true;
					}

					Vector3D v1_reflected = v1;
					v1_reflected.Z *= -1;
					Circle3D c = new Circle3D( v1_reflected, v1, v2 );
					Vector3D radial = v1 - c.Center;
					radial.Z = 0;
					if( !radial.Normalize() )
					{
						radial = v2 - c.Center;
						radial.Z = 0;
						if( !radial.Normalize() )
							System.Diagnostics.Debugger.Break();
					}

					radial *= c.Radius;			
					z1 = c.Center + radial;
					z2 = c.Center - radial;

					// Make sure the order will be right.
					// (z1 closest to v1 along arc).
					if( v1.Dist( z1 ) > v2.Dist( z1 ) )
						Utils.SwapPoints( ref z1, ref z2 );
					if( swapped )
						Utils.SwapPoints( ref z1, ref z2 );
				}
			}

			/// <summary>
			/// Takes a set of finite edges, and returns a new set of ideal edges which touch the boundary.
			/// Duplicate ideal edges are removed (since multiple finite edges can result in the same ideal edge).
			/// </summary>
			public static IEnumerable<H3.Cell.Edge> ExtendEdges( IEnumerable<H3.Cell.Edge> edges )
			{
				HashSet<H3.Cell.Edge> infiniteEdges = new HashSet<H3.Cell.Edge>();
				foreach( H3.Cell.Edge edge in edges )
				{
					Vector3D start_i, end_i;
					H3Models.Ball.GeodesicIdealEndpoints( edge.Start, edge.End, out start_i, out end_i );
					infiniteEdges.Add( new H3.Cell.Edge( start_i, end_i ) );
				}
				return infiniteEdges;
			}

			public static void Geodesic( Vector3D v1, Vector3D v2, out Vector3D center, out double radius )
			{
				Vector3D dummy;
				double dummyAngle;
				Geodesic( v1, v2, out center, out radius, out dummy, out dummyAngle );
			}

			public static void Geodesic( Vector3D v1, Vector3D v2, out Vector3D center, out double radius, out Vector3D normal, out double angleTot )
			{
				Vector3D _v1, _v2;
				GeodesicIdealEndpoints( v1, v2, out _v1, out _v2 );

				center = ( _v1 + _v2 ) / 2;
				radius = _v1.Dist( _v2 ) / 2;
				Vector3D vertical = new Vector3D( center.X, center.Y, radius );
				normal = ( _v1 - center ).Cross( vertical - center );
				normal.Normalize();
				angleTot = ( v1 - center ).AngleTo( v2 - center );
			}

			/// <summary>
			/// Calculate points along a geodesic segment from v1 to v2.
			/// </summary>
			public static Vector3D[] GeodesicPoints( Vector3D v1, Vector3D v2 )
			{
				return GeodesicPoints( v1, v2, 37 );
			}

			/// <summary>
			/// Calculate points along a geodesic segment from v1 to v2.
			/// </summary>
			public static Vector3D[] GeodesicPoints( Vector3D v1, Vector3D v2, int div )
			{
				Vector3D center, normal;
				double radius, angleTot;
				Geodesic( v1, v2, out center, out radius, out normal, out angleTot );

				// Vertical?
				if( Infinity.IsInfinite( radius ) )
				{
					Segment seg = Segment.Line( v1, v2 );
					return seg.Subdivide( div );
				}

				return Shapeways.CalcArcPoints( center, radius, v1, normal, angleTot, div );
			}

			/// <summary>
			/// Given a geodesic sphere, returns it's intersection with the boundary plane.
			/// </summary>
			public static Circle IdealCircle( Sphere s )
			{
				Vector3D s1, s2, s3;
				IdealPoints( s, out s1, out s2, out s3 );
				return new Circle( s1, s2, s3 );
			}
			
			/// <summary>
			/// Given a geodesic sphere, calculates 3 ideal points of the sphere.
			/// </summary>
			public static void IdealPoints( Sphere s, out Vector3D s1, out Vector3D s2, out Vector3D s3 )
			{
				if( s.IsPlane )
				{
					s1 = s.Offset;
					s2 = s3 = s.Normal;
					s2.RotateXY( Math.PI / 2 ); s2 += s1;
					s3.RotateXY( -Math.PI / 2 ); s3 += s1;
					return;
				}

				Vector3D cen = s.Center;
				cen.Z = 0;

				s1 = new Vector3D( s.Radius, 0 );
				s2 = s3 = s1;
				s2.RotateXY( Math.PI / 2 );
				s3.RotateXY( Math.PI );

				s1 += cen;
				s2 += cen;
				s3 += cen;
			}
		}
	}

	public static class H3Sphere
	{
		/// <summary>
		/// A helper for adding a sphere.  center should be passed in the ball model.
		/// The approach is similar to how we do the bananas below.
		/// </summary>
		public static void AddSphere( Shapeways mesh, Vector3D center, H3.Settings settings )
		{
			Vector3D centerUHS = H3Models.BallToUHS( center );

			// Find the Mobius we need.
			// We'll do this in two steps.  
			// (1) Find a mobius taking center to (0,0,h).
			// (2) Deal with scaling to a height of 1.
			Vector3D flattened = centerUHS;
			flattened.Z = 0;
			Mobius m1 = new Mobius( flattened, Complex.One, Infinity.InfinityVector );
			Vector3D centerUHS_transformed = m1.ApplyToQuaternion( centerUHS );
			double scale = 1.0 / centerUHS_transformed.Z;
			Mobius m2 = new Mobius( scale, Complex.Zero, Complex.Zero, Complex.One );
			Mobius m = m2 * m1;	// Compose them (multiply in reverse order).

			// Add the sphere at the Ball origin.
			// It will *always* be generated with the same radius.
			Shapeways tempMesh = new Shapeways();
			tempMesh.AddSphere( new Vector3D(), H3Models.Ball.SizeFunc( new Vector3D(), settings.AngularThickness ) );

			// Unwind the transforms.
			for( int i=0; i<tempMesh.Mesh.Triangles.Count; i++ )
			{
				tempMesh.Mesh.Triangles[i] = new Mesh.Triangle(
					H3Models.BallToUHS( tempMesh.Mesh.Triangles[i].a ),
					H3Models.BallToUHS( tempMesh.Mesh.Triangles[i].b ),
					H3Models.BallToUHS( tempMesh.Mesh.Triangles[i].c ) );
			}

			Banana.TakePointsBack( tempMesh.Mesh, m.Inverse(), settings );
			mesh.Mesh.Triangles.AddRange( tempMesh.Mesh.Triangles );
		}
	}

	/// <summary>
	/// A helper class for doing proper calculations of H3 "bananas"
	/// 
	/// Henry thought this out.  His words:
	/// Take a pair of points giving the ends of the geodesic. 
	/// If the points are in the ball model, move them to the UHS model.
	/// Find the endpoints of the geodesic through the two points on the z=0 plane in the UHS model.
	/// Apply the Mobius transform that takes the geodesic to the z-axis, and takes the first endpoint of the segment to height 1, and so the other to height h>1.
	/// The hyperbolic banana is a truncated cone in this configuration with axis the z-axis, truncated at 1 and h. The slope of the cone is the parameter for the thickness of the banana.
	/// Choose points for approximating the cone with polygons. We have some number of circles spaced vertically up the cone, and lines perpendicular to these circles that go through the origin. The intersections between the circles and the lines are our vertices. We want the lines with equal angle spacing around the z-axis, and the circles spaced exponentially up the z-axis, with one circle at 1 and one at h.
	/// Now map those vertices forward through all of our transformations.
	/// </summary>
	public static class Banana
	{
		/// <summary>
		/// Add an ideal banana to our mesh.  Passed in edge should be in Ball model.
		/// </summary>
		public static void AddIdealBanana( Shapeways mesh, Vector3D e1, Vector3D e2, H3.Settings settings )
		{
			Vector3D z1 = H3Models.BallToUHS( e1 );
			Vector3D z2 = H3Models.BallToUHS( e2 );

			// Mobius taking z1,z2 to origin,inf
			Complex dummy = new Complex( Math.E, Math.PI );
			Mobius m = new Mobius( z1, dummy, z2 );

			// Make our truncated cone.  We need to deal with the two ideal endpoints specially.
			List<Vector3D> points = new List<Vector3D>();
			double logHeight = 2;	// XXX - magic number, and going to cause problems for infinity checks if too big.
			int div1, div2;
			H3Models.Ball.LOD_Ideal( e1, e2, out div1, out div2, settings );
			double increment = logHeight / div1;
			for( int i=-div1; i<=div1; i+=2 )
				points.Add( new Vector3D( 0, 0, Math.Exp( increment * i ) ) );

			Shapeways tempMesh = new Shapeways();
			tempMesh.Div = div2;
			System.Func<Vector3D, double> sizeFunc = v => H3Models.UHS.SizeFunc( v, settings.AngularThickness );
			//Mesh.OpenCylinder...  pass in two ideal endpoints?
			tempMesh.AddCurve( points.ToArray(), sizeFunc, new Vector3D(), Infinity.InfinityVector );

			// Unwind the transforms.
			TakePointsBack( tempMesh.Mesh, m.Inverse(), settings );
			mesh.Mesh.Triangles.AddRange( tempMesh.Mesh.Triangles );
		}

		/// <summary>
		/// Add a finite (truncated) banana to our mesh.  Passed in edge should be in Ball model.
		/// </summary>
		public static void AddBanana( Shapeways mesh, Vector3D e1, Vector3D e2, H3.Settings settings )
		{
			Vector3D e1UHS = H3Models.BallToUHS( e1 );
			Vector3D e2UHS = H3Models.BallToUHS( e2 );

			// Endpoints of the goedesic on the z=0 plane.
			Vector3D z1, z2;
			H3Models.UHS.GeodesicIdealEndpoints( e1UHS, e2UHS, out z1, out z2 );
			
			// XXX - Do we want to do a better job worrying about rotation here? 
			// (multiply by complex number with certain imaginary part as well)
			//Vector3D z3 = ( z1 + z2 ) / 2;
			//if( Infinity.IsInfinite( z3 ) )
			//	z3 = new Vector3D( 1, 0 );
			Vector3D z3 = new Vector3D( Math.E, Math.PI );	// This should vary the rotations a bunch.

			// Find the Mobius we need.
			// We'll do this in two steps.  
			// (1) Find a mobius taking z1,z2 to origin,inf 
			// (2) Deal with scaling e1 to a height of 1.  
			Mobius m1 = new Mobius( z1, z3, z2 );
			Vector3D e1UHS_transformed = m1.ApplyToQuaternion( e1UHS );
			double scale = 1.0 / e1UHS_transformed.Z;
			Mobius m2 = Mobius.Scale( scale );
			Mobius m = m2 * m1;	// Compose them (multiply in reverse order).
			Vector3D e2UHS_transformed = m.ApplyToQuaternion( e2UHS );

			// Make our truncated cone.
			// For regular tilings, we really would only need to do this once for a given LOD.
			List<Vector3D> points = new List<Vector3D>();
			double logHeight = Math.Log( e2UHS_transformed.Z );
			if( logHeight < 0 )
				throw new System.Exception( "impl issue" );
			int div1, div2;
			H3Models.Ball.LOD_Finite( e1, e2, out div1, out div2, settings );
			double increment = logHeight / div1;
			for( int i=0; i<=div1; i++ )
			{
				double h = increment * i;

				// This is to keep different bananas from sharing exactly coincident vertices.
				double tinyOffset = 0.001;
				if( i == 0 )
					h -= tinyOffset;
				if( i == div1 )
					h += tinyOffset;

				Vector3D point = new Vector3D( 0, 0, Math.Exp( h ) );
				points.Add( point );
			}
			Shapeways tempMesh = new Shapeways();
			tempMesh.Div = div2;
			tempMesh.AddCurve( points.ToArray(), v => H3Models.UHS.SizeFunc( v, settings.AngularThickness ) );

			// Unwind the transforms.
			TakePointsBack( tempMesh.Mesh, m.Inverse(), settings );
			mesh.Mesh.Triangles.AddRange( tempMesh.Mesh.Triangles );
		}

		internal static void TakePointsBack( Mesh mesh, Mobius m, H3.Settings settings )
		{
			for( int i=0; i<mesh.Triangles.Count; i++ )
			{
				mesh.Triangles[i] = new Mesh.Triangle(
					m.ApplyToQuaternion( mesh.Triangles[i].a ),
					m.ApplyToQuaternion( mesh.Triangles[i].b ),
					m.ApplyToQuaternion( mesh.Triangles[i].c ) );

				/*if( Infinity.IsInfinite( mesh.Triangles[i].a ) ||
					Infinity.IsInfinite( mesh.Triangles[i].b ) ||
					Infinity.IsInfinite( mesh.Triangles[i].c ) )
					System.Diagnostics.Debugger.Break();*/
			}

			// Take all points back to Ball, if needed.
			if( !settings.Halfspace )
			{
				for( int i=0; i<mesh.Triangles.Count; i++ )
				{
					mesh.Triangles[i] = new Mesh.Triangle(
						H3Models.UHSToBall( mesh.Triangles[i].a ),
						H3Models.UHSToBall( mesh.Triangles[i].b ),
						H3Models.UHSToBall( mesh.Triangles[i].c ) );
				}
			}
		}
	}
}
