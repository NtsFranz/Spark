using System.Windows;

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
			if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath)) return;
			Program.StartEchoVR(Program.JoinType.Player, session_id: sessionid);
			Close();
			Program.Quit();
		}

		private void JoinAsSpectatorClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath)) return;
			Program.StartEchoVR(Program.JoinType.Spectator, session_id: sessionid, noovr: SparkSettings.instance.sparkLinkNoOVR);
			SparkSettings.instance.Save();
			Close();
			Program.Quit();
		}
	}
}
