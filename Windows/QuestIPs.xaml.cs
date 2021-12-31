using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EchoVRAPI;

namespace Spark
{
	public partial class QuestIPs : Window
	{
		public QuestIPs()
		{
			InitializeComponent();
			FindQuestIPs();
		}


		private void CloseButtonClicked(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void RefreshClicked(object sender, RoutedEventArgs e)
		{
			FindQuestIPs();
		}

		private void FindQuestIPs()
		{
			Task.Run(async () =>
			{
				List<IPAddress> ips = await QuestIPFetching.FindAllQuestIPs();

				QuestIPsBox.Text = string.Join('\n', ips.Select(ip => ip.ToString()));
			});
		}
	}
}