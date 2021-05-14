using System;
using System.Windows;

namespace Spark
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{

		protected override void OnStartup(StartupEventArgs e)
		{
			// load settings file
			SparkSettings.Load();

			System.Threading.Thread.CurrentThread.CurrentUICulture = SparkSettings.instance.languageIndex switch
			{
				0 => new System.Globalization.CultureInfo("en"),
				1 => new System.Globalization.CultureInfo("ja-JP"),
				_ => System.Threading.Thread.CurrentThread.CurrentUICulture
			};

			ThemesController.SetTheme((ThemesController.ThemeTypes)SparkSettings.instance.theme);



			base.OnStartup(e);

			Program.Main(e.Args, this);
		}

		public void ExitApplication()
		{
			Program.running = false;
			Current.Shutdown();
			Environment.Exit(Environment.ExitCode);
		}
	}
}
