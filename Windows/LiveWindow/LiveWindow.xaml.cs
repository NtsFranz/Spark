using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EchoVRAPI;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Logger;
using Frame = EchoVRAPI.Frame;
// Aliased rather than importing System.Windows.Shapes wholesale, which would make Path ambiguous
// against the System.IO.Path used throughout this file.
using Rectangle = System.Windows.Shapes.Rectangle;
using Shape = System.Windows.Shapes.Shape;

namespace Spark
{
    public class CombatLoadout
    {
        public string Name { get; set; }
        public int Ping { get; set; }
        public string Weapon { get; set; }
        public string Ordnance { get; set; }
        public string TacMod { get; set; }
        public int Kills { get; set; }
        public int Assists { get; set; }
        public int Deaths { get; set; }
        public int Damage { get; set; }

        /// <summary>Ordnance and TacMod combined onto one line ("Arc Mine · Barrier"), the way the roster row shows them.</summary>
        public string Mods => string.IsNullOrEmpty(Ordnance) || Ordnance == "N/A"
            ? (string.IsNullOrEmpty(TacMod) || TacMod == "N/A" ? "" : TacMod)
            : (string.IsNullOrEmpty(TacMod) || TacMod == "N/A" ? Ordnance : $"{Ordnance} · {TacMod}");

        public string DamageText => Damage.ToString("N0");

        /// <summary>Highlights the local player's own row in the roster.</summary>
        public Brush RowBg { get; set; } = Brushes.Transparent;

        /// <summary>This player's damage against the match's single highest damage figure, so both rosters read on the same scale.</summary>
        public GridLength DmgFill { get; set; } = new GridLength(0.001, GridUnitType.Star);
        public GridLength DmgRest { get; set; } = new GridLength(1, GridUnitType.Star);
    }

    /// <summary>One row in the combat Kill Feed card.</summary>
    public class CombatKillFeedItem
    {
        public string Killer { get; set; }
        public string Victim { get; set; }
        public string Weapon { get; set; }
        public Brush KillerColor { get; set; }
        public Brush VictimColor { get; set; }
        public Brush RowBg { get; set; } = Brushes.Transparent;
    }

    /// <summary>
    /// Interaction logic for LiveWindow.xaml
    /// </summary>
    /// 
    public partial class LiveWindow
    {
        private readonly System.Timers.Timer outputUpdateTimer = new System.Timers.Timer();

        /// <summary>
        /// 0 when no Update pass is queued or running. The timer fires every 150 ms regardless of
        /// how long the UI thread takes, so without this the dispatcher queue grows without bound
        /// whenever a pass runs long and the app falls further behind the more it's already behind.
        /// </summary>
        private int updateInFlight;

        /// <summary>Keeps a repeating per-tick failure from flooding the log.</summary>
        private bool loggedUpdateFailure;

        /// <summary>
        /// The event log is appended to on every timer tick and never shrinks, so WPF ends up
        /// re-measuring the whole buffer on each append. Cap it so a long session stays flat.
        /// </summary>
        private const int maxOutputLogChars = 10_000;

        private const int outputLogTrimToChars = 8_000;

        /// <summary>Backs the reworked Event Log tab's structured, colour-coded row list.</summary>
        private readonly ObservableCollection<EventLogEntry> eventLogEntries = new ObservableCollection<EventLogEntry>();

        private const int maxEventLogEntries = 3000;
        private const int eventLogTrimToEntries = 2500;
        private bool eventLogAtBottom = true;

        // Re-created on every tick in the original code, which allocated ~7 brushes/second and
        // left each one mutable, so WPF had to keep change-tracking them.
        private static readonly SolidColorBrush statusRedBrush = FrozenBrush(Colors.Red);
        private static readonly SolidColorBrush statusYellowBrush = FrozenBrush(Colors.Yellow);
        private static readonly SolidColorBrush statusGreenBrush = FrozenBrush(Colors.Green);

        private static readonly FontFamily monospaceFont = new FontFamily("Consolas");

        private static SolidColorBrush FrozenBrush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private string updateFilename = "";

        public static readonly object lastSnapshotLock = new object();

        private string lastDiscordUsername = string.Empty;
        private bool accessCodeDropdownListenerActive;
        public bool hidden;
        private bool isExplicitClose;

        string blueLogo = "";
        string orangeLogo = "";


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
        private static extern long SetWindowLong(IntPtr hwnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);


        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left; // x position of upper-left corner
            public int Top; // y position of upper-left corner
            public int Right; // x position of lower-right corner
            public int Bottom; // y position of lower-right corner
        }


        public Process SpeakerSystemProcess;
        private IntPtr unityHWND = IntPtr.Zero;

        const int UNITY_READY = 0x00000003;
        private const int WM_ACTIVATE = 0x0006;
        private readonly IntPtr WA_ACTIVE = new IntPtr(1);
        private const int GWL_STYLE = (-16);
        private const int WS_VISIBLE = 0x10000000;
        private const int GWL_USERDATA = (-21);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private Process GetActiveProcessFileName()
        {
            IntPtr hwnd = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return Process.GetProcessById((int)pid);
        }


        private bool initialized;

        public LiveWindow()
        {
            InitializeComponent();
            PopulateOverlayPicker();

            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

            outputUpdateTimer.Interval = 150;
            outputUpdateTimer.Elapsed += Update;
            outputUpdateTimer.Enabled = true;

            eventLogListBox.ItemsSource = eventLogEntries;
            eventLogListBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(EventLogScrollChanged));

            dashboardWeb = new DashboardWebHost(DashboardWebView);
            dashboardWeb.DashItemChanged += index =>
            {
                SparkSettings.instance.dashboardItem1 = index;
                Dispatcher.Invoke(() => SetDashboardItem1Visibility(index));
            };
            dashboardWeb.JoustOrderChanged += index => SparkSettings.instance.dashboardJoustTimeOrder = index;
            // Fires on first load AND every reload. A reload resets the page back to its hardcoded
            // default theme, so the "did the theme change?" cache in PushDashboard needs clearing —
            // otherwise it still matches ThemesController's unchanged colours and never re-sends them,
            // leaving the dashboard stuck on default grey after a refresh.
            dashboardWeb.Loaded += () => Dispatcher.Invoke(() =>
            {
                pushedInitialTheme = false;
                lastPushedThemeDark = null;
                lastPushedThemeMid = null;
                lastPushedThemeLight = null;
            });
            dashboardWeb.Start();

            Loaded += (_, _) =>
            {
                if (SparkSettings.instance.startMinimized)
                {
                    Hide();
                    showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
                    hidden = true;
                }
            };

            DiscordOAuth.AccessCodeChanged += _ =>
            {
                Dispatcher.Invoke(() =>
                {
                    RefreshAccessCodeList();
                    RefreshDiscordLogin();
                });
            };

