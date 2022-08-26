using System;
using System.Windows;

namespace Spark
{
	public static class ThemesController
	{
		public enum ThemeTypes
		{
			YellowDark,
			OrangeDark,
			RedDark
		}

		public static ThemeTypes CurrentTheme { get; set; }

		private static ResourceDictionary ThemeDictionary
		{
			get { return Application.Current.Resources.MergedDictionaries[0]; }
			set { Application.Current.Resources.MergedDictionaries[0] = value; }
		}

		private static void ChangeTheme(Uri uri)
		{
			ThemeDictionary = new ResourceDictionary { Source = uri };
		}

		public static void SetTheme(ThemeTypes theme)
		{
			CurrentTheme = theme;
			string themeName = theme switch
			{
				ThemeTypes.YellowDark => "ColourfulDarkTheme_Yellow",
				ThemeTypes.OrangeDark => "ColourfulDarkTheme_Orange",
				ThemeTypes.RedDark => "ColourfulDarkTheme_Red",
				_ => null
			};

			try
			{
				if (!string.IsNullOrEmpty(themeName))
				{
					ChangeTheme(new Uri($"Themes/{themeName}.xaml", UriKind.Relative));
				}
			}
			catch (Exception e)
			{
				Logger.Error($"Error changing the theme.\n{e}");
				// ignored
			}
		}
	}
}