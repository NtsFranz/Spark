using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;

namespace Spark
{
	/// <summary>
	/// Interaction logic for GameOverlay.xaml
	/// </summary>
	public partial class GameOverlay : Window
	{
		IntPtr targetWindow;
		bool uiVisible = true;
		bool minimapVisible = true;

		public GameOverlay()
		{
			InitializeComponent();

			// // add buttons for each player
			// for (int i = 1; i <= 4; i++)
			// {
			// 	Button button = MakeButton(i);
			// 	orangePlayerList.Children.Add(button);
			// }
			//
			// for (int i = 1; i <= 4; i++)
			// {
			// 	Button button = MakeButton(i + 5);
			// 	bluePlayerList.Children.Add(button);
			// }

			Process[] processes = Process.GetProcessesByName("echovr");
			// Process[] processes = Process.GetProcessesByName("Unity Hub");

			if (processes.Length < 1)
			{
				Close();
				return;
			}

			targetWindow = NativeMethods.Find(processes[0].ProcessName, processes[0].MainWindowTitle);

			// if window not found
			if (targetWindow.Equals(IntPtr.Zero))
			{
				Close();
				return;
			}

			NativeMethods.SetWindowPosition(this, targetWindow);


			Dictionary<AccessibleEvents, NativeMethods.WinEventProc> events = InitializeWinEventToHandlerMap();

			//initialize the first event to LocationChanged
			NativeMethods.WinEventProc eventHandler = new NativeMethods.WinEventProc(events[AccessibleEvents.LocationChange].Invoke);

			//When you use SetWinEventHook to set a callback in managed code, 
			//you should use the GCHandle
			//(Provides a way to access a managed object from unmanaged memory.) 
			//structure to avoid exceptions. 
			//This tells the garbage collector not to move the callback.
			GCHandle gch = GCHandle.Alloc(eventHandler);

			//Set Window Event Hool on Location changed.
			g_hook = NativeMethods.SetWinEventHook(AccessibleEvents.LocationChange,
				AccessibleEvents.LocationChange, IntPtr.Zero, eventHandler
				, 0, 0, NativeMethods.SetWinEventHookParameter.WINEVENT_OUTOFCONTEXT);

			//Hook window close event - close our HoverContorl on Target window close.
			// eventHandler = events[AccessibleEvents.Destroy].Invoke;
			//
			// gch = GCHandle.Alloc(eventHandler);
			//
			// g_hook = NativeMethods.SetWinEventHook(AccessibleEvents.Destroy,
			// 	AccessibleEvents.Destroy, IntPtr.Zero, eventHandler
			// 	, 0, 0, NativeMethods.SetWinEventHookParameter.WINEVENT_OUTOFCONTEXT);
		}

