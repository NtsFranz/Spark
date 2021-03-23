using System;
using System.Windows;
using IgniteBot.Properties;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{

		protected override void OnStartup(StartupEventArgs e)
		{
			System.Threading.Thread.CurrentThread.CurrentUICulture = Settings.Default.language switch
			{
				0 => new System.Globalization.CultureInfo("en"),
				1 => new System.Globalization.CultureInfo("ja-JP"),
				_ => System.Threading.Thread.CurrentThread.CurrentUICulture
			};
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
