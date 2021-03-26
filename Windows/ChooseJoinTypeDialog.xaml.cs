using System.Diagnostics;
using System.Windows;
using Spark.Properties;

namespace Spark
{
	/// <summary>
	/// Interaction logic for ChooseJoinTypeDialog.xaml
	/// </summary>
	public partial class ChooseJoinTypeDialog : Window
	{
		private readonly string sessionid;

		public ChooseJoinTypeDialog(string sessionid)
		{
			InitializeComponent();

			this.sessionid = sessionid;
		}

		private void JoinAsPlayerClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(Settings.Default.echoVRPath)) return;
			Process.Start(Settings.Default.echoVRPath, (Settings.Default.capturevp2 ? "-capturevp2 " : "") + "-lobbyid " + sessionid);
			Close();
			Program.Quit();
		}

		private void JoinAsSpectatorClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(Settings.Default.echoVRPath)) return;
			Process.Start(Settings.Default.echoVRPath, (Settings.Default.capturevp2 ? "-capturevp2 " : "") + "-spectatorstream -lobbyid " + sessionid);
			Close();
			Program.Quit();
		}
	}
}
