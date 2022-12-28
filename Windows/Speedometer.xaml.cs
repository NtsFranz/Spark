using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using Timer = System.Timers.Timer;

namespace Spark
{
	/// <summary>
	/// Interaction logic for Speedometer.xaml
	/// </summary>
	public partial class Speedometer : Window
	{
		public enum GameVersion
		{
			EchoVR,
			LoneEcho1,
			LoneEcho2
		}
		
		// private string[] gameExeNames =
		// {
		// 	"echovr.exe",
		// 	"loneecho.exe",
		// 	"loneecho2.exe",
		// };

		public int GameVersionDropdownValue
		{
			get => SparkSettings.instance.speedometerGameVersion;
			set => SparkSettings.instance.speedometerGameVersion = value;
		}

		private readonly Timer outputUpdateTimer = new Timer();

		public Speedometer()
		{
			InitializeComponent();


			Grid.Background = SparkSettings.instance.loneEchoSubtitlesStreamerMode ? Brushes.Green : Brushes.Black;

			outputUpdateTimer.Interval = 100;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;
		}

		private async void Update(object sender, EventArgs e)
		{
			try
			{
				float speed = await FetchSpeed();

				Dispatcher.Invoke(() => { CurrentSpeedText.Text = speed.ToString("N2") + " m/s"; });
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error in Speedometer Update.\n{ex}");
			}
		}

		private static async Task<float> FetchSpeed()
		{
			switch ((GameVersion) SparkSettings.instance.speedometerGameVersion)
			{
				case GameVersion.EchoVR:
					return Program.lastFrame?.GetPlayer(Program.lastFrame.client_name)?.velocity.ToVector3().Length() ??
					       -1;
				case GameVersion.LoneEcho1:
					try
					{
						string resp = await FetchUtils.GetRequestAsync(Program.WRITE_API_URL + "le1/speed", null);
						Debug.WriteLine(resp);
						return (float) JObject.Parse(resp)["speed"];
					}
					catch (Exception)
					{
						return -1;
					}
				case GameVersion.LoneEcho2:
					try
					{
						string resp = await FetchUtils.GetRequestAsync(Program.WRITE_API_URL + "le2/speed", null);
						Debug.WriteLine(resp);
						return (float) JObject.Parse(resp)["speed"];
					}
					catch (Exception)
					{
						return -1;
					}
				default:
					return -1;
			}
		}
	}
}