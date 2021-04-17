using System.Windows;

namespace Spark
{
	public partial class YouSureAboutClosing : Window
	{
		public YouSureAboutClosing()
		{
			InitializeComponent();
		}

		private void HideClicked(object sender, RoutedEventArgs e)
		{
			Close();
			Program.liveWindow.Hide();
			Program.liveWindow.showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
			Program.liveWindow.hidden = true;
		}

		private void ExitClicked(object sender, RoutedEventArgs e)
		{
			Close();
			Program.Quit();
		}
	}
}
