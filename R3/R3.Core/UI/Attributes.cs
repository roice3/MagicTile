namespace R3.UI
{
	using System;

	[AttributeUsage( AttributeTargets.Property, Inherited = true, AllowMultiple = false )]
	public sealed class RangeAttribute : Attribute
	{
		readonly double _low;
		readonly double _high;

		public RangeAttribute( double low, double high )
		{
			this._low = low;
			this._high = high;
			System.Diagnostics.Debug.Assert( _low <= _high );
		}

		public double Low
		{
			get { return this._low; }
		}

		public double High
		{
			get { return this._high; }
		}
	}
}
