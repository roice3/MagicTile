namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using System.Diagnostics;

	public static class Euclidean2D
	{
		/// <summary>
		/// Returns the counterclock angle between two vectors (between 0 and 2*pi)
		/// NOTE: A unique counter clockwise angle really only makes sense onces you've picked a plane normal direction.
		///		 So as coded, this function is really only intended to be used with 2D vector inputs.
		/// </summary>
		public static double AngleToCounterClock( Vector3D v1, Vector3D v2 )
		{
			double angle = Math.Atan2( v2.Y, v2.X ) - Math.Atan2( v1.Y, v1.X );
			if( angle < 0 )
				return angle + 2 * Math.PI;
			return angle;
		}

		/// <summary>
		/// Returns the clockwise angle between two vectors (between 0 and 2*pi)
		/// NOTE: A unique clockwise angle really only makes sense onces you've picked a plane normal direction.
		///		 So as coded, this function is really only intended to be used with 2D vector inputs.
		/// </summary>
		public static double AngleToClock( Vector3D v1, Vector3D v2 )
		{
			double result = AngleToCounterClock( v1, v2 );
			return ( 2 * Math.PI - result );
		}

		public static double DistancePointLine( Vector3D p, Vector3D lineP1, Vector3D lineP2 )
		{
			// The line vector
			Vector3D v1 = lineP2 - lineP1;
			double lineMag = v1.Abs();
			if( Tolerance.Zero( lineMag ) )
			{
				// Line definition points are the same.
				Debug.Assert( false );
				return double.NaN;
			}

			Vector3D v2 = p - lineP1;
			double distance = ( v1.Cross( v2 ) ).Abs() / lineMag;
			return distance;
		}

		public static Vector3D ProjectOntoLine( Vector3D p, Vector3D lineP1, Vector3D lineP2 )
		{
			Vector3D v1 = lineP2 - lineP1;
			double lineMag = v1.Abs();
			if( Tolerance.Zero( lineMag ) )
			{
				Debug.Assert( false );
				return new Vector3D();
			}
			v1.Normalize();

			Vector3D v2 = p - lineP1;
			double distanceAlongLine = v2.Dot( v1 );
			return lineP1 + v1 * distanceAlongLine;
		}

		public static int IntersectionLineLine( Vector3D p1, Vector3D p2,
			Vector3D p3, Vector3D p4, out Vector3D intersection )
		{
			intersection = new Vector3D();

			Vector3D n1 = p2 - p1;
			Vector3D n2 = p4 - p3;

			// Intersect?
			// XXX - Handle the case where lines are one and the same separsately? 
			//		 (infinite interesection points)
			if( Tolerance.Zero( n1.Cross( n2 ).Abs() ) )
				return 0;

			double d3 = DistancePointLine( p3, p1, p2 );
			double d4 = DistancePointLine( p4, p1, p2 );

			// Distances on the same side?
			// This tripped me up.
			double a3 = AngleToClock( p3 - p1, n1 );
			double a4 = AngleToClock( p4 - p1, n1 );
			bool sameSide = a3 > Math.PI ? a4 > Math.PI : a4 <= Math.PI;

			double factor = sameSide ?
				d3 / ( d3 - d4 ) :
				d3 / ( d3 + d4 );
			intersection = p3 + n2 * factor;

			// XXX - Unfortunately, this is happening sometimes.
			//if( !Tolerance.Zero( DistancePointLine( intersection, p1, p2 ) ) )
				//Debug.Assert( false );

			return 1;
		}

		public static int IntersectionCircleCircle( Circle c1, Circle c2, out Vector3D p1, out Vector3D p2 )
		{
			p1 = new Vector3D();
			p2 = new Vector3D();

			// A useful page describing the cases in this function is:
			// http://ozviz.wasp.uwa.edu.au/~pbourke/geometry/2circle/

			p1.Empty();
			p2.Empty();

			// Vector and distance between the centers.
			Vector3D v = c2.Center - c1.Center;
			double d = v.Abs();
			double r1 = c1.Radius;
			double r2 = c2.Radius;

			// Circle centers coincident.
			if( Tolerance.Zero( d ) )
			{
				if( Tolerance.Equal( r1, r2 ) )
					return -1;
				else
					return 0;
			}

			// We should be able to normalize at this point.
			if( !v.Normalize() )
			{
				Debug.Assert( false );
				return 0;
			}

			// No intersection points.
			// First case is disjoint circles.
			// Second case is where one circle contains the other.
			if( Tolerance.GreaterThan( d, r1 + r2 ) ||
				Tolerance.LessThan( d, Math.Abs( r1 - r2 ) ) )
				return 0;

			// One intersection point.
			if( Tolerance.Equal( d, r1 + r2 ) ||
				Tolerance.Equal( d, Math.Abs( r1 - r2 ) ) )
			{
				p1 = c1.Center + v * r1;
				return 1;
			}

			// There must be two intersection points.
			p1 = p2 = v * r1;
			double temp = ( r1 * r1 - r2 * r2 + d * d ) / ( 2 * d );
			double angle = Math.Acos( temp / r1 );
			Debug.Assert( !Tolerance.Zero( angle ) && !Tolerance.Equal( angle, Math.PI ) );
			p1.RotateXY( angle );
			p2.RotateXY( -angle );
			p1 += c1.Center;
			p2 += c1.Center;
			return 2;
		}

		public static int IntersectionLineCircle( Vector3D lineP1, Vector3D lineP2, Circle circle,
			out Vector3D p1, out Vector3D p2 )
		{
			p1 = new Vector3D();
			p2 = new Vector3D();

			// Distance from the circle center to the closest point on the line.
			double d = DistancePointLine( circle.Center, lineP1, lineP2 );

			// No intersection points.
			double r = circle.Radius;
			if( d > r )
				return 0;

			// One intersection point.
			p1 = ProjectOntoLine( circle.Center, lineP1, lineP2 );
			if( Tolerance.Equal( d, r ) )
				return 1;

			// Two intersection points.
			// Special case when the line goes through the circle center,
			// because we can see numerical issues otherwise.
			//
			// I had further issues where my default tolerance was too strict for this check.
			// The line was close to going through the center and the second block was used,
			// so I had to loosen the tolerance used by my comparison macros.
			if( Tolerance.Zero( d ) )
			{
				Vector3D line = lineP2 - lineP1;
				line.Normalize();
				line *= r;
				p1 = circle.Center + line;
				p2 = circle.Center - line;
			}
			else
			{
				// To origin.
				p1 -= circle.Center;

				p1.Normalize();
				p1 *= r;
				p2 = p1;
				double angle = Math.Acos( d / r );
				p1.RotateXY( angle );
				p2.RotateXY( -angle );

				// Back out.
				p1 += circle.Center;
				p2 += circle.Center;
			}
			return 2;
		}

		/// <summary>
		/// Reflects a point in a line defined by two points.
		/// </summary>
		public static Vector3D ReflectPointInLine( Vector3D input, Vector3D p1, Vector3D p2 )
		{
			Vector3D p = Euclidean2D.ProjectOntoLine( input, p1, p2 );
			return input + ( p - input ) * 2;
		}


		public static bool SameSideOfLine( Vector3D lineP1, Vector3D lineP2, Vector3D test1, Vector3D test2 )
		{
			Vector3D d = lineP2 - lineP1;
			Vector3D t1 = (test1 - lineP1).Cross( d );
			Vector3D t2 = (test2 - lineP1).Cross( d );
			bool pos1 = t1.Z > 0;
			bool pos2 = t2.Z > 0;
			return !(pos1 ^ pos2);
		}
	}
}