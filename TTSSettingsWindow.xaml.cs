using IgniteBot2.Properties;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace IgniteBot2
{
	/// <summary>
	/// Interaction logic for TTSSettingsWindow.xaml
	/// </summary>
	public partial class TTSSettingsWindow : Window
	{
		public TTSSettingsWindow()
		{
			InitializeComponent();
		}

		private void TTSSettingsWindow_Load(object sender, RoutedEventArgs e)
		{
			joustTimeCheckbox.IsChecked = Settings.Default.joustTimeTTS;
			joustSpeedCheckbox.IsChecked = Settings.Default.joustSpeedTTS;
			serverLocationCheckbox.IsChecked = Settings.Default.serverLocationTTS;
			maxBoostSpeedCheckbox.IsChecked = Settings.Default.maxBoostSpeedTTS;
			tubeExitSpeedCheckbox.IsChecked = Settings.Default.tubeExitSpeedTTS;
			playerJoinCheckbox.IsChecked = Settings.Default.playerJoinTTS;
			playerLeaveCheckbox.IsChecked = Settings.Default.playerLeaveTTS;

			Console.WriteLine(speechSpeed.SelectedIndex);
			speechSpeed.SelectedIndex = Settings.Default.TTSSpeed;
			Console.WriteLine(speechSpeed.SelectedIndex);

			//_ = SetSelectIndex();

			//var t = Task.Run(async delegate
			//{
			//	await Task.Delay(1000);
			//});
				speechSpeed.SelectedIndex = Settings.Default.TTSSpeed;

			// Speak a string.  
			Program.synth.SpeakAsync("text to speech settings lol");
		}

		private void CloseButtonClick(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void JoustTimeClicked(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.joustTimeTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("orange 1.8");
		}

		private void JoustSpeedClicked(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.joustSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("orange 32 meters per second");
		}

		private void ServerLocationClicked(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.serverLocationTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("Chicago, Illinois");
		}

		private void MaxBoostClicked(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.maxBoostSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("32 meters per second");
		}

		private void TubeExitSpeedClicked(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.tubeExitSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("32 meters per second");
		}

		private void SpeechSpeedChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			var newVal = ((ComboBox)sender).SelectedIndex;
			Program.synth.Rate = (newVal - 1) * 4;

			if (newVal != Settings.Default.TTSSpeed)
				Program.synth.SpeakAsync("This is the new speed");

			Settings.Default.TTSSpeed = newVal;
			Settings.Default.Save();
		}

		private void PlayerJoinClicked(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.playerJoinTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("NtsFranz joined");
		}

		private void PlayerLeaveClicked(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.playerLeaveTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("NtsFranz left");
		}

		private void throwSpeedCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.throwSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("19");
		}

		private void goalSpeed_CheckedChanged(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.goalSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("19 meters per second");
		}

		private void goalDistance_CheckedChanged(object sender, RoutedEventArgs e)
		{
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.goalDistanceTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("23 meters");
		}
	}
}
