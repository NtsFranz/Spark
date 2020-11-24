using IgniteBot2.Properties;
using System.Collections.Generic;
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

			Refresh();
		}

		public void Refresh()
		{
			Dispatcher.Invoke(() =>
			{
				accessCodeComboBox.Items.Clear();
				foreach (Dictionary<string, string> code in DiscordOAuth.availableAccessCodes)
				{
					accessCodeComboBox.Items.Add(code["username"]);
				}
				// if not logged in with discord
				if (!accessCodeComboBox.Items.Contains("Personal")) accessCodeComboBox.Items.Add("Personal");

				accessCodeComboBox.SelectedIndex = DiscordOAuth.GetAccessCodeIndex(Settings.Default.accessCode);

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
			Settings.Default.accessCode = SecretKeys.Hash(DiscordOAuth.GetAccessCode(username));
			Program.currentAccessCodeUsername = username;
			Program.currentSeasonName = DiscordOAuth.GetSeasonName(username);
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
			if (string.IsNullOrEmpty(DiscordOAuth.DiscordUsername))
			{
				DiscordOAuth.OAuthLogin(force: true);
			}
			else
			{
				DiscordOAuth.Unlink();
			}

			Refresh();
		}
	}
}
