using IgniteBot2.Properties;
using System.Diagnostics;
using System.Windows;

namespace IgniteBot2
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
			Process.Start(Settings.Default.echoVRPath, "-lobbyid " + sessionid);
			Close();
		}

		private void JoinAsSpectatorClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(Settings.Default.echoVRPath)) return;
			Process.Start(Settings.Default.echoVRPath, "-spectatorstream -lobbyid " + sessionid);
			Close();
		}
	}
}
