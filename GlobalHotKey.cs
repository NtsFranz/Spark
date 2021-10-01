using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Spark
{
	[Flags]
	public enum KeyModifier
	{
		None = 0x0000,
		Alt = 0x0001,
		Ctrl = 0x0002,
		NoRepeat = 0x4000,
		Shift = 0x0004,
		Win = 0x0008
	}
	
	/// <summary>
	/// https://stackoverflow.com/questions/48935/how-can-i-register-a-global-hot-key-to-say-ctrlshiftletter-using-wpf-and-ne
	/// </summary>
	public class GlobalHotKey : IDisposable
	{
		private static Dictionary<int, GlobalHotKey> dictHotKeyToCalBackProc;

		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vlc);

		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		public const int WmHotKey = 0x0312;

		private bool disposed = false;

		public Key Key { get; private set; }
		public KeyModifier KeyModifiers { get; private set; }
		public Action<GlobalHotKey> Action { get; private set; }
		public int Id { get; set; }

		// ******************************************************************
		public GlobalHotKey(Key k, KeyModifier keyModifiers, Action<GlobalHotKey> action, bool register = true)
		{
			Key = k;
			KeyModifiers = keyModifiers;
			Action = action;
			if (register)
			{
				Register();
			}
		}

		// ******************************************************************
		public bool Register()
		{
			int virtualKeyCode = KeyInterop.VirtualKeyFromKey(Key);
			Id = virtualKeyCode + ((int) KeyModifiers * 0x10000);
			bool result = RegisterHotKey(IntPtr.Zero, Id, (uint) KeyModifiers, (uint) virtualKeyCode);

			if (dictHotKeyToCalBackProc == null)
			{
				dictHotKeyToCalBackProc = new Dictionary<int, GlobalHotKey>();
				ComponentDispatcher.ThreadFilterMessage +=
					new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);
			}

			dictHotKeyToCalBackProc.Add(Id, this);

			Debug.Print($"{result}, {Id}, {virtualKeyCode}");
			return result;
		}

		// ******************************************************************
		public void Unregister()
		{
			if (dictHotKeyToCalBackProc.TryGetValue(Id, out GlobalHotKey hotKey))
			{
				UnregisterHotKey(IntPtr.Zero, Id);
			}
		}

		// ******************************************************************
		private static void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
		{
			if (handled) return;
			if (msg.message != WmHotKey) return;
			if (!dictHotKeyToCalBackProc.TryGetValue((int) msg.wParam, out GlobalHotKey hotKey)) return;
			
			hotKey.Action?.Invoke(hotKey);
			handled = false;
		}

		// ******************************************************************
		// Implement IDisposable.
		// Do not make this method virtual.
		// A derived class should not be able to override this method.
		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		// ******************************************************************
		// Dispose(bool disposing) executes in two distinct scenarios.
		// If disposing equals true, the method has been called directly
		// or indirectly by a user's code. Managed and unmanaged resources
		// can be _disposed.
		// If disposing equals false, the method has been called by the
		// runtime from inside the finalizer and you should not reference
		// other objects. Only unmanaged resources can be _disposed.
		protected virtual void Dispose(bool disposing)
		{
			// Check to see if Dispose has already been called.
			if (disposed) return;
			
			// If disposing equals true, dispose all managed
			// and unmanaged resources.
			if (disposing)
			{
				// Dispose managed resources.
				Unregister();
			}

			// Note disposing has been done.
			disposed = true;
		}
	}
}