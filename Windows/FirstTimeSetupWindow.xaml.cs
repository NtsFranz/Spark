using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Spark
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
			setupLabel.Content = Properties.Resources.Searching_for_Quest_on_network;
			setupText.Visibility = Visibility.Hidden;
			spectatorButton.IsEnabled = false;
			playerButton.IsEnabled = false;
			Progress<string> progress = new Progress<string>(s => setupLabel.Content = s);
			await Task.Factory.StartNew(() => Program.echoVRIP = QuestIPFetching.FindQuestIP(progress),
										TaskCreationOptions.None);
			spectatorButton.IsEnabled = true;
			playerButton.IsEnabled = true;
			SparkSettings.instance.echoVRIP = Program.echoVRIP;
			SparkSettings.instance.Save();
			Thread.Sleep(2000);

			Close();
		}

		private void PCClicked(object sender, RoutedEventArgs e)
		{
			Program.echoVRIP = "127.0.0.1";
			SparkSettings.instance.echoVRIP = Program.echoVRIP;
			SparkSettings.instance.Save();

			Close();
		}
	}
}
