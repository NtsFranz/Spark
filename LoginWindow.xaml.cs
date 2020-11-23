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

			UpdateAccessCodesDropdown();
		}

		public void UpdateAccessCodesDropdown()
		{
			Dispatcher.Invoke(() =>
			{
				accessCodeComboBox.Items.Clear();
				accessCodeComboBox.Items.Add("Personal");
				foreach (Dictionary<string, string> code in DiscordOAuth.availableAccessCodes)
				{
					accessCodeComboBox.Items.Add(code["username"]);
				}
				accessCodeComboBox.SelectedIndex = 0;

				currentUsernameLabel.Content = "Logged in as:\n" + DiscordOAuth.DiscordUsername;
			});
		}

		private void StartButtonClicked(object sender, RoutedEventArgs e)
		{
			Program.authorized = true;
			Settings.Default.accessMode = accessCodeComboBox.SelectedValue.ToString();
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
			DiscordOAuth.OAuthLogin(force: true);
		}

		private void AccessCodeChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			string chosenCode = accessCodeComboBox.SelectedValue.ToString();
			string seasonName = "";
			if (chosenCode == "Personal")
			{
				seasonName = "personal";
			}
			else
			{
				foreach (var code in DiscordOAuth.availableAccessCodes)
				{
					if (code["username"] == chosenCode)
					{
						seasonName = code["season_name"];
						break;
					}
				}
			}

			Program.currentAccessCodeUsername = chosenCode;
			Program.currentSeasonName = seasonName;
		}
	}
}
