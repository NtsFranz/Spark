using IgniteBot.Properties;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
			HighlightsHelper.isNVHighlightsEnabled &= HighlightsHelper.isNVHighlightsSupported;
			Settings.Default.isNVHighlightsEnabled = HighlightsHelper.isNVHighlightsEnabled;   // This shouldn't change anything
			Settings.Default.Save();

			secondsBefore.Text = Settings.Default.nvHighlightsSecondsBefore.ToString();
			secondsAfter.Text = Settings.Default.nvHighlightsSecondsAfter.ToString();
			enableNVHighlightsCheckbox.IsChecked = HighlightsHelper.isNVHighlightsEnabled;
			clearHighlightsOnExitCheckbox.IsChecked = Settings.Default.clearHighlightsOnExit;
			highlightScope.SelectedIndex = Settings.Default.clientHighlightScope;
			clearHighlightsButton.IsEnabled = HighlightsHelper.DoNVClipsExist();

			enableNVHighlightsCheckbox.IsEnabled = HighlightsHelper.isNVHighlightsSupported;
			enableNVHighlightsCheckbox.Content = HighlightsHelper.isNVHighlightsSupported
				? "Enable NVIDIA Highlights"
				: "NVIDIA Highlights isn't supported by your PC";


			nvHighlightsBox.IsEnabled = HighlightsHelper.isNVHighlightsEnabled;
			nvHighlightsBox.Opacity = HighlightsHelper.isNVHighlightsEnabled ? 1 : .5;
			secondsBefore.Text = Settings.Default.nvHighlightsSecondsBefore.ToString();
			secondsAfter.Text = Settings.Default.nvHighlightsSecondsAfter.ToString();
			clearHighlightsButton.Content = $"Clear {HighlightsHelper.nvHighlightClipCount} Unsaved Highlights";

			Console.WriteLine(highlightScope.SelectedIndex);
		}

		private void HighlightScopeChanged(object sender, SelectionChangedEventArgs e)
		{
			int index = ((ComboBox)sender).SelectedIndex;
			HighlightsHelper.ClientHighlightScope = (HighlightLevel)index;
			Settings.Default.clientHighlightScope = index;
			Settings.Default.Save();
		}

		private void ClearHighlightsOnExitEvent(object sender, RoutedEventArgs e)
		{
			HighlightsHelper.clearHighlightsOnExit = ((CheckBox)sender).IsChecked == true;
			Settings.Default.clearHighlightsOnExit = HighlightsHelper.clearHighlightsOnExit;
			Settings.Default.Save();

			clearHighlightsOnExitCheckbox.IsEnabled = HighlightsHelper.clearHighlightsOnExit;
		}

		private void EnableNVHighlightsEvent(object sender, RoutedEventArgs e)
		{
			if (HighlightsHelper.isNVHighlightsEnabled && !((CheckBox)sender).IsChecked == true)
			{
				HighlightsHelper.CloseNVHighlights(true);
			}
			else if (!HighlightsHelper.isNVHighlightsEnabled && ((CheckBox)sender).IsChecked == true)
			{
				if (HighlightsHelper.SetupNVHighlights() < 0)
				{
					HighlightsHelper.isNVHighlightsEnabled = false;
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

			HighlightsHelper.isNVHighlightsEnabled = ((CheckBox)sender).IsChecked == true;
			Settings.Default.isNVHighlightsEnabled = HighlightsHelper.isNVHighlightsEnabled;
			Settings.Default.Save();

			nvHighlightsBox.IsEnabled = HighlightsHelper.isNVHighlightsEnabled;
			nvHighlightsBox.Opacity = HighlightsHelper.isNVHighlightsEnabled ? 1 : .5;
		}

		private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}

		private void ClearHighlightsEvent(object sender, RoutedEventArgs e)
		{
			HighlightsHelper.ClearUnsavedNVHighlights(true);
			clearHighlightsButton.IsEnabled = false;
			clearHighlightsButton.Content = "Clear 0 Unsaved Highlights";
			new MessageBox("Highlights Cleared: All unsaved highlights have been cleared from the temporary highlights directory.").Show();
		}

		private void CloseButtonClick(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void SecondsBeforeChanged(object sender, TextChangedEventArgs e)
		{
			if (float.TryParse(((TextBox)sender).Text, out float value))
			{
				Settings.Default.nvHighlightsSecondsBefore = value;
				Settings.Default.Save();
			}
		}

		private void SecondsAfterChanged(object sender, TextChangedEventArgs e)
		{
			if (float.TryParse(((TextBox)sender).Text, out float value))
			{
				Settings.Default.nvHighlightsSecondsAfter = value;
				Settings.Default.Save();
			}
		}
	}
}