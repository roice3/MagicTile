namespace MagicTile.Utils
{
	using System.IO;

	internal static class StandardPaths
	{
		private const string programDir = "MagicTile v2";
		private const string settingsFile = "settings.xml";
		private const string menuFile = "menu.xml";
		private const string config = "config";
		private const string irpDir = "irp";

		private static string MainAppDataFolder
		{
			get
			{
				return System.Environment.GetFolderPath( System.Environment.SpecialFolder.ApplicationData );
			}
		}

		private static string AppDataDir
		{
			get
			{
				// Make our program folder if needed.
				string appDataFolder = MainAppDataFolder;
				string fullProgramDir = Path.Combine( appDataFolder, programDir );
				Directory.CreateDirectory( fullProgramDir );
				return fullProgramDir;
			}
		}

		public static string SettingsFile
		{
			get
			{
				return Path.Combine( AppDataDir, settingsFile );
			}
		}

		public static string MenuFile
		{
			get
			{
				return Path.Combine( ConfigDir, menuFile );
			}
		}

		public static string ConfigDir
		{
			get
			{
				string current = Directory.GetCurrentDirectory();
				string dir = Path.Combine( current, config );
				if( Directory.Exists( dir ) )
					return dir;

				// Diff directory for development.
				string dev = Directory.GetParent( current ).Parent.FullName;
				return Path.Combine( dev, config );
			}
		}

		public static string IrpDir
		{
			get
			{
				return Path.Combine( StandardPaths.ConfigDir, irpDir ); 
			}
		}
	}
}
