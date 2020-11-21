using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
