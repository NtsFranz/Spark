using Microsoft.Toolkit.Uwp.Notifications;

namespace Spark
{
	public class Notifications
	{
		public static void ShowNotification()
		{
			new ToastContentBuilder()
				.AddText("Something copied to clipboard")
				.Show();
		}
	}
}