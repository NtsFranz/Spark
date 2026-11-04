using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;

namespace Spark
{
	/// <summary>
	/// Drives the "Spectator overlay" toggle in the Live window. While it is on, the overlay picked in the
	/// Live window (SparkSettings.gameOverlayUrl: a built-in one, the player's own file, or any URL) sits
	/// over the Echo VR window; while it is off, nothing is shown. Built-in full-screen overlays also hide
	/// Echo's own spectator HUD while they show, and give it back when they close.
	/// </summary>
	public class SpectatorOverlayController
	{
		private int checkedPid = -1;
		private bool pidIsSpectatorStream;
		private bool hudHidden;
		private bool weOpenedOverlay;

		public SpectatorOverlayController()
		{
			// the game resets its HUD on every load, so it has to be hidden again after each join
			Program.JoinedGame += _ => hudHidden = false;
			Program.LeftGame += _ => hudHidden = false;
			_ = Loop();
		}

		private async Task Loop()
		{
			while (Program.running)
			{
				Refresh();
				await Task.Delay(2000);
			}
		}

		/// <summary>Applies the current toggle state now. Safe to call from any thread.</summary>
		public void Refresh()
		{
			try
			{
				Application app = Application.Current;
				if (app == null) return;
				if (!app.Dispatcher.CheckAccess())
				{
					app.Dispatcher.BeginInvoke(new Action(Refresh));
					return;
				}

				bool enabled = SparkSettings.instance.spectatorOverlayEnabled;
				Process echo = enabled
					? Process.GetProcessesByName("echovr").FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero)
					: null;
				bool want = echo != null;
				bool open = Program.GetWindowIfOpen(typeof(GameOverlay)) != null;

				if (want && !open)
				{
					Program.ToggleWindow(typeof(GameOverlay));
					weOpenedOverlay = Program.GetWindowIfOpen(typeof(GameOverlay)) != null;
				}
				else if (!want && open && weOpenedOverlay)
				{
					Program.ToggleWindow(typeof(GameOverlay));
					weOpenedOverlay = false;
				}

				bool hideHud = want && ReplacesGameHud(SparkSettings.instance.gameOverlayUrl);
				if (!hideHud && hudHidden)
				{
					// toggled off: give the game its own HUD back
					if (Program.InGame)
					{
						CameraWriteController.SetUIVisibility(true);
						CameraWriteController.SetMinimapVisibility(true);
					}
					hudHidden = false;
				}
				else if (hideHud && weOpenedOverlay && !hudHidden && Program.InGame &&
				         SparkSettings.instance.spectatorStreamHideGameHud && IsSpectatorStream(echo))
				{
					CameraWriteController.SetUIVisibility(false);
					CameraWriteController.SetMinimapVisibility(false);
					hudHidden = true;
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"SpectatorOverlayController: {e}");
			}
		}

		/// <summary>Closes the overlay and reopens it with the currently picked URL.</summary>
		public void Reload()
		{
			Application app = Application.Current;
			if (app == null) return;
			app.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (weOpenedOverlay && Program.GetWindowIfOpen(typeof(GameOverlay)) != null)
				{
					Program.ToggleWindow(typeof(GameOverlay));
					weOpenedOverlay = false;
				}
				Refresh();
			}));
		}

		/// <summary>The built-in overlays that draw their own scoreboard and minimap.</summary>
		private static bool ReplacesGameHud(string url)
		{
			string u = (url ?? "").Trim().TrimEnd('/').ToLowerInvariant();
			return new[] { "/arena_overlay", "/configurable_overlay", "/full_overlay" }
				.Any(b => u == b || u == "http://localhost:6724" + b || u == "http://127.0.0.1:6724" + b);
		}

		private bool IsSpectatorStream(Process process)
		{
			if (process.Id == checkedPid) return pidIsSpectatorStream;
			checkedPid = process.Id;
			pidIsSpectatorStream = false;
			try
			{
				using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
				foreach (ManagementBaseObject o in searcher.Get())
				{
					string cmd = o["CommandLine"]?.ToString() ?? "";
					pidIsSpectatorStream = cmd.Contains("-spectatorstream", StringComparison.OrdinalIgnoreCase);
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Couldn't read Echo VR command line: {e.Message}");
			}

			return pidIsSpectatorStream;
		}
	}
}
