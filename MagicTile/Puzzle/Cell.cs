namespace MagicTile
{
	using R3.Drawing;
	using R3.Geometry;
	using R3.Math;
	using System.Collections.Generic;

	public class Cell : ITransformable
	{
		public Cell( Polygon boundary, CircleNE vertexCircle )
		{
			Boundary = boundary;
			VertexCircle = vertexCircle;
			Stickers = new List<Sticker>(); 
			Isometry = new Isometry();
			IndexOfMaster = -1;
			PickInfo = new PickInfo[] { };
			Neighbors = new HashSet<Cell>();
		}

		/// <summary>
		/// This is the index of the master cell associate with this cell (the index of ourselves if we are a master).
		/// It is used for state calculations.
		/// </summary>
		public int IndexOfMaster { get; set; }

		/// <summary>
		/// The boundary of this cell.
		/// </summary>
		public Polygon Boundary { get; set; }

		/// <summary>
		/// The center of this cell.
		/// </summary>
		public Vector3D Center
		{
			get { return Boundary.Center; }
		}

		/// <summary>
		/// The vertex circle for this cell.
		/// </summary>
		public CircleNE VertexCircle { get; set; }

		/// <summary>
		/// The stickers for this cell.
		/// </summary>
		public List<Sticker> Stickers { get; set; }

		/// <summary>
		/// For slave cells, a reference to the master cell.
		/// </summary>
		public Cell Master { get; set; }

		/// <summary>
		/// Whether or not we are a master.
		/// </summary>
		public bool IsMaster { get { return Master == null; } }

		/// <summary>
		/// Whether or not we are a slave.
		/// </summary>
		public bool IsSlave { get { return Master != null; } }

		/// <summary>
		/// Return the master cell of this cell, if this is a master itself, return itself
		/// </summary>
		public Cell MasterOrSelf { get { return Master ?? this; } }

		/// <summary>
		/// List of master cells that share boundaries with this cell. 
		/// If a master cell borders a slave cell, the master of the slave cell is in this list.
		/// A master cell is its own neighbor by definition
		/// </summary>
		public HashSet<Cell> Neighbors { get; set; }

		/// <summary>
		/// This is the isometry that will take us back to the master cell.
		/// </summary>
		public Isometry Isometry { get; set; }

		/// <summary>
		/// This is the isometry that will take the master cell to us.
		/// Cache for speed?
		/// </summary>
		public Isometry IsometryInverse
		{
			get
			{
				return Isometry.Inverse();
			}
		}

		/// <summary>
		/// Whether or not we have gone through an odd number of reflections.
		/// </summary>
		public bool Reflected { get { return Isometry.Reflection != null; } }

		/// <summary>
		/// The texture coordinates.
		/// NOTE: We don't fill out texturecoords for slaves (but just reference the master ones).
		/// </summary>
		public Vector3D[] TextureCoords { get; set; }

		/// <summary>
		/// The texture vertices.
		/// </summary>
		public Vector3D[] TextureVertices { get; set; }

		public void Transform( Mobius m )
		{
			Boundary.Transform( m );
			foreach( Sticker s in Stickers )
				s.Poly.Transform( m );
			VertexCircle.Transform( m );
		}

		public void Transform( Isometry i )
		{
			Boundary.Transform( i );
			foreach( Sticker s in Stickers )
				s.Poly.Transform( i );
			VertexCircle.Transform( i );
		}

		/// <summary>
		/// Data used for picking.
		/// This is only used for IRP cells at the moment.
		/// </summary>
		internal PickInfo[] PickInfo { get; set; }
	}

	internal class PickInfo
	{
		public PickInfo( Polygon poly, TwistData twistData )
		{
			Poly = poly;
			TwistData = twistData;
		}

		public Polygon Poly { get; set; }
		public TwistData TwistData { get; set; }

		// We need to subdivide for skew polyhedra, since we project them out to the hypersphere.
		public Vector3D[] SubdividedVerts { get; set; }
		public int[] SubdividedElements { get; set; }

		public void Subdivide()
		{
			int lod = 3;
			SubdividedVerts = TextureHelper.TextureCoords( Poly, Geometry.Euclidean );
			SubdividedElements = TextureHelper.TextureElements( Poly.NumSides, lod );	// ZZZ - slow to recalc for every pick info!
		}

		/// <summary>
		/// Color (for debug drawing only).
		/// </summary>
		public System.Drawing.Color Color { get; set; }
	}
}