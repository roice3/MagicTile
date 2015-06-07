namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.Diagnostics;
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

		private void Reset()
		{
			Center = new Vector3D();
			Radius = 1;
			Offset = new Vector3D();
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
		/// XXX - A vector is not required...We could use a double.
		/// </summary>
		public Vector3D Offset { get; set; }

		/// <summary>
		/// For planes, the normal.
		/// </summary>
		public Vector3D Normal
		{
			get
			{
				return Center;
			}
		}

		public bool IsPlane
		{
			get
			{
				return Infinity.IsInfinite( Radius );
			}
		}

		public override bool Equals( object obj )
		{
			Sphere s = (Sphere)obj;
			return 
				Radius == s.Radius && 
				Center == s.Center &&
				Offset == s.Offset;
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
			// For planes, this calcs us relative to the normal.
			if( IsPlane )
			{
				return (test - this.Offset).Dot( this.Normal ) < 0;
			}

			return Tolerance.LessThan( ( test - Center ).Abs(), Radius );
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
				// By convention, plane goes through origin and this.Center designates normal.
				Vector3D v = Euclidean3D.ProjectOntoPlane( this.Normal, this.Offset, p );
				v = p + (v - p)*2;
				return v;
			}
			else
			{
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
			if( this.Equals( sphere ) )
				return;

			// Are we a plane?
			if( IsPlane )
			{
				if( Infinity.IsInfinite( sphere.Radius ) )
					throw new System.NotImplementedException();

				// XXX - not general, but I know the planes I'll be dealing with go through the origin.
				Vector3D projected  = Euclidean3D.ProjectOntoPlane( this.Normal, this.Offset, sphere.Center );
				Vector3D p = sphere.ReflectPoint( projected );
				Center = sphere.Center + ( p - sphere.Center ) / 2;
				Radius = Center.Dist( sphere.Center );
				return;
			}

			// Is mirror a plane?
			if( sphere.IsPlane )
			{
				if( Infinity.IsInfinite( Radius ) )
					throw new System.NotImplementedException();

				Vector3D projected = Euclidean3D.ProjectOntoPlane( sphere.Normal, sphere.Offset, Center );
				Vector3D diff = Center - projected;
				Center -= 2 * diff;
				// Radius remains unchanged.
				return;
			}

			//
			// Now sphere reflecting in a sphere.
			//

			// Reflecting to a plane?
			if( IsPointOn( sphere.Center ) )
			{
				if( Center == sphere.Center )
					throw new System.Exception();

				Radius = double.PositiveInfinity;
				Vector3D center = Center - sphere.Center;
				//center.Normalize();
				Center = center;
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
		}

		// Sphere from 4 points.
		// http://paulbourke.net/geometry/circlesphere/
		public static Sphere From4Points( Vector3D s1, Vector3D s2, Vector3D s3, Vector3D s4 )
		{
			throw new System.NotImplementedException();
		}

		/// <summary>
		/// Radially project a point onto our surface.
		/// </summary>
		public Vector3D ProjectToSurface( Vector3D p )
		{
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
	}
}
