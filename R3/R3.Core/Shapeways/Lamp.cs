namespace R3.Geometry
{
	using R3.Core;
	using R3.Math;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Numerics;
	using Math = System.Math;

	internal class Lamp
	{
		private static Inventory m_inventory = new Inventory();

		/// <summary>
		/// Function I was using for lamp project, probably a bit out of date.
		/// </summary>
		public static void AddToMeshLamp( Shapeways mesh, Vector3D v1, Vector3D v2 )
		{
			// need to get these from CalcBallArc
			Vector3D center = Vector3D.DneVector();
			double radius = double.NaN;
			Vector3D normal = Vector3D.DneVector();
			double angleTot = double.NaN;

			double length1 = Scale( 2.1 );	// 12-end piece
			//double length1 = Scale( 1.6 );	// 6-end piece
			//double length1 = Scale( 1.4 );	// 4-end piece
			double length2 = Scale( 0.5 );

			double outerRadStart = Scale( 0.0625 / 2 );
			double outerRadEnd = Scale( 0.25 / 2 );

			System.Func<Vector3D, double> outerSizeFunc = v =>
			{
				double angle = (v1 - center).AngleTo( v - center );
				double len = radius * angle;
				return outerRadStart + (outerRadEnd - outerRadStart) * (len / length1);
			};

			System.Func<Vector3D, double> outerSizeFunc2 = v =>
			{
				double angle = (v2 - center).AngleTo( v - center );
				double len = radius * angle;
				return outerRadStart + (outerRadEnd - outerRadStart) * (len / length1);
			};

			System.Func<Vector3D, double> innerSizeFunc = v =>
			{
				// Very slightly bigger than 1/8 inch OD.
				return Scale( 0.13 / 2 );
			};

			Vector3D[] outerPoints = Shapeways.CalcArcPoints( center, radius, v1, normal, length1 / radius );
			Vector3D[] innerPoints = Shapeways.CalcArcPoints( center, radius, outerPoints[outerPoints.Length - 1], normal * -1, length2 / radius );
			mesh.AddCornucopia( outerPoints, outerSizeFunc, innerPoints, innerSizeFunc );

			outerPoints = Shapeways.CalcArcPoints( center, radius, v2, normal * -1, length1 / radius );
			innerPoints = Shapeways.CalcArcPoints( center, radius, outerPoints[outerPoints.Length - 1], normal, length2 / radius );
			mesh.AddCornucopia( outerPoints, outerSizeFunc2, innerPoints, innerSizeFunc );

			m_inventory.AddRod( Rod.Create( radius, angleTot ) );
		}

		private static double Scale( double rad )
		{
			// This is ball model radius of final object
			double scale = 9;
			return rad / scale;
		}
	}

	internal class Rod
	{
		public static Rod Create( double radius, double angle )
		{
			Rod r = new Rod();
			r.Radius = radius;
			r.Length = radius * angle;
			return r;
		}

		public double Radius { get; set; }
		public double Length { get; set; }

		public static bool operator ==( Rod r1, Rod r2 )
		{
			return r1.Compare( r2, Tolerance.Threshold );
		}

		public static bool operator !=( Rod r1, Rod r2 )
		{
			return !(r1 == r2);
		}

		public override bool Equals( object obj )
		{
			Rod r = (Rod)obj;
			return (r == this);
		}

		public override int GetHashCode()
		{
			double inverse = 1 / Tolerance.Threshold;
			int decimals = (int)Math.Log10( inverse );

			return
				Math.Round( Radius, decimals ).GetHashCode() ^
				Math.Round( Length, decimals ).GetHashCode();
		}

		public bool Compare( Rod other, double threshold )
		{
			return (Tolerance.Equal( Radius, other.Radius, threshold ) &&
					 Tolerance.Equal( Length, other.Length, threshold ));
		}
	}

	internal class Inventory
	{
		public Dictionary<Rod, int> Rods = new Dictionary<Rod, int>();

		public void AddRod( Rod rod )
		{
			int num;
			if( Rods.TryGetValue( rod, out num ) )
				num++;
			else
				num = 1;
			Rods[rod] = num;
		}

		public double TotalLength
		{
			get
			{
				double result = 0;
				foreach( KeyValuePair<Rod, int> kvp in Rods )
					result += kvp.Key.Length * kvp.Value;
				return result;
			}
		}
	}
}
