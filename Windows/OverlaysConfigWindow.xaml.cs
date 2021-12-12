using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Spark
{
	/// <summary>
	/// Interaction logic for OverlaysConfigWindow.xaml
	/// </summary>
	public partial class OverlaysConfigWindow : UserControl
	{
		public Visibility ManualInputVisible { get => SparkSettings.instance.overlaysTeamSource == 0 ? Visibility.Visible : Visibility.Collapsed; }
		private bool init;
		public OverlaysConfigWindow()
		{
			InitializeComponent();
			init = true;
		}

		private void OpenOverlaysMainPage(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo("http://localhost:6724/") { UseShellExecute = true });
				e.Handled = true;
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, ex.ToString());
			}
		}

		private void SwapTeamSettings(object sender, RoutedEventArgs e)
		{
			string tempName = ManualTeamNameOrange.Text;
			ManualTeamNameOrange.Text = ManualTeamNameBlue.Text;
			ManualTeamNameBlue.Text = tempName;

			string tempLogo = ManualTeamLogoOrange.Text;
			ManualTeamLogoOrange.Text = ManualTeamLogoBlue.Text;
			ManualTeamLogoBlue.Text = tempLogo;

			SparkSettings.instance.overlaysManualTeamNameOrange = ManualTeamNameOrange.Text;
			SparkSettings.instance.overlaysManualTeamNameBlue = ManualTeamNameBlue.Text;
			SparkSettings.instance.overlaysManualTeamLogoOrange = ManualTeamLogoOrange.Text;
			SparkSettings.instance.overlaysManualTeamLogoBlue = ManualTeamLogoBlue.Text;
		}

		private void TeamsDataSourceChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!init) return;
			ComboBox dropdown = (ComboBox)sender;
			ManualInputSettings.Visibility = dropdown.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
		}
	}
}
