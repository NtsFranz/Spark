using IgniteBot.Properties;
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

		private void QuestClicked(object sender, RoutedEventArgs e)
		{
			Program.echoVRIP = Program.FindQuestIP();
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();

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
