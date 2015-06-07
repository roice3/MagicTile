namespace R3.Control
{
	using R3.Math;
	using System;

	/// <summary>
	/// Class which manages 4D view rotations
	/// </summary>
	public class RotationHandler4D
	{
		public RotationHandler4D() 
		{
			Current4dView = null;
		}

		public RotationHandler4D( Matrix4D initialMatrix )
		{
			Current4dView = initialMatrix;
		}
	
		private Matrix4D ViewMat4d = Matrix4D.Identity();

		/// <summary>
		/// The current viewpoint.
		/// </summary>
		public Matrix4D Current4dView
		{
			get
			{
				return ViewMat4d;
			}
			set
			{
				if( value == null )
				{
					ViewMat4d = Matrix4D.Identity();
					return;
				}

				ViewMat4d = Matrix4D.GramSchmidt( value );	// Orthonormalize
			}
		}
	
		/// <summary>
		/// Handles updating our rotation matrices based on mouse dragging.
		/// </summary>
		public void MouseDragged( double dx, double dy,
			bool xz_yz, bool xw_yw, bool xy_zw )
		{
			Matrix4D spinDelta = new Matrix4D();

			// Sensitivity.
			dx *= 0.012;
			dy *= 0.012;

			if( xz_yz )
			{
				spinDelta[0,2] += dx;
				spinDelta[2,0] -= dx;
	
				spinDelta[1,2] += dy;
				spinDelta[2,1] -= dy;
			}

			if( xw_yw )
			{
				spinDelta[0,3] -= dx;
				spinDelta[3,0] += dx;
	
				spinDelta[1,3] -= dy;
				spinDelta[3,1] += dy;
			}

			if( xy_zw )
			{
				spinDelta[0,1] += dx;
				spinDelta[1,0] -= dx;
	
				spinDelta[3,2] -= dy;
				spinDelta[2,3] += dy;
			}

			ApplySpinDelta( spinDelta );
		}
	
		private void ApplySpinDelta( Matrix4D spinDelta )
		{
			Matrix4D delta = Matrix4D.Identity() + spinDelta;
			delta = Matrix4D.GramSchmidt( delta );			// Orthonormalize
			ViewMat4d = delta * ViewMat4d;
			ViewMat4d = Matrix4D.GramSchmidt( ViewMat4d );	// Orthonormalize
		}
	}	
}
