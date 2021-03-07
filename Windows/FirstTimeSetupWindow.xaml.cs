using IgniteBot.Properties;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for FirstTimeSetupWindow.xaml
	/// </summary>
	public partial class FirstTimeSetupWindow : Window
	{

		public FirstTimeSetupWindow()
		{
			InitializeComponent();
		}

		private async void QuestClicked(object sender, RoutedEventArgs e)
		{
			setupLabel.Content = "Searching for Quest on network";
			setupText.Visibility = Visibility.Hidden;
			spectatorButton.IsEnabled = false;
			playerButton.IsEnabled = false;
			var progress = new Progress<string>(s => setupLabel.Content = s);
			await Task.Factory.StartNew(() => Program.echoVRIP = Program.FindQuestIP(progress),
										TaskCreationOptions.None);
			spectatorButton.IsEnabled = true;
			playerButton.IsEnabled = true;
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();
			Thread.Sleep(2000);

			Close();
		}

		private void PCClicked(object sender, RoutedEventArgs e)
		{
			Program.echoVRIP = "127.0.0.1";
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();

			Close();
		}
	}
}
