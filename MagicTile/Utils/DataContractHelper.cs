namespace MagicTile.Utils
{
	using System.IO;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Xml;

	/// <summary>
	/// Class with useful methods for saving/loading objects to files.
	/// </summary>
	public class DataContractHelper
	{
		public static void SaveToXml( object obj, string filename )
		{
			using( var writer = XmlWriter.Create( filename, Persistence.WriterSettings ) )
			{
				DataContractSerializer dcs = new DataContractSerializer( obj.GetType() );
				dcs.WriteObject( writer, obj );
			}
		}

		public static object LoadFromXml( System.Type objectType, string filename )
		{
			using( var reader = XmlReader.Create( filename, Persistence.ReaderSettings ) )
			{
				DataContractSerializer dcs = new DataContractSerializer( objectType );
				return dcs.ReadObject( reader, verifyObjectName: false );
			}
		}

		public static string SaveToString( object obj )
		{
			StringBuilder sb = new StringBuilder();
			using( XmlWriter writer = XmlWriter.Create( sb, Persistence.WriterSettings ) )
			{
				DataContractSerializer dcs = new DataContractSerializer( obj.GetType() );
				dcs.WriteObject( writer, obj );
			}
			return sb.ToString();
		}

		public static object LoadFromString( System.Type objectType, string saved )
		{
			using( StringReader sr = new StringReader( saved ) )
			using( XmlReader reader = XmlReader.Create( sr, Persistence.ReaderSettings ) )
			{
				DataContractSerializer dcs = new DataContractSerializer( objectType );
				return dcs.ReadObject( reader, verifyObjectName: false );
			}
		}
	}
}
