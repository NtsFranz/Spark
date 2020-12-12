using System.Windows;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			Program.Main(e.Args, this);
		}

		public void ExitApplication()
		{
			Program.running = false;
			Current.Shutdown();
		}
	}
}
