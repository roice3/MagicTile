namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.Serialization;

	public class Circle3D
	{
		public Vector3D Center { get; set; }
		public double Radius { get; set; }
		public Vector3D Normal { get; set; }

		public Circle3D()
		{
			Radius = 1;
			Normal = new Vector3D( 0, 0, 1 );
		}

		public Circle3D( Vector3D t1, Vector3D t2, Vector3D t3 )
		{
			Vector3D center;
			double radius;
			From3Points( t1, t2, t3, out center, out radius );

			Center = center;
			Radius = radius;
			
			Vector3D normal = ( t2 - t1 ).Cross( t3 - t1 );
			normal.Normalize();
			Normal = normal;
		}

		public Circle3D Clone()
		{
			return (Circle3D)MemberwiseClone();
		}

		/// <summary>
		/// Caller is responsible to make sure our normal is in the z direction.
		/// </summary>
		public Circle ToFlatCircle()
		{
			return new Circle { Center = Center, Radius = Radius };
		}

		public static Circle3D FromCenterAnd2Points( Vector3D cen, Vector3D p1, Vector3D p2 )
		{
			Circle3D circle = new Circle3D();
			circle.Center = cen;
			circle.Radius = ( p1 - cen ).Abs();

			if( !Tolerance.Equal( circle.Radius, ( p2 - cen ).Abs() ) )
				throw new System.ArgumentException( "Points are not on the same circle." );

			Vector3D normal = ( p2 - cen ).Cross( p1 - cen );
			normal.Normalize();
			circle.Normal = normal;
			return circle;
		}

		public Vector3D PointOnCircle
		{
			get
			{
				Vector3D[] points = Subdivide( 1 );
				return points.First();
			}
		}

		/// <summary>
		/// Returns 3 points that will define the circle (120 degrees apart).
		/// </summary>
		public Vector3D[] RepresentativePoints
		{
			get
			{
				return Subdivide( 3 );
			}
		}

		/// <summary>
		/// Calculate n points around the circle
		/// </summary>
		public Vector3D[] Subdivide( int n )
		{
			List<Vector3D> points = new List<Vector3D>();
			Vector3D start = Normal.Perpendicular();
			start *= Radius;

			double angleInc = 2 * Math.PI / n;
			for( int i=0; i<n; i++ )
			{
				Vector3D v = start;
				v.RotateAboutAxis( Normal, angleInc * i );
				points.Add( Center + v );
			}

			return points.ToArray();
		}

		private static void From3Points( Vector3D v1, Vector3D v2, Vector3D v3, 
			out Vector3D center, out double radius )
		{
			// Circumcenter/Circumradius of triangle (circle from 3 points)
			// http://mathworld.wolfram.com/Circumcenter.html
			// http://mathworld.wolfram.com/Circumradius.html
			// http://mathworld.wolfram.com/BarycentricCoordinates.html

			// side lengths and their squares
			double a = ( v3 - v2 ).Abs();	// Opposite v1
			double b = ( v1 - v3 ).Abs();	// Opposite v2
			double c = ( v2 - v1 ).Abs();	// Opposite v3
			double a2 = a * a;
			double b2 = b * b;
			double c2 = c * c;

			Vector3D circumCenterBary = new Vector3D(
				a2 * ( b2 + c2 - a2 ),
				b2 * ( c2 + a2 - b2 ),
				c2 * ( a2 + b2 - c2 ) );
			circumCenterBary /= (circumCenterBary.X + circumCenterBary.Y + circumCenterBary.Z);	// Normalize.
			center = BaryToCartesian( v1, v2, v3, circumCenterBary );

			double s = (a + b + c) / 2; // semiperimeter
			radius = a * b * c / ( 4 * Math.Sqrt( s * ( a + b - s ) * ( a + c - s ) * ( b + c - s ) ) );
		}

		/// <summary>
		/// Barycentric coords to Cartesian
		/// http://stackoverflow.com/questions/11262391/from-barycentric-to-cartesian
		/// </summary>
		private static Vector3D BaryToCartesian( Vector3D t1, Vector3D t2, Vector3D t3, Vector3D bary )
		{
			return bary.X * t1 + bary.Y * t2 + bary.Z * t3;
		}
	}

	public class Sphere
	{
		public Sphere()
		{
			Reset();
		}

		public Sphere( Vector3D center, double radius )
		{
			Reset();
			Center = center;
			Radius = radius;
		}

		public static Sphere Plane( Vector3D normal )
		{
			return Plane( new Vector3D(), normal );
		}

		public static Sphere Plane( Vector3D offset, Vector3D normal )
		{
			Sphere result = new Sphere();
			result.Center = normal;
			result.Offset = offset;
			result.Radius = double.PositiveInfinity;
			return result;
		}

		private void Reset()
		{
			Center = new Vector3D();
			Radius = 1;
			Offset = new Vector3D();
			Invert = false;
		}

		/// <summary>
		/// Our Center.
		/// </summary>
		public Vector3D Center { get; set; }

		/// <summary>
		/// Our Radius. As a limiting case, we support infinite radii.
		/// The sphere is then a plane with a normal equal to the center, and an optional offset.
		/// </summary>
		public double Radius { get; set; }

		/// <summary>
		/// Required for planes which do not go through the origin.
		/// This can be any point on the plane.
		/// XXX - A vector is not required...We could use a double.
		/// </summary>
		public Vector3D Offset { get; set; }

		/// <summary>
		/// Used to track which part of the sphere is "inside".
		/// </summary>
		public bool Invert { get; set; }

		/// <summary>
		/// For planes, the normal.
		/// </summary>
		public Vector3D Normal
		{
			get
			{
				if( !this.IsPlane )
					return Vector3D.DneVector();

				Vector3D n = Center;
				n.Normalize();
				return n;
			}
		}

		public bool IsPlane
		{
			get
			{
				return Infinity.IsInfinite( Radius );
			}
		}

		public Vector3D ID
		{
			get
			{
				if( IsPlane )
				{
					Vector3D n = this.Normal;
					n.Normalize();
					if( n.Z > 0 )
						n *= -1;
					return n + this.Offset;
				}

				return this.Center + new Vector3D( this.Radius/2, 0 );
			}
		}

		// XXX - Do we only want to compare the surface, or also the orientation?
		// This just does the surface.
		public override bool Equals( object obj )
		{
			Sphere s = (Sphere)obj;

			if( IsPlane )
			{
				if( !s.IsPlane )
					return false;

				Vector3D n1 = this.Normal;
				n1.Normalize();
				Vector3D n2 = s.Normal;
				n2.Normalize();
				return 
					( n1 == n2 || n1 == -n2 ) &&
					Offset == s.Offset;
			}

			if( s.IsPlane )
				return false;

			return
				Radius == s.Radius &&
				Center == s.Center /*&&
				Offset == s.Offset &&
				Invert == s.Invert*/;
		}

		public override int GetHashCode()
		{
			return Radius.GetHashCode() ^ Center.GetHashCode() ^ Offset.GetHashCode();
		}

		public Sphere Clone()
		{
			return (Sphere)MemberwiseClone();
		}

		// Strictly less than.
		public bool IsPointInside( Vector3D test )
		{
			bool inside;

			// For planes, this calcs us relative to the normal.
			if( IsPlane )
			{
				// Normal vector points to the "outside" (same in pov-ray).
				inside = ( test - this.Offset ).Dot( this.Normal ) < 0;
				//inside = Tolerance.LessThanOrEqual( ( test - this.Offset ).Dot( this.Normal ), 0 );
			}
			else
			{
				// XXX - Not General! (not as good as CenterNE calcs)
				inside = Tolerance.LessThan( ( test - Center ).Abs(), Radius );
				//inside = Tolerance.LessThanOrEqual( ( test - Center ).Abs(), Radius );
			}

			//if( Invert )
			//	throw new System.Exception();

			return Invert ? !inside : inside;
			//return inside;
		}

		public bool IsPointOn( Vector3D test )
		{
			if( IsPlane )
			{
				double dist = Euclidean3D.DistancePointPlane( this.Normal, this.Offset, test );
				return Tolerance.Zero( dist );
			}

			return Tolerance.Equal( ( test - Center ).Abs(), Radius );
		}

		public bool IsPointInsideOrOn( Vector3D test )
		{
			return IsPointInside( test ) || IsPointOn( test );
		}

		/// <summary>
		/// Reflect a point in us.
		/// </summary>
		public Vector3D ReflectPoint( Vector3D p )
		{
			if( IsPlane )
			{
				//Debug.Assert( !Infinity.IsInfinite( p ) );
				Vector3D v = Euclidean3D.ProjectOntoPlane( this.Normal, this.Offset, p );
				v = p + (v - p)*2;
				return v;
			}
			else
			{
				if( p == Center )
					return Infinity.InfinityVector;
				if( Infinity.IsInfinite( p ) )
					return Center;

				Vector3D v = p - Center;
				double d = v.Abs();
				v.Normalize();
				return Center + v * ( Radius * Radius / d );
			}
		}

		/// <summary>
		/// Reflect ourselves about another sphere.
		/// </summary>
		public void Reflect( Sphere sphere )
		{
			// An interior point used to calculate whether we get inverted.
			Vector3D interiorPoint;
			if( IsPlane )
			{
				Debug.Assert( !this.Normal.IsOrigin );
				interiorPoint = -this.Normal;
			}
			else
			{
				// We don't want it to be the center, because that will reflect to infinity.
				interiorPoint = ( this.Center + new Vector3D( this.Radius / 2, 0 ) );
			}
			if( Invert )
				interiorPoint = ReflectPoint( interiorPoint );
			Debug.Assert( IsPointInside( interiorPoint ) );
			interiorPoint = sphere.ReflectPoint( interiorPoint );
			Debug.Assert( !interiorPoint.DNE );

			if( this.Equals( sphere ) )
			{
				if( IsPlane )
				{
					//this.Center = -this.Center;	// Same as inverting, but we need to do it this way because of Pov-Ray
					this.Invert = !this.Invert;
				}
				else
					this.Invert = !this.Invert;

				Debug.Assert( this.IsPointInside( interiorPoint ) );
				return;
			}

			// Both planes?
			if( IsPlane && sphere.IsPlane )
			{
				// XXX - not general, but I know the planes I'll be dealing with go through the origin.
				//if( !sphere.Offset.IsOrigin )
				//	throw new System.NotImplementedException();

				/*Vector3D p1 = this.Normal.Cross( sphere.Normal );
				if( !p1.Normalize() )
				{
					this.Center *= -1;
					return;
				}
				
				Vector3D p2 = p1.Cross( this.Normal );
				p2.Normalize();
				p1 = sphere.ReflectPoint( p1 );
				p2 = sphere.ReflectPoint( p2 );
				Vector3D newNormal = p2.Cross( p1 );
				if( !newNormal.Normalize() )
					throw new System.Exception( "Reflection impl" );
				this.Center = newNormal;*/

				// Reflect the normal relative to the plane (conjugate with sphere.Offset).
				Vector3D newNormal = this.Normal + sphere.Offset;
				newNormal = sphere.ReflectPoint( newNormal );
				newNormal -= sphere.Offset;
				newNormal.Normalize();
				this.Center = newNormal;

				// Calc the new offset (so far we have considered planes through origin).
				this.Offset = sphere.ReflectPoint( this.Offset );

				//Debug.Assert( Offset.IsOrigin );	// XXX - should handle more generality.
				Debug.Assert( this.IsPointInside( interiorPoint ) );
				return;
			}

			// We are a plane and reflecting in a sphere.
			if( IsPlane )
			{
				// Think of 2D case here (circle and line)...
				Vector3D projected  = Euclidean3D.ProjectOntoPlane( this.Normal, this.Offset, sphere.Center );
				Vector3D p = sphere.ReflectPoint( projected );
				if( Infinity.IsInfinite( p ) )
				{
					// This can happen if we go through sphere.Center.
					// This reflection does not change our orientation (does not invert us).
					return;
				}

				Center = sphere.Center + ( p - sphere.Center ) / 2;
				Radius = Center.Dist( sphere.Center );

				// Did this invert us?
				if( !this.IsPointInside( interiorPoint ) )
					Invert = !Invert;

				return;
			}

			// Is mirror a plane?
			if( sphere.IsPlane )
			{
				Vector3D projected = Euclidean3D.ProjectOntoPlane( sphere.Normal, sphere.Offset, Center );
				Vector3D diff = Center - projected;
				Center -= 2 * diff;
				// Radius remains unchanged.
				// NOTE: This does not invert us.
				Debug.Assert( this.IsPointInside( interiorPoint ) );
				return;
			}

			//
			// Now sphere reflecting in a sphere.
			//

			// Reflecting to a plane?
			if( IsPointOn( sphere.Center ) )
			{
				// Concentric spheres?
				if( Center == sphere.Center )
					throw new System.Exception();

				// Center
				Vector3D center = Center - sphere.Center;

				// Offset
				Vector3D direction = center;
				direction.Normalize();
				Vector3D offset = direction * Radius * 2;
				offset = sphere.ReflectPoint( offset );

				// We are a line now.
				Center = center;
				//Offset = offset;	// Not working??  Caused issues in old generation code for 435.
				Radius = double.PositiveInfinity;

				// Did this invert us?
				if( !this.IsPointInside( interiorPoint ) )
					this.Invert = !this.Invert;

				Debug.Assert( this.IsPointInside( interiorPoint ) );
				return;
			}

			// XXX - Could try to share code below with Circle class.
			// NOTE: We can't just reflect the center.
			//		 See http://mathworld.wolfram.com/Inversion.html
			double a = Radius;
			double k = sphere.Radius;
			Vector3D v = Center - sphere.Center;
			double s = k * k / ( v.MagSquared() - a * a );
			Center = sphere.Center + v * s;
			Radius = Math.Abs( s ) * a;

			// Did this invert us?
			if( !this.IsPointInside( interiorPoint ) )
				Invert = !Invert;
		}

		// Sphere from 4 points.
		// Potentially good resource:  http://paulbourke.net/geometry/circlesphere/
		// Try to generalize Circle3D.From3Points
		public static Sphere From4Points( Vector3D s1, Vector3D s2, Vector3D s3, Vector3D s4 )
		{
			// http://www.gamedev.net/topic/652490-barycentric-coordinates-of-circumcenter-of-tetrahedron/

			/*
			// Python from Henry:
			def sphere_from_4_points(points):
			O, A, B, C = points
			a = A - O
			b = B - O
			c = C - O

			det = matrix3_det([a, b, c])
			denominator = 2. * det

			temp_v1 = cross(a,b) * dot(c,c)
			temp_v2 = cross(c,a) * dot(b,b)
			temp_v3 = cross(b,c) * dot(a,a)

			o = (temp_v1 + temp_v2 + temp_v3) * (1./denominator)

			radius = o.norm()
			center = O + o
			return (center,radius) */

			Vector3D O = s1, A = s2, B = s3, C = s4;
			Vector3D a = A - O;
			Vector3D b = B - O;
			Vector3D c = C - O;

			double det = a.X * ( b.Y * c.Z - b.Z * c.Y ) - a.Y * ( b.X * c.Z - b.Z * c.X ) + a.Z * ( b.X * c.Y - b.Y * c.X );
			double denominator = 2 * det;

			Vector3D temp_v1 = a.Cross( b ) * c.Dot( c );
			Vector3D temp_v2 = c.Cross( a ) * b.Dot( b );
			Vector3D temp_v3 = b.Cross( c ) * a.Dot( a );

			Vector3D o = (temp_v1 + temp_v2 + temp_v3) * (1/denominator);

			double radius = o.Abs();
			Vector3D center = O + o;
			return new Sphere() { Center = center, Radius = radius };
		}

		/// <summary>
		/// Radially project a point onto our surface.
		/// </summary>
		public Vector3D ProjectToSurface( Vector3D p )
		{
			if( this.IsPlane )
				return Euclidean3D.ProjectOntoPlane( this.Normal, this.Offset, p );

			Vector3D direction = p - Center;
			direction.Normalize();
			direction *= Radius;
			return Center + direction;
		}

		/// <summary>
		/// Finds the intersection (a circle) between us and another sphere.
		/// Returns null if sphere centers are coincident or no intersection exists.
		/// Does not currently work for planes.
		/// </summary>
		public Circle3D Intersection( Sphere s )
		{
			if( this.IsPlane || s.IsPlane )
				throw new System.NotImplementedException();

			double r = s.Radius;
			double R = this.Radius;

			Vector3D diff = this.Center - s.Center;
			double d = diff.Abs();
			if( Tolerance.Zero( d ) || d > r + R )
				return null;

			double x = ( d*d + r*r - R*R ) / ( 2*d );
			double y = Math.Sqrt( r*r - x*x );

			Circle3D result = new Circle3D();
			diff.Normalize();
			result.Normal = diff;
			result.Center = s.Center + diff * x;
			result.Radius = y; 
			return result;
		}

		public static void RotateSphere( Sphere s, Vector3D axis, double rotation )
		{
			if( s.IsPlane )
			{
				Vector3D o = s.Offset;
				o.RotateAboutAxis( axis, rotation );
				s.Offset = o;
			}

			Vector3D c = s.Center;
			c.RotateAboutAxis( axis, rotation );
			s.Center = c;
		}

		public static void ScaleSphere( Sphere s, double factor )
		{
			if( s.IsPlane )
				s.Offset *= factor;

			s.Center *= factor;
			s.Radius *= factor;
		}
	}
}
