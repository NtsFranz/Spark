using System;
using System.Numerics;
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

			if (SparkSettings.instance == null)
			{
				new MessageBox($"Error accessing settings.\nTry renaming/deleting the file in C:\\Users\\[USERNAME]\\AppData\\Roaming\\IgniteVR\\Spark\\settings.json").Show();
				return;
			}
			

			System.Threading.Thread.CurrentThread.CurrentUICulture = SparkSettings.instance.languageIndex switch
			{
				0 => new System.Globalization.CultureInfo("en"),
				1 => new System.Globalization.CultureInfo("ja-JP"),
				_ => System.Threading.Thread.CurrentThread.CurrentUICulture
			};

			ThemesController.SetTheme((ThemesController.ThemeTypes)SparkSettings.instance.theme);
			CheckWindowPositionsValid();

			base.OnStartup(e);

			Program.Main(e.Args, this);
		}



		private static void CheckWindowPositionsValid()
		{
			if (OffScreen(
				new Vector2(
					SparkSettings.instance.liveWindowLeft,
					SparkSettings.instance.liveWindowTop
					),
				new Vector2(500, 500)))
			{
				SparkSettings.instance.liveWindowLeft = 0;
				SparkSettings.instance.liveWindowTop = 0;
			}

			if (OffScreen(
				new Vector2(
					SparkSettings.instance.settingsWindowLeft,
					SparkSettings.instance.settingsWindowTop
					),
				new Vector2(500, 500)))
			{
				SparkSettings.instance.settingsWindowLeft = 0;
				SparkSettings.instance.settingsWindowTop = 0;
			}

		}

		private static bool OffScreen(Vector2 topLeft, Vector2 size)
		{
			return
				(topLeft.X <= SystemParameters.VirtualScreenLeft - size.X) ||
				(topLeft.Y <= SystemParameters.VirtualScreenTop - size.Y) ||
				(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth <= topLeft.X) ||
				(SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight <= topLeft.Y);
		}

		public void ExitApplication()
		{
			Program.running = false;
			Dispatcher.Invoke(() =>
			{
				Current.Shutdown();
				Environment.Exit(Environment.ExitCode);
			});
		}
	}
}