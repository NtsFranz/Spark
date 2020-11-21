using IgniteBot2.Properties;
using System.Windows;
using System.Windows.Controls;

namespace IgniteBot2
{
	/// <summary>
	/// Interaction logic for LoginWindow.xaml
	/// </summary>
	public partial class LoginWindow : Window
	{
		public LoginWindow()
		{
			InitializeComponent();
		}

		private void StartButtonClicked(object sender, RoutedEventArgs e)
		{
			Program.authorized = true;
			Settings.Default.accessMode = ((ComboBoxItem)accessCodeComboBox.SelectedValue).Content.ToString();
			Settings.Default.Save();

			if (Program.liveWindow == null)
			{
				Program.liveWindow = new LiveWindow();
				Program.liveWindow.Closed += (sender, args) => Program.liveWindow = null;
				Program.liveWindow.Show();
			}

			Close();
		}

		private void DiscordLoginButtonClicked(object sender, RoutedEventArgs e)
		{
			OAuth.OAuthLogin(force: true);
		}
	}
}
