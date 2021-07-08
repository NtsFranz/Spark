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

namespace Spark
{
	/// <summary>
	/// Interaction logic for UploadTabletStatsMenu.xaml
	/// </summary>
	public partial class UploadTabletStatsMenu : Window
	{
		public UploadTabletStatsMenu(List<TabletStats> tabletStats)
		{
			InitializeComponent();

			tabletStats.ForEach(t =>
			{
				Grid grid = new Grid();

				grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(150) });
				grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });
				grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(30) });
				grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(115) });

				grid.Children.Add(new Border()
				{
					Child = new TextBlock()
					{
						Text = t.player_name

					},
					BorderThickness = new Thickness(1)
				});

				Border level = new Border()
				{
					Child = new TextBlock()
					{
						Text = t.level.ToString(),
						VerticalAlignment = VerticalAlignment.Center,
						HorizontalAlignment = HorizontalAlignment.Center,
					},
					BorderThickness = new Thickness(1),
				};
				Grid.SetColumn(level, 1);
				grid.Children.Add(level);

				Border checkbox = new Border()
				{
					Child = new CheckBox()
					{
						HorizontalAlignment = HorizontalAlignment.Center,
					},
					BorderThickness = new Thickness(1),
				};
				Grid.SetColumn(checkbox, 2);
				grid.Children.Add(checkbox);



				Button button = new Button()
				{
					Content = "Upload",
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Stretch,
					Height = 40,
				};
				button.Click += (sender, _) =>
				{
					uploadStatus.Text = "Uploading...";
					Program.UploadTabletStats(t, (success) =>
					{
						Dispatcher.Invoke(() =>
						{
							uploadStatus.Text = success ? "Success!" : "Failed";
							(sender as Button).Content = success ? "Uploaded" : "Failed";
						});
					});
				};
				Border buttonBorder = new Border()
				{
					Child = button,
					BorderThickness = new Thickness(1),
				};
				Grid.SetColumn(buttonBorder, 3);
				grid.Children.Add(buttonBorder);


				profilesList.Children.Add(grid);
			});
		}
	}
}
