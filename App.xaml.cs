using System;
using System.Windows;
using Spark.Properties;

namespace Spark
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{

		protected override void OnStartup(StartupEventArgs e)
		{
			// Reload old settings file
			if (Settings.Default.UpdateSettings)
			{
				Settings.Default.Upgrade();
				Settings.Default.UpdateSettings = false;
				Settings.Default.Save();
			}

			System.Threading.Thread.CurrentThread.CurrentUICulture = Settings.Default.languageIndex switch
			{
				0 => new System.Globalization.CultureInfo("en"),
				1 => new System.Globalization.CultureInfo("ja-JP"),
				_ => System.Threading.Thread.CurrentThread.CurrentUICulture
			};

			ThemesController.SetTheme((ThemesController.ThemeTypes)Settings.Default.theme);



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
