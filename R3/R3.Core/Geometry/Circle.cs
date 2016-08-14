namespace R3.Geometry
{
	using Math = System.Math;
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.Serialization;

	/// <summary>
	/// Class for generalized circles (lines are a limiting case).
	/// </summary>
	[DataContract( Namespace = "" )]
	public class Circle : ITransformable
	{
		public Circle()
		{
			Reset();
		}

		private void Reset()
		{
			Center = P1 = P2 = new Vector3D();
			Radius = 1;
		}

		/// <summary>
		/// Constructs a circle from 3 points.
		/// </summary>
		public Circle( Vector3D p1, Vector3D p2, Vector3D p3 )
		{
			From3Points( p1, p2, p3 );
		}

		/// <summary>
		/// Constructs a circle with infinite radius going through 2 points.
		/// </summary>
		public Circle( Vector3D p1, Vector3D p2 )
		{
			From2Points( p1, p2 );
		}

		[DataMember]
		public Vector3D Center { get; set; }
		[DataMember]
		public double Radius { get; set; }

		/// <summary>
		/// Line variables.
		/// </summary>
		[DataMember]
		public Vector3D P1 { get; set; }
		[DataMember]
		public Vector3D P2 { get; set; }

		/// <summary>
		/// Whether we are a line.
		/// </summary>
		public bool IsLine
		{
			get { return double.IsInfinity( Radius ); }
		}

		public Circle Clone()
		{
			return (Circle)MemberwiseClone();
		}

		/// <summary>
		/// Construct a circle from 3 points
		/// </summary>
		/// <returns>false if the construction failed (if we are a line).</returns>
		public bool From3Points( Vector3D p1, Vector3D p2, Vector3D p3 )
		{
			Reset();

			// Check for any infinite points, in which case we are a line.
			// I'm not sure these checks are smart, since our IsInfinite check is so inclusive,
			// but Big Chop puzzle doesn't work if we don't do this.
			// ZZZ - Still, I need to think on this more.
			if( Infinity.IsInfinite( p1 ) )
			{
				this.From2Points( p2, p3 );
				return false;
			}
			else if( Infinity.IsInfinite( p2 ) )
			{
				this.From2Points( p1, p3 );
				return false;
			}
			else if( Infinity.IsInfinite( p3 ) )
			{
				this.From2Points( p1, p2 );
				return false;
			}

			/* Some links
			http://mathforum.org/library/drmath/view/54323.html
			http://delphiforfun.org/Programs/Math_Topics/circle_from_3_points.htm
			There is lots of info out there about solving via equations,
			but as with other code in this project, I wanted to use geometrical constructions. */

			// Midpoints.
			Vector3D m1 = ( p1 + p2 ) / 2;
			Vector3D m2 = ( p1 + p3 ) / 2;

			// Perpendicular bisectors.
			Vector3D b1 = ( p2 - p1 ) / 2;
			Vector3D b2 = ( p3 - p1 ) / 2;
			b1.Normalize();
			b2.Normalize();
			b1.Rotate90();
			b2.Rotate90();

			Vector3D newCenter;
			int found = Euclidean2D.IntersectionLineLine( m1, m1 + b1, m2, m2 + b2, out newCenter );
			Center = newCenter;
			if( 0 == found )
			{
				// The points are collinear, so we are a line.
				From2Points( p1, p2 );
				return false;
			}

			Radius = ( p1 - Center ).Abs();
			Debug.Assert( Tolerance.Equal( Radius, ( p2 - Center ).Abs() ) );
			Debug.Assert( Tolerance.Equal( Radius, ( p3 - Center ).Abs() ) );
			return true;
		}

		/// <summary>
		/// Creates a circle with infinite radius going through 2 points.
		/// </summary>
		public void From2Points( Vector3D p1, Vector3D p2 )
		{
			P1 = p1;
			P2 = p2;

			// We do this normalization so that line comparisons will work.
			NormalizeLine();

			Radius = double.PositiveInfinity;
			Center.Empty();
		}

		/// <summary>
		/// Normalize so P1 is closest point to origin,
		/// and direction vector is of unit length.
		/// </summary>
		public void NormalizeLine()
		{
			if( !this.IsLine )
				return;

			Vector3D d = P2 - P1;
			d.Normalize();

			P1 = Euclidean2D.ProjectOntoLine( new Vector3D(), P1, P2 );

			// ZZZ - Could probably do something more robust to choose proper direction.
			if( Tolerance.GreaterThanOrEqual( Euclidean2D.AngleToClock( d, new Vector3D( 1, 0 ) ), Math.PI ) )
				d *= -1;

			P2 = P1 + d;
		}

		// Strictly less than.
		public bool IsPointInside( Vector3D test )
		{
			return Tolerance.LessThan( ( test - Center ).Abs(), Radius );
		}

		public bool IsPointOn( Vector3D test )
		{
			return Tolerance.Equal( ( test - Center ).Abs(), Radius );
		}

		/// <summary>
		/// Reflect ourselves about another circle.
		/// </summary>
		public virtual void Reflect( Circle c )
		{
			ReflectInternal( c );
		}

		private void ReflectInternal( Circle c )
		{
			// Reflecting to a line?
			if( IsPointOn( c.Center ) )
			{
				// Grab 2 points to reflect to P1/P2.
				// We'll use the 2 points that are 120 degrees from c.Center.
				Vector3D v = c.Center - this.Center;
				v.RotateXY( 2 * Math.PI / 3 );
				P1 = c.ReflectPoint( this.Center + v );
				v.RotateXY( 2 * Math.PI / 3 );
				P2 = c.ReflectPoint( this.Center + v );

				Radius = double.PositiveInfinity;
				Center.Empty();
			}
			else
			{
				// NOTE: We can't just reflect the center.
				//		 See http://mathworld.wolfram.com/Inversion.html
				double a = Radius;
				double k = c.Radius;
				Vector3D v = Center - c.Center;
				double s = k*k / ( v.MagSquared() - a*a );
				Center = c.Center + v * s;
				Radius = Math.Abs( s ) * a;
				P1.Empty();
				P2.Empty();
			}
		}

		/// <summary>
		/// Reflect ourselves about a segment.
		/// </summary>
		public virtual void Reflect( Segment s )
		{
			if( SegmentType.Arc == s.Type )
			{
				ReflectInternal( s.Circle );
			}
			else
			{
				// We just need to reflect the center.
				Center = s.ReflectPoint( Center );
			}
		}

		/// <summary>
		/// Reflect a point in us.
		/// ZZZ - This method is confusing in that it is opposite the above (we aren't reflecting ourselves here).
		/// </summary>
		/// <param name="p"></param>
		public Vector3D ReflectPoint( Vector3D p )
		{
			if( this.IsLine )
			{
				return Euclidean2D.ReflectPointInLine( p, P1, P2 );
			}
			else
			{
				// Handle infinities.
				Vector3D infinityVector = Infinity.InfinityVector;
				if( p.Compare( Center ) )
					return infinityVector;
				if( p == infinityVector )
					return Center;

				Vector3D v = p - Center;
				double d = v.Abs();
				v.Normalize();
				return Center + v * ( Radius * Radius / d );
			}
		}

		public virtual void Transform( Mobius m )
		{
			TransformInternal( m );
		}

		public virtual void Transform( Isometry i )
		{
			TransformInternal( i );
		}

		/// <summary>
		/// Apply a transform to us.
		/// </summary>
		private void TransformInternal<T>( T transform ) where T : ITransform
		{
			// Get 3 points on the circle.
			Vector3D p1, p2, p3;
			if( this.IsLine )
			{
				p1 = P1;
				p2 = ( P1 + P2 ) / 2;
				p3 = P2;
			}
			else
			{
				p1 = Center + new Vector3D( Radius, 0, 0 );
				p2 = Center + new Vector3D( -Radius, 0, 0 );
				p3 = Center + new Vector3D( 0, Radius, 0 );
			}

			p1 = transform.Apply( p1 );
			p2 = transform.Apply( p2 );
			p3 = transform.Apply( p3 );
			
			this.From3Points( p1, p2, p3 );
		}

		// Get the intersection points with a segment.
		// Returns null if the segment is an arc coincident with the circle (infinite number of intersection points).
		public Vector3D[] GetIntersectionPoints( Segment segment )
		{
			Vector3D p1, p2;
			int result;

			// Are we a line?
			if( this.IsLine )
			{
				if( SegmentType.Arc == segment.Type )
				{
					Circle tempCircle = segment.Circle;
					result = Euclidean2D.IntersectionLineCircle( this.P1, this.P2, tempCircle, out p1, out p2 );
				}
				else
				{
					result = Euclidean2D.IntersectionLineLine( this.P1, this.P2, segment.P1, segment.P2, out p1 );
					p2 = Vector3D.DneVector();
				}
			}
			else
			{
				if( SegmentType.Arc == segment.Type )
				{
					Circle tempCircle = segment.Circle;
					result = Euclidean2D.IntersectionCircleCircle( tempCircle, this, out p1, out p2 );
				}
				else
					result = Euclidean2D.IntersectionLineCircle( segment.P1, segment.P2, this, out p1, out p2 );
			}

			if( -1 == result )
				return null;

			List<Vector3D> ret = new List<Vector3D>();
			if( result >= 1 && segment.IsPointOn( p1 ) )
				ret.Add( p1 );
			if( result >= 2 && segment.IsPointOn( p2 ) )
				ret.Add( p2 );

			return ret.ToArray();
		}

		public bool Intersects( Polygon poly )
		{
			foreach( Segment seg in poly.Segments )
			{
				Vector3D[] iPoints = GetIntersectionPoints( seg );
				if( iPoints != null && iPoints.Length > 0 )
					return true;
			}

			return false;
		}

		public bool HasVertexInside( Polygon poly )
		{
			foreach( Segment seg in poly.Segments )
			{
				if( IsPointInside( seg.P1 ) )
					return true;
			}

			return false;
		}
	}

	/// <summary>
	/// A class to represent projected circles from non-Euclidean geometries.
	/// This also stores the location of the true circle center,
	/// which does not in general coincide with the Euclidean circle center.
	/// </summary>
	public class CircleNE : Circle, ITransformable
	{
		public CircleNE() { }

		public CircleNE( Circle c, Vector3D centerNE )
		{
			Center = c.Center;
			Radius = c.Radius;
			P1 = c.P1;
			P2 = c.P2;
			CenterNE = centerNE;
		}

		public Vector3D CenterNE { get; set; }

		public new CircleNE Clone()
		{
			return (CircleNE)MemberwiseClone();
		}

		public override void Reflect( Circle c )
		{
			base.Reflect( c );
			CenterNE = c.ReflectPoint( CenterNE );
		}

		public override void Reflect( Segment s )
		{
			base.Reflect( s );
			CenterNE = s.ReflectPoint( CenterNE );
		}

		public override void Transform( Mobius m )
		{
			base.Transform( m );
			CenterNE = m.Apply( CenterNE );
		}

		public override void Transform( Isometry i )
		{
			base.Transform( i );
			CenterNE = i.Apply( CenterNE );
		}

		public bool Inverted
		{
			get
			{
				// If our NE center is infinite, or is on the outside.
				return
					Infinity.IsInfinite( this.CenterNE ) ||
					!IsPointInside( this.CenterNE );
			}
		}

		/// <summary>
		/// Checks to see if a point is inside us, in a non-Euclidean sense.
		/// This works if we are inverted, and even if we are a line!
		/// (if we are a line, half of the plane is still "inside").
		/// </summary>
		public bool IsPointInsideNE( Vector3D testPoint )
		{
			if( this.IsLine )
			{
				// We are inside if the test point is on the same side
				// as the non-Euclidean center.
				return Euclidean2D.SameSideOfLine( P1, P2, testPoint, CenterNE );
			}
			else
			{
				// Whether we are inside in the Euclidean sense.
				bool pointInside = false;
				if( !Infinity.IsInfinite( testPoint ) )
					pointInside = this.IsPointInside( testPoint );

				// And in the Non-Euclidean sense.
				bool inverted = this.Inverted;
				return (!inverted && pointInside) ||
					   (inverted && !pointInside);
			}
		}
		
		/// <summary>
		/// This is an optimized version for puzzle building when not in spherical geometry,
		/// in which case we know our circles will not be inverted.
		/// Profiling showed the general code in IsPointInsideNE to be very slow.
		/// </summary>
		public bool IsPointInsideFast( Vector3D testPoint )
		{
			return this.IsPointInside( testPoint );
		}
	}

	public class CircleNE_EqualityComparer : IEqualityComparer<CircleNE>
	{
		// ZZZ - I wonder if we want to do normalization of lines before comparing.

		public bool Equals( CircleNE c1, CircleNE c2 )
		{
			bool radiusEqual =
				Tolerance.Equal( c1.Radius, c2.Radius ) ||
				(Infinity.IsInfinite( c1.Radius ) && Infinity.IsInfinite( c2.Radius ));

			if( c1.IsLine )
				return c1.P1 == c2.P1 &&
					   c1.P2 == c2.P2 &&
					   radiusEqual;
			else
				return
					c1.Center == c2.Center &&
					c1.CenterNE == c2.CenterNE &&
					radiusEqual;
		}

		public int GetHashCode( CircleNE c )
		{
			if( c.IsLine )
				return
					c.P1.GetHashCode() ^
					c.P2.GetHashCode();
			else
			{
				double inverse = 1 / Tolerance.Threshold;
				int decimals = (int)Math.Log10( inverse );

				return
					c.Center.GetHashCode() ^
					c.CenterNE.GetHashCode() ^
					Math.Round( c.Radius, decimals ).GetHashCode();
			}
		}
	}
}
