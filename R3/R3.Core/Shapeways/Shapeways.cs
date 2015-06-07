namespace R3.Geometry
{
	using System.Collections.Generic;
	using System.Linq;
	using Math = System.Math;

	/// <summary>
	/// Class with utility method for generating meshes for shapeways models.
	/// </summary>
	public class Shapeways
	{
		public Shapeways()
		{
			Mesh = new Mesh();
			Div = 24;
		}

		public Mesh Mesh { get; set; }

		// Number of divisions (meaning depends on context).
		public int Div { get; set; }

		/// <summary>
		/// Add a sphere to a mesh.
		/// </summary>
		public void AddSphere( Vector3D center, double radius )
		{
			// Implemented as a curved cylinder with capped ends.
			// Geodesic dome would be better.
			int n1 = 24; //Div;
			int n2 = n1/2;
			Div = n1;

			// Cylinder axis points.
			// Points along the cylinder are not equally spaced.
			// We want there to be equal angular distance along meridians.
			List<Vector3D> axisPoints = new List<Vector3D>();
			for( int i=0; i<=n2; i++ )
			{
				double theta = -Math.PI/2 + Math.PI/n2 * i;
				Vector3D offset = new Vector3D( 0, 0, Math.Sin( theta ) * radius );
				axisPoints.Add( center + offset );
			}

			// Cylinder thickness function.
			System.Func<Vector3D,double> sizeFunc = v =>
			{
				double h = (v - center).Z;
				return Math.Sqrt( radius*radius - h*h );
			};
			List<Vector3D[]> disks = CalcDisks( axisPoints.ToArray(), sizeFunc );

			// Add the caps and the cylinder.
			AddCap( axisPoints[0], disks[1] );
			for( int i=1; i<disks.Count - 2; i++ )
				AddSegment( disks[i], disks[i + 1] );
			AddCap( axisPoints[axisPoints.Count - 1], disks[axisPoints.Count - 2], reverse: true );
		}

		/// <summary>
		/// Add a curve with a constant radius size.
		/// </summary>
		public void AddCurve( Vector3D[] points, double size )
		{
			System.Func<Vector3D, double> sizeFunc = vector => size;
			AddCurve( points, sizeFunc, points.First(), points.Last() );
		}

		/// <summary>
		/// Adds a curve to a mesh.
		/// </summary>
		public void AddCurve( Vector3D[] points, System.Func<Vector3D, double> sizeFunc )
		{
			if( points.Length < 2 )
				throw new System.ArgumentException( "AddCurve requires at least two input points." );

			List<Vector3D[]> disks = CalcDisks( points, sizeFunc );
			AddCurve( disks, points, points.First(), points.Last() );
		}

		public void AddCurve( Vector3D[] points, System.Func<Vector3D, double> sizeFunc, Vector3D start, Vector3D end )
		{
			if( points.Length < 2 )
				throw new System.ArgumentException( "AddCurve requires at least two input points." );

			List<Vector3D[]> disks = CalcDisks( points, sizeFunc );
			AddCurve( disks, points, start, end );
		}

		private void AddCurve( List<Vector3D[]> disks, Vector3D[] points, Vector3D start, Vector3D end )
		{
			// Cap1
			AddCap( start, disks.First() );

			// Interior points.
			for( int i = 0; i < disks.Count - 1; i++ )
				AddSegment( disks[i], disks[i + 1] );

			// Cap2
			AddCap( end, disks.Last(), reverse: true );
		}

		public void AddArc( Vector3D center, double arcRadius, Vector3D v1, Vector3D normal, double angleTot, int numPoints, System.Func<Vector3D, double> sizeFunc )
		{
			if( numPoints < 2 )
				numPoints = 2;

			Vector3D[] points = CalcArcPoints( center, arcRadius, v1, normal, angleTot, numPoints );

			// Calculate all the disks.
			// XXX - duplicated code with CalcDisks, but we need to calc our own perpendicular here.
			List<Vector3D[]> disks = new List<Vector3D[]>();
			for( int i=0; i<points.Length; i++ )
			{
				Vector3D p1 = i == 0 ? points[0] : points[i - 1];
				Vector3D p2 = points[i];
				Vector3D p3 = i == points.Length - 1 ? points[points.Length - 1] : points[i + 1];
				double radius = sizeFunc( points[i] );

				// We can calculate these directly.
				Vector3D perp = p2 - center;
				perp.Normalize();
				Vector3D axis = perp;
				axis.RotateAboutAxis( normal, -Math.PI / 2 );
				perp *= radius;

				disks.Add( Disk( p2, axis, perp, this.Div, reverse: false ) );
			}

///////////////////////////////////////// Hack to avoid having to put spheres at verts.
			double thickness = 0.0055;	// relates to SizeFuncConst
			double thetaOffset = thickness / arcRadius;
			Vector3D start = points[0], end = points[points.Length-1];
			start -= center;
			end -= center;
			start.RotateAboutAxis( normal, -thetaOffset );
			end.RotateAboutAxis( normal, thetaOffset );
			start += center;
			end += center;
			points[0] = start;
			points[points.Length - 1] = end;
/////////////////////////////////////////

			AddCurve( disks, points, points.First(), points.Last() );
		}

		/// <summary>
		/// Helper to calculate points along an arc.
		/// </summary>
		public static Vector3D[] CalcArcPoints( Vector3D center, double radius, Vector3D v1, Vector3D normal, double angleTot )
		{
			// Try to optimize the number of segments.
			int numPoints = 8;
			//int numPoints = (int)(Math.Sqrt(radius) * divisions);
			numPoints = Math.Max( 3, numPoints );

			return CalcArcPoints( center, radius, v1, normal, angleTot, numPoints );
		}

		/// <summary>
		/// Helper to calculate points along an arc.
		/// XXX - This function belongs in a better shared location.
		/// </summary>
		public static Vector3D[] CalcArcPoints( Vector3D center, double radius, Vector3D v1, Vector3D normal, double angleTot, int numPoints )
		{
			List<Vector3D> points = new List<Vector3D>();
			double angle = 0;
			double angleInc = angleTot / numPoints;
			for( int i = 0; i <= numPoints; i++ )
			{
				Vector3D p = v1 - center;

/////////////////////////// This is to avoid duplicate points in output.
				bool avoidDuplicatePoints = false;
				double angleToRotate = angle;
				if( avoidDuplicatePoints )
				{
					// We only do this for endpoints, since that is where multiple arcs meet.
					double thickness = 0.001;	// relates to SizeFuncConst
					double thetaOffset = thickness / radius;	// theta = s / r

					if( i == 0 )
					{
						//System.Random rand = new System.Random();
						//final *= (1 + 0.001 * radius + 0.001 * rand.NextDouble());
						angleToRotate -= thetaOffset;
					}
					else if( i == numPoints )
						angleToRotate += thetaOffset;
				}
///////////////////////////

				p.RotateAboutAxis( normal, angleToRotate );
				Vector3D final = p + center;

				angle += angleInc;

				//if( i < 2 || i > numPoints - 2 )
				//	continue;
				points.Add( final );
			}

			return points.ToArray();
		}

		/// <summary>
		/// Add a cornucopia.
		/// NOTE: outerPoints and innerPoints are ordered in a different direction.
		/// </summary>
		public void AddCornucopia( 			
			Vector3D[] outerPoints, System.Func<Vector3D, double> outerSizeFunc,
			Vector3D[] innerPoints, System.Func<Vector3D, double> innerSizeFunc )
		{
			if( outerPoints.Length < 2 )
				throw new System.ArgumentException( "AddCornucopia requires at least two input points." );

			// Calculate all the disks.
			List<Vector3D[]> disksOuter = CalcDisks( outerPoints, outerSizeFunc );
			List<Vector3D[]> disksInner = CalcDisks( innerPoints, innerSizeFunc, reverse: true );

			// Cap1
			AddCap( outerPoints[0], disksOuter[0] );

			// Interior points.
			for( int i = 0; i < disksOuter.Count - 1; i++ )
				AddSegment( disksOuter[i], disksOuter[i + 1] );

			// Ring
			AddRing( disksOuter[outerPoints.Length - 1], disksInner[0] );

			// Interior points.
			for( int i = 0; i < disksInner.Count - 1; i++ )
				AddSegment( disksInner[i], disksInner[i + 1] );

			// Interior cap.
			AddCap( innerPoints[innerPoints.Length - 1], disksInner[innerPoints.Length - 1], reverse: true );
		}

		/// <summary>
		/// Helper to calculate a set of disks perpendicular to a polyline.
		/// </summary>
		private List<Vector3D[]> CalcDisks( Vector3D[] points, System.Func<Vector3D, double> sizeFunc, bool reverse = false )
		{
			List<Vector3D[]> disks = new List<Vector3D[]>();
			for( int i=0; i<points.Length; i++ )
			{
				Vector3D p1 = i == 0 ? points[0] : points[i - 1];
				Vector3D p2 = points[i];
				Vector3D p3 = i == points.Length - 1 ? points[points.Length - 1] : points[i + 1];
				double radius = sizeFunc( points[i] );

				// Experimenting closing up gaps without having to put spheres at verts.
				//if( i == 0 )
				//	p3 = Euclidean3D.ProjectOntoPlane( p2, p2, p3 );
				//else if( i == points.Length - 1 )
				//	p1 = Euclidean3D.ProjectOntoPlane( p2, p2, p1 );

				Vector3D axis = CurveAxis( p1, p2, p3 );
				Vector3D perpendicular = CurvePerp( p1, p2, p3 );
				perpendicular *= radius;
				disks.Add( Disk( p2, axis, perpendicular, this.Div, reverse ) );
			}
			return disks;
		}

		/// <summary>
		/// Function to add a cap to the end of our curve, so that it will be 'manifold'.
		/// </summary>
		private void AddCap( Vector3D center, Vector3D[] diskPoints, bool reverse = false )
		{
			for( int i=0; i<diskPoints.Length; i++ )
			{
				int idx1 = i;
				int idx2 = i == diskPoints.Length - 1 ? 0 : i + 1;

				if( reverse )
					Mesh.Triangles.Add( new Mesh.Triangle( center, diskPoints[idx2], diskPoints[idx1] ) );
				else
					Mesh.Triangles.Add( new Mesh.Triangle( center, diskPoints[idx1], diskPoints[idx2] ) );
			}

			/* // No center
			for( int i=1; i<diskPoints.Length-1; i++ )
			{
				Vector3D a = diskPoints[0];
				Vector3D b = diskPoints[i];
				Vector3D c = diskPoints[i + 1];
				if( reverse )
					Mesh.Triangles.Add( new Mesh.Triangle( c, b, a ) );
				else
					Mesh.Triangles.Add( new Mesh.Triangle( a, b, c ) );
			} */
		}

		/// <summary>
		/// Adds an annulus.
		/// </summary>
		private void AddRing( Vector3D[] disk1Points, Vector3D[] disk2Points )
		{
			AddSegment( disk1Points, disk2Points );
		}

		/// <summary>
		/// Function to add one segment of our curve.
		/// d1 and d2 are two pre-calc'd disks of points perpendicular to the curve.
		/// </summary>
		public void AddSegment( Vector3D[] d1, Vector3D[] d2 )
		{
			if( d1.Length != d2.Length )
				throw new System.ArgumentException( "Disks must have the same length." );

			for( int i=0; i<d1.Length; i++ )
			{
				int idx1 = i;
				int idx2 = i == d1.Length - 1 ? 0 : i + 1;
				Mesh.Triangles.Add( new Mesh.Triangle( d1[idx1], d2[idx1], d1[idx2] ) );
				Mesh.Triangles.Add( new Mesh.Triangle( d1[idx2], d2[idx1], d2[idx2] ) );
			}
		}

		/// <summary>
		/// Used to get the axis of a polyline at a point p2, given adjacent points.
		/// </summary>
		private static Vector3D CurveAxis( Vector3D p1, Vector3D p2, Vector3D p3 )
		{
			Vector3D axis = p1 - p3;	// Important for orientation of meshes.
			if( !axis.Normalize() )
				throw new System.Exception( "Calculating axis requires distinct points." );
			return axis;
		}

		/// <summary>
		/// Used to get the perpendicular to the polyline at a point p2, given adjacent points.
		/// </summary>
		private static Vector3D CurvePerp( Vector3D p1, Vector3D p2, Vector3D p3 )
		{
			// Just use p1 and p3 for now, close to the 3 point rule.
			// http://math.depaul.edu/mash/optnum.pdf

			Vector3D perpendicular = p1.Cross( p3 );
			//Vector3D perpendicular = axis.Cross( new Vector3D( 0, 0, 1 ) );
			if( !perpendicular.Normalize() )
			{
				// This can happen if p1 and p3 are collinear with origin.
				Vector3D axis = p1 - p3;
				perpendicular = axis.Cross( new Vector3D( 0, 1, 0 ) );
				perpendicular.Normalize();
			}

			return perpendicular;
		}

		/// <summary>
		/// Create a circle of points, centered at p.
		/// </summary>
		private static Vector3D[] Disk( Vector3D p, Vector3D axis, Vector3D perpendicular, int divisions, bool reverse = false )
		{
			List<Vector3D> points = new List<Vector3D>();
			double angleInc = 2 * Math.PI / divisions;
			if( reverse )
			{
				axis *= -1;
				perpendicular *= -1;
			}
			double angle = angleInc / 2;  // angleInc / 2 or 0
			for( int i=0; i<divisions; i++ )
			{
				Vector3D point = perpendicular;
				point.RotateAboutAxis( axis, angle );
				points.Add( p + point );
				angle += angleInc;
			}

			return points.ToArray();
		}
	}
}
