using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

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
			(ManualTeamNameOrange.Text, ManualTeamNameBlue.Text) = (ManualTeamNameBlue.Text, ManualTeamNameOrange.Text);
			(ManualTeamLogoOrange.Text, ManualTeamLogoBlue.Text) = (ManualTeamLogoBlue.Text, ManualTeamLogoOrange.Text);

			SparkSettings.instance.overlaysManualTeamNameOrange = ManualTeamNameOrange.Text;
			SparkSettings.instance.overlaysManualTeamNameBlue = ManualTeamNameBlue.Text;
			SparkSettings.instance.overlaysManualTeamLogoOrange = ManualTeamLogoOrange.Text;
			SparkSettings.instance.overlaysManualTeamLogoBlue = ManualTeamLogoBlue.Text;
			Program.OverlayConfigChanged?.Invoke();
		}

		public void SetUIToSettings()
		{
			ManualTeamNameOrange.Text = SparkSettings.instance.overlaysManualTeamNameOrange;
			ManualTeamNameBlue.Text = SparkSettings.instance.overlaysManualTeamNameBlue;
			ManualTeamLogoOrange.Text = SparkSettings.instance.overlaysManualTeamLogoOrange;
			ManualTeamLogoBlue.Text = SparkSettings.instance.overlaysManualTeamLogoBlue;
		}

		private void TeamsDataSourceChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!init) return;
			ComboBox dropdown = (ComboBox)sender;
			ManualInputSettings.Visibility = dropdown.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
			Program.OverlayConfigChanged?.Invoke();
		}

		private void TeamNameChanged(object sender, TextChangedEventArgs e)
		{
			Program.OverlayConfigChanged?.Invoke();
		}
	}
}