		private async void OnLoaded(object sender, RoutedEventArgs e)
		{
			try
			{
				string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "WebView");
				CoreWebView2Environment webView2Environment = await CoreWebView2Environment.CreateAsync(null, path);
				await WebView.EnsureCoreWebView2Async(webView2Environment);
				if (SparkSettings.instance.gameOverlayUrl.StartsWith('/'))
				{
					WebView.Source = new Uri("http://localhost:6724" + SparkSettings.instance.gameOverlayUrl);
				}
				else
				{
					WebView.Source = new Uri(SparkSettings.instance.gameOverlayUrl);
				}
			}
			catch (FileNotFoundException ex)
			{
				Logger.LogRow(Logger.LogType.Error, "1895: Failed to load WebView.\n" + ex);
				new MessageBox("Failed to load. Please report this to NtsFranz or else ┗|｀O′|┛").Show();
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, "1239: Failed to load WebView for an unknown reason.\n" + ex);
				new MessageBox("Failed to load. Please report this to NtsFranz. ( ╯□╰ )").Show();
			}
		}

		private static Button MakeButton(int i)
		{
			Button button = new Button()
			{
				Content = $"Player {i}",
				FontSize = 26,
				Height = 65,
				Background = new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)),
			};
			button.Click += (_, _) =>
			{
				CameraWriteController.SetCameraMode(CameraWriteController.CameraMode.pov, i);
				Program.FocusEchoVR();
			};
			return button;
		}

		private Dictionary<AccessibleEvents, NativeMethods.WinEventProc> InitializeWinEventToHandlerMap()
		{
			Dictionary<AccessibleEvents, NativeMethods.WinEventProc> dictionary = new();

			//You can add more events like ValueChanged - for more info please read - 
			//http://msdn.microsoft.com/en-us/library/system.windows.forms.accessibleevents.aspx
			//dictionary.Add(AccessibleEvents.ValueChange, new NativeMethods.WinEventProc(this.ValueChangedCallback));

			dictionary.Add(AccessibleEvents.LocationChange, new NativeMethods.WinEventProc(LocationChangedCallback));

			dictionary.Add(AccessibleEvents.Destroy, new NativeMethods.WinEventProc(DestroyCallback));

			return dictionary;
		}

		private void DestroyCallback(IntPtr winEventHookHandle,
			AccessibleEvents accEvent, IntPtr windowHandle, int objectId,
			int childId, uint eventThreadId, uint eventTimeInMilliseconds)
		{
			//Make sure AccessibleEvents equals to LocationChange and the 
			//current window is the Target Window.
			if (accEvent == AccessibleEvents.Destroy && windowHandle.ToInt32() ==
			    targetWindow.ToInt32())
			{
				//Queues a method for execution. The method executes when a thread pool 
				//thread becomes available.
				ThreadPool.QueueUserWorkItem(new WaitCallback(DestroyHelper));
			}
		}

		private void DestroyHelper(object state)
		{
			Dispatcher.Invoke(() =>
			{
				//Removes an event hook function created by a previous call to 
				NativeMethods.UnhookWinEvent(g_hook);
				//Close HoverControl window.
				Close();
			});
		}

		private void LocationChangedCallback(IntPtr winEventHookHandle,
			AccessibleEvents accEvent, IntPtr windowHandle, int objectId,
			int childId, uint eventThreadId, uint eventTimeInMilliseconds)
		{
			//Make sure AccessibleEvents equals to LocationChange and the 
			//current window is the Target Window.
			if (accEvent == AccessibleEvents.LocationChange && windowHandle.ToInt32() ==
			    targetWindow.ToInt32())
			{
				//Queues a method for execution. The method executes when a thread pool 
				//thread becomes available.
				ThreadPool.QueueUserWorkItem(new WaitCallback(LocationChangedHelper));
			}
		}

		private void LocationChangedHelper(object state)
		{
			Dispatcher.Invoke(() => { NativeMethods.SetWindowPosition(this, targetWindow); });
		}

		IntPtr g_hook;

		private void btn_set_event_Click(object sender, RoutedEventArgs e)
		{
			Dictionary<AccessibleEvents, NativeMethods.WinEventProc> events =
				InitializeWinEventToHandlerMap();

			//initialize the first event to LocationChanged
			NativeMethods.WinEventProc eventHandler =
				new NativeMethods.WinEventProc(events[AccessibleEvents.LocationChange].Invoke);

			//When you use SetWinEventHook to set a callback in managed code, 
			//you should use the GCHandle
			//(Provides a way to access a managed object from unmanaged memory.) 
			//structure to avoid exceptions. 
			//This tells the garbage collector not to move the callback.
			GCHandle gch = GCHandle.Alloc(eventHandler);

			//Set Window Event Hool on Location changed.
			g_hook = NativeMethods.SetWinEventHook(AccessibleEvents.LocationChange,
				AccessibleEvents.LocationChange, IntPtr.Zero, eventHandler
				, 0, 0, NativeMethods.SetWinEventHookParameter.WINEVENT_OUTOFCONTEXT);

			//Hook window close event - close our HoverContorl on Target window close.
			eventHandler = new NativeMethods.WinEventProc
				(events[AccessibleEvents.Destroy].Invoke);

			gch = GCHandle.Alloc(eventHandler);

			g_hook = NativeMethods.SetWinEventHook(AccessibleEvents.Destroy,
				AccessibleEvents.Destroy, IntPtr.Zero, eventHandler
				, 0, 0, NativeMethods.SetWinEventHookParameter.WINEVENT_OUTOFCONTEXT);

			//AccessibleEvents -> 
			//http://msdn.microsoft.com/en-us/library/system.windows.forms.accessibleevents.aspx
			//SetWinEventHookParameter -> 
			//http://msdn.microsoft.com/en-us/library/dd373640(VS.85).aspx
		}

		private void ToggleMap(object sender, RoutedEventArgs e)
		{
			CameraWriteController.SetMinimapVisibility(!minimapVisible);
			minimapVisible = !minimapVisible;
		}

		private void ToggleUI(object sender, RoutedEventArgs e)
		{
			CameraWriteController.SetUIVisibility(!uiVisible);
			uiVisible = !uiVisible;
		}

		private void GoToDischolder(object sender, RoutedEventArgs e)
		{
			CameraWriteController.SpectatorCamFindPlayer(Program.lastFrame?.GetAllPlayers()?.Find(p => p.possession)?.name);
		}
	}
}