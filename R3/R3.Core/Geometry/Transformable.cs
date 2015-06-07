namespace R3.Geometry
{
	using R3.Math;
	using System.Numerics;

	/// <summary>
	/// For objects that can be transformed.
	/// </summary>
	public interface ITransformable
	{
		// ZZZ - Make this take in an ITransform?
		void Transform( Mobius m );
		void Transform( Isometry m );
	}

	/// <summary>
	/// For objects that can transform.
	/// </summary>
	public interface ITransform
	{
		Vector3D Apply( Vector3D input );
		Complex Apply( Complex input );
	}
}
