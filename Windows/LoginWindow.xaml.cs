using System.Windows;

namespace Spark
{
	/// <summary>
	/// Interaction logic for LoginWindow.xaml
	/// </summary>
	public partial class LoginWindow : Window
	{
		public LoginWindow()
		{
			InitializeComponent();

			Refresh();
			DiscordOAuth.Authenticated += Refresh;
		}

		~LoginWindow()
		{
			DiscordOAuth.Authenticated -= Refresh;
		}

		public void Refresh()
		{
			Dispatcher.Invoke(() =>
			{
				accessCodeComboBox.Items.Clear();
				foreach (DiscordOAuth.AccessCodeKey code in DiscordOAuth.availableAccessCodes)
				{
					accessCodeComboBox.Items.Add(code.username);
				}

				// if not logged in with discord
				if (!accessCodeComboBox.Items.Contains("Personal")) accessCodeComboBox.Items.Add("Personal");

				accessCodeComboBox.SelectedIndex = DiscordOAuth.GetAccessCodeIndexByHash(SparkSettings.instance.accessCode);

				if (string.IsNullOrEmpty(DiscordOAuth.DiscordUsername))
				{
					currentUsernameLabel.Content = "Not logged in";
					discordLoginButton.Content = "Discord Login";
				}
				else
				{
					currentUsernameLabel.Content = "Logged in as:\n" + DiscordOAuth.DiscordUsername;
					discordLoginButton.Content = "Unlink Discord";
				}
			});
		}

		private void StartButtonClicked(object sender, RoutedEventArgs e)
		{
			string username = accessCodeComboBox.SelectedValue.ToString();
			DiscordOAuth.SetAccessCodeByUsername(username);

			Close();
			
			Program.liveWindow.Activate();
		}

		private void DiscordLoginButtonClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(DiscordOAuth.DiscordUsername))
			{
				DiscordOAuth.OAuthLogin(force: true);
			}
			else
			{
				DiscordOAuth.Unlink();
			}

			Refresh();

			string username = accessCodeComboBox.SelectedValue.ToString();
			DiscordOAuth.SetAccessCodeByUsername(username);
		}
	}
}
