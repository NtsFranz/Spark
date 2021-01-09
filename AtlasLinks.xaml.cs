using IgniteBot.Properties;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for AtlasLinks.xaml
	/// </summary>
	public partial class AtlasLinks : Window
	{
		public AtlasLinks()
		{
			InitializeComponent();

			if (Program.lastFrame != null)
			{
				joinLink.Text = "<atlas://j/" + Program.lastFrame.sessionid + ">";
				spectateLink.Text = "<atlas://s/" + Program.lastFrame.sessionid + ">";
				chooseLink.Text = "<ignitebot://choose/" + Program.lastFrame.sessionid + ">";
			}

			linksFromLabel.Content = $"Links from: {Settings.Default.echoVRIP}";

			alternateIPTextBox.Text = Settings.Default.alternateEchoVRIP;
		}

		public void GetLinks(object sender, RoutedEventArgs e)
		{
			string ip = alternateIPTextBox.Text;
			Task.Run(() => GetAsync($"http://{ip}:6721/session", (responseJSON) =>
			{
				try
				{
					g_InstanceSimple obj = JsonConvert.DeserializeObject<g_InstanceSimple>(responseJSON);

					if (obj != null && !string.IsNullOrEmpty(obj.sessionid))
					{
						Dispatcher.Invoke(() =>
						{
							joinLink.Text = "<atlas://j/" + obj.sessionid + ">";
							spectateLink.Text = "<atlas://s/" + obj.sessionid + ">";
							chooseLink.Text = "<ignitebot://choose/" + obj.sessionid + ">";

							linksFromLabel.Content = $"Links from: {alternateIPTextBox.Text}";
							Settings.Default.alternateEchoVRIP = alternateIPTextBox.Text;
							Settings.Default.Save();
						});
					}

				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"Can't parse response\n{e}");
				}
			}));
		}

		public static async Task GetAsync(string uri, Action<string> callback)
		{
			try
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
				using HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();
				using Stream stream = response.GetResponseStream();
				using StreamReader reader = new StreamReader(stream);

				callback(await reader.ReadToEndAsync());
			}
			catch (Exception e)
			{
				Console.WriteLine($"Can't get data\n{e}");
				callback("");
			}
		}

		private void closeButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
