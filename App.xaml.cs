using System.ComponentModel;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		//private System.Windows.Forms.NotifyIcon trayIcon;
		private TaskbarIcon trayIcon;
		private bool isExit;

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			Program.Main(e.Args, this);

			//trayIcon = new TaskbarIcon();
			////trayIcon.Double += (s, args) => ShowMainWindow();
			//trayIcon.Icon = IgniteBot.Properties.Resources.ignite_logo;
			//trayIcon.Visibility = Visibility.Visible;

			CreateContextMenu();
		}

		private void CreateContextMenu()
		{
			//trayIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
			//trayIcon.ContextMenuStrip.Items.Add("Show Main Output Window...").Click += (s, e) => ShowMainWindow();
			//trayIcon.ContextMenuStrip.Items.Add("Exit").Click += (s, e) => ExitApplication();
		}

		public void ExitApplication()
		{
			Program.running = false;
			isExit = true;
			Current.Shutdown();
			//trayIcon.Dispose();
			//trayIcon = null;
		}

		private void ShowMainWindow()
		{
			if (Program.liveWindow == null)
			{
				Program.liveWindow = new LiveWindow();
				Program.liveWindow.Closed += (sender, args) => Program.liveWindow = null;
				Program.liveWindow.Show();
			}
			else if (Program.liveWindow.IsVisible)
			{
				if (Program.liveWindow.WindowState == WindowState.Minimized)
				{
					Program.liveWindow.WindowState = WindowState.Normal;
				}
				Program.liveWindow.Activate();
			}
			else
			{
				Program.liveWindow.Show();
			}
		}

		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			if (!isExit)
			{
				e.Cancel = true;
				Program.liveWindow.Hide(); // A hidden window can be shown again, a closed one not
			}
		}
	}
}
