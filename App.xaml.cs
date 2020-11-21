using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace IgniteBot2
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private System.Windows.Forms.NotifyIcon trayIcon;
		private bool isExit;

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			Program.Main(e.Args, this);

			trayIcon = new System.Windows.Forms.NotifyIcon();
			trayIcon.DoubleClick += (s, args) => ShowMainWindow();
			trayIcon.Icon = IgniteBot2.Properties.Resources.ignite_logo;
			trayIcon.Visible = true;

			CreateContextMenu();
		}

		private void CreateContextMenu()
		{
			trayIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
			trayIcon.ContextMenuStrip.Items.Add("Show Main Output Window...").Click += (s, e) => ShowMainWindow();
			trayIcon.ContextMenuStrip.Items.Add("Exit").Click += (s, e) => ExitApplication();
		}

		public void ExitApplication()
		{
			Program.running = false;
			isExit = true;
			Current.Shutdown();
			trayIcon.Dispose();
			trayIcon = null;
		}

		private void ShowMainWindow()
		{
			if (MainWindow.IsVisible)
			{
				if (MainWindow.WindowState == WindowState.Minimized)
				{
					MainWindow.WindowState = WindowState.Normal;
				}
				MainWindow.Activate();
			}
			else
			{
				MainWindow.Show();
			}
		}

		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			if (!isExit)
			{
				e.Cancel = true;
				MainWindow.Hide(); // A hidden window can be shown again, a closed one not
			}
		}
	}
}
