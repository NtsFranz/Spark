using IgniteBot.Properties;
using System;
using System.Windows;
using System.Windows.Controls;

namespace IgniteBot
{
	public enum HighlightLevel : int
	{
		CLIENT_ONLY,
		CLIENT_TEAM,
		ALL
	};

	/// <summary>
	/// Interaction logic for NVHighlightsSettingsWindow.xaml
	/// </summary>
	public partial class NVHighlightsSettingsWindow : Window
	{
		public NVHighlightsSettingsWindow()
		{
			InitializeComponent();
		}

		private void NVHighlightsSettingsWindow_Load(object sender, RoutedEventArgs e)
		{
			Program.isNVHighlightsEnabled &= Program.isNVHighlightsSupported;
			Settings.Default.isNVHighlightsEnabled = Program.isNVHighlightsEnabled;   // This shouldn't change anything
			Settings.Default.Save();

			enableNVHighlightsCheckbox.IsChecked = Program.isNVHighlightsEnabled;
			clearHighlightsOnExitCheckbox.IsChecked = Settings.Default.clearHighlightsOnExit;
			highlightScope.SelectedIndex = Settings.Default.clientHighlightScope;
			clearHighlightsButton.IsEnabled = Program.DoNVClipsExist();

			enableNVHighlightsCheckbox.IsEnabled = Program.isNVHighlightsSupported;
			enableNVHighlightsCheckbox.Content = Program.isNVHighlightsSupported ? "Enable NVIDIA Highlights" : "NVIDIA Highlights isn't supported by your PC";


			nvHighlightsBox.IsEnabled = Program.isNVHighlightsEnabled;
			nvHighlightsBox.Opacity = Program.isNVHighlightsEnabled ? 1 : .5;
			clearHighlightsButton.Content = "Clear " + Program.nvHighlightClipCount + " Unsaved Highlights";

			Console.WriteLine(highlightScope.SelectedIndex);
		}

		private void HighlightScopeChanged(object sender, SelectionChangedEventArgs e)
		{
			int index = ((ComboBox)sender).SelectedIndex;
			Program.ClientHighlightScope = (HighlightLevel)index;
			Settings.Default.clientHighlightScope = index;
			Settings.Default.Save();
		}

		private void ClearHighlightsOnExitEvent(object sender, RoutedEventArgs e)
		{
			Program.clearHighlightsOnExit = ((CheckBox)sender).IsChecked == true;
			Settings.Default.clearHighlightsOnExit = Program.clearHighlightsOnExit;
			Settings.Default.Save();

			clearHighlightsOnExitCheckbox.IsEnabled = Program.clearHighlightsOnExit;
		}

		private void EnableNVHighlightsEvent(object sender, RoutedEventArgs e)
		{
			if (Program.isNVHighlightsEnabled && !((CheckBox)sender).IsChecked == true)
			{
				Program.CloseNVHighlights(true);
			}
			else if (!Program.isNVHighlightsEnabled && ((CheckBox)sender).IsChecked == true)
			{
				if (Program.SetupNVHighlights() < 0)
				{
					Program.isNVHighlightsEnabled = false;
					Settings.Default.isNVHighlightsEnabled = false;
					Settings.Default.Save();
					enableNVHighlightsCheckbox.IsChecked = false;
					enableNVHighlightsCheckbox.IsEnabled = false;
					enableNVHighlightsCheckbox.Content = "NVIDIA Highlights failed to initialize or isn't supported by your PC";
					return;
				}
				else
				{
					enableNVHighlightsCheckbox.Content = "Enable NVIDIA Highlights";
				}
			}

			Program.isNVHighlightsEnabled = ((CheckBox)sender).IsChecked == true;
			Settings.Default.isNVHighlightsEnabled = Program.isNVHighlightsEnabled;
			Settings.Default.Save();

			nvHighlightsBox.IsEnabled = Program.isNVHighlightsEnabled;
			nvHighlightsBox.Opacity = Program.isNVHighlightsEnabled ? 1 : .5;
		}

		private void ClearHighlightsEvent(object sender, RoutedEventArgs e)
		{
			Program.ClearUnsavedNVHighlights(true);
			clearHighlightsButton.IsEnabled = false;
			clearHighlightsButton.Content = "Clear 0 Unsaved Highlights";
			new MessageBox("Highlights Cleared: All unsaved highlights have been cleared from the temporary highlights directory.").Show();
		}

		private void CloseButtonClick(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}