
namespace MagicTile
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Forms;
	using OpenTK.Graphics.OpenGL;

	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			AppDomain currentDomain = AppDomain.CurrentDomain;
			currentDomain.UnhandledException += new UnhandledExceptionEventHandler( ExceptionHandler );

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault( false );

			MainForm form = null;
			try
			{
				form = new MainForm();
			}
			catch( Exception e )
			{
				WriteException( e );
			}

			Application.Run( form );
		}

		static void ExceptionHandler( object sender, UnhandledExceptionEventArgs args )
		{
			Exception e = (Exception)args.ExceptionObject;
			WriteException( e );
		}

		static void WriteException( Exception e )
		{
			Console.WriteLine( "ExceptionHandler caught: " + e.Message );
			if( e.InnerException != null )
				Console.WriteLine( "Inner exception was: " + e.InnerException.Message );
		}
	}
}