            DiscordOAuth.Authenticated += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    RefreshAccessCodeList();
                    RefreshDiscordLogin();
                });
            };
            RefreshAccessCodeList();
            RefreshDiscordLogin();

            Program.NewMatch += frame =>
            {
                // Server IP / location is now resolved from the per-tick watchdog below (see its
                // comment) since this one-shot event can fire before sessionip is populated.
                Dispatcher.Invoke(() => { RefreshPlayerList(frame); });
            };

            Program.PlayerJoined += (frame, team, arg3) => { Dispatcher.Invoke(() => { RefreshPlayerList(frame); }); };

            Program.PlayerLeft += (frame, team, arg3) => { Dispatcher.Invoke(() => { RefreshPlayerList(frame); }); };
            Program.PlayerSwitchedTeams += (frame, team, arg3, arg4) => { Dispatcher.Invoke(() => { RefreshPlayerList(frame); }); };
            Program.LeftGame += frame =>
            {
                Dispatcher.Invoke(() =>
                {
                    RefreshLastRoundsList();
                    RefreshPlayerList(frame);
                });
            };
            Program.JoinedGame += frame =>
            {
                Dispatcher.Invoke(() =>
                {
                    RefreshLastRoundsList();
                    RefreshPlayerList(frame);
                });
            };
            Program.Goal += (frame, data) =>
            {
                // Session card is "your" stats, not the lobby's — only count it if the scorer is
                // the local client. frame.client_name is the same identity check Program.cs already
                // uses elsewhere (e.g. GetPlayer(frame.client_name)) to mean "the local player".
                if (data.Player?.name == frame.client_name)
                {
                    Interlocked.Increment(ref sessionGoals);
                }

                Dispatcher.Invoke(() =>
                {
                    RefreshLastRoundsList();
                    RefreshLastGoalsList();
                });
            };
            Program.Save += (frame, data) =>
            {
                if (data.player?.name == frame.client_name)
                {
                    Interlocked.Increment(ref sessionSaves);
                }
            };
            Program.NewRound += (frame) => { Dispatcher.Invoke(() => { RefreshLastRoundsList(); }); };
            Program.RoundOver += (frame, reason) => { Dispatcher.Invoke(() => { RefreshLastRoundsList(); }); };

            RefreshLastRoundsList();
            RefreshLastGoalsList();

            RefreshPlayerList(Program.lastFrame);

            JToken gameSettings = EchoVRSettingsManager.ReadEchoVRSettings();
            if (gameSettings != null)
            {
                try
                {
                    if (gameSettings["game"]?["EnableAPIAccess"] != null)
                    {
                        // TODO re-enable this feature once game setting saving works again
                        enableAPIButton.Visibility = !(bool)gameSettings["game"]["EnableAPIAccess"] ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                catch (Exception)
                {
                    LogRow(LogType.Error, "Can't read EchoVR settings file. It exists, but something went wrong.");
                    enableAPIButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                enableAPIButton.Visibility = Visibility.Collapsed;
            }
            //hostLiveReplayButton.Visible = !Program.Personal;

            showHighlights.IsEnabled = HighlightsHelper.DoNVClipsExist();
            showHighlights.Visibility = (HighlightsHelper.didHighlightsInit && HighlightsHelper.isNVHighlightsEnabled) ? Visibility.Visible : Visibility.Collapsed;
            showHighlights.Content = HighlightsHelper.DoNVClipsExist() ? Properties.Resources.Show + " " + HighlightsHelper.nvHighlightClipCount + " " + Properties.Resources.Highlights : Properties.Resources.No_clips_available;

#if DEBUG
            EchoGPTab.Visibility = Visibility.Visible;
            ShowClickableOverlayButton.Visibility = Visibility.Visible;
#endif


            tabControl.SelectionChanged += TabControl_SelectionChanged;

            SetDashboardItem1Visibility(SparkSettings.instance.dashboardItem1);

            _ = Task.Run(async () =>
            {
                AppUpdater.UpdateInfo update = await AppUpdater.CheckForUpdatesAsync();
                if (update != null)
                {
                    Dispatcher.Invoke(() => ShowUpdatePrompt(update));
                }
            });

            initialized = true;
        }

        private async void LiveWindow_Load(object sender, EventArgs e)
        {
            lock (Program.logOutputWriteLock)
            {
                mainOutputTextBox.Text = string.Join('\n', fullFileCache);

                // fullFileCache can hold up to 5000 historical lines (see Logger.cs); only the most
                // recent few hundred are worth paying startup cost to parse and add as rows one by one.
                AppendEventLogEntries(string.Join('\n', fullFileCache.TakeLast(500)));
            }

            if (SparkSettings.instance.spectateMeOnByDefault)
            {
                spectateMeSubtitle.Text = Properties.Resources.Waiting_until_you_join_a_game;
                spectateMeLabel.Content = Properties.Resources.Stop_Spectating_Me;
            }

            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IgniteVR", "Spark", "WebView");
                CoreWebView2Environment webView2Environment = await CoreWebView2Environment.CreateAsync(null, path);
                //await PlayercardWebView.EnsureCoreWebView2Async(webView2Environment);

            }
            catch (FileNotFoundException ex)
            {
                LogRow(LogType.Error, "4538: Failed to load WebView.\n" + ex);
                new MessageBox("Failed to load. Please report this to NtsFranz or else ┗|｀O′|┛ (4538)").Show();
            }
            catch (WebView2RuntimeNotFoundException ex)
            {
                Error("Error setting up webview: " + ex);
                string sparkFolder = Path.GetDirectoryName(SparkSettings.instance.sparkExeLocation) ?? "";
                string exePath = Path.Combine(sparkFolder, "resources", "MicrosoftEdgeWebview2Setup.exe");

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                });
            }
            catch (Exception ex)
            {
                LogRow(LogType.Error, "3645: Failed to load WebView for an unknown reason.\n" + ex);
                new MessageBox("Failed to load. Please report this to NtsFranz ( ╯□╰ ) (3645)").Show();
            }

            //_ = CheckForAppUpdate();
        }

        public void SetSpectateMeSubtitle(string text)
        {
            Dispatcher.Invoke(() => { spectateMeSubtitle.Text = text; });
        }

        public void FocusSpark()
        {
            //WPF focus the Spark Window 
            Dispatcher.Invoke(() =>
            {
                if (!IsVisible)
                {
                    Show();
                }

                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }

                Activate();
                Topmost = true;
                Topmost = false;
                Focus();
            });
        }

        public static string AppVersionLabelText => $"v{Program.AppVersionString()}  {(Program.IsWindowsStore() ? Properties.Resources.Microsoft_Store : "GitHub")}";
        public static Visibility PlayercardsTabVisibility => Visibility.Visible; //Program.IsWindowsStore() ? Visibility.Visible : Visibility.Collapsed;

        private void ActivateUnityWindow()
        {
            SendMessage(unityHWND, WM_ACTIVATE, WA_ACTIVE, IntPtr.Zero);
        }

        /// <summary>
        /// Finds the EchoVRMusic window among the children of <paramref name="parent"/>.
        ///
        /// EchoVRMusicSetup.exe creates itself as a child of Spark because of --embed, but the
        /// window doesn't exist the moment the process starts, and Spark's window has other
        /// native children too (a Static, and WebView2's Chrome_* windows for the Browser tab),
        /// so match the owning process and the window class.
        /// </summary>
        private IntPtr FindSpeakerSystemChild(IntPtr parent, int essPid)
        {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(parent, (hwnd, lparam) =>
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid != (uint)essPid) return 1;

                StringBuilder cls = new StringBuilder(256);
                GetClassName(hwnd, cls, cls.Capacity);
                if (cls.ToString() != "EchoVRMusicSetup") return 1;

                found = hwnd;
                return 0;
            }, IntPtr.Zero);
            return found;
        }

        private int WindowEnum(IntPtr hwnd, IntPtr lparam)
        {
            // Kept for the delegate signature; the real lookup is FindSpeakerSystemChild.
            unityHWND = hwnd;
            return 0;
        }

        /// <summary>
        /// Positions the embedded EchoVRMusic window over the Music panel. Must run on the UI
        /// thread - it reads WPF layout - and converts to physical pixels, because
        /// MoveWindow works in pixels while WPF reports device-independent units.
        /// </summary>
        private void MoveSpeakerSystemWindow()
        {
            if (unityHWND == IntPtr.Zero) return;

            double scaleX = 1.0, scaleY = 1.0;
            PresentationSource src = PresentationSource.FromVisual(this);
            if (src != null && src.CompositionTarget != null)
            {
                scaleX = src.CompositionTarget.TransformToDevice.M11;
                scaleY = src.CompositionTarget.TransformToDevice.M22;
            }

            Point origin = speakerSystemPanel.TransformToAncestor(this).Transform(new Point(0, 0));
            MoveWindow(unityHWND,
                (int)(origin.X * scaleX), (int)(origin.Y * scaleY),
                (int)(speakerSystemPanel.ActualWidth * scaleX),
                (int)(speakerSystemPanel.ActualHeight * scaleY),
                true);
            ActivateUnityWindow();
        }

        private void speakerSystemPanel_Resize(object sender, EventArgs e)
        {
            if (!speakerSystemPanel.IsVisible || SpeakerSystemProcess == null || SpeakerSystemProcess.Handle.ToInt32() <= 0) return;

            MoveSpeakerSystemWindow();
        }

        private void liveWindow_FormClosed(object sender, EventArgs e)
        {
            try
            {
                SparkSettings.instance.totalPlaytimeSeconds = allTimePlaytimeBaseSeconds + sessionPlaySeconds;
                SparkSettings.instance.Save();

                KillSpeakerSystem();
                SpeakerSystemProcess?.CloseMainWindow();
            }
            catch (Exception ex)
            {
                LogRow(LogType.Error, $"Error closing live window\n{ex}");
            }
        }

        public void KillSpeakerSystem()
        {
            try
            {
                if (SpeakerSystemProcess == null) return;

                while (!SpeakerSystemProcess.HasExited)
                {
                    SpeakerSystemProcess.Kill();
                }

                unityHWND = IntPtr.Zero;
                Thread.Sleep(100);
            }
            catch (Exception e)
            {
                LogRow(LogType.Error, $"Error killing speaker system\n{e}");
            }
        }


        private void Update(object source, ElapsedEventArgs e)
        {
            if (!Program.running) return;

            // Skip this tick if the previous one hasn't finished rather than queueing behind it.
            if (Interlocked.Exchange(ref updateInFlight, 1) == 1) return;

            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    lock (Program.logOutputWriteLock)
                    {
                        string newText = FilterLines(unusedFileCache.ToString());
                        if (newText != string.Empty && newText != Environment.NewLine)
                        {
                            try
                            {
                                mainOutputTextBox.AppendText(newText);
                                TrimOutputLog();
                                AppendEventLogEntries(newText);

                                //    if (Program.writeToOBSHTMLFile) // TODO this file path won't work
                                //    {
                                //        // write to html file for overlay as well
                                //        File.WriteAllText("html_output/events.html", @"
                                //    <html>
                                //    <head>
                                //    <meta http-equiv=""refresh"" content=""1"">
                                //    <link rel=""stylesheet"" type=""text/css"" href=""styles.css"">
                                //    </head>
                                //    <body>

                                //    <div id=""info""> " +
                                //                    newText
                                //                    + @"
                                //    </div>

                                //    </body>
                                //    </html>
                                //");
                                //    }
                            }
                            catch (Exception ex)
                            {
                                LogRow(LogType.Error, $"Error writing to output log.\n{ex}");
                            }

                            //ColorizeOutput("Entered state:", gameStateChangedCheckBox.ForeColor, mainOutputTextBox.Text.Length - newText.Length);
                        }

                        unusedFileCache.Clear();
                    }

                    // Banked before the visibility gate below: playing with Spark minimised to the
                    // tray is the normal case, and while this sat with the rest of the Session card
                    // that time was never credited to either the local or the global total.
                    BankPlaytime();

                    // Everything past this point only writes to controls the user can't see while
                    // the window is hidden to the tray or minimised, so skip it entirely. The log
                    // above still drains so its backlog doesn't build up while we're away.
                    if (hidden || WindowState == WindowState.Minimized) return;

                    showHighlights.IsEnabled = HighlightsHelper.DoNVClipsExist();
                    showHighlights.Visibility = (HighlightsHelper.didHighlightsInit && HighlightsHelper.isNVHighlightsEnabled) ? Visibility.Visible : Visibility.Collapsed;
                    showHighlights.Content = HighlightsHelper.DoNVClipsExist() ? "Show " + HighlightsHelper.nvHighlightClipCount + " Highlights" : Properties.Resources.No_clips_available;

                    DiscordNotLoggedInHosting.Visibility = !DiscordOAuth.IsLoggedIn ? Visibility.Visible : Visibility.Collapsed;

                    switch (Program.connectionState)
                    {
                        case Program.ConnectionState.NotConnected:
                            statusLabel.Content = Properties.Resources.Not_Connected;
                            statusCircle.Fill = statusRedBrush;
                            NotConnectedHelp.Visibility = Visibility.Visible;
                            break;
                        case Program.ConnectionState.Menu:
                            statusLabel.Content = Properties.Resources.In_Loading_Screen;
                            statusCircle.Fill = statusYellowBrush;
                            NotConnectedHelp.Visibility = Visibility.Collapsed;
                            break;
                        case Program.ConnectionState.NoAPI:
                            statusLabel.Content = Properties.Resources.API_Setting_Disabled;
                            statusCircle.Fill = statusYellowBrush;
                            NotConnectedHelp.Visibility = Visibility.Collapsed;
                            break;
                        case Program.ConnectionState.InLobby:
                            statusLabel.Content = Properties.Resources.In_Lobby;
                            statusCircle.Fill = statusYellowBrush;
                            NotConnectedHelp.Visibility = Visibility.Collapsed;
                            break;
                        case Program.ConnectionState.InGame:
                            statusLabel.Content = Properties.Resources.Connected;
                            statusCircle.Fill = statusGreenBrush;
                            NotConnectedHelp.Visibility = Visibility.Collapsed;
                            break;
                    }


                    // The dashboard's theme is pushed from here, so this has to run every tick and
                    // not only when there's a frame. Gated behind `Program.lastFrame != null` it
                    // never ran outside a match, so changing theme while not in game never reached
                    // the WebView and the dashboard sat on the page's hardcoded default grey.
                    // PushFrame already no-ops on a null frame, so this is safe to call always.
                    PushDashboard();

                    // Session ID. Deliberately outside the lastFrame check below: in a social lobby
                    // the API reports no frame at all, and that's exactly when the link has to come
                    // from the log instead.
                    UpdateJoinLink();

                    // update the other labels in the stats box
                    if (Program.lastFrame != null) // 'mpl_lobby_b2' may change in the future
                    {
                        // Server IP / location. NewMatch below only fires once per sessionid change,
                        // and for combat private matches sessionip is sometimes still blank on that
                        // first frame (server not fully allocated yet) — Arena's InLobby gating skips
                        // that premature frame, but combat's doesn't, so the one-shot NewMatch handler
                        // was firing with an empty IP and never getting a chance to retry. Checking
                        // every tick instead means it just resolves as soon as the IP actually shows up.
                        if (!string.IsNullOrEmpty(Program.lastFrame.sessionip) &&
                            Program.lastFrame.sessionip != lastResolvedSessionIp)
                        {
                            lastResolvedSessionIp = Program.lastFrame.sessionip;
                            serverLocationLabel.Content = "Server IP: " + lastResolvedSessionIp;
                            _ = GetServerLocation(lastResolvedSessionIp);
                        }

                        UpdateLastThrowCard(Program.lastFrame.last_throw);
                        UpdateStatRows(Program.lastFrame);
                        RefreshHistoryIfChanged();
                        ServerScoreLabel.Text = FormatServerScore();

                        if (blueLogo != Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamLogo)
                        {
                            blueLogo = Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamLogo;
                            blueTeamLogo.Source = string.IsNullOrEmpty(blueLogo) ? null : new BitmapImage(new Uri(blueLogo));
                            blueTeamLogo.ToolTip = Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamName;
                        }

                        if (orangeLogo != Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamLogo)
                        {
                            orangeLogo = Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamLogo;
                            orangeTeamLogo.Source = string.IsNullOrEmpty(orangeLogo) ? null : new BitmapImage(new Uri(orangeLogo));
                            orangeTeamLogo.ToolTip = Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamName;
                        }


                        #region Rejoiner

                        // show the button once the player hasn't been getting data for some time
                        float secondsUntilRejoiner = 1f;
                        if (!Program.InGame &&
                            Program.lastFrame != null &&
                            Program.lastFrame.private_match &&
                            Program.lastFrame.GetAllPlayers(true).Count > 1 && // if we weren't the last
                            DateTime.Compare(Program.lastDataTime.AddSeconds(secondsUntilRejoiner), DateTime.UtcNow) < 0 &&
                            SparkSettings.instance.echoVRIP == "127.0.0.1")
                        {
                            rejoinButton.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            rejoinButton.Visibility = Visibility.Collapsed;
                        }

                        #endregion

                        // Combat Dashboard Logic
                        // match_type varies by mode — "Echo_Combat_Private", "Echo_Combat_Tournament",
                        // "Echo_Combat_Public_AI", etc. — so an exact match against "Echo_Combat" alone
                        // only ever caught the public queue and silently fell through to the Arena
                        // dashboard for every other combat variant, private matches included.
                        if (Program.lastFrame.match_type != null &&
                            Program.lastFrame.match_type.StartsWith("Echo_Combat", StringComparison.OrdinalIgnoreCase))
                        {
                            ArenaDashboardGrid.Visibility = Visibility.Collapsed;
                            CombatDashboardGrid.Visibility = Visibility.Visible;

                            try
                            {
                                if (!string.IsNullOrEmpty(Program.lastJSON))
                                {
                                    UpdateCombatDashboard(JObject.Parse(Program.lastJSON), Program.lastFrame);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogRow(LogType.Error, $"Error parsing combat JSON:\n{ex}");
                            }
                        }
                        else
                        {
                            ArenaDashboardGrid.Visibility = Visibility.Visible;
                            CombatDashboardGrid.Visibility = Visibility.Collapsed;
                        }
                    }

                    bool blueReadyVisible = false;
                    bool orangeReadyVisible = false;
                    bool bluePauseVisible = false;
                    bool orangePauseVisible = false;
                    bool blueRestartVisible = false;
                    bool orangeRestartVisible = false;
                    bool bluePauseEnabled = false;
                    bool orangePauseEnabled = false;
                    string bluePauseText = "Pause";
                    string orangePauseText = "Pause";
                    if (Program.InGame && Program.lastFrame != null && Program.lastFrame.private_match && Program.lastFrame.client_name != "anonymous")
                    {
                        blueReadyVisible = true;
                        bluePauseVisible = true;
                        blueRestartVisible = true;
                        bluePauseEnabled = true;

                        orangeReadyVisible = true;
                        orangePauseVisible = true;
                        orangeRestartVisible = true;
                        orangePauseEnabled = true;

                        if (Program.lastFrame.pause?.paused_state == "paused_requested" || Program.lastFrame.pause?.paused_state == "paused")
                        {
                            if (Program.lastFrame.pause.paused_requested_team == "blue")
                            {
                                bluePauseText = "Unpause";
                                orangePauseText = "Unpause";
                            }

                            if (Program.lastFrame.pause.paused_requested_team == "orange")
                            {
                                bluePauseText = "Unpause";
                                orangePauseText = "Unpause";
                            }
                        }
                    }

                    BlueTeamReadyUp.Visibility = blueReadyVisible ? Visibility.Visible : Visibility.Collapsed;
                    OrangeTeamReadyUp.Visibility = orangeReadyVisible ? Visibility.Visible : Visibility.Collapsed;
                    BlueTeamPause.Visibility = bluePauseVisible ? Visibility.Visible : Visibility.Collapsed;
                    OrangeTeamPause.Visibility = orangePauseVisible ? Visibility.Visible : Visibility.Collapsed;
                    BlueTeamRestart.Visibility = blueRestartVisible ? Visibility.Visible : Visibility.Collapsed;
                    OrangeTeamRestart.Visibility = orangeRestartVisible ? Visibility.Visible : Visibility.Collapsed;
                    BlueTeamPause.IsEnabled = bluePauseEnabled;
                    OrangeTeamPause.IsEnabled = orangePauseEnabled;
                    BlueTeamPause.Content = bluePauseText;
                    OrangeTeamPause.Content = orangePauseText;

                    if (Program.lastFrame?.InArena == true) // only the arena has a disc
                    {
                        discSpeedLabel.Text = $"{Program.lastFrame.disc.velocity.ToVector3().Length():N2}";
                        SetDiscSpeedTint(Program.lastFrame.possession[0]);
                        //discSpeedProgressBar.Value = (int)Program.lastFrame.disc.Velocity.Length();
                        //if (Program.lastFrame.teams[0].possession)
                        //{
                        //    discSpeedProgressBar.ForeColor = Color.Blue;
                        //} else if (Program.lastFrame.teams[1].possession)
                        //{
                        //    discSpeedProgressBar.ForeColor = Color.Orange;
                        //} else
                        //{
                        //    discSpeedProgressBar.ForeColor = Color.Gray;
                        //}


                        OrangePoints.Text = Program.lastFrame.orange_points.ToString();
                        BluePoints.Text = Program.lastFrame.blue_points.ToString();
                        GameClock.Text = Program.lastFrame.game_clock_display[..^3];
                        RoundStatusLabel.Text = FormatRoundStatus(Program.lastFrame.game_status);

                        UpdateJoustTimes();
                        UpdateDiscSpeedHistory(Program.lastFrame.disc.velocity.ToVector3().Length());
                    }
                    else if (Program.lastFrame?.match_type != null &&
                        Program.lastFrame.match_type.StartsWith("Echo_Combat", StringComparison.OrdinalIgnoreCase))
                    {
                        // Combat has no disc, but the header score strip still reads as "stuck at 0-0"
                        // if it's left on the XAML placeholder — show the round score there instead.
                        discSpeedLabel.Text = "--";
                        SetDiscSpeedTint(-1);
                        OrangePoints.Text = Program.lastFrame.orange_round_score.ToString();
                        BluePoints.Text = Program.lastFrame.blue_round_score.ToString();
                        GameClock.Text = Program.lastFrame.game_clock_display?.Length > 3
                            ? Program.lastFrame.game_clock_display[..^3]
                            : Program.lastFrame.game_clock_display;
                        RoundStatusLabel.Text = FormatRoundStatus(Program.lastFrame.game_status);
                    }
                    else
                    {
                        discSpeedLabel.Text = "--";
                        SetDiscSpeedTint(-1);
                    }


                    UpdateSessionCard();
                    RefreshPlayerPings(Program.lastFrame);
                    RefreshDiscordLogin();

                    if (SparkSettings.instance.echoVRIP != "127.0.0.1" || SparkSettings.instance.allowSpectateMeOnLocalPC)
                    {
                        spectateMeButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        spectateMeButton.Visibility = Visibility.Collapsed;
                    }


                    hostMatchButton.IsEnabled = Program.lastFrame != null && Program.lastFrame.private_match;

                    UpdateJoinLink();

                    // SpectatorOverlayController opens and closes the overlay; this only reports its state.
                    if (!SparkSettings.instance.spectatorOverlayEnabled)
                    {
                        ClickableOverlaySubtitle.Text = Properties.Resources.Not_active;
                    }
                    else
                    {
                        ClickableOverlaySubtitle.Text = Program.GetWindowIfOpen(typeof(GameOverlay)) != null
                            ? Properties.Resources.Active
                            : Properties.Resources.Echo_VR_not_active;
                    }

                    // Collapsed, not Hidden — these now sit in the status row, so Hidden would leave a
                    // gap in the middle of it.
                    DownloadingOverlaysBar.Visibility = OverlaysCustom.downloading ? Visibility.Visible : Visibility.Collapsed;
                    DownloadingOverlaysText.Visibility = OverlaysCustom.downloading ? Visibility.Visible : Visibility.Collapsed;


                    if (!Program.running)
                    {
                        outputUpdateTimer.Stop();
                    }
                }
                catch (Exception ex)
                {
                    // One bad field used to abandon the rest of the pass silently, leaving whatever
                    // came after it frozen at its last value with no clue why.
                    if (!loggedUpdateFailure)
                    {
                        loggedUpdateFailure = true;
                        LogRow(LogType.Error, $"Dashboard update failed.\n{ex}");
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref updateInFlight, 0);
                }
            });
        }

        #region Dashboard cards

        /// <summary>One roster row's visuals, kept so per-tick updates can write values without rebuilding.</summary>
        private sealed class StatRow
        {
            public TextBlock Value;
            public ColumnDefinition BarFilled;
            public ColumnDefinition BarRest;
            public Border BarFill;
        }

        private readonly List<StatRow> pingRows = new List<StatRow>();
        private readonly List<StatRow> speedRows = new List<StatRow>();
        private string lastRosterSignature = string.Empty;

        private readonly List<StatRow> combatPingRows = new List<StatRow>();
        private string lastCombatRosterSignature = string.Empty;

        private float lastThrowTotal = float.NaN;
        private float lastThrowArm = float.NaN;
        private float lastThrowSpin = float.NaN;

        /// <summary>
        /// Fills the Last Throw card. The old version rebuilt a tab-aligned string and reassigned it
        /// on every tick; this only touches the UI when the throw actually changes.
        /// </summary>
        private void UpdateLastThrowCard(LastThrow throwData)
        {
            if (throwData == null) return;

            // last_throw is telemetry from the local client's own controllers, so unlike the world
            // disc speed it's already scoped to "my" throws — safe to track as the session peak.
            if (throwData.total_speed > sessionFastestThrow)
            {
                sessionFastestThrow = throwData.total_speed;
            }

            if (throwData.total_speed == lastThrowTotal
                && throwData.speed_from_arm == lastThrowArm
                && throwData.off_axis_spin_deg == lastThrowSpin)
            {
                return;
            }

            lastThrowTotal = throwData.total_speed;
            lastThrowArm = throwData.speed_from_arm;
            lastThrowSpin = throwData.off_axis_spin_deg;

            ThrowTotalValue.Text = throwData.total_speed.ToString("N2");
            ThrowArmValue.Text = throwData.speed_from_arm.ToString("N2");
            ThrowWristValue.Text = throwData.speed_from_wrist.ToString("N2");
            ThrowMoveValue.Text = throwData.speed_from_movement.ToString("N2");

            ThrowArmSpeedValue.Text = $"{throwData.arm_speed:N2} m/s";
            ThrowRotsValue.Text = $"{throwData.rot_per_sec:N2} r/s";
            ThrowPotSpeedValue.Text = $"{throwData.pot_speed_from_rot:N2} m/s";

            ThrowOffAxisValue.Text = $"{throwData.off_axis_spin_deg:N1}°";
            ThrowWristAlignValue.Text = $"{throwData.wrist_align_to_throw_deg:N1}°";
            ThrowMoveAlignValue.Text = $"{throwData.throw_align_to_movement_deg:N1}°";

            // Shows what the throw was actually made of — the numbers alone left that to the reader.
            // Hidden until the first throw so equal thirds don't read as a real measurement.
            ThrowCompositionBar.Visibility = Visibility.Visible;
            ThrowArmShare.Width = ShareWidth(throwData.speed_from_arm);
            ThrowWristShare.Width = ShareWidth(throwData.speed_from_wrist);
            ThrowMoveShare.Width = ShareWidth(throwData.speed_from_movement);

            // Derived here, not reported by the game: the three alignment angles rolled into one
            // 0-100 figure so a throw can be judged at a glance. 0 deg on all three reads 100.
            float misalignment = Math.Abs(throwData.off_axis_spin_deg)
                                 + Math.Abs(throwData.wrist_align_to_throw_deg)
                                 + Math.Abs(throwData.throw_align_to_movement_deg);
            int quality = (int)Math.Clamp(100f - misalignment / 1.8f, 0f, 100f);

            ThrowQualityValue.Text = quality.ToString();
            double qualityFraction = Math.Clamp(quality / 100.0, 0.001, 1.0);
            ThrowQualityFilled.Width = new GridLength(qualityFraction, GridUnitType.Star);
            ThrowQualityRest.Width = new GridLength(Math.Max(0.001, 1 - qualityFraction), GridUnitType.Star);
            ThrowQualityFill.SetResourceReference(Border.BackgroundProperty,
                quality >= 70 ? "StatusGood" : quality >= 45 ? "StatusWarn" : "StatusBad");
        }

        private readonly Queue<float> discSpeedHistory = new Queue<float>();
        private const int discSpeedHistoryLength = 90;
        private float discSpeedPeak;

        /// <summary>
        /// Feeds the disc-speed sparkline. At the 150 ms tick rate, 90 samples is roughly the last
        /// minute of play.
        /// </summary>
        private void UpdateDiscSpeedHistory(float speed)
        {
            discSpeedHistory.Enqueue(speed);
            while (discSpeedHistory.Count > discSpeedHistoryLength) discSpeedHistory.Dequeue();

            if (speed > discSpeedPeak)
            {
                discSpeedPeak = speed;
                DiscSpeedPeak.Text = $"peak {discSpeedPeak:N1}";
            }

            double width = DiscSpeedSparkline.ActualWidth;
            if (width < 2 || discSpeedHistory.Count < 2) return;

            const double height = 30;
            float ceiling = Math.Max(1f, discSpeedPeak);
            PointCollection points = new PointCollection(discSpeedHistory.Count);
            int index = 0;
            foreach (float sample in discSpeedHistory)
            {
                double x = width * index / (discSpeedHistoryLength - 1.0);
                double y = height - Math.Clamp(sample / ceiling, 0, 1) * (height - 2) - 1;
                points.Add(new Point(x, y));
                index++;
            }

            DiscSpeedSparkline.Points = points;
        }

        /// <summary>
        /// Redraws the joust list as one bar per joust, scaled against the slowest in view, instead of
        /// the flat text block it used to be.
        /// </summary>
        private void UpdateJoustTimes()
        {
            List<EventData> jousts = Program.LastJousts.ToList();
            if (jousts.Count == 0)
            {
                if (JoustTimesBox.Children.Count > 0) JoustTimesBox.Children.Clear();
                JoustAverageLabel.Text = string.Empty;
                lastJoustSignature = string.Empty;
                return;
            }

            if (SparkSettings.instance.dashboardJoustTimeOrder == 1)
            {
                jousts.Sort((first, second) => second.joustTimeMillis.CompareTo(first.joustTimeMillis));
            }

            // Rebuilding ~10 rows on every tick would undo the point of the tick budget, so only
            // redraw when the list actually changed.
            StringBuilder signature = new StringBuilder();
            for (int i = jousts.Count - 1; i >= 0; i--)
            {
                signature.Append(jousts[i].player.name).Append(jousts[i].joustTimeMillis).Append('|');
            }

            if (signature.ToString() == lastJoustSignature) return;
            lastJoustSignature = signature.ToString();

            float slowest = 1f;
            float total = 0f;
            foreach (EventData joust in jousts)
            {
                slowest = Math.Max(slowest, joust.joustTimeMillis);
                total += joust.joustTimeMillis;
            }

            JoustAverageLabel.Text = $"avg {total / jousts.Count / 1000f:N2} s";

            JoustTimesBox.Children.Clear();
            for (int i = jousts.Count - 1; i >= 0; i--)
            {
                EventData joust = jousts[i];
                bool blueTeam = joust.player.team_color == Team.TeamColor.blue;
                JoustTimesBox.Children.Add(CreateJoustRow(
                    joust.player.name,
                    joust.joustTimeMillis / 1000f,
                    joust.joustTimeMillis / slowest,
                    blueTeam ? "TeamBlue" : "TeamOrange",
                    joust.eventType == EventContainer.EventType.joust_speed));
            }
        }

        private string lastJoustSignature = string.Empty;

        private static UIElement CreateJoustRow(string playerName, float seconds, float fraction, string teamBrushKey, bool neutralJoust)
        {
            TextBlock nameLabel = new TextBlock
            {
                Text = neutralJoust ? playerName + " N" : playerName,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, teamBrushKey);

            Border barTrack = new Border { CornerRadius = new CornerRadius(2) };
            barTrack.SetResourceReference(Border.BackgroundProperty, "SurfaceTrack");
            Grid.SetColumnSpan(barTrack, 2);

            Border barFill = new Border { CornerRadius = new CornerRadius(2) };
            barFill.SetResourceReference(Border.BackgroundProperty, teamBrushKey);

            double clamped = Math.Clamp(fraction, 0.02, 1.0);
            Grid bar = new Grid { Height = 4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(clamped, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - clamped, GridUnitType.Star) });
            bar.Children.Add(barTrack);
            bar.Children.Add(barFill);
            Grid.SetColumn(bar, 1);

            TextBlock timeLabel = MonoCell(seconds.ToString("N2"), 32, TextAlignment.Right, "TextPrimary");
            Grid.SetColumn(timeLabel, 2);

            Grid layout = new Grid { Margin = new Thickness(0, 2.5, 0, 2.5) };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.Children.Add(nameLabel);
            layout.Children.Add(bar);
            layout.Children.Add(timeLabel);

            return layout;
        }

        private int lastDiscPossession = int.MinValue;

        /// <summary>
        /// Tints the disc speed by which team holds it. Only re-resolves the brush when possession
        /// actually changes, since this sits in the per-tick path.
        /// </summary>
        private void SetDiscSpeedTint(int possessingTeam)
        {
            if (possessingTeam == lastDiscPossession) return;
            lastDiscPossession = possessingTeam;

            discSpeedLabel.SetResourceReference(TextBlock.ForegroundProperty, possessingTeam switch
            {
                0 => "TeamBlue",
                1 => "TeamOrange",
                -1 => "TextFaint",
                _ => "TextPrimary"
            });
        }

        private static GridLength ShareWidth(float component)
        {
            return new GridLength(Math.Max(0.01, component), GridUnitType.Star);
        }

        /// <summary>
        /// Refreshes the ping and speed lists. Rows are only rebuilt when the roster changes; the rest
        /// of the time this just writes new values into the existing ones.
        /// </summary>
        private void UpdateStatRows(Frame frame)
        {
            string signature = RosterSignature(frame);
            if (signature != lastRosterSignature)
            {
                lastRosterSignature = signature;
                RebuildStatRows(frame);

                // The left rail is otherwise only refreshed from join/leave/switch events, so it
                // stays stale if one is ever missed. Rebuilding on the same signal keeps them agreed.
                RefreshPlayerList(frame);
            }

            bool showingSpeeds = playerSpeedsBox.Visibility == Visibility.Visible;
            int index = 0;
            int pingTotal = 0;
            int pingCount = 0;
            int worstPing = 0;
            float lossTotal = 0f;

            for (int team = 0; team < 3; team++)
            {
                foreach (Player player in frame.teams[team].players)
                {
                    if (player.ping > 0)
                    {
                        pingTotal += player.ping;
                        pingCount++;
                        worstPing = Math.Max(worstPing, player.ping);
                    }

                    lossTotal += player.packetlossratio;

                    if (index >= pingRows.Count) return;

                    StatRow pingRow = pingRows[index];
                    pingRow.Value.Text = player.ping > 0 ? player.ping.ToString() : "--";
                    pingRow.Value.SetResourceReference(TextBlock.ForegroundProperty, PingQualityBrushKey(player.ping));
                    pingRow.BarFill.SetResourceReference(Border.BackgroundProperty, PingQualityBrushKey(player.ping));
                    SetBarFraction(pingRow, player.ping / 200f);

                    if (showingSpeeds && index < speedRows.Count)
                    {
                        float speed = player.velocity.ToVector3().Length();
                        StatRow speedRow = speedRows[index];
                        speedRow.Value.Text = speed.ToString("N1");
                        SetBarFraction(speedRow, speed / 15f);
                    }

                    index++;
                }
            }

            AvgPingValue.Text = pingCount > 0 ? (pingTotal / pingCount).ToString() : "--";
            WorstPingValue.Text = worstPing > 0 ? worstPing.ToString() : "--";
            WorstPingValue.SetResourceReference(TextBlock.ForegroundProperty, PingQualityBrushKey(worstPing));

            float lossPercent = index > 0 ? lossTotal / index * 100f : 0f;
            PacketLossValue.Text = index > 0 ? lossPercent.ToString("N1") : "--";
            PacketLossValue.SetResourceReference(TextBlock.ForegroundProperty,
                lossPercent < 1f ? "StatusGood" : lossPercent < 3f ? "StatusWarn" : "StatusBad");

            float score = Program.CurrentRound.smoothedServerScore;
            ServerScoreNumber.Text = score > 0 ? score.ToString("N1") : "--";
            double scoreFraction = Math.Clamp(score / 10f, 0.001, 1.0);
            ServerScoreFilled.Width = new GridLength(scoreFraction, GridUnitType.Star);
            ServerScoreRest.Width = new GridLength(Math.Max(0.001, 1 - scoreFraction), GridUnitType.Star);
        }

        private DashboardWebHost dashboardWeb;
        private bool pushedInitialTheme;

        private int sessionGoals;
        private int sessionSaves;
        private float sessionFastestThrow;
        private string lastResolvedSessionIp = "";

        // Banked only for ticks where Echo is actually connected — Session/All-Time Playtime used
        // to be wall-clock time since this window opened, so leaving Spark running in the
        // background (or with Echo closed entirely) silently counted as played time.
        private double sessionPlaySeconds;
        private DateTime lastPlaytimeTick = DateTime.UtcNow;

        // Snapshotted once at launch, before this session's playtime is added on top — the sum of
        // every previous launch's playtime, persisted so the All-Time figure survives restarts.
        private readonly double allTimePlaytimeBaseSeconds = SparkSettings.instance.totalPlaytimeSeconds;
        private DateTime lastPlaytimePersist = DateTime.UtcNow;

        /// <summary>
        /// Fills the Session card. Every figure here is counted from real events or measured from
        /// this run — the card shipped with placeholder numbers hard-coded into the XAML, which
        /// showed the same invented totals in every session.
        /// </summary>
        private void UpdateSessionCard()
        {
            SessionGoalsLabel.Text = sessionGoals.ToString();
            SessionSavesLabel.Text = sessionSaves.ToString();
            SessionFastestDiscLabel.Text = sessionFastestThrow > 0 ? sessionFastestThrow.ToString("N2") : "--";

            TimeSpan sessionPlayed = TimeSpan.FromSeconds(sessionPlaySeconds);
            SessionPlaytimeLabel.Text = sessionPlayed.TotalHours >= 1
                ? $"{(int)sessionPlayed.TotalHours}h {sessionPlayed.Minutes}m"
                : $"{sessionPlayed.Minutes}m";

            AllTimePlaytimeLabel.Text =
                FormatAllTimePlaytime(allTimePlaytimeBaseSeconds + sessionPlaySeconds);

            GlobalPlaytimeLabel.Text = Program.GlobalPlaytimeSeconds.HasValue
                ? FormatGlobalPlaytime(Program.GlobalPlaytimeSeconds.Value)
                : "--";
        }

        /// <summary>
        /// Accrues playtime and persists it. Split out of <see cref="UpdateSessionCard"/> and
        /// called ahead of the visibility gate, because the clock has to keep running while the
        /// window is minimised or in the tray — the labels are the only part that doesn't.
        /// </summary>
        private void BankPlaytime()
        {
            DateTime now = DateTime.UtcNow;
            double delta = (now - lastPlaytimeTick).TotalSeconds;
            lastPlaytimeTick = now;

            // Only bank time while Echo is actually connected. The upper bound discards the delta
            // after a sleep/resume or debugger pause instead of crediting the gap as playtime.
            bool echoConnected = Program.connectionState != Program.ConnectionState.NotConnected &&
                Program.connectionState != Program.ConnectionState.NoAPI;
            if (echoConnected && delta > 0 && delta < 30)
            {
                sessionPlaySeconds += delta;
            }

            // Persist periodically (not every tick) so a crash only loses a minute of credit, not
            // the whole session's worth.
            if ((now - lastPlaytimePersist).TotalSeconds >= 60)
            {
                lastPlaytimePersist = now;
                SparkSettings.instance.totalPlaytimeSeconds = allTimePlaytimeBaseSeconds + sessionPlaySeconds;
                SparkSettings.instance.Save();
            }

            // The community-wide figure. Moves in hours per minute across all installs, so there's
            // nothing to gain from refreshing it on the 150 ms UI tick.
            if ((now - lastGlobalPlaytimeFetch).TotalMinutes >= 5)
            {
                lastGlobalPlaytimeFetch = now;
                _ = Program.RefreshGlobalPlaytime();
            }
        }

        // Far enough in the past that the first tick fetches immediately.
        private DateTime lastGlobalPlaytimeFetch = DateTime.MinValue;

        /// <summary>
        /// Formats the community total, which is in a different league from one person's — this is
        /// every install's hours summed, so it reaches millions and needs to stay narrow enough for
        /// the card. Falls back to a plain grouped hour count below 10k.
        /// </summary>
        private static string FormatGlobalPlaytime(double totalSeconds)
        {
            double hours = totalSeconds / 3600;
            // Thresholds account for the rounding that follows, so 999,999 h reads "1.0M h"
            // rather than rolling up into a nonsensical "1,000.0k h".
            if (hours >= 999_950) return $"{hours / 1_000_000:N1}M h";
            if (hours >= 9_999.5) return $"{hours / 1_000:N1}k h";
            return $"{hours:N0} h";
        }

        /// <summary>
        /// Resolved once per call, not a DynamicResource — CombatObjectiveBorder.BorderBrush is set
        /// imperatively from code (it depends on which team owns the objective), so it can't just be
        /// bound in XAML like everything else in the combat dashboard.
        /// </summary>
        private static Brush CombatThemeBrush(string key)
        {
            return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
        }

        private static string FormatAllTimePlaytime(double totalSeconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(totalSeconds);
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
            return $"{span.Minutes}m";
        }

        /// <summary>
        /// Feeds the web dashboard, and hides the native fallback grid once it's up.
        /// </summary>
        private string lastPushedThemeDark;
        private string lastPushedThemeMid;
        private string lastPushedThemeLight;

        private void PushDashboard()
        {
            if (dashboardWeb == null || !dashboardWeb.Ready) return;

            if (!pushedInitialTheme)
            {
                pushedInitialTheme = true;
                dashboardWeb.PushDashItem(SparkSettings.instance.dashboardItem1);
                ArenaDashboardGrid.Visibility = Visibility.Collapsed;
            }

            // The dashboard used to only get its theme once, on startup, so changing theme in
            // Settings afterward never reached it — everything there stayed on Stealth Gray. This
            // re-checks every tick and re-pushes when it changes.
            //
            // Reads ThemesController's live-applied colours, not SparkSettings.instance: a preset
            // click or slider drag only live-previews (ApplyCustomTheme, which the native chrome
            // picks up instantly via DynamicResource) without touching SparkSettings until a
            // separate Apply/Save action. Reading the settings field here would miss every live
            // preview and only catch the theme after it's explicitly saved.
            string dark = ThemesController.CurrentDarkHex;
            string mid = ThemesController.CurrentMidHex;
            string light = ThemesController.CurrentLightHex;
            if (dark != lastPushedThemeDark || mid != lastPushedThemeMid || light != lastPushedThemeLight)
            {
                lastPushedThemeDark = dark;
                lastPushedThemeMid = mid;
                lastPushedThemeLight = light;
                dashboardWeb.PushTheme(dark, mid, light);
            }

            dashboardWeb.PushFrame(Program.lastFrame);
        }

        private int lastRoundCount = -1;
        private int lastGoalCount = -1;

        /// <summary>
        /// Rebuilds the Previous Rounds and Previous Goals lists when their contents change.
        /// <para>
        /// Both were only refreshed from Goal/NewRound/RoundOver events, so anything that added a
        /// round without raising one left them showing stale contents until the next goal.
        /// </para>
        /// </summary>
        private void RefreshHistoryIfChanged()
        {
            int roundCount = Program.rounds.Count;
            int goalCount = Program.LastGoals.Count();

            if (roundCount != lastRoundCount)
            {
                lastRoundCount = roundCount;
                RefreshLastRoundsList();
            }

            if (goalCount != lastGoalCount)
            {
                lastGoalCount = goalCount;
                RefreshLastGoalsList();
            }
        }

        private static void SetBarFraction(StatRow row, float fraction)
        {
            double clamped = Math.Clamp(fraction, 0.0, 1.0);
            row.BarFilled.Width = new GridLength(Math.Max(0.001, clamped), GridUnitType.Star);
            row.BarRest.Width = new GridLength(Math.Max(0.001, 1.0 - clamped), GridUnitType.Star);
        }

        private static string RosterSignature(Frame frame)
        {
            StringBuilder signature = new StringBuilder();
            for (int team = 0; team < 3; team++)
            {
                foreach (Player player in frame.teams[team].players)
                {
                    signature.Append(team).Append(':').Append(player.name).Append('|');
                }
            }

            return signature.ToString();
        }

        private void RebuildStatRows(Frame frame)
        {
            PlayerPingsBox.Children.Clear();
            playerSpeedsBox.Children.Clear();
            pingRows.Clear();
            speedRows.Clear();

            for (int team = 0; team < 3; team++)
            {
                string teamBrushKey = team switch
                {
                    0 => "TeamBlue",
                    1 => "TeamOrange",
                    _ => "TextFaint"
                };

                foreach (Player player in frame.teams[team].players)
                {
                    PlayerPingsBox.Children.Add(CreateStatRow(player.name, teamBrushKey, 40, out StatRow pingRow));
                    pingRows.Add(pingRow);

                    playerSpeedsBox.Children.Add(CreateStatRow(player.name, teamBrushKey, 44, out StatRow speedRow));
                    speedRow.BarFill.SetResourceReference(Border.BackgroundProperty, teamBrushKey);
                    speedRows.Add(speedRow);
                }
            }
        }

        /// <summary>
        /// Builds one "[team bar] name [progress] value" row, with the name and value in fixed columns
        /// so figures line up down the list — the old side-by-side TextBlocks never did.
        /// </summary>
        private static UIElement CreateStatRow(string playerName, string teamBrushKey, double valueWidth, out StatRow row)
        {
            Rectangle accentBar = new Rectangle
            {
                Width = 3,
                Height = 11,
                RadiusX = 2,
                RadiusY = 2,
                VerticalAlignment = VerticalAlignment.Center
            };
            accentBar.SetResourceReference(Shape.FillProperty, teamBrushKey);

            TextBlock nameLabel = new TextBlock
            {
                Text = playerName,
                FontSize = 11.5,
                Margin = new Thickness(9, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            Grid.SetColumn(nameLabel, 1);

            Border barTrack = new Border { CornerRadius = new CornerRadius(3) };
            barTrack.SetResourceReference(Border.BackgroundProperty, "SurfaceTrack");
            Grid.SetColumnSpan(barTrack, 2);

            Border barFill = new Border { CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Stretch };

            ColumnDefinition filled = new ColumnDefinition { Width = new GridLength(0.001, GridUnitType.Star) };
            ColumnDefinition rest = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };

            Grid bar = new Grid { Height = 5, VerticalAlignment = VerticalAlignment.Center };
            bar.ColumnDefinitions.Add(filled);
            bar.ColumnDefinitions.Add(rest);
            bar.Children.Add(barTrack);
            bar.Children.Add(barFill);
            Grid.SetColumn(bar, 2);

            TextBlock valueLabel = new TextBlock
            {
                Text = "--",
                FontSize = 11,
                FontFamily = monospaceFont,
                Margin = new Thickness(9, 0, 0, 0),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            valueLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            Grid.SetColumn(valueLabel, 3);

            Grid layout = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(valueWidth) });
            layout.Children.Add(accentBar);
            layout.Children.Add(nameLabel);
            layout.Children.Add(bar);
            layout.Children.Add(valueLabel);

            row = new StatRow
            {
                Value = valueLabel,
                BarFilled = filled,
                BarRest = rest,
                BarFill = barFill
            };

            return layout;
        }

        /// <summary>
        /// One Previous Rounds row: time, both scores, and a bar showing how the points split.
        /// </summary>
        private static UIElement CreateRoundRow(string when, float orangePoints, float bluePoints, string round, string tooltip, bool highlight)
        {
            TextBlock timeLabel = MonoCell(when, 44, TextAlignment.Left, "TextFaint");

            TextBlock orangeLabel = MonoCell(orangePoints.ToString("N0"), 22, TextAlignment.Right, "TeamOrange");
            orangeLabel.FontSize = 12.5;
            orangeLabel.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(orangeLabel, 1);

            Grid split = new Grid { Height = 4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 7, 0) };
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, orangePoints), GridUnitType.Star) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, bluePoints), GridUnitType.Star) });

            Border orangeBar = new Border { CornerRadius = new CornerRadius(2, 0, 0, 2) };
            orangeBar.SetResourceReference(Border.BackgroundProperty, "TeamOrange");
            Border blueBar = new Border { CornerRadius = new CornerRadius(0, 2, 2, 0) };
            blueBar.SetResourceReference(Border.BackgroundProperty, "TeamBlue");
            Grid.SetColumn(blueBar, 1);
            split.Children.Add(orangeBar);
            split.Children.Add(blueBar);
            Grid.SetColumn(split, 2);

            TextBlock blueLabel = MonoCell(bluePoints.ToString("N0"), 22, TextAlignment.Left, "TeamBlue");
            blueLabel.FontSize = 12.5;
            blueLabel.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(blueLabel, 3);

            TextBlock roundLabel = MonoCell(round, 34, TextAlignment.Right, "TextFaint");
            Grid.SetColumn(roundLabel, 4);

            Grid layout = new Grid { Margin = new Thickness(8, 5, 8, 5) };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.Children.Add(timeLabel);
            layout.Children.Add(orangeLabel);
            layout.Children.Add(split);
            layout.Children.Add(blueLabel);
            layout.Children.Add(roundLabel);

            return WrapRow(layout, highlight, tooltip);
        }

        /// <summary>One Previous Goals row: time, points, scorer, disc speed and distance in fixed columns.</summary>
        private static UIElement CreateGoalRow(GoalData goal, bool highlight)
        {
            TextBlock timeLabel = MonoCell($"{goal.GameClock:N0}s", 40, TextAlignment.Left, "TextFaint");

            TextBlock pointsLabel = MonoCell($"{goal.LastScore.point_amount}", 24, TextAlignment.Right, "TextPrimary");
            Grid.SetColumn(pointsLabel, 1);

            TextBlock scorerLabel = new TextBlock
            {
                Text = goal.LastScore.person_scored,
                FontSize = 11.5,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            scorerLabel.SetResourceReference(TextBlock.ForegroundProperty,
                goal.LastScore.team == "orange" ? "TeamOrange" : "TeamBlue");
            Grid.SetColumn(scorerLabel, 2);

            TextBlock speedLabel = MonoCell($"{goal.LastScore.disc_speed:N1}", 52, TextAlignment.Right, "TextPrimary");
            Grid.SetColumn(speedLabel, 3);

            TextBlock distanceLabel = MonoCell($"{goal.LastScore.distance_thrown:N1}", 48, TextAlignment.Right, "TextDim");
            Grid.SetColumn(distanceLabel, 4);

            Grid layout = new Grid { Margin = new Thickness(8, 5, 8, 5) };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.Children.Add(timeLabel);
            layout.Children.Add(pointsLabel);
            layout.Children.Add(scorerLabel);
            layout.Children.Add(speedLabel);
            layout.Children.Add(distanceLabel);

            return WrapRow(layout, highlight, null);
        }

        private static TextBlock MonoCell(string text, double width, TextAlignment alignment, string brushKey)
        {
            TextBlock cell = new TextBlock
            {
                Text = text,
                Width = width,
                FontSize = 11,
                FontFamily = monospaceFont,
                TextAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center
            };
            cell.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            return cell;
        }

        /// <summary>
        /// Only the newest row gets a fill. Zebra striping every row, as before, competes with the
        /// team colours that carry the actual meaning here.
        /// </summary>
        private static UIElement WrapRow(UIElement content, bool highlight, string tooltip)
        {
            Border row = new Border { Child = content, CornerRadius = new CornerRadius(4), ToolTip = tooltip };
            if (highlight)
            {
                row.SetResourceReference(Border.BackgroundProperty, "SurfaceRaised");
            }

            return row;
        }

        /// <summary>
        /// The server-quality caption that used to be appended onto the Player Pings group header.
        /// </summary>
        private static string FormatServerScore()
        {
            if (Program.CurrentRound.serverScore > 0)
            {
                return $"{Properties.Resources.Score_} {Program.CurrentRound.smoothedServerScore:N1}";
            }

            if (Math.Abs(Program.CurrentRound.serverScore - -1) < .1f)
            {
                return ">150";
            }

            if (Program.CurrentRound.serverScore < -1.5f)
            {
                return "Wrong player count";
            }

            return $"{Properties.Resources.Score_} --";
        }

        #endregion

        /// <summary>
        /// Turns the API's raw game_status ("pre_match", "round_start", ...) into the short caption
        /// under the game clock.
        /// </summary>
        private static string FormatRoundStatus(string gameStatus)
        {
            return gameStatus switch
            {
                null or "" => string.Empty,
                "pre_match" => "Pre-match",
                "round_start" => "Round start",
                "playing" => "Playing",
                "score" => "Score",
                "round_over" => "Round over",
                "post_match" => "Post-match",
                "pre_sudden_death" or "sudden_death" => "Sudden death",
                _ => gameStatus.Replace('_', ' ')
            };
        }

        /// <summary>
        /// Drops the oldest lines once the event log passes its cap, cutting on a line boundary so
        /// the visible text never starts mid-line.
        /// </summary>
        private void TrimOutputLog()
        {
            string text = mainOutputTextBox.Text;
            if (text.Length <= maxOutputLogChars) return;

            int lineStart = text.IndexOf('\n', text.Length - outputLogTrimToChars);
            mainOutputTextBox.Text = lineStart < 0
                ? text[^outputLogTrimToChars..]
                : text[(lineStart + 1)..];
        }

        /// <summary>
        /// Parses each new line into the structured row list the Event Log tab actually displays.
        /// Auto-scrolls only if the user was already at the bottom, so scrolling up to read history
        /// doesn't get yanked back down by the next tick's new rows.
        /// </summary>
        private void AppendEventLogEntries(string newText)
        {
            string[] lines = newText.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                eventLogEntries.Add(EventLogEntry.Parse(line));
            }

            if (eventLogEntries.Count > maxEventLogEntries)
            {
                int toRemove = eventLogEntries.Count - eventLogTrimToEntries;
                for (int i = 0; i < toRemove; i++)
                {
                    eventLogEntries.RemoveAt(0);
                }
            }

            if (eventLogAtBottom)
            {
                ScrollEventLogToEnd();
            }
        }

        /// <summary>
        /// Tracks whether the user is scrolled to the bottom of the event log, so new rows only
        /// auto-scroll the view when they weren't already reading back through history.
        /// </summary>
        private void EventLogScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            eventLogAtBottom = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 20;
        }

        private void RefreshLastRoundsList()
        {
            LastRoundScoresBox.Children.Clear();

            AccumulatedFrame[] lastMatches = Program.rounds.ToArray();
            if (lastMatches.Length > 0)
            {
                for (int i = lastMatches.Length - 1; i >= 0; i--)
                {
                    AccumulatedFrame match = lastMatches[i];

                    string when = match.finishReason switch
                    {
                        AccumulatedFrame.FinishReason.not_finished => "live",
                        AccumulatedFrame.FinishReason.game_time => match.matchTime.ToLocalTime().ToString("t"),
                        _ => match.matchTime.ToLocalTime().ToString("t")
                    };

                    string round = string.Empty;
                    if (match.frame.total_round_count > 0)
                    {
                        int played = match.frame.blue_round_score + match.frame.orange_round_score;
                        if (match.finishReason == AccumulatedFrame.FinishReason.not_finished) played++;
                        round = $"R{played / match.frame.total_round_count}";
                    }

                    string tooltip = match.finishReason == AccumulatedFrame.FinishReason.game_time
                        ? null
                        : match.finishReason.ToString();

                    LastRoundScoresBox.Children.Add(CreateRoundRow(
                        when,
                        match.frame.orange_points,
                        match.frame.blue_points,
                        round,
                        tooltip,
                        i == lastMatches.Length - 1));
                }
            }
        }

        private void RefreshLastGoalsList()
        {
            LastGoalsBox.Children.Clear();

            GoalData[] lastGoals = Program.LastGoals.ToArray();
            BestGoalSpeedLabel.Text = lastGoals.Length > 0
                ? $"best {lastGoals.Max(goal => goal.LastScore.disc_speed):N1} m/s"
                : string.Empty;

            if (lastGoals.Length > 0)
            {
                for (int i = lastGoals.Length - 1; i >= 0; i--)
                {
                    GoalData goal = lastGoals[i];
                    LastGoalsBox.Children.Add(CreateGoalRow(goal, i == lastGoals.Length - 1));
                }
            }
        }

        private void RefreshPlayerList(Frame frame)
        {
            if (frame == null) return;

            BuildTeamRows(BlueTeamPlayersBox, frame.teams[0].players, "TeamBlue");
            BuildTeamRows(OrangeTeamPlayersBox, frame.teams[1].players, "TeamOrange");
            BuildTeamRows(SpectatorsPlayersBox, frame.teams[2].players, "TextFaint");

            BlueTeamCount.Text = frame.teams[0].players.Count.ToString();
            OrangeTeamCount.Text = frame.teams[1].players.Count.ToString();
            NoSpectatorsLabel.Visibility = frame.teams[2].players.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Updates just the ping figure on each existing roster row, every tick. BuildTeamRows only
        /// runs on join/leave/team-switch, so without this the ping shown would freeze at whatever it
        /// was when the row was built.
        /// </summary>
        private void RefreshPlayerPings(Frame frame)
        {
            if (frame == null) return;

            UpdateTeamPings(BlueTeamPlayersBox, frame.teams[0].players);
            UpdateTeamPings(OrangeTeamPlayersBox, frame.teams[1].players);
            UpdateTeamPings(SpectatorsPlayersBox, frame.teams[2].players);
        }

        private void UpdateTeamPings(Panel container, IReadOnlyList<Player> players)
        {
            // Row count only matches player count right after BuildTeamRows ran with this same
            // roster; a mismatch means a join/leave/switch is in flight and will rebuild the rows
            // properly on its own, so just skip this tick rather than update the wrong row.
            if (container.Children.Count != players.Count) return;

            for (int i = 0; i < players.Count; i++)
            {
                if (container.Children[i] is not Border { Child: Grid layout }) continue;

                TextBlock pingLabel = layout.Children.OfType<TextBlock>().FirstOrDefault(tb => Grid.GetColumn(tb) == 2);
                if (pingLabel == null) continue;

                Player player = players[i];
                pingLabel.Text = player.ping > 0 ? player.ping.ToString() : "--";
                pingLabel.SetResourceReference(TextBlock.ForegroundProperty, PingQualityBrushKey(player.ping));
            }
        }

        /// <summary>
        /// Rebuilds one team's roster rows: a team-coloured bar, the player name, and their ping
        /// coloured by quality.
        /// </summary>
        /// <remarks>
        /// The three rosters used to be near-identical copies of this loop, and two of them resolved
        /// their row brushes once through FindResource — so those rows kept their old colours after a
        /// theme change while the third followed along. Everything here uses SetResourceReference.
        /// </remarks>
        private void BuildTeamRows(Panel container, IReadOnlyList<Player> players, string teamBrushKey)
        {
            container.Children.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i];

                Rectangle accentBar = new Rectangle
                {
                    Width = 3,
                    Height = 12,
                    RadiusX = 2,
                    RadiusY = 2,
                    VerticalAlignment = VerticalAlignment.Center
                };
                accentBar.SetResourceReference(Shape.FillProperty, teamBrushKey);

                TextBlock nameLabel = new TextBlock
                {
                    Text = player.name,
                    FontSize = 12,
                    Margin = new Thickness(8, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
                Grid.SetColumn(nameLabel, 1);

                TextBlock pingLabel = new TextBlock
                {
                    Text = player.ping > 0 ? player.ping.ToString() : "--",
                    FontSize = 10.5,
                    FontFamily = monospaceFont,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pingLabel.SetResourceReference(TextBlock.ForegroundProperty, PingQualityBrushKey(player.ping));
                Grid.SetColumn(pingLabel, 2);

                Grid layout = new Grid { Margin = new Thickness(10, 6, 10, 6) };
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                layout.Children.Add(accentBar);
                layout.Children.Add(nameLabel);
                layout.Children.Add(pingLabel);

                // Transparent rather than null so the row still gets hover hit-tests.
                Border row = new Border
                {
                    Child = layout,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0, 0, 0, i < players.Count - 1 ? 1 : 0)
                };
                row.SetResourceReference(Border.BorderBrushProperty, "SurfaceBorderSoft");

                if (player.name != "anonymous")
                {
                    row.Cursor = Cursors.Hand;
                    row.MouseEnter += (_, _) => row.SetResourceReference(Border.BackgroundProperty, "ControlMouseOverBackground");
                    row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
                    row.MouseLeftButtonUp += (_, _) => ClickedOnPlayer(player.name);
                }

                container.Children.Add(row);
            }
        }

        /// <summary>
        /// Shows the update prompt and, if the user dismisses it without installing (Later, or just
        /// closing the window), reveals the footer badge so the update isn't lost — clicking that
        /// badge calls back in here with the same info to reopen the prompt.
        /// </summary>
        private void ShowUpdatePrompt(AppUpdater.UpdateInfo update)
        {
            UpdatePromptWindow prompt = new UpdatePromptWindow(update.Version, update.Changelog, update.DownloadUrl, update.FileName)
            {
                Owner = this
            };
            prompt.Dismissed += () => Dispatcher.Invoke(() => UpdateAvailableBadge.Visibility = Visibility.Visible);
            UpdateAvailableBadge.Visibility = Visibility.Collapsed;
            prompt.Show();
            prompt.Focus();
        }

        private void UpdateAvailableBadgeClicked(object sender, MouseButtonEventArgs e)
        {
            if (AppUpdater.PendingUpdate == null) return;
            ShowUpdatePrompt(AppUpdater.PendingUpdate);
        }

        private void OpenWikiClick(object sender, RoutedEventArgs e)
        {
            OpenExternalLink("https://echopedia.gg/wiki/Spark");
        }

        private void OpenDiscordClick(object sender, RoutedEventArgs e)
        {
            OpenExternalLink("https://discord.gg/echo-vr-lounge");
        }

        private static void OpenExternalLink(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        /// <summary>
        /// Fills every card in the Combat dashboard: Match (score/clock/objective/team totals),
        /// Kill Feed, Network, and the two roster cards. Mirrors the Arena dashboard's own cards
        /// (same data, same anatomy) so the two match types read as one product.
        /// </summary>
        private void UpdateCombatDashboard(JObject jsonObj, Frame frame)
        {
            // Use round scores for the top header if available, otherwise fallback to points (e.g. for payload or older API)
            string blueScore = jsonObj["blue_round_score"]?.ToString() ?? jsonObj["blue_points"]?.ToString() ?? "0";
            string orangeScore = jsonObj["orange_round_score"]?.ToString() ?? jsonObj["orange_points"]?.ToString() ?? "0";
            CombatBlueScore.Text = blueScore;
            CombatOrangeScore.Text = orangeScore;

            CombatClockText.Text = frame.game_clock_display?.Length > 3 ? frame.game_clock_display[..^3] : frame.game_clock_display;

            int roundsPlayed = (int)(jsonObj["blue_round_score"]?.ToObject<float>() ?? 0) + (int)(jsonObj["orange_round_score"]?.ToObject<float>() ?? 0);
            int totalRounds = jsonObj["total_round_count"]?.ToObject<int>() ?? frame.total_round_count;
            CombatRoundText.Text = totalRounds > 0 ? $"Round {Math.Min(roundsPlayed + 1, totalRounds)} of {totalRounds}" : "No round";

            string mapName = jsonObj["map_name"]?.ToString();
            CombatMapLabel.Text = CombatMapDisplayName(mapName);

            // ── Loadouts / rosters ──────────────────────────────────────────────
            var blueLoadouts = new List<CombatLoadout>();
            var orangeLoadouts = new List<CombatLoadout>();
            var teamsArray = jsonObj["teams"] as JArray;
            Brush raisedBrush = CombatThemeBrush("SurfaceRaised");
            Brush blueBrush = CombatThemeBrush("TeamBlue");
            Brush orangeBrush = CombatThemeBrush("TeamOrange");
            var nameColors = new Dictionary<string, Brush>();

            for (int t = 0; t < 2 && t < frame.teams.Count; t++)
            {
                Team apiTeam = frame.teams[t];
                JToken jsonTeam = teamsArray != null && teamsArray.Count > t ? teamsArray[t] : null;
                JArray jsonPlayers = jsonTeam?["players"] as JArray;
                Brush teamBrush = t == 0 ? blueBrush : orangeBrush;

                for (int pIndex = 0; pIndex < apiTeam.players.Count; pIndex++)
                {
                    Player apiPlayer = apiTeam.players[pIndex];
                    JToken jsonPlayer = jsonPlayers != null && jsonPlayers.Count > pIndex ? jsonPlayers[pIndex] : null;

                    // The API gives internal names ("assault", "det"); show the ones players know.
                    string weapon = GearNames.Display(jsonPlayer?["Weapon"]?.ToString() ?? jsonPlayer?["weapon"]?.ToString() ?? "N/A");
                    string ordnance = GearNames.Display(jsonPlayer?["Ordnance"]?.ToString() ?? jsonPlayer?["ordnance"]?.ToString() ?? "N/A");
                    string tacmod = GearNames.Display(jsonPlayer?["TacMod"]?.ToString() ?? jsonPlayer?["tacmod"]?.ToString() ?? "N/A");
                    CombatStats stats = CombatDataParser.GetCombatStats(apiPlayer.userid);

                    CombatLoadout loadout = new CombatLoadout
                    {
                        Name = apiPlayer.name,
                        Ping = apiPlayer.ping,
                        Weapon = weapon,
                        Ordnance = ordnance,
                        TacMod = tacmod,
                        Kills = stats.kills,
                        Assists = stats.assists,
                        Deaths = stats.deaths,
                        Damage = (int)stats.damage,
                        RowBg = apiPlayer.name == frame.client_name ? raisedBrush : Brushes.Transparent
                    };

                    if (t == 0) blueLoadouts.Add(loadout); else orangeLoadouts.Add(loadout);
                    nameColors[apiPlayer.name] = teamBrush;
                }
            }

            int peakDamage = Math.Max(1, blueLoadouts.Concat(orangeLoadouts).DefaultIfEmpty().Max(l => l?.Damage ?? 0));
            foreach (CombatLoadout loadout in blueLoadouts.Concat(orangeLoadouts))
            {
                double pct = Math.Clamp((double)loadout.Damage / peakDamage, 0.0, 1.0);
                loadout.DmgFill = new GridLength(Math.Max(0.001, pct), GridUnitType.Star);
                loadout.DmgRest = new GridLength(Math.Max(0.001, 1.0 - pct), GridUnitType.Star);
            }

            BlueCombatLoadouts.ItemsSource = blueLoadouts;
            OrangeCombatLoadouts.ItemsSource = orangeLoadouts;

            int blueKills = blueLoadouts.Sum(l => l.Kills), orangeKills = orangeLoadouts.Sum(l => l.Kills);
            int blueDamage = blueLoadouts.Sum(l => l.Damage), orangeDamage = orangeLoadouts.Sum(l => l.Damage);
            CombatBlueRosterSummary.Text = $"{blueKills} K · {blueDamage:N0} dmg";
            CombatOrangeRosterSummary.Text = $"{orangeKills} K · {orangeDamage:N0} dmg";

            // ── Team totals (Match card) ────────────────────────────────────────
            CombatBlueTotalKills.Text = blueKills.ToString();
            CombatOrangeTotalKills.Text = orangeKills.ToString();
            CombatBlueTotalDamage.Text = blueDamage.ToString("N0");
            CombatOrangeTotalDamage.Text = orangeDamage.ToString("N0");
            CombatBlueTotalObjective.Text = FormatObjectiveTime(SumObjectiveTime(frame.teams.Count > 0 ? frame.teams[0] : null));
            CombatOrangeTotalObjective.Text = FormatObjectiveTime(SumObjectiveTime(frame.teams.Count > 1 ? frame.teams[1] : null));

            // ── Objective (Capture point vs Payload) ────────────────────────────
            bool isPayload = mapName == "mpl_combat_fission" || mapName == "mpl_combat_gauss";
            CombatCaptureObjectivePanel.Visibility = isPayload ? Visibility.Collapsed : Visibility.Visible;
            CombatPayloadObjectivePanel.Visibility = isPayload ? Visibility.Visible : Visibility.Collapsed;

            if (isPayload)
            {
                JToken payload = jsonObj["payload"];
                float distance = payload?["distance"]?.ToObject<float>() ?? 0;
                float speed = payload?["speed"]?.ToObject<float>() ?? 0;
                float pct = payload?["progress"]?.ToObject<float>() ?? payload?["percentage"]?.ToObject<float>() ?? Math.Clamp(distance / 200f, 0f, 1f);

                bool isMoving = speed > 0.05f;
                CombatPayloadStateText.Text = isMoving ? "MOVING" : "STOPPED";
                Brush stateBrush = CombatThemeBrush(isMoving ? "StatusGood" : "TextFaint");
                CombatPayloadStateDot.Fill = stateBrush;
                CombatPayloadStateText.Foreground = stateBrush;

                CombatPayloadFill.Width = new GridLength(Math.Max(0.001, pct), GridUnitType.Star);
                CombatPayloadRest.Width = new GridLength(Math.Max(0.001, 1.0 - pct), GridUnitType.Star);
                CombatDistanceText.Text = $"{distance:N1} m";
                CombatSpeedText.Text = $"{speed:N2} m/s";
            }
            else
            {
                bool isContested = jsonObj["contested"]?.ToObject<bool>() ?? false;
                float blueProgress = jsonObj["blue_points"]?.ToObject<float>() ?? 0;
                float orangeProgress = jsonObj["orange_points"]?.ToObject<float>() ?? 0;

                string stateText; Brush stateBrush;
                if (isContested) { stateText = "CONTESTED"; stateBrush = CombatThemeBrush("StatusBad"); }
                else if (blueProgress > orangeProgress) { stateText = "BLUE HOLDS"; stateBrush = blueBrush; }
                else if (orangeProgress > blueProgress) { stateText = "ORANGE HOLDS"; stateBrush = orangeBrush; }
                else { stateText = "NEUTRAL"; stateBrush = CombatThemeBrush("TextFaint"); }

                CombatCaptureStateText.Text = stateText;
                CombatCaptureStateDot.Fill = stateBrush;
                CombatCaptureStateText.Foreground = stateBrush;

                double bluePct = Math.Clamp(blueProgress / 100.0, 0.0, 1.0);
                double orangePct = Math.Clamp(orangeProgress / 100.0, 0.0, 1.0);
                CombatBlueCaptureFill.Width = new GridLength(Math.Max(0.001, bluePct), GridUnitType.Star);
                CombatBlueCaptureRest.Width = new GridLength(Math.Max(0.001, 1.0 - bluePct), GridUnitType.Star);
                CombatOrangeCaptureFill.Width = new GridLength(Math.Max(0.001, orangePct), GridUnitType.Star);
                CombatOrangeCaptureRest.Width = new GridLength(Math.Max(0.001, 1.0 - orangePct), GridUnitType.Star);
                CombatBluePctText.Text = $"{blueProgress:N0}%";
                CombatOrangePctText.Text = $"{orangeProgress:N0}%";
            }

            // ── Kill feed ────────────────────────────────────────────────────────
            List<CombatKillFeedItem> feed;
            lock (CombatDataParser.ParseLock)
            {
                feed = CombatDataParser.KillFeed.Select((k, i) => new CombatKillFeedItem
                {
                    Killer = string.IsNullOrEmpty(k.killer) ? "Self" : k.killer,
                    Victim = string.IsNullOrEmpty(k.killed) ? "Unknown" : k.killed,
                    Weapon = GearNames.Display(k.killed_with),
                    KillerColor = nameColors.TryGetValue(k.killer ?? "", out Brush kc) ? kc : CombatThemeBrush("TextDim"),
                    VictimColor = nameColors.TryGetValue(k.killed ?? "", out Brush vc) ? vc : CombatThemeBrush("TextDim"),
                    RowBg = i == 0 ? raisedBrush : Brushes.Transparent
                }).ToList();
            }
            CombatKillFeedList.ItemsSource = feed;
            CombatKillFeedCount.Text = $"{feed.Count} total";

            // ── Network ──────────────────────────────────────────────────────────
            CombatNetworkStatusText.Text = FormatServerScore();

            string combatRosterSignature = RosterSignature2(frame);
            if (combatRosterSignature != lastCombatRosterSignature)
            {
                lastCombatRosterSignature = combatRosterSignature;
                CombatPingRowsBox.Children.Clear();
                combatPingRows.Clear();

                for (int t = 0; t < 2 && t < frame.teams.Count; t++)
                {
                    string teamBrushKey = t == 0 ? "TeamBlue" : "TeamOrange";
                    foreach (Player player in frame.teams[t].players)
                    {
                        CombatPingRowsBox.Children.Add(CreateStatRow(player.name, teamBrushKey, 40, out StatRow row));
                        combatPingRows.Add(row);
                    }
                }
            }

            int idx = 0, pingTotal = 0, pingCount = 0, worstPing = 0;
            float lossTotal = 0f;
            for (int t = 0; t < 2 && t < frame.teams.Count; t++)
            {
                foreach (Player player in frame.teams[t].players)
                {
                    if (player.ping > 0)
                    {
                        pingTotal += player.ping;
                        pingCount++;
                        worstPing = Math.Max(worstPing, player.ping);
                    }
                    lossTotal += player.packetlossratio;

                    if (idx >= combatPingRows.Count) break;
                    StatRow row = combatPingRows[idx];
                    row.Value.Text = player.ping > 0 ? player.ping.ToString() : "--";
                    row.Value.SetResourceReference(TextBlock.ForegroundProperty, PingQualityBrushKey(player.ping));
                    row.BarFill.SetResourceReference(Border.BackgroundProperty, PingQualityBrushKey(player.ping));
                    SetBarFraction(row, player.ping / 200f);
                    idx++;
                }
            }

            CombatAvgPingValue.Text = pingCount > 0 ? (pingTotal / pingCount).ToString() : "--";
            CombatWorstPingValue.Text = worstPing > 0 ? worstPing.ToString() : "--";
            CombatWorstPingValue.SetResourceReference(TextBlock.ForegroundProperty, PingQualityBrushKey(worstPing));
            float lossPercent = idx > 0 ? lossTotal / idx * 100f : 0f;
            CombatLossValue.Text = idx > 0 ? lossPercent.ToString("N1") : "--";
            CombatLossValue.SetResourceReference(TextBlock.ForegroundProperty,
                lossPercent < 1f ? "StatusGood" : lossPercent < 3f ? "StatusWarn" : "StatusBad");

            double serverScorePct = Math.Clamp(Program.CurrentRound.smoothedServerScore / 150.0, 0.0, 1.0);
            CombatServerScoreFill.Width = new GridLength(Math.Max(0.001, serverScorePct), GridUnitType.Star);
            CombatServerScoreRest.Width = new GridLength(Math.Max(0.001, 1.0 - serverScorePct), GridUnitType.Star);
            CombatServerScoreText.Text = Program.CurrentRound.serverScore > 0 ? Program.CurrentRound.smoothedServerScore.ToString("N1") : "--";
        }

        private static float SumObjectiveTime(Team team)
        {
            if (team?.players == null) return 0f;
            return team.players.Sum(p => CombatDataParser.GetCombatStats(p.userid).objective_time);
        }

        private static string FormatObjectiveTime(float seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)span.TotalMinutes}:{span.Seconds:D2}";
        }

        private static string CombatMapDisplayName(string mapName)
        {
            return mapName switch
            {
                "mpl_combat_dyson" => "DYSON",
                "mpl_combat_combustion" => "COMBUSTION",
                "mpl_combat_fission" => "FISSION",
                "mpl_combat_gauss" => "SURGE",
                _ => "--"
            };
        }

        /// <summary>Same idea as <see cref="RosterSignature"/> but scoped to blue/orange only (no spectators) for the combat ping list.</summary>
        private static string RosterSignature2(Frame frame)
        {
            StringBuilder signature = new StringBuilder();
            for (int team = 0; team < 2 && team < frame.teams.Count; team++)
            {
                foreach (Player player in frame.teams[team].players)
                {
                    signature.Append(team).Append(':').Append(player.name).Append('|');
                }
            }
            return signature.ToString();
        }

        private static string PingQualityBrushKey(int ping)
        {
            if (ping <= 0) return "TextFaint";
            if (ping < 70) return "StatusGood";
            return ping < 110 ? "StatusWarn" : "StatusBad";
        }


        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // if not specifically a exit button press, hide (unless the user has opted to always exit on close)
            if (isExplicitClose == false)
            {
                e.Cancel = true;

                if (SparkSettings.instance.closeButtonExitsApp)
                {
                    isExplicitClose = true;
                    Program.Quit();
                }
                else
                {
                    Program.ToggleWindow(typeof(YouSureAboutClosing), null, this);
                }
            }
        }

        private void ClickedOnPlayer(string playerName)
        {
            Process.Start(new ProcessStartInfo("https://echovrce.com")
            {
                UseShellExecute = true
            });
        }


        /// <summary>
        /// Enables or disables parts of the UI to match the current access code
        /// </summary>
        public void RefreshDiscordLogin()
        {
            string username = DiscordOAuth.DiscordUsername;
            if (username != lastDiscordUsername)
            {
                if (string.IsNullOrEmpty(username))
                {
                    discordUsernameLabel.Text = Properties.Resources.Discord_Login;
                    discordPFPImage.Source = null;
                    discordPFPImage.Visibility = Visibility.Collapsed;
                }
                else
                {
                    discordUsernameLabel.Text = username;
                    string imgUrl = DiscordOAuth.DiscordPFPURL;
                    if (!string.IsNullOrEmpty(imgUrl))
                    {
                        discordPFPImage.Source = new BitmapImage(new Uri(imgUrl));
                        discordPFPImage.Visibility = Visibility.Visible;
                    }
                }
            }

            lastDiscordUsername = username;
        }

        /// <summary>
        /// Regenerates the options in the dropdown for access codes.
        /// </summary>
        private void RefreshAccessCodeList()
        {
            accessCodeDropdownListenerActive = false;
            string accessCodeLocalized = DiscordOAuth.Personal ? Properties.Resources.Personal : DiscordOAuth.AccessCode.username;
            if (DiscordOAuth.availableAccessCodes.Count < 2)
            {
                accessCodeLabel.Text = Properties.Resources.Mode + accessCodeLocalized;
            }
            else
            {
                accessCodeLabel.Text = Properties.Resources.Mode;
            }


            AccessCodesComboboxLiveWindow.Items.Clear();
            foreach (DiscordOAuth.AccessCodeKey code in DiscordOAuth.availableAccessCodes)
            {
                AccessCodesComboboxLiveWindow.Items.Add(code.username);
            }

            // if not logged in with discord
            if (!AccessCodesComboboxLiveWindow.Items.Contains("Personal")) AccessCodesComboboxLiveWindow.Items.Add("Personal");

            // set the dropdown value
            AccessCodesComboboxLiveWindow.SelectedIndex = DiscordOAuth.GetAccessCodeIndexByHash(SparkSettings.instance.accessCode);

            // show or hide the dropdown entirely
            AccessCodesComboboxLiveWindow.Visibility = DiscordOAuth.availableAccessCodes.Count < 2 ? Visibility.Collapsed : Visibility.Visible;

            casterToolsBox.Visibility = !DiscordOAuth.Personal ? Visibility.Visible : Visibility.Collapsed;
            PasteLinkInLiveButton.Visibility = DiscordOAuth.AccessCode?.series_name.Contains("vrml") ?? false ? Visibility.Visible : Visibility.Collapsed;
            MatchSetupButton.Visibility = DiscordOAuth.AccessCode?.series_name.Contains("vrml") ?? false ? Visibility.Visible : Visibility.Collapsed;

            accessCodeDropdownListenerActive = true;
        }


        private async Task CheckForAppUpdate()
        {
#if !WINDOWS_STORE_RELEASE
            try
            {
                string respString = await FetchUtils.GetRequestAsync("https://api.github.com/repos/NtsFranz/Spark/releases", null);

                List<VersionJson> versions = JsonConvert.DeserializeObject<List<VersionJson>>(respString);

                // find the appropriate version
                VersionJson chosenVersion = versions?.First(v => !v.prerelease || v.prerelease == SparkSettings.instance.betaUpdates);

                // get the details from the version
                if (chosenVersion != null)
                {
                    string downloadUrl = chosenVersion.assets.First(url => url.browser_download_url.EndsWith(".msi")).browser_download_url;
                    string version = chosenVersion.tag_name.TrimStart('v');
                    string changelog = chosenVersion.body;

                    Version remoteVersion = new Version(version);

                    // if we need a new version
                    if (remoteVersion > Program.AppVersion())
                    {
                        updateFilename = downloadUrl;
                        updateButton.Visibility = Visibility.Visible;

                        MessageBox box = new MessageBox(changelog, Properties.Resources.Update_Available);
                        box.Topmost = true;
                        box.Show();
                    }
                    else
                    {
                        updateButton.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception e)
            {
                LogRow(LogType.Error, $"Couldn't check for update.\n{e}");
            }
#endif
        }


        private async Task GetServerLocation(string ip)
        {
            if (!string.IsNullOrEmpty(ip))
            {
                try
                {
                    // string resp = await FetchUtils.client.GetStringAsync(new Uri($"{Program.APIURL}/ip_geolocation/{ip}"));
                    string resp = await FetchUtils.client.GetStringAsync(new Uri($"http://ip-api.com/json/{ip}"));
                    Dictionary<string, dynamic> ipApiObj = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(resp);
                    if (ipApiObj == null || (ipApiObj.ContainsKey("status") && ipApiObj["status"] == "fail")) return;
                    
                    Dictionary<string, dynamic> obj = new Dictionary<string, dynamic>
                    {
                        { "ip-api", ipApiObj }
                    };
                    string modifiedResp = JsonConvert.SerializeObject(obj);
                    Program.CurrentRound.serverLocationResponse = modifiedResp;

                    string loc = (string)obj["ip-api"]["city"] + ", " + (string)obj["ip-api"]["regionName"];

                    // if an aws server, use ipdata.co instead
                    if ((string)obj["ip-api"]["org"] == "AWS EC2 (us-east-1)" || (string)obj["ip-api"]["org"] == "Amazon.com, Inc.")
                    {
                        loc = "Ashburn, Virginia";
                    }

                    Program.CurrentRound.serverLocation = loc;

                    // The header chip is a single line, so the "Server Location:" caption moves into
                    // the tooltip rather than wrapping the label onto a second row.
                    serverLocationLabel.Content = loc;
                    serverLocationLabel.ToolTip =
                        $"{Properties.Resources.Server_Location_} {loc}\n{obj["ip-api"]["query"]}\n{obj["ip-api"]["org"]}\n{obj["ip-api"]["as"]}";

                    try
                    {
                        Program.IPGeolocated?.Invoke(modifiedResp);
                    }
                    catch (Exception)
                    {
                        LogRow(LogType.Error, "Error processing event for IP Geolocation");
                    }

                    if (SparkSettings.instance.serverLocationTTS)
                    {
                        Program.synth.SpeakAsync(loc);
                    }
                }
                catch (HttpRequestException)
                {
                    LogRow(LogType.Error, "Couldn't get city of ip address.");
                }
            }
        }


        private void CloseButtonClicked(object sender, RoutedEventArgs e)
        {
            Hide();
            showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
            hidden = true;
        }

        private void SettingsButtonClicked(object sender, RoutedEventArgs e)
        {
            Program.ToggleWindow(typeof(UnifiedSettingsWindow), "Settings");
        }

        /// <summary>Reloads the dashboard's WebView2 page, resetting its local state (sparkline history, peak).</summary>
        private void RefreshDashboardClick(object sender, RoutedEventArgs e)
        {
            DashboardWebView.CoreWebView2?.Reload();
        }

        /// <summary>
        /// Opens Settings already on the Change Theme tab — a shortcut to what the Settings button
        /// plus a couple of clicks would otherwise take. If Settings is already open, it's brought to
        /// the theme tab in place rather than being toggled closed, since that's more useful for a
        /// "jump to this" icon than the regular open/close toggle.
        /// </summary>
        private void OpenThemeSettingsClick(object sender, RoutedEventArgs e)
        {
            if (Program.GetWindowIfOpen(typeof(UnifiedSettingsWindow), "Settings") is UnifiedSettingsWindow open)
            {
                open.JumpToChangeThemeTab();
                open.Activate();
                return;
            }

            UnifiedSettingsWindow.OpenToChangeTheme = true;
            Program.ToggleWindow(typeof(UnifiedSettingsWindow), "Settings");
        }

        private void QuitButtonClicked(object sender, RoutedEventArgs e)
        {
            isExplicitClose = true;
            Program.Quit();
        }

        private string FilterLines(string input)
        {
            string output = input;
            IEnumerable<string> lines = output
                    .Split('\r', '\n')
                    .Select(l => l.Trim())
                //.Where(l =>
                //{
                //    if (
                //    (!showHideLinesBox.Visible && l.Length > 0) || (
                //    (SparkSettings.instance.outputGameStateEvents && l.Contains("Entered state:")) ||
                //    (SparkSettings.instance.outputScoreEvents && l.Contains("scored")) ||
                //    (SparkSettings.instance.outputStunEvents && l.Contains("just stunned")) ||
                //    (SparkSettings.instance.outputDiscThrownEvents && l.Contains("threw the disk")) ||
                //    (SparkSettings.instance.outputDiscCaughtEvents && l.Contains("caught the disk")) ||
                //    (SparkSettings.instance.outputDiscStolenEvents && l.Contains("stole the disk")) ||
                //    (SparkSettings.instance.outputSaveEvents && l.Contains("save"))
                //    ))
                //    {
                //        return true;
                //    }
                //    else
                //    {
                //        return false;
                //    }
                //})
                ;

            output = string.Join(Environment.NewLine, lines) + ((output != string.Empty) ? Environment.NewLine : string.Empty);

            //return output;
            return input;
        }

        private string FilterLines(List<string> input)
        {
            IEnumerable<string> lines = input
                    .Select(l => l.Trim())
                //.Where(l =>
                //{
                //    if (
                //    (!showHideLinesBox.Visible && l.Length > 0) || (
                //    (SparkSettings.instance.outputGameStateEvents && l.Contains("Entered state:")) ||
                //    (SparkSettings.instance.outputScoreEvents && l.Contains("scored")) ||
                //    (SparkSettings.instance.outputStunEvents && l.Contains("just stunned")) ||
                //    (SparkSettings.instance.outputDiscThrownEvents && l.Contains("threw the disk")) ||
                //    (SparkSettings.instance.outputDiscCaughtEvents && l.Contains("caught the disk")) ||
                //    (SparkSettings.instance.outputDiscStolenEvents && l.Contains("stole the disk")) ||
                //    (SparkSettings.instance.outputSaveEvents && l.Contains("save"))
                //    ))
                //    {
                //        // Show this line
                //        return true;
                //    }
                //    else
                //    {
                //        // hide this line
                //        return false;
                //    }
                //})
                ;

            string output = string.Join(Environment.NewLine, lines) + ((input.Count != 0 && input[0] != string.Empty) ? Environment.NewLine : string.Empty);

            //return output;
            return string.Join(Environment.NewLine, input);
        }

        private void updateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WebClient webClient = new WebClient();
                webClient.DownloadFileCompleted += Completed;
                webClient.DownloadProgressChanged += ProgressChanged;
                webClient.DownloadFileAsync(new Uri(updateFilename), Path.GetTempPath() + Path.GetFileName(updateFilename));
            }
            catch (Exception)
            {
                new MessageBox(Properties.Resources.Something_broke_while_trying_to_download_update_, Properties.Resources.Error).Show();
            }
        }

        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            updateProgressBar.Visibility = Visibility.Visible;
            updateProgressBar.Value = e.ProgressPercentage;
        }

        private void Completed(object sender, AsyncCompletedEventArgs e)
        {
            updateProgressBar.Visibility = Visibility.Collapsed;

            try
            {
                // Install the update
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Path.GetTempPath(), Path.GetFileName(updateFilename) ?? throw new InvalidOperationException()),
                    UseShellExecute = true
                });

                Program.Quit();
            }
            catch (Exception)
            {
                new MessageBox(Properties.Resources.Something_broke_while_trying_to_launch_update_installer, Properties.Resources.Error).Show();
            }
        }

        private void RejoinClicked(object sender, RoutedEventArgs e)
        {
            if (Program.lastFrame == null)
            {
                LogRow(LogType.Error, "Last frame null when trying to use rejoiner.");
                return;
            }

            Program.KillEchoVR();

            // join in spectator if we were in spectator before
            Team team = Program.lastFrame.GetTeam(Program.lastFrame.client_name);
            if (team != null && team.color == Team.TeamColor.spectator)
            {
                Program.StartEchoVR(Program.JoinType.Spectator, session_id: Program.lastFrame.sessionid);
            }

            Program.StartEchoVR(Program.JoinType.Player, session_id: Program.lastFrame.sessionid);
        }

        private void RestartAsSpectatorClick(object sender, RoutedEventArgs e)
        {
            Program.KillEchoVR();
            if (Program.lastFrame != null)
            {
                Program.StartEchoVR(Program.JoinType.Spectator, session_id: Program.lastFrame.sessionid);
            }
        }

        private void showEventLogFileButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Spark", logFolder);
            if (Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            else
            {
                Directory.CreateDirectory(folder);
            }
        }

        private void OpenSpeedometer(object sender, RoutedEventArgs e)
        {
            Program.ToggleWindow(typeof(Speedometer), ownedBy: this);
        }

        private void enableAPIButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                JToken settings = EchoVRSettingsManager.ReadEchoVRSettings();
                if (settings != null)
                {
                    new MessageBox(Properties.Resources.Enabled_API_access_in_the_game_settings__CLOSE_ECHOVR_BEFORE_PRESSING_OK_, callback: () =>
                    {
                        settings["game"]!["EnableAPIAccess"] = true;
                        EchoVRSettingsManager.WriteEchoVRSettings(settings);
                    }).Show();

                    enableAPIButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    new MessageBox("Could not read EchoVR settings. \n How are you even here?").Show();
                }
            }
            catch (Exception)
            {
                LogRow(LogType.Error, "Can't write to EchoVR settings file.");
            }
        }

        private void playspaceButton_Click(object sender, RoutedEventArgs e)
        {
            Program.ToggleWindow(typeof(Playspace));
        }

        private void showHighlights_Click(object sender, RoutedEventArgs e)
        {
            HighlightsHelper.ShowNVHighlights();
        }

        private void LoginWindowButtonClicked(object sender, RoutedEventArgs e)
        {
            Program.ToggleWindow(typeof(LoginWindow), ownedBy: this);
        }

        private void StartSpectatorStreamClick(object sender, RoutedEventArgs e)
        {
            SpectatorStreamModeWindow modePicker = new SpectatorStreamModeWindow { Owner = this };
            if (modePicker.ShowDialog() != true) return;

            Program.StartEchoVR(Program.JoinType.Spectator, noovr: SparkSettings.instance.spectatorStreamNoOVR, combat: modePicker.Combat);
        }

        private void ToggleHidden(object sender, RoutedEventArgs e)
        {
            if (hidden)
            {
                Show();
                showHideMenuItem.Header = Properties.Resources.Hide_Main_Window;
            }
            else
            {
                Hide();
                showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
            }

            hidden = !hidden;
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl) return;

            // if switched to atlas tab
            if (Equals(((TabControl)sender).SelectedItem, LinksTab))
            {
                RefreshCurrentLink();
                GetAtlasMatches();
            }
            // switched to event log tab
            else if (Equals(((TabControl)sender).SelectedItem, EventLogTab))
            {
                ScrollEventLogToEnd();
            }

            if (SpeakerSystemProcess != null)
            {
                if (!Equals(((TabControl)sender).SelectedItem, SpeakerSystemTab))
                {
                    ShowWindow(unityHWND, 0);
                }
                else
                {
                    ShowWindow(unityHWND, 1);
                }
            }

            e.Handled = true;
        }

        private void SpectateMeClicked(object sender, RoutedEventArgs e)
        {
            (string labelText, string subtitleText) = Program.spectateMeController.ToggleSpectateMe();

            Program.liveWindow.spectateMeLabel.Content = labelText;
            Program.liveWindow.spectateMeSubtitle.Text = subtitleText;
        }

        private void EventLogTabClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ScrollEventLogToEnd();
        }

        private void EventLogTabClicked(object sender, System.Windows.Input.TouchEventArgs e)
        {
            ScrollEventLogToEnd();
        }

        private void ScrollEventLogToEnd()
        {
            if (eventLogEntries.Count > 0)
            {
                eventLogListBox.ScrollIntoView(eventLogEntries[^1]);
            }
        }

        private void CopyIgniteJoinLink(object sender, RoutedEventArgs e)
        {
            string link = sessionIdTextBox.Text;
            try
            {
                Clipboard.SetText(link);
                Task.Run(ShowCopiedText);
            }
            catch (COMException ex)
            {
                LogRow(LogType.Error, "Failed to copy text.\n" + ex);
            }
        }

        private async Task ShowCopiedText()
        {
            Dispatcher.Invoke(() => { copySessionIdButton.Content = Properties.Resources.Copied_; });
            await Task.Delay(3000);

            Dispatcher.Invoke(() => { copySessionIdButton.Content = Properties.Resources.Copy; });
        }

        private void speakerSystemPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!speakerSystemPanel.IsVisible) return;
            ShowEchoVRMusic();
        }

        /// <summary>
        /// The Music tab: EchoVRMusic's own window when it's installed, otherwise the offer to
        /// install it into the Echo folder Spark already knows.
        /// </summary>
        private void ShowEchoVRMusic()
        {
            try
            {
                CheckEchoVRMusicUpdate();
                string dir = Program.EchoGameDir;
                echoMusicFolderText.Text = dir.Length > 0
                    ? dir
                    : "Spark doesn't know where Echo VR is yet. Start Echo once with Spark open.";
                if (!Program.IsEchoVRMusicInstalled)
                {
                    if (unityHWND != IntPtr.Zero) ShowWindow(unityHWND, 0);
                    echoMusicInstallPanel.Visibility = Visibility.Visible;
                    echoMusicInstallButton.IsEnabled = dir.Length > 0;
                    return;
                }
                echoMusicInstallPanel.Visibility = Visibility.Collapsed;
                StartEchoVRMusic();
            }
            catch (Exception ex)
            {
                LogRow(LogType.Error, $"[EchoVRMusic] couldn't show the Music tab\n{ex}");
            }
        }

        private bool echoMusicUpdateChecked;
        private string echoMusicUpdateUrl;

        /// <summary>
        /// Once per Spark session, asks GitHub for the latest EchoVRMusic release and offers it in
        /// the bar at the top of the Music tab when it's newer than the app Spark has.
        /// </summary>
        private async void CheckEchoVRMusicUpdate()
        {
            if (echoMusicUpdateChecked) return;
            echoMusicUpdateChecked = true;
            var latest = await Program.GetLatestEchoVRMusicRelease();
            if (latest == null || latest.Value.version <= Program.EchoVRMusicVersion) return;
            echoMusicUpdateUrl = latest.Value.url;
            echoMusicUpdateText.Text = $"EchoVRMusic v{latest.Value.version} is available (you have v{Program.EchoVRMusicVersion}).";
            echoMusicUpdateButton.IsEnabled = true;
            echoMusicUpdateBar.Visibility = Visibility.Visible;
        }

        private async void echoMusicUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            echoMusicUpdateButton.IsEnabled = false;
            echoMusicUpdateText.Text = "Downloading EchoVRMusic...";
            if (!await Program.DownloadEchoVRMusic(echoMusicUpdateUrl))
            {
                echoMusicUpdateText.Text = "Couldn't download the update. Check your internet connection.";
                echoMusicUpdateButton.IsEnabled = true;
                return;
            }

            // The tab's app is the old version: close it, put the new plugin into Echo, reopen it.
            KillSpeakerSystem();
            SpeakerSystemProcess = null;
            if (Program.IsEchoVRMusicInstalled)
            {
                echoMusicUpdateText.Text = "Installing...";
                int code = await Task.Run(Program.InstallEchoVRMusic);
                if (code != 0)
                {
                    echoMusicUpdateText.Text = code == 2
                        ? "Close Echo VR, then press Update again."
                        : "Couldn't install the update into the Echo VR folder.";
                    echoMusicUpdateButton.IsEnabled = true;
                    ShowEchoVRMusic();
                    return;
                }
            }
            echoMusicUpdateBar.Visibility = Visibility.Collapsed;
            ShowEchoVRMusic();
        }

        private async void echoMusicInstallButton_Click(object sender, RoutedEventArgs e)
        {
            echoMusicInstallButton.IsEnabled = false;
            echoMusicStatus.Text = "Installing...";
            int code = await Task.Run(Program.InstallEchoVRMusic);
            echoMusicStatus.Text = code switch
            {
                0 => "",
                1 => "Echo VR isn't in that folder.",
                2 => "Close Echo VR first, then press Install again.",
                3 => "Couldn't download the plugin loader. Check your internet connection.",
                4 => "Couldn't write to the Echo VR folder.",
                _ => "EchoVRMusic is missing from Spark's install. Reinstall Spark.",
            };
            echoMusicInstallButton.IsEnabled = true;
            if (code == 0) ShowEchoVRMusic();
        }

        /// <summary>
        /// Spark's current colours, in the form EchoVRMusicSetup.exe takes (--theme, or WM_COPYDATA
        /// while it runs), so the embedded window matches the theme.
        /// </summary>
        private static string EchoVRMusicTheme()
        {
            static string Hex(string key)
            {
                return Application.Current.Resources[key] is SolidColorBrush b
                    ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}"
                    : null;
            }

            (string name, string key)[] map =
            {
                ("bg", "ControlDarkerBackground"), ("surface", "SurfaceCard"), ("line", "SurfaceBorder"),
                ("text", "TextPrimary"), ("muted", "TextDim"), ("accent", "ControlAccent"),
            };
            return string.Join(",", map.Select(m => (m.name, hex: Hex(m.key)))
                .Where(m => m.hex != null)
                .Select(m => $"{m.name}={m.hex}"));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendCopyData(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT data);

        private const int WM_COPYDATA = 0x004A;
        private const int ECHOVRMUSIC_THEME = 0x45564D54;   // "EVMT"
        private bool echoMusicThemeHooked;

        /// <summary>Sends the current theme to the running EchoVRMusic window.</summary>
        private void SendEchoVRMusicTheme()
        {
            if (unityHWND == IntPtr.Zero) return;
            string theme = EchoVRMusicTheme() + "\0";
            IntPtr text = Marshal.StringToHGlobalUni(theme);
            try
            {
                COPYDATASTRUCT data = new COPYDATASTRUCT
                {
                    dwData = (IntPtr)ECHOVRMUSIC_THEME,
                    cbData = theme.Length * 2,
                    lpData = text,
                };
                SendCopyData(unityHWND, WM_COPYDATA, IntPtr.Zero, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(text);
            }
        }

        /// <summary>
        /// Starts EchoVRMusicSetup.exe as a child of this window (--embed) and puts it over the
        /// Music panel. It shows its settings, and closes by itself when Spark does.
        /// </summary>
        private void StartEchoVRMusic()
        {
            bool running;
            try
            {
                running = SpeakerSystemProcess != null && !SpeakerSystemProcess.HasExited;
            }
            catch (InvalidOperationException)
            {
                running = false;
            }
            if (running)
            {
                if (unityHWND != IntPtr.Zero)
                {
                    ShowWindow(unityHWND, 1);
                    MoveSpeakerSystemWindow();
                }
                return;
            }

            if (!echoMusicThemeHooked)
            {
                echoMusicThemeHooked = true;
                ThemesController.ThemeApplied += () => Dispatcher.BeginInvoke(SendEchoVRMusicTheme);
            }

            try
            {
                IntPtr host = new WindowInteropHelper(this).EnsureHandle();
                Process starting = new Process();
                starting.StartInfo.FileName = Program.EchoVRMusicSetupPath;
                starting.StartInfo.Arguments =
                    $"--embed {host.ToInt64()} --dir \"{Program.EchoGameDir}\" --theme \"{EchoVRMusicTheme()}\"";
                starting.StartInfo.UseShellExecute = false;
                starting.Start();
                SpeakerSystemProcess = starting;
                int pid = starting.Id;
                unityHWND = IntPtr.Zero;

                // The window appears a moment after the process starts.
                Task.Run(() =>
                {
                    IntPtr child = IntPtr.Zero;
                    for (int i = 0; i < 50 && child == IntPtr.Zero; i++)
                    {
                        Thread.Sleep(100);
                        try { child = FindSpeakerSystemChild(host, pid); }
                        catch (Exception) { }
                    }
                    Dispatcher.Invoke(() =>
                    {
                        if (child == IntPtr.Zero)
                        {
                            LogRow(LogType.Error, $"[EchoVRMusic] no window from pid {pid} appeared under Spark's window");
                            return;
                        }
                        unityHWND = child;
                        MoveSpeakerSystemWindow();
                        ShowWindow(child, speakerSystemPanel.IsVisible ? 1 : 0);
                    });
                });
            }
            catch (Exception ex)
            {
                LogRow(LogType.Error, $"[EchoVRMusic] failed to start\n{ex}");
                SpeakerSystemProcess = null;
            }
        }

        private void LoneEchoSubtitlesClick(object sender, RoutedEventArgs e)
        {
            Program.ToggleWindow(typeof(LoneEchoSubtitles));
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogRow(LogType.Error, ex.ToString());
            }
        }

        #region Atlas Links Tab

        private void HostMatchClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Program.hostedAtlasSessionId))
            {
                Program.atlasHostingThread = new Thread(AtlasHostingThread);
                Program.atlasHostingThread.IsBackground = true;
                Program.atlasHostingThread.Start();
                hostingMatchCheckbox.IsChecked = true;
                hostingMatchLabel.Content = Properties.Resources.Stop_Hosting;
            }
            else
            {
                Program.hostedAtlasSessionId = "";
                hostingMatchCheckbox.IsChecked = false;
                hostingMatchLabel.Content = Properties.Resources.Host_Match;
            }
        }

        public class AtlasMatchResponse
        {
            public List<AtlasMatch> matches;
            public string player;
            public string qtype;
            public string datetime;
        }

        public class AtlasMatch
        {
            public class AtlasTeamInfo
            {
                public int count;
                public float percentage;
                public string team_logo;
                public string team_name;
            }

            [Obsolete("Use matchid instead")] public string session_id;

            /// <summary>
            /// Session id. This could be empty if the match isn't available to join
            /// </summary>
            public string matchid;

            /// <summary>
            /// Who hosted this match?
            /// </summary>
            public string username;

            public AtlasTeamInfo blue_team_info;
            public AtlasTeamInfo orange_team_info;

            /// <summary>
            /// List of player names
            /// </summary>
            public string[] blue_team;

            /// <summary>
            /// List of player names
            /// </summary>
            public string[] orange_team;

            /// <summary>
            /// If this is true, users with the caster login in Spark can see this match
            /// </summary>
            public bool visible_to_casters;

            /// <summary>
            /// Hides the match from public view. Can still be viewed by whitelist or casters if visible_for_casters is true
            /// </summary>
            public bool is_protected;

            /// <summary>
            /// Resolved location of the server (e.g. Chicago, Illinois)
            /// </summary>
            public string server_location;

            public float server_score;

            /// <summary>
            /// arena
            /// </summary>
            public string match_type;

            public string description;
            public bool is_lfg;
            public string[] whitelist;

            /// <summary>
            /// Currently used-up slots
            /// </summary>
            public int slots;

            /// <summary>
            /// Maximum allowed people in the match
            /// </summary>
            public int max_slots;

            public int blue_points;
            public int orange_points;
            public string title;
            public string map_name;
            public string game_type;
            public bool tournament_match;
            public string game_status;
            public bool allow_spectators;
            public bool private_match;
            public float game_clock;
            public string game_clock_display;

            public Dictionary<string, object> ToDict()
            {
                try
                {
                    Dictionary<string, object> values = new()
                    {
                        { "matchid", matchid },
                        { "username", username },
                        { "blue_team", blue_team },
                        { "orange_team", orange_team },
                        { "is_protected", is_protected },
                        { "visible_to_casters", visible_to_casters },
                        { "server_location", server_location },
                        { "server_score", server_score },
                        { "private_match", private_match },
                        { "whitelist", whitelist },
                        { "blue_points", blue_points },
                        { "orange_points", orange_points },
                        { "slots", slots },
                        { "allow_spectators", allow_spectators },
                        { "game_status", game_status },
                        { "game_clock", game_clock },
                    };
                    return values;
                }
                catch (Exception e)
                {
                    LogRow(LogType.Error, $"Can't serialize atlas match data.\n{e.Message}\n{e.StackTrace}");
                    return new Dictionary<string, object>
                    {
                        { "none", 0 }
                    };
                }
            }
        }

        public class AtlasWhitelist
        {
            public class AtlasTeam
            {
                public string teamName;
                public List<string> players = new();

                public AtlasTeam(string teamName)
                {
                    this.teamName = teamName;
                }
            }

            public List<AtlasTeam> teams = new();
            public List<string> players = new();

            public List<string> TeamNames => teams.Select(t => t.teamName).ToList();

            public List<string> AllPlayers
            {
                get
                {
                    List<string> allPlayers = new List<string>(players);
                    foreach (AtlasTeam team in teams)
                    {
                        allPlayers.AddRange(team.players);
                    }

                    return allPlayers;
                }
            }
        }

        private void UpdateUIWithAtlasMatches(IEnumerable<AtlasMatch> matches)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // remove all the old children
                    MatchesBox.Children.RemoveRange(0, MatchesBox.Children.Count);

                    foreach (AtlasMatch match in matches)
                    {
                        Grid content = new Grid();
                        StackPanel header = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            VerticalAlignment = VerticalAlignment.Top,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 0, 10, 0)
                        };
                        header.Children.Add(new Label
                        {
                            Content = match.is_protected ? (match.visible_to_casters ? Properties.Resources.Casters_Only : Properties.Resources.Private) : Properties.Resources.Public
                        });

                        byte buttonColor = 70;
                        Button copyLinkButton = new Button
                        {
                            Content = Properties.Resources.Copy_Spark_Link,
                            Margin = new Thickness(50, 0, 0, 0),
                            Padding = new Thickness(10, 0, 10, 0),
                            Background = new SolidColorBrush(Color.FromRgb(buttonColor, buttonColor, buttonColor)),
                        };
                        copyLinkButton.Click += (_, _) => { Clipboard.SetText(Program.CurrentSparkLink(match.matchid)); };
                        header.Children.Add(copyLinkButton);
                        Button joinButton = new Button
                        {
                            Content = Properties.Resources.Join,
                            Margin = new Thickness(20, 0, 0, 0),
                            Padding = new Thickness(10, 0, 10, 0),
                            Background = new SolidColorBrush(Color.FromRgb(buttonColor, buttonColor, buttonColor)),
                        };
                        joinButton.Click += (_, _) =>
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://echo.taxi/spark://c/" + match.matchid,
                                UseShellExecute = true
                            });
                        };
                        header.Children.Add(joinButton);

                        if (!string.IsNullOrEmpty(match.title) && match.title != "Default Lobby Name")
                        {
                            header.Children.Add(new Label
                            {
                                Content = match.title
                            });
                        }
                        else if (!string.IsNullOrEmpty(match.server_location))
                        {
                            header.Children.Add(new Label
                            {
                                Content = match.server_location
                            });
                        }

                        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

                        content.ShowGridLines = true;

                        Image blueLogo2 = new Image
                        {
                            Width = 100,
                            Height = 100
                        };
                        if (match.blue_team_info?.team_logo != string.Empty)
                        {
                            blueLogo2.Source = string.IsNullOrEmpty(match.blue_team_info?.team_logo) ? null : (new BitmapImage(new Uri(match.blue_team_info.team_logo)));
                        }

                        StackPanel blueLogoBox = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(5, 10, 5, 10)
                        };
                        blueLogoBox.SetValue(Grid.ColumnProperty, 0);
                        blueLogoBox.Children.Add(blueLogo2);
                        blueLogoBox.Children.Add(new Label
                        {
                            Content = match.blue_team_info?.team_name,
                            HorizontalAlignment = HorizontalAlignment.Center
                        });


                        Image orangeLogo2 = new Image
                        {
                            Width = 100,
                            Height = 100
                        };
                        if (match.orange_team_info?.team_logo != string.Empty)
                        {
                            orangeLogo2.Source = string.IsNullOrEmpty(match.orange_team_info?.team_logo) ? null : (new BitmapImage(new Uri(match.orange_team_info.team_logo)));
                        }

                        StackPanel orangeLogoBox = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(5, 10, 5, 10)
                        };
                        orangeLogoBox.SetValue(Grid.ColumnProperty, 3);
                        orangeLogoBox.Children.Add(orangeLogo2);
                        orangeLogoBox.Children.Add(new Label
                        {
                            Content = match.orange_team_info?.team_name,
                            HorizontalAlignment = HorizontalAlignment.Center
                        });

                        TextBlock bluePlayers = new TextBlock
                        {
                            Text = string.Join('\n', match.blue_team),
                            Margin = new Thickness(10, 10, 10, 10),
                            TextAlignment = TextAlignment.Right
                        };
                        bluePlayers.SetValue(Grid.ColumnProperty, 1);
                        TextBlock orangePlayers = new TextBlock
                        {
                            Text = string.Join('\n', match.orange_team),
                            Margin = new Thickness(10, 10, 10, 10)
                        };
                        orangePlayers.SetValue(Grid.ColumnProperty, 2);
                        // Label sessionIdTextBox = new Label
                        // {
                        //  Content = match.matchid
                        // };
                        //content.Children.Add(sessionIdTextBox);
                        content.Children.Add(blueLogoBox);
                        content.Children.Add(orangeLogoBox);
                        content.Children.Add(bluePlayers);
                        content.Children.Add(orangePlayers);
                        MatchesBox.Children.Add(new GroupBox
                        {
                            Content = content,
                            Margin = new Thickness(10, 10, 10, 10),
                            Header = header
                        });
                    }
                });
            }
            catch (Exception e)
            {
                LogRow(LogType.Error, $"Error showing matches in UI\n{e}");
            }
        }

        private void AtlasHostingThread()
        {
            const string hostURL = Program.APIURL + "/host_match";
            const string unhostURL = Program.APIURL + "/unhost_match";

            // TODO show error message instead of just quitting
            if (Program.lastFrame == null || Program.lastFrame.teams == null) return;

            Program.hostedAtlasSessionId = Program.lastFrame.sessionid;

            AtlasMatch match = new AtlasMatch
            {
                matchid = Program.lastFrame.sessionid,
                blue_team = Program.lastFrame.teams[0].player_names.ToArray(),
                orange_team = Program.lastFrame.teams[1].player_names.ToArray(),
                is_protected = (SparkSettings.instance.atlasHostingVisibility > 0),
                visible_to_casters = (SparkSettings.instance.atlasHostingVisibility == 1),
                server_location = Program.CurrentRound.serverLocation,
                server_score = Program.CurrentRound.serverScore,
                private_match = Program.lastFrame.private_match,
                username = Program.lastFrame.client_name,
                whitelist = Program.atlasWhitelist.AllPlayers.ToArray(),
            };
            bool firstHost = true;

            while (Program.running &&
                   Program.InGame &&
                   Program.lastFrame != null &&
                   Program.lastFrame.teams != null &&
                   Program.hostedAtlasSessionId == Program.lastFrame.sessionid)
            {
                bool diff =
                    firstHost ||
                    match.blue_team.Length != Program.lastFrame.teams[0].players.Count ||
                    match.orange_team.Length != Program.lastFrame.teams[1].players.Count ||
                    (Program.lastFrame.teams[0].stats != null && match.blue_points != Program.lastFrame.teams[0].stats.points) ||
                    (Program.lastFrame.teams[1].stats != null && match.orange_points != Program.lastFrame.teams[1].stats.points) ||
                    match.is_protected != (SparkSettings.instance.atlasHostingVisibility > 0) ||
                    // match.visible_to_casters != (SparkSettings.instance.atlasHostingVisibility == 1) ||
                    match.whitelist.Length != Program.atlasWhitelist.AllPlayers.Count;

                if (diff)
                {
                    // actually update values
                    match.blue_team = Program.lastFrame.teams[0].player_names.ToArray();
                    match.orange_team = Program.lastFrame.teams[1].player_names.ToArray();
                    match.blue_points = Program.lastFrame.teams[0].stats != null ? Program.lastFrame.teams[0].stats.points : 0;
                    match.orange_points = Program.lastFrame.teams[1].stats != null ? Program.lastFrame.teams[1].stats.points : 0;
                    match.is_protected = (SparkSettings.instance.atlasHostingVisibility > 0);
                    // match.visible_to_casters = (SparkSettings.instance.atlasHostingVisibility == 1);
                    match.server_score = Program.CurrentRound.serverScore;
                    match.username = Program.lastFrame.client_name;
                    match.whitelist = Program.atlasWhitelist.AllPlayers.ToArray();
                    match.slots = Program.lastFrame.GetAllPlayers().Count;

                    string data = JsonConvert.SerializeObject(match.ToDict());
                    firstHost = false;

                    // post new data, then fetch the updated list
                    FetchUtils.PostRequestCallback(
                        hostURL,
                        new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } },
                        data,
                        _ => { GetAtlasMatches(); });
                }

                Thread.Sleep(100);
            }

            // post new data, then fetch the updated list
            string matchInfo = JsonConvert.SerializeObject(match.ToDict());
            FetchUtils.PostRequestCallback(
                unhostURL,
                new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } },
                matchInfo,
                _ =>
                {
                    Program.hostedAtlasSessionId = string.Empty;
                    Dispatcher.Invoke(() =>
                    {
                        hostingMatchCheckbox.IsChecked = false;
                        hostingMatchLabel.Content = Properties.Resources.Host_Match;
                    });
                    Thread.Sleep(10);
                    GetAtlasMatches();
                });
        }

        private void GetAtlasMatches()
        {
            string matchesAPIURL = $"{Program.APIURL}/hosted_matches/{(SparkSettings.instance.client_name == string.Empty ? "_" : SparkSettings.instance.client_name)}";
            FetchUtils.GetRequestCallback(
                matchesAPIURL,
                new Dictionary<string, string>()
                {
                    { "x-api-key", DiscordOAuth.igniteUploadKey },
                    { "access_code", DiscordOAuth.AccessCode.series_name }
                },
                responseJSON =>
                {
                    try
                    {
                        AtlasMatchResponse igniteAtlasResponse = JsonConvert.DeserializeObject<AtlasMatchResponse>(responseJSON);
                        if (igniteAtlasResponse != null) UpdateUIWithAtlasMatches(igniteAtlasResponse.matches);
                    }
                    catch (Exception e)
                    {
                        LogRow(LogType.Error, $"Can't parse Atlas matches response\n{e}");
                    }
                }
            );
        }

        private void RefreshMatchesClicked(object sender, RoutedEventArgs e)
        {
            GetAtlasMatches();
        }

        private void RefreshCurrentLink()
        {
            UpdateJoinLink();
        }

        /// <summary>Shown in the join-link boxes when there's no session to share.</summary>
        private const string SessionIdPlaceholder = "********-****-****-****-************";

        /// <summary>
        /// The session worth sharing right now.
        ///
        /// In a match it's the one the game API reports. In a social lobby the API answers error -6
        /// with no session id at all, so the link would sit on its placeholder and a lobby could
        /// never be shared — the client's own log records the room it's in, and that fills the gap.
        /// </summary>
        private static string CurrentJoinableSessionId()
        {
            if (Program.connectionState == Program.ConnectionState.InLobby)
            {
                return EchoLogSessionReader.CurrentSessionId;
            }

            return Program.lastFrame?.sessionid;
        }

        private void UpdateJoinLink()
        {
            string sessionId = CurrentJoinableSessionId();
            string link = string.IsNullOrEmpty(sessionId)
                ? SessionIdPlaceholder
                : Program.CurrentSparkLink(sessionId);

            if (sessionIdTextBox != null && sessionIdTextBox.Text != link) sessionIdTextBox.Text = link;
            if (joinLink != null && joinLink.Text != link) joinLink.Text = link;
        }

        private void CopyMainLinkToClipboard(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(joinLink.Text);
        }

        private void FollowMainLink(object sender, RoutedEventArgs e)
        {
            try
            {
                if (joinLink.Text.Length > 10)
                {
                    string text = joinLink.Text;
                    if (joinLink.Text.StartsWith('<'))
                    {
                        text = text[1..^1];
                    }

                    text = text.Split(' ')[0];
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = text,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                LogRow(LogType.Error, ex.ToString());
            }
        }

        private void WhitelistButtonClicked(object sender, RoutedEventArgs e)
        {
            Program.ToggleWindow(typeof(AtlasWhitelistWindow), "Atlas Whitelist", this);
        }

        public int LinkType
        {
            get => SparkSettings.instance.atlasLinkStyle;
            set
            {
                SparkSettings.instance.atlasLinkStyle = value;
                RefreshCurrentLink();
            }
        }

        #endregion

        private void DashboardItem1Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!initialized) return;
            int index = ((ComboBox)sender).SelectedIndex;
            SetDashboardItem1Visibility(index);
        }

        private void SetDashboardItem1Visibility(int index)
        {
            switch (index)
            {
                case 0:
                    playerSpeedsBox.Visibility = Visibility.Collapsed;
                    lastThrowStats.Visibility = Visibility.Visible;
                    break;
                case 1:
                    playerSpeedsBox.Visibility = Visibility.Visible;
                    lastThrowStats.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void chooseServerRegion_Click(object sender, RoutedEventArgs e)
        {
            Program.ToggleWindow(typeof(CreateServer), ownedBy: this);
        }

        private void showOverlay_Click(object sender, RoutedEventArgs e)
        {
            SparkSettings.instance.spectatorOverlayEnabled = !SparkSettings.instance.spectatorOverlayEnabled;
            SparkSettings.instance.Save();

            // apply right away instead of waiting for the controller's next check
            Program.spectatorOverlayController?.Refresh();
            ClickableOverlaySubtitle.Text = SparkSettings.instance.spectatorOverlayEnabled
                ? (Program.GetWindowIfOpen(typeof(GameOverlay)) != null ? Properties.Resources.Active : Properties.Resources.Echo_VR_not_active)
                : Properties.Resources.Not_active;
        }

        // ───── overlay picker ─────

        private static readonly (string name, string url)[] BuiltInOverlays =
        {
            ("Spark Arena", "/arena_overlay"),
            ("Configurable overlay", "/configurable_overlay"),
            ("Full overlay", "/full_overlay"),
            ("Midmatch overlay", "/midmatch_overlay"),
            ("Minimap only", "/components/minimap"),
        };

        private bool populatingOverlayPicker;

        private void PopulateOverlayPicker()
        {
            populatingOverlayPicker = true;
            try
            {
                string current = SparkSettings.instance.gameOverlayUrl ?? "";
                OverlayPicker.Items.Clear();

                void Add(string name, string url)
                {
                    if (OverlayPicker.Items.Cast<ComboBoxItem>().Any(i => SameOverlay((string)i.Tag, url))) return;
                    OverlayPicker.Items.Add(new ComboBoxItem { Content = name, Tag = url, ToolTip = url });
                }

                foreach ((string name, string url) in BuiltInOverlays) Add(name, url);

                // your own overlays: .html files in the Overlays folder are served at /<name>
                try
                {
                    if (Directory.Exists(OverlayServer.StaticOverlayFolder))
                    {
                        foreach (string file in Directory.EnumerateFiles(OverlayServer.StaticOverlayFolder, "*.html", SearchOption.TopDirectoryOnly).OrderBy(f => f))
                        {
                            string n = Path.GetFileNameWithoutExtension(file);
                            if (n == "index") continue;
                            Add(n + " (my overlay)", "/" + n);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogRow(Logger.LogType.Error, $"Couldn't list custom overlays: {e.Message}");
                }

                // URLs typed in before
                foreach (string url in SparkSettings.instance.gameOverlayCustomUrls ?? new List<string>()) Add(url, url);
                if (!string.IsNullOrWhiteSpace(current)) Add(current, current);

                OverlayPicker.SelectedItem = OverlayPicker.Items.Cast<ComboBoxItem>().FirstOrDefault(i => SameOverlay((string)i.Tag, current));
            }
            finally
            {
                populatingOverlayPicker = false;
            }
        }

        private static string NormalizeOverlayUrl(string url)
        {
            url = (url ?? "").Trim();
            if (url.StartsWith("http://localhost:6724/", StringComparison.OrdinalIgnoreCase)) url = url["http://localhost:6724".Length..];
            if (url.StartsWith("http://127.0.0.1:6724/", StringComparison.OrdinalIgnoreCase)) url = url["http://127.0.0.1:6724".Length..];
            return url.TrimEnd('/').ToLowerInvariant();
        }

        private static bool SameOverlay(string a, string b) => NormalizeOverlayUrl(a) == NormalizeOverlayUrl(b);

        private void SetOverlayUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || SameOverlay(url, SparkSettings.instance.gameOverlayUrl)) return;
            SparkSettings.instance.gameOverlayUrl = url;
            SparkSettings.instance.Save();
            Program.spectatorOverlayController?.Reload();
        }

        private void OverlayPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (populatingOverlayPicker) return;
            if (OverlayPicker.SelectedItem is ComboBoxItem item) SetOverlayUrl((string)item.Tag);
        }

        private void OverlayPicker_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CommitTypedOverlay();
        }

        private void OverlayPicker_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitTypedOverlay();
        }

        private void OverlayPicker_DropDownOpened(object sender, EventArgs e)
        {
            // pick up overlay files added since the window opened
            PopulateOverlayPicker();
        }

        /// <summary>Accepts whatever was typed: a URL, a path on this server, or a local .html file.</summary>
        private void CommitTypedOverlay()
        {
            if (populatingOverlayPicker) return;
            string text = OverlayPicker.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // the text of an existing entry: that entry's URL
            ComboBoxItem match = OverlayPicker.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Content == text);
            if (match != null)
            {
                SetOverlayUrl((string)match.Tag);
                return;
            }

            string url = text;
            if (File.Exists(text)) url = new Uri(Path.GetFullPath(text)).AbsoluteUri;
            else if (!url.Contains("://") && !url.StartsWith('/')) url = (url.Contains('.') && !url.EndsWith(".html") ? "https://" : "/") + url;

            SparkSettings.instance.gameOverlayCustomUrls ??= new List<string>();
            if (!SparkSettings.instance.gameOverlayCustomUrls.Any(u => SameOverlay(u, url)) && !BuiltInOverlays.Any(b => SameOverlay(b.url, url)))
            {
                SparkSettings.instance.gameOverlayCustomUrls.Add(url);
            }

            SetOverlayUrl(url);
            PopulateOverlayPicker();
        }

        private void OpenOverlayFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(OverlayServer.StaticOverlayFolder);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{OverlayServer.StaticOverlayFolder}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.LogRow(Logger.LogType.Error, $"Couldn't open overlay folder: {ex.Message}");
            }
        }


        private void AccessCodeChangedLiveWindow(object sender, SelectionChangedEventArgs e)
        {
            if (accessCodeDropdownListenerActive)
            {
                string username = AccessCodesComboboxLiveWindow.SelectedValue.ToString();
                DiscordOAuth.SetAccessCodeByUsername(username);
            }
        }


        private void FindAllQuests(object sender, RoutedEventArgs e)
        {
            Program.ToggleWindow(typeof(QuestIPs));
        }

        private void OpenOverlays(object sender, RoutedEventArgs e)
        {
            OpenWebpage("http://localhost:6724/");
        }

        private static void OpenWebpage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogRow(LogType.Error, ex.ToString());
            }
        }

        private async void PasteLinkInLive(object sender, RoutedEventArgs e)
        {
            if (Program.lastFrame == null) return;
            try
            {
                Process.Start(new ProcessStartInfo("discord://discordapp.com/channels/776209623857889361/794763716645355560") { UseShellExecute = true });
                Clipboard.SetText(Program.CurrentSparkLink(Program.lastFrame.sessionid));
                await Task.Delay(1000);
                Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_LCONTROL, false, Keyboard.InputType.Keyboard);
                await Task.Delay(10);
                Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_V, false, Keyboard.InputType.Keyboard);
                await Task.Delay(10);
                Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_V, true, Keyboard.InputType.Keyboard);
                await Task.Delay(10);
                Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_LCONTROL, true, Keyboard.InputType.Keyboard);
            }
            catch (Exception ex)
            {
                LogRow(LogType.Error, ex.ToString());
            }
        }

        private void DefaultMatchSetupClick(object sender, RoutedEventArgs e)
        {
            OpenWebpage("http://localhost:6724/overlays/match_setup");
        }

        private void MatchSetupClick(object sender, RoutedEventArgs e)
        {
            OpenWebpage("http://localhost:6724/" + DiscordOAuth.AccessCode.series_name.Split('_')[0] + "/match_setup");
        }

        private void LeagueOverlaysClick(object sender, RoutedEventArgs e)
        {
            OpenWebpage("http://localhost:6724/" + DiscordOAuth.AccessCode.series_name.Split('_')[0]);
        }

        private void ServerLocationButtonClicked(object sender, RoutedEventArgs e)
        {
            tabControl.SelectedItem = ServerInfoTab;
        }



        private void ACIIECHO_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    string sparkFolder = Path.GetDirectoryName(SparkSettings.instance.sparkExeLocation) ?? "";
                    string exePath = Path.Combine(sparkFolder, "resources", "asciiecho.exe");

                    Process p = Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Maximized,
                    });
                }
                catch (Exception ex)
                {
                    Error(ex.ToString());
                }
            });
        }

        private void BlueTeamPauseClick(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "team_idx", 0 }
            };
            FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_pause", null, JsonConvert.SerializeObject(data), null);
        }

        private void OrangeTeamPauseClick(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "team_idx", 1 }
            };
            FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_pause", null, JsonConvert.SerializeObject(data), null);
        }

        private void BlueTeamReadyClick(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "team_idx", 0 }
            };
            FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_ready", null, JsonConvert.SerializeObject(data), null);
        }

        private void OrangeTeamReadyClick(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "team_idx", 1 }
            };
            FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_ready", null, JsonConvert.SerializeObject(data), null);
        }

        private void BlueTeamRestartClick(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "team_idx", 0 }
            };
            FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/restart_request", null, JsonConvert.SerializeObject(data), null);
        }

        private void OrangeTeamRestartClick(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "team_idx", 1 }
            };
            FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/restart_request", null, JsonConvert.SerializeObject(data), null);
        }

        private void OpenLocalDatabaseBrowser(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:6724/local_database",
                UseShellExecute = true
            });
        }

        private async void ReplayViewer_Click(object sender, RoutedEventArgs e)
        {
            await LaunchInstallReplayViewer(false);
        }

        private async Task LaunchInstallReplayViewer(bool vrMode)
        {
            string versionFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Replay Viewer", "version.txt");

            VersionJson remoteVersion = await GetGitHubVersion("robidasdavid", "Demo-Viewer");
            if (remoteVersion == null)
            {
                Error("Failed to get Replay Viewer version from GitHub.");
                return;
            }

            if (File.Exists(versionFile))
            {
                string localVersion = await File.ReadAllTextAsync(versionFile);
                if (localVersion != remoteVersion.tag_name)
                {
                    await InstallReplayViewer(versionFile, remoteVersion);
                }
            }
            else
            {
                await InstallReplayViewer(versionFile, remoteVersion);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Replay Viewer", "Replay Viewer.exe"),
                Arguments = vrMode ? " -useVR" : "",
                UseShellExecute = true
            });
        }

        private async Task InstallReplayViewer(string versionFile, VersionJson remoteVersion)
        {
            ReplayViewerProgressBar.Visibility = Visibility.Visible;
            string zipUrl = remoteVersion.assets.First(url => url.browser_download_url.EndsWith("zip")).browser_download_url;
            HttpResponseMessage response = await FetchUtils.client.GetAsync(zipUrl);
            string fileName = Path.Combine(Path.GetTempPath(), "replay_viewer.zip");
            await using (FileStream fs = new FileStream(fileName, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }

            string replayViewerFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Replay Viewer");
            if (!Directory.Exists(replayViewerFolder))
            {
                Directory.CreateDirectory(replayViewerFolder);
            }

            await Task.Run(() => Directory.Delete(replayViewerFolder, true));
            await Task.Run(() => ZipFile.ExtractToDirectory(fileName, replayViewerFolder));
            await File.WriteAllTextAsync(versionFile, remoteVersion.tag_name);
            ReplayViewerProgressBar.Visibility = Visibility.Collapsed;
        }

        private static async Task<VersionJson> GetGitHubVersion(string authorName, string repoName)
        {
            try
            {
                string resp = await FetchUtils.client.GetStringAsync($"https://api.github.com/repos/{authorName}/{repoName}/releases/latest");
                return JsonConvert.DeserializeObject<VersionJson>(resp);
            }
            catch (Exception e)
            {
                LogRow(LogType.Error, e.Message);
                return null;
            }
        }

        private async void ReplayViewerVR_Click(object sender, RoutedEventArgs e)
        {
            await LaunchInstallReplayViewer(true);
        }
    }
}
