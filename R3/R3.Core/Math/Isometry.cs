namespace R3.Math
{
	using Math = System.Math;

	using R3.Geometry;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Numerics;
	using System.Runtime.Serialization;

	/// <summary>
	/// Class to represent an isometry.
	/// This is really just a wrapper around a Mobius transformation, but also includes a reflection in a generalized circle.
	/// (Reflections can't be defined with a Mobius transformation.)
	/// NOTE: The order in which the two elements are applied is important.  We will apply the Mobius part of the isometry first.
	/// </summary>
	[DataContract( Namespace = "" )]
	public class Isometry : ITransform
	{
		public Isometry()
		{
			m_mobius.Unity();
		}

		public Isometry( Mobius m, Circle r )
		{
			Mobius = m;
			Reflection = r;
		}

		public Isometry( Isometry i )
		{
			Mobius = i.Mobius;
			if( i.Reflection != null )
				Reflection = i.Reflection.Clone();
		}

		public Isometry Clone()
		{
			return new Isometry( this );
		}

		/// <summary>
		/// Mobius Transform for this isometry.
		/// </summary>
		[DataMember]
		public Mobius Mobius 
		{ 
			get { return m_mobius; } 
			set { m_mobius = value; } 
		}
		private Mobius m_mobius;

		/// <summary>
		/// Defines the circle (or line) in which to reflect for this isometry.
		/// Null if we don't want to include a reflection.
		/// </summary>
		[DataMember]
		public Circle Reflection
		{
			get { return m_reflection; }
			set 
			{ 
				m_reflection = value;
				CacheCircleInversion( m_reflection );
			}
		}

		/// <summary>
		/// Whether or not we are reflected.
		/// </summary>
		public bool Reflected
		{
			get
			{
				return m_reflection != null;
			}
		}

		// NOTE: Applying isometries with reflections was really slow, so we cache the Mobius transforms we need to more quickly do it.
		private Circle m_reflection;
		private Mobius m_cache1;
		private Mobius m_cache2;

		/// <summary>
		/// Composition operator.
		/// </summary>>
		public static Isometry operator *( Isometry i1, Isometry i2 )
		{
			// ZZZ - Probably a better way.
			// We'll just apply both isometries to a canonical set of points,
			// Then calc which isometry makes that.

			Complex p1 = new Complex( 1, 0 );
			Complex p2 = new Complex( -1, 0 );
			Complex p3 = new Complex( 0, 1 );
			Complex w1 = p1, w2 = p2, w3 = p3;

			// Compose (apply in reverse order).
			w1 = i2.Apply( w1 );
			w2 = i2.Apply( w2 );
			w3 = i2.Apply( w3 );
			w1 = i1.Apply( w1 );
			w2 = i1.Apply( w2 );
			w3 = i1.Apply( w3 );

			Mobius m = new Mobius();
			m.MapPoints( p1, p2, p3, w1, w2, w3 );
			
			Isometry result = new Isometry();
			result.Mobius = m;

			// Need to reflect at end?
			bool r1 = i1.Reflection != null;
			bool r2 = i2.Reflection != null;
			if( r1 ^ r2 )	// One and only one reflection.
			{
				result.Reflection = new Circle(
					Vector3D.FromComplex( w1 ), 
					Vector3D.FromComplex( w2 ), 
					Vector3D.FromComplex( w3 ) );
			}

			return result;
		}

		/// <summary>
		/// Applies an isometry to a vector.
		/// </summary>
		/// <remarks>Use the complex number version if you can.</remarks>
		public Vector3D Apply( Vector3D z )
		{
			Complex cInput = z;
			Complex cOutput = Apply( cInput );
			return Vector3D.FromComplex( cOutput );
		}

		/// <summary>
		/// Applies an isometry to a complex number.
		/// </summary>
		public Complex Apply( Complex z )
		{
			z = Mobius.Apply( z );
			if( Reflection != null )
				z = ApplyCachedCircleInversion( z );
			return z;
		}

		public Vector3D ApplyInfiniteSafe( Vector3D z )
		{
			return Vector3D.FromComplex( ApplyInfiniteSafe( z.ToComplex() ) );
		}

		public Complex ApplyInfiniteSafe( Complex z )
		{
			z = Mobius.ApplyInfiniteSafe( z );
			if( Reflection != null )
				z = ApplyCachedCircleInversion( z );
			if( Infinity.IsInfinite( z ) )
				z = Infinity.InfinityVector2D.ToComplex();
			return z;
		}

		/// <summary>
		/// Does a circle inversion on an arbitrary circle.
		/// </summary>
		private void CacheCircleInversion( Circle inversionCircle )
		{
			if( inversionCircle == null )
				return;

			Complex p1, p2, p3;
			if( inversionCircle.IsLine )
			{
				p1 = inversionCircle.P1;
				p2 = inversionCircle.P2;
				p3 = (p1 + p2) / 2;
			}
			else
			{
				p1 = (inversionCircle.Center + new Vector3D( inversionCircle.Radius, 0 ));
				p2 = (inversionCircle.Center + new Vector3D( -inversionCircle.Radius, 0 ));
				p3 = (inversionCircle.Center + new Vector3D( 0, inversionCircle.Radius ));
			}

			CacheCircleInversion( p1, p2, p3 );
		}

		/// <summary>
		/// Does a circle inversion in an arbitrary, generalized circle.
		/// IOW, the three points may be collinear, in which case we are talking about a reflection.
		/// </summary>
		private void CacheCircleInversion( Complex c1, Complex c2, Complex c3 )
		{
			Mobius toUnitCircle = new Mobius();
			toUnitCircle.MapPoints(
				c1, c2, c3,
				new Complex( 1, 0 ),
				new Complex( -1, 0 ),
				new Complex( 0, 1 ) );

			m_cache1 = toUnitCircle;
			m_cache2 = m_cache1.Inverse();
		}

		private Complex ApplyCachedCircleInversion( Complex input )
		{
			Complex result = m_cache1.Apply( input );
			result = CircleInversion( result );
			result = m_cache2.Apply( result );
			return result;
		}

		private static bool IsNaN( Complex c )
		{
			return
				double.IsNaN( c.Real ) ||
				double.IsNaN( c.Imaginary );
		}

		/// <summary>
		/// This will reflect a point in an origin centered circle.
		/// </summary>
		private Complex CircleInversion( Complex input )
		{
			if( IsNaN( input ) )
				return Complex.Zero;

			return Complex.One / Complex.Conjugate( input );
		}

		/// <summary>
		/// Does a Euclidean reflection across a line.
		/// </summary>
		/*public void ReflectAcrossLine( Vector3D p1, Vector3D p2 )
		{
			// Do a circle inversion using a generalized circle (third point is at infinity).
			//Complex p3 = Complex.ImaginaryOne * Math.Pow( 10, 10 );
			//Vector3D p3 = p1 + (p2 - p1) * Math.Pow( 10, 10 );
			Vector3D p3 = (p1 + p2) / 2;
			p3 *= 1000;
			//CircleInversion( p1, p2, p3 );
			//CircleInversion( new Complex( 1, 0 ), new Complex( -1, 0 ), new Complex( 0, 1 ) ); 
			Mobius m1 = new Mobius();
			m1.MapPoints( p1, p2, p3,
				new Complex( 1, 0 ), new Complex( -1, 0 ), new Complex( 0, 1 ) );

			this = m1;
		}*/

		/// <summary>
		/// Returns a new Isometry that is the inverse of us.
		/// </summary>
		public Isometry Inverse()
		{
			Mobius inverse = this.Mobius.Inverse();
			if( Reflection == null )
			{
				return new Isometry( inverse, null );
			}
			else
			{
				Circle reflection = Reflection.Clone();
				reflection.Transform( inverse );
				return new Isometry( inverse, reflection );
			}
		}

		/// <summary>
		/// Returns an isometry which represents a reflection across the x axis.
		/// </summary>
		public static Isometry ReflectX()
		{
			Isometry i = new Isometry();
			Circle reflection = new Circle( new Vector3D(), new Vector3D( 1, 0 ) );
			i.Reflection = reflection;
			return i;
		}

		/// <summary>
		/// Calculates an isometry by taking a tile boundary polygon to a home.
		/// </summary>
		public void CalculateFromTwoPolygons( Tile home, Tile tile, Geometry g )
		{
			Polygon poly = tile.Boundary;
			CalculateFromTwoPolygons( home, poly, g );
		}
		
		public void CalculateFromTwoPolygons( Tile home, Polygon boundaryPolygon, Geometry g )
		{
			CalculateFromTwoPolygonsInternal( home.Boundary, boundaryPolygon, home.VertexCircle, g );
		}

		private void CalculateFromTwoPolygonsInternal( Polygon home, Polygon boundary, CircleNE homeVertexCircle, Geometry g )
		{
			// ZZZ - We have to use the boundary, but that can be projected to infinity for some of the spherical tilings.
			//		 Trying to use the Drawn tile produced weird (yet interesting) results.
			Polygon poly1 = boundary;
			Polygon poly2 = home;

			if( poly1.Segments.Count < 3 ||
				poly2.Segments.Count < 3 )	// Poor poor digons.
			{
				Debug.Assert( false );
				return;
			}

			// Same?
			Vector3D p1 = poly1.Segments[0].P1, p2 = poly1.Segments[1].P1, p3 = poly1.Segments[2].P1;
			Vector3D w1 = poly2.Segments[0].P1, w2 = poly2.Segments[1].P1, w3 = poly2.Segments[2].P1;
			if( p1 == w1 && p2 == w2 && p3 == w3 )
			{
				this.Mobius = Mobius.Identity();
				return;
			}

			Mobius m = new Mobius();
			m.MapPoints( p1, p2, p3, w1, w2, w3 );
			this.Mobius = m;

			// Worry about reflections as well.
			if( g == Geometry.Spherical )
			{
				// If inverted matches the orientation, we need a reflection.
				bool inverted = poly1.IsInverted;
				if( !(inverted ^ poly1.Orientation) )
					this.Reflection = homeVertexCircle;
			}
			else
			{
				if( !poly1.Orientation )
					this.Reflection = homeVertexCircle;
			}

			// Some testing.
			Vector3D test = this.Apply( boundary.Center );
			if( test != home.Center )
			{
				// ZZZ: What is happening here is that the mobius can project a point to infinity before the reflection brings it back to the origin.
				//		It hasn't been much of a problem in practice yet, but will probably need fixing at some point.
				//Trace.WriteLine( "oh no!" );
			}
		}

		/// <summary>
		/// Simple helper to transform an array of vertices using an isometry.
		/// Warning! Allocates a new array.
		/// </summary>
		public static Vector3D[] TransformVertices( Vector3D[] vertices, Isometry isometry )
		{
			List<Vector3D> result = new List<Vector3D>();
			for( int i = 0; i < vertices.Length; i++ )
			{
				Vector3D transformed = isometry.Apply( vertices[i] );
				result.Add( transformed );
			}
			return result.ToArray();
		}
	}
}
