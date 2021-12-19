using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace Spark
{
	class NativeMethods
	{
		// Get a handle to an application window.
		[DllImport("user32.dll", SetLastError = true)]
		internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		// Find window by Caption only. Note you must pass IntPtr.Zero as the first parameter.
		[DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
		internal static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

		[return: MarshalAs(UnmanagedType.Bool)]
		[DllImport("user32.dll")]
		internal static extern bool GetTitleBarInfo(IntPtr hwnd, ref TITLEBARINFO pti);

		//GetLastError- retrieves the last system error.
		[DllImport("coredll.dll", SetLastError = true)]
		internal static extern Int32 GetLastError();

		[DllImport("User32.dll")]
		static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool redraw);

		[DllImport("user32.dll")]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		internal delegate int WindowEnumProc(IntPtr hwnd, IntPtr lparam);
		[DllImport("user32.dll")]
		internal static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc func, IntPtr lParam);

		[DllImport("user32.dll")]
		static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		[DllImport("user32.dll", EntryPoint = "SetWindowLongA", SetLastError = true)]
		public static extern long SetWindowLong(IntPtr hwnd, int nIndex, long dwNewLong);

		[DllImport("user32.dll")]
		private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		internal static extern IntPtr SetWinEventHook(
			AccessibleEvents eventMin,  //Specifies the event constant for the 
										//lowest event value in the range of events that are 
										//handled by the hook function. This parameter can 
										//be set to EVENT_MIN to indicate the 
										//lowest possible event value.
			AccessibleEvents eventMax,  //Specifies the event constant for the highest event 
										//value in the range of events that are handled 
										//by the hook function. This parameter can be set 
										//to EVENT_MAX to indicate the highest possible 
										//event value.
			IntPtr eventHookAssemblyHandle,     //Handle to the DLL that contains the hook 
												//function at lpfnWinEventProc, if the 
												//WINEVENT_INCONTEXT flag is specified in the 
												//dwFlags parameter. If the hook function is not 
												//located in a DLL, or if the WINEVENT_OUTOFCONTEXT 
												//flag is specified, this parameter is NULL.
			WinEventProc eventHookHandle,   //Pointer to the event hook function. 
											//For more information about this function
			uint processId,         //Specifies the ID of the process from which the 
									//hook function receives events. Specify zero (0) 
									//to receive events from all processes on the 
									//current desktop.
			uint threadId,          //Specifies the ID of the thread from which the 
									//hook function receives events. 
									//If this parameter is zero, the hook function is 
									//associated with all existing threads on the 
									//current desktop.
			SetWinEventHookParameter parameterFlags //Flag values that specify the location 
													//of the hook function and of the events to be 
													//skipped. The following flags are valid:
		);

		internal delegate void WinEventProc(IntPtr winEventHookHandle, AccessibleEvents accEvent,
			IntPtr windowHandle, int objectId, int childId, uint eventThreadId,
			uint eventTimeInMilliseconds);

		[DllImport("user32.dll")]
		internal static extern IntPtr SetFocus(IntPtr hWnd);

		[Flags]
		internal enum SetWinEventHookParameter
		{
			WINEVENT_INCONTEXT = 4,
			WINEVENT_OUTOFCONTEXT = 0,
			WINEVENT_SKIPOWNPROCESS = 2,
			WINEVENT_SKIPOWNTHREAD = 1
		}

		[return: MarshalAs(UnmanagedType.Bool)]
		[DllImport("user32.dll")]
		internal static extern bool UnhookWinEvent(IntPtr eventHookHandle);


		[StructLayout(LayoutKind.Sequential)]
		internal struct TITLEBARINFO
		{
			public const int CCHILDREN_TITLEBAR = 5;
			public uint cbSize; //Specifies the size, in bytes, of the structure. 
								//The caller must set this to sizeof(TITLEBARINFO).

			public RECT rcTitleBar; //Pointer to a RECT structure that receives the 
									//coordinates of the title bar. These coordinates include all title-bar elements
									//except the window menu.

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]

			//Add reference for System.Windows.Forms
			public AccessibleStates[] rgstate;
			//0    The title bar itself.
			//1    Reserved.
			//2    Minimize button.
			//3    Maximize button.
			//4    Help button.
			//5    Close button.
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;        // x position of upper-left corner
			public int Top;         // y position of upper-left corner
			public int Right;       // x position of lower-right corner
			public int Bottom;      // y position of lower-right corner
		}

		//Specifies the zero-based offset to the value to be set.
		//Valid values are in the range zero through the number of bytes of extra window memory, 
		//minus the size of an integer.
		public enum GWLParameter
		{
			GWL_EXSTYLE = -20, //Sets a new extended window style
			GWL_HINSTANCE = -6, //Sets a new application instance handle.
			GWL_HWNDPARENT = -8, //Set window handle as parent
			GWL_ID = -12, //Sets a new identifier of the window.
			GWL_STYLE = -16, // Set new window style
			GWL_USERDATA = -21, //Sets the user data associated with the window. 
								//This data is intended for use by the application 
								//that created the window. Its value is initially zero.
			GWL_WNDPROC = -4 //Sets a new address for the window procedure.
		}


		#region Helpers
		public static IntPtr Find(string ModuleName, string MainWindowTitle)
		{
			//Search the window using Module and Title
			IntPtr WndToFind = FindWindow(ModuleName, MainWindowTitle);
			if (WndToFind.Equals(IntPtr.Zero))
			{
				if (!string.IsNullOrEmpty(MainWindowTitle))
				{
					//Search window using TItle only.
					WndToFind = FindWindowByCaption(WndToFind, MainWindowTitle);
					if (WndToFind.Equals(IntPtr.Zero))
						return new IntPtr(0);
				}
			}
			return WndToFind;
		}

		public static RECT? GetWindowPosition(IntPtr wnd)
		{
			//RECT rect = new RECT();
			bool result = GetWindowRect(wnd, out RECT rect);

			return result ? rect : null;
		}

		public static void SetWindowPosition(Window window, IntPtr targetWindow)
		{
			// get title bar info
			RECT? pos = GetWindowPosition(targetWindow);

			if (pos == null) return;

			//Search for HoverControl handle
			IntPtr onTopHandle = Find(window.Name, window.Title);

			//Set the new location of the control (on top the titlebar)
			window.Left = pos.Value.Left;
			window.Top = pos.Value.Top;
			window.Width = pos.Value.Right - pos.Value.Left;
			window.Height = pos.Value.Bottom - pos.Value.Top;

			//Change target window to be parent of HoverControl.
			SetWindowLong(onTopHandle, (int)GWLParameter.GWL_HWNDPARENT, targetWindow.ToInt32());
		}



		#endregion
	}
}
