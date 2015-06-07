namespace MagicTile.Utils
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Xml;

	interface IPersistable
	{
		string SaveToString();
		void LoadFromString( string saved );
	}

	internal static class Persistence
	{
		public static XmlWriterSettings WriterSettings
		{
			get
			{
				XmlWriterSettings settings = new XmlWriterSettings();
				settings.OmitXmlDeclaration = true;
				settings.Indent = true;
				return settings;
			}
		}

		public static XmlReaderSettings ReaderSettings
		{
			get
			{
				XmlReaderSettings settings = new XmlReaderSettings();
				settings.IgnoreWhitespace = true;
				return settings;
			}
		}

		private const char StandardSeparator = ',';
		
		public static string SaveArray<T>( T[] array )
		{
			return SaveArray<T>( array, StandardSeparator );
		}

		public static T[] LoadArray<T>( string saved )
		{
			return LoadArray<T>( saved, StandardSeparator );
		}

		/// <summary>
		/// Save an array (of a simple type) to a string.
		/// </summary>
		public static string SaveArray<T>( T[] array, char separator )
		{
			StringBuilder sb = new StringBuilder();
			foreach( T item in array )
				sb.Append( item.ToString() + separator );

			string result = sb.ToString();
			result = result.TrimEnd( separator );
			return result;
		}

		/// <summary>
		/// Extension method to convert simple type to a string.
		/// http://stackoverflow.com/questions/3502493/is-there-a-generic-parse-function-that-will-convert-a-string-to-any-type-using/3502523#3502523
		/// </summary>
		private static T ChangeType<T>( this object obj )
		{
			return (T)System.Convert.ChangeType( obj, typeof( T ) );
		}

		/// <summary>
		/// Load an array (of a simple type) from a string.
		/// </summary>
		public static T[] LoadArray<T>( string saved, char separator )
		{
			List<T> result = new List<T>();

			string[] split = saved.Split( separator );
			foreach( string s in split )
			{
				if( string.IsNullOrEmpty( s ) )
					continue;

				result.Add( s.ChangeType<T>() );
			}

			return result.ToArray();
		}

		public static string SaveList<T>( List<T> list, char separator ) where T : IPersistable
		{
			StringBuilder sb = new StringBuilder();
			foreach( T item in list )
				sb.Append( item.SaveToString() + separator );

			string result = sb.ToString();
			result = result.TrimEnd( separator );
			return result;
		}

		public static List<T> LoadList<T>( string saved, char separator ) where T : IPersistable, new()
		{
			List<T> result = new List<T>();

			string[] split = saved.Split( separator );
			foreach( string s in split )
			{
				if( string.IsNullOrEmpty( s ) )
					continue;

				T item = new T();
				item.LoadFromString( s );
				result.Add( item );
			}

			return result;
		}
	}
}
