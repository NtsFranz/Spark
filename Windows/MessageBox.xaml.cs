using System;
using System.Windows;

namespace Spark
{
	/// <summary>
	/// Interaction logic for MessageBox.xaml
	/// </summary>
	public partial class MessageBox : Window
	{
		private readonly Action callback = null;

		public MessageBox(string message, string title = null, Action callback = null)
		{
			InitializeComponent();

			textBlock.Text = message;
			if (!string.IsNullOrEmpty(title))
			{
				Title = title;
			}
			this.callback = callback;
		}

		private void ButtonClicked(object sender, RoutedEventArgs e)
		{
			callback?.Invoke();
			Hide();
		}
	}
}
