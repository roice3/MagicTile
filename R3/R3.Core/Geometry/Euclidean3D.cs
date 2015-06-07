namespace R3.Geometry
{
	using R3.Core;
	using Math = System.Math;

	public static class Euclidean3D
	{
		public static double DistancePointLine( Vector3D n1, Vector3D p1, Vector3D point )
		{
			// Check to make sure that n1 is not degenerate.
			if( Tolerance.Zero( n1.MagSquared() ) )
				return double.NaN;

			// ZZZ - Can we make this a signed distance?
			return ( ( point - p1 ).Cross( n1 ) ).Abs() / n1.Abs();
		}

		public static double DistancePointPlane( Vector3D normalVector, Vector3D planePoint, Vector3D point )
		{
			// Check to make sure that plane is not degenerate.
			if( Tolerance.Zero( normalVector.MagSquared() ) )
				return double.NaN;

			// Here is the distance (signed depending on which side of the plane we are on).
			return ( point - planePoint ).Dot( normalVector ) / normalVector.Abs();
		}

		public static Vector3D ProjectOntoPlane( Vector3D normalVector, Vector3D planePoint, Vector3D point )
		{
			if( !normalVector.Normalize() )
				throw new System.ArgumentException( "Invalid normal vector." );

			double dist = DistancePointPlane( normalVector, planePoint, point );
			normalVector *= dist;
			return point - normalVector;
		}

		public static double DistanceLineLine( Vector3D n1, Vector3D p1, Vector3D n2, Vector3D p2 )
		{
			// Check to make sure that neither of the normal vectors are degenerate.
			if( Tolerance.Zero( n1.MagSquared() ) || Tolerance.Zero( n2.MagSquared() ) )
				return double.NaN;

			Vector3D plane = n1.Cross( n2 );

			//	Case where the lines are parallel (magnitude of the cross product will be 0).
			if( Tolerance.Zero( plane.MagSquared() ) )
				return DistancePointLine( n1, p1, p2 );
	
			return DistancePointPlane( plane, p1, p2 );
		}

		/// <summary>
		/// Checks if a point is anywhere on a segment.
		/// </summary>
		public static bool PointOnSegment( Vector3D s1, Vector3D s2, Vector3D point )
		{
			// Look for a degenerate triangle.
			double d1 = ( point - s1 ).MagSquared();
			double d2 = ( s2 - point ).MagSquared();
			double d3 = ( s2 - s1 ).MagSquared();
			return Tolerance.Equal( d1 + d2, d3 );
		}

		/// <summary>
		/// Checks to see if two segments intersect.
		/// This does not actually calculate the intersection point.
		/// It uses information from the following paper:
		/// http://www.geometrictools.com/Documentation/DistanceLine3Line3.pdf
		/// </summary>
		public static bool DoSegmentsIntersect( Vector3D a1, Vector3D a2, Vector3D b1, Vector3D b2 )
		{
			/* Our approach.
			(1) Check if endpoints are on other segment.  Note that this also checks if A endpoints equal B endpoints.
			(2) Calc line-line distance.  If > 0, we don't intersect.
			(3) At this point, we only have to deal with non-parallel case in the paper, and only 
				need to determine if we are in "region 0" (we intersect) or not (we don't)
			 */

			// (1)
			if( PointOnSegment( a1, a2, b1 ) ||
				PointOnSegment( a1, a2, b2 ) ||
				PointOnSegment( b1, b2, a1 ) ||
				PointOnSegment( b1, b2, a2 ) )
				return true;

			// (2)
			Vector3D ma = a2 - a1;
			Vector3D mb = b2 - b1;
			if( Tolerance.GreaterThan( DistanceLineLine( ma, a1, mb, b1 ), 0 ) )
				return false;

			// (3)
			Vector3D D = a1 - b1;
			double a = ma.Dot( ma );
			double b = -ma.Dot( mb );
			double c = mb.Dot( mb );
			double d = ma.Dot( D );
			double e = -mb.Dot( D );

			double det = a * c - b * b; 
			double s = b * e - c * d; 
			double t = b * d - a * e;
			if( s >= 0 && s <= det &&
				t >= 0 && t <= det )
				return true;

			return false;
		}

		/// <summary>
		/// Calculate a plane normal after a transformation function is applied
		/// to the points.
		/// </summary>
		public static Vector3D NormalFrom3Points( Vector3D p1, Vector3D p2, Vector3D p3, 
			System.Func<Vector3D, Vector3D> transform )
		{
			Vector3D p1t = transform( p1 );
			Vector3D p2t = transform( p2 );
			Vector3D p3t = transform( p3 );
			return NormalFrom3Points( p1t, p2t, p3t );
		}

		public static Vector3D NormalFrom3Points( Vector3D p1, Vector3D p2, Vector3D p3 )
		{
			Vector3D v1 = p1 - p3;
			Vector3D v2 = p2 - p3;
			Vector3D normal = v1.Cross( v2 );
			normal.Normalize();
			return normal;
		}

		public static double TriangleAreaAfterTransform( ref Vector3D p1, ref Vector3D p2, ref Vector3D p3,
			System.Func<Vector3D, Vector3D> transform )
		{
			p1 = transform( p1 );
			p2 = transform( p2 );
			p3 = transform( p3 );

			Vector3D v1 = p1 - p3;
			Vector3D v2 = p2 - p3;
			return 0.5 * v1.Cross( v2 ).Abs();
		}

		public static double MaxTriangleEdgeLengthAfterTransform( ref Vector3D p1, ref Vector3D p2, ref Vector3D p3,
			System.Func<Vector3D, Vector3D> transform )
		{
			p1 = transform( p1 );
			p2 = transform( p2 );
			p3 = transform( p3 );

			double l1Squared = (p2 - p1).MagSquared();
			double l2Squared = (p3 - p2).MagSquared();
			double l3Squared = (p1 - p3).MagSquared();
			return Math.Sqrt( 
				Math.Max( l1Squared, Math.Max( l2Squared, l3Squared ) ) 
			);
		}

		public static bool Coplanar( Vector3D[] points )
		{
			throw new System.NotImplementedException();
		}
	}
}
