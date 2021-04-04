using System.Windows;

namespace Spark
{
	/// <summary>
	/// Interaction logic for UpdateWindow.xaml
	/// </summary>
	public partial class UpdateWindow : Window
	{
		public UpdateWindow(string version, string changelong)
		{
			InitializeComponent();

			textBlock.Text = changelong;
		}

		private void ButtonClicked(object sender, RoutedEventArgs e)
		{
			Hide();
		}
	}
}