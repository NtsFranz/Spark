using System;
using System.Windows;

namespace Spark
{
	public static class ThemesController
	{
		public enum ThemeTypes
		{
			YellowDark, OrangeDark, RedDark
		}

		public static ThemeTypes CurrentTheme { get; set; }

		private static ResourceDictionary ThemeDictionary {
			get { return Application.Current.Resources.MergedDictionaries[0]; }
			set { Application.Current.Resources.MergedDictionaries[0] = value; }
		}

		private static void ChangeTheme(Uri uri)
		{
			ThemeDictionary = new ResourceDictionary { Source = uri };
		}

		public static void SetTheme(ThemeTypes theme)
		{
			string themeName = null;
			CurrentTheme = theme;
			switch (theme)
			{
				case ThemeTypes.YellowDark: themeName = "ColourfulDarkTheme_Yellow"; break;
				case ThemeTypes.OrangeDark: themeName = "ColourfulDarkTheme_Orange"; break;
				case ThemeTypes.RedDark: themeName = "ColourfulDarkTheme_Red"; break;
			}

			try
			{
				if (!string.IsNullOrEmpty(themeName))
					ChangeTheme(new Uri($"Themes/{themeName}.xaml", UriKind.Relative));
			}
			catch { }
		}
	}
}
