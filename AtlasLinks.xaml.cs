using System.Windows;

namespace IgniteBot2
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
		}

		private void closeButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
