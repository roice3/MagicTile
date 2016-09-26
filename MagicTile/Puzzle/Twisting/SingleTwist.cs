namespace MagicTile
{
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Xml.Linq;

	public class SingleTwist
	{
		public SingleTwist Clone()
		{
			SingleTwist newTwist = new SingleTwist();
			newTwist.IdentifiedTwistData = this.IdentifiedTwistData;
			newTwist.IdentifiedTwistDataEarthquake = this.IdentifiedTwistDataEarthquake;
			newTwist.LeftClick = this.LeftClick;
			newTwist.SliceMask = this.SliceMask;
			newTwist.SliceMaskEarthquake = this.SliceMaskEarthquake;
			newTwist.MacroStart = this.MacroStart;
			newTwist.MacroEnd = this.MacroEnd;
			return newTwist;
		}

		/// <summary>
		/// All the cached twist data for this twist.
		/// </summary>
		public IdentifiedTwistData IdentifiedTwistData { get; set; }

		/// <summary>
		/// Earthquake twists involve two sets of identified twist data.
		/// Normally, this will be null.
		/// </summary>
		public IdentifiedTwistData IdentifiedTwistDataEarthquake { get; set; }

		/// <summary>
		/// Convenient access to the state calc twist data.
		/// </summary>
		public List<TwistData> StateCalcTD
		{
			get
			{
				List<TwistData> twistDataList = null;
				if( IdentifiedTwistDataEarthquake != null )
					twistDataList = IdentifiedTwistData.TwistDataForStateCalcs.Concat(
						IdentifiedTwistDataEarthquake.TwistDataForStateCalcs ).ToList();
				else
					twistDataList = IdentifiedTwistData.TwistDataForStateCalcs;
				return twistDataList;
			}
		}

		/// <summary>
		/// Left or right click.
		/// </summary>
		public bool LeftClick { get; set; }

		/// <summary>
		/// The slicemask for this twist.
		/// </summary>
		public int SliceMask { get; set; }

		/// <summary>
		/// The slicemask for the earthquake part of the twist.
		/// </summary>
		public int SliceMaskEarthquake { get; set; }

		public bool MacroStart { get; set; }
		public bool MacroEnd { get; set; }

		/// <summary>
		/// Compare two twists.
		/// NOTE: This ignores the MacroStart/MacroEnd properties.
		/// </summary>
		public bool Compare( SingleTwist other )
		{
			return 
				this.IdentifiedTwistData == other.IdentifiedTwistData &&
				this.LeftClick == other.LeftClick &&
				this.SliceMask == other.SliceMask;
		}

		/// <summary>
		/// Checks if we are an undo of another twist.
		/// </summary>
		public bool IsUndo( SingleTwist other )
		{
			if( other == null )
				return false;

			SingleTwist clone = this.Clone();
			clone.ReverseTwist();
			return clone.Compare( other );
		}

		/// <summary>
		/// The Magnitude of the twist (in radians).
		/// </summary>
		public double Magnitude
		{
			get
			{
				return 2 * System.Math.PI / this.IdentifiedTwistData.Order;
			}
		}

		public void ReverseTwist()
		{
			LeftClick = !LeftClick;
		}

		public string SaveToString()
		{
			string ret = string.Empty;
			if( this.MacroStart )
				ret += "[";
			ret += this.IdentifiedTwistData.Index.ToString() + ":";
			ret += this.LeftClick ? "L" : "R";
			ret += ":" + this.SliceMask.ToString();
			if( this.MacroEnd )
				ret += "]";
			return ret;
		}

		public void LoadFromString( string saved, List<IdentifiedTwistData> AllTwistData )
		{
			this.MacroStart = saved.StartsWith( "[" );
			this.MacroEnd = saved.EndsWith( "]" );
			if( this.MacroStart && this.MacroEnd )
			{
				// Clear out macro markings if there is only one twist in the macro.
				this.MacroStart = this.MacroEnd = false;
			}

			saved = saved.Trim( new char[] { '[', ']' } );

			string[] split = saved.Split( ':' );
			if( 3 != split.Length )
			{
				Debug.Assert( false );
				return;
			}

			int index = System.Convert.ToInt32( split[0] );
			this.IdentifiedTwistData = AllTwistData[index];
			this.LeftClick = split[1] == "L";
			this.SliceMask = System.Convert.ToInt32( split[2] );
			if( this.SliceMask == 0 )
				this.SliceMask = 1;
		}
	}

	public class TwistList : List<SingleTwist>
	{
		public TwistList Clone()
		{
			TwistList result = new TwistList();
			foreach( SingleTwist t in this )
				result.Add( t.Clone() );
			return result;
		}

		public XElement SaveToXml( XElement xParent )
		{
			string line = "";
			int count = 0;
			foreach( SingleTwist t in this )
			{
				string twistString = t.SaveToString();
				line += twistString;
				count++;

				if( count >= 10 )
				{
					xParent.Add( new XElement( "Block", line ) );
					line = "";
					count = 0;
				}
				else
				{
					line += "\t";
				}
			}

			if( line != "" )
			{
				line.TrimEnd( '\t' );
				xParent.Add( new XElement( "Block", line ) );
			}

			return xParent;
		}

		public void LoadFromXml( XElement xElement, List<IdentifiedTwistData> allTwistData )
		{
			this.Clear();

			foreach( XElement xCell in xElement.Elements( "Block" ) )
			{
				string line = xCell.Value;
				string[] split = line.Split( '\t' );
				foreach( string item in split )
				{
					if( string.IsNullOrEmpty( item ) )
						continue;

					SingleTwist newTwist = new SingleTwist();
					newTwist.LoadFromString( item, allTwistData );
					this.Add( newTwist );
				}
			}
		}

	}
}
