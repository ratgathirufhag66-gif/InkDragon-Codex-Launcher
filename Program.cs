using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace InkDragonCodexVideo
{
    internal static class Program
    {
        private static Mutex _singleInstance;

        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            _singleInstance = new Mutex(true, "InkDragonCodex.VideoLauncher.v1", out created);
            if (!created)
            {
                WindowFinder.ActivateCodexWindow();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Write("Unhandled: " + e.ExceptionObject);
            };

            var options = LaunchOptions.Load(args);
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                Log.Write("Dispatcher: " + e.Exception);
                MessageBox.Show("启动器遇到问题。\n\n" + e.Exception.Message + "\n\n日志：" + Log.Path,
                    "墨龙 Codex 启动器", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
                app.Shutdown();
            };

            app.Run(new VideoSplashWindow(options));
            _singleInstance.ReleaseMutex();
        }
    }

    internal sealed class LaunchOptions
    {
        public string Video = "media\\startup.mp4";
        public int LaunchDelayMs = 0;
        public int AutoSkipMs = -1;
        public double Volume = 1.0;
        public double MaxWaitSeconds = 20.0;
        public double HandoffWaitSeconds = 60.0;
        public double ReadyStabilitySeconds = 6.0;
        public double FadeSeconds = 0.55;
        public bool LaunchCodex = true;
        public bool ForceLaunch = false;
        public bool Fullscreen = true;
        public bool FillScreen = false;
        public string AppLaunch = "shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App";
        public string[] ProcessNames = new[] { "ChatGPT", "Codex" };

        public static LaunchOptions Load(string[] args)
        {
            var result = new LaunchOptions();
            string config = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.ini");
            if (File.Exists(config))
            {
                foreach (string raw in File.ReadAllLines(config))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int index = line.IndexOf('=');
                    if (index < 1) continue;
                    string key = line.Substring(0, index).Trim();
                    string value = line.Substring(index + 1).Trim();
                    int integer;
                    double number;
                    bool boolean;
                    if (key.Equals("Video", StringComparison.OrdinalIgnoreCase)) result.Video = value;
                    else if (key.Equals("LaunchDelayMs", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out integer)) result.LaunchDelayMs = Math.Max(0, integer);
                    else if (key.Equals("Volume", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) result.Volume = Math.Max(0, Math.Min(1, number));
                    else if (key.Equals("MaxWaitSeconds", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) result.MaxWaitSeconds = Math.Max(3, number);
                    else if (key.Equals("HandoffWaitSeconds", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) result.HandoffWaitSeconds = Math.Max(5, number);
                    else if (key.Equals("ReadyStabilitySeconds", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) result.ReadyStabilitySeconds = Math.Max(1, number);
                    else if (key.Equals("FadeSeconds", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) result.FadeSeconds = Math.Max(0, Math.Min(2, number));
                    else if (key.Equals("Fullscreen", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out boolean)) result.Fullscreen = boolean;
                    else if (key.Equals("FillScreen", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out boolean)) result.FillScreen = boolean;
                    else if (key.Equals("LaunchCodex", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out boolean)) result.LaunchCodex = boolean;
                    else if (key.Equals("AppLaunch", StringComparison.OrdinalIgnoreCase)) result.AppLaunch = value;
                    else if (key.Equals("ProcessNames", StringComparison.OrdinalIgnoreCase)) result.ProcessNames = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
                }
            }

            foreach (string arg in args)
            {
                if (arg.Equals("--preview", StringComparison.OrdinalIgnoreCase) || arg.Equals("--no-launch", StringComparison.OrdinalIgnoreCase)) result.LaunchCodex = false;
                else if (arg.Equals("--windowed", StringComparison.OrdinalIgnoreCase)) result.Fullscreen = false;
                else if (arg.Equals("--fill", StringComparison.OrdinalIgnoreCase)) result.FillScreen = true;
                else if (arg.Equals("--mute", StringComparison.OrdinalIgnoreCase)) result.Volume = 0;
                else if (arg.Equals("--force-launch", StringComparison.OrdinalIgnoreCase)) result.ForceLaunch = true;
                else if (arg.StartsWith("--auto-skip-ms=", StringComparison.OrdinalIgnoreCase))
                {
                    int autoSkip;
                    if (int.TryParse(arg.Substring(15), out autoSkip)) result.AutoSkipMs = Math.Max(100, autoSkip);
                }
            }
            return result;
        }
    }

    internal sealed class VideoSplashWindow : Window
    {
        private readonly LaunchOptions _options;
        private readonly Grid _root = new Grid();
        private readonly MediaElement _video = new MediaElement();
        private readonly Border _loadingCover = new Border();
        private readonly TextBlock _hint = new TextBlock();
        private readonly Border _progress = new Border();
        private readonly ScaleTransform _progressScale = new ScaleTransform(0, 1);
        private readonly DispatcherTimer _progressTimer = new DispatcherTimer();
        private readonly DispatcherTimer _fallbackTimer = new DispatcherTimer();
        private readonly DispatcherTimer _handoffTimer = new DispatcherTimer();
        private readonly Border _handoffPanel = new Border();
        private readonly TextBlock _handoffMessage = new TextBlock();
        private bool _launched;
        private bool _finishing;
        private bool _endFadeStarted;
        private bool _handoffRetried;
        private DateTime _handoffStartedAt;
        private DateTime _candidateWindowSince;
        private DateTime _lastReadinessProbe;
        private IntPtr _candidateWindow;
        private TimeSpan? _naturalDuration;

        public VideoSplashWindow(LaunchOptions options)
        {
            _options = options;
            Title = "墨龙 Codex 启动器";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Background = Brushes.Black;
            AllowsTransparency = false;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            var bounds = Forms.Screen.PrimaryScreen.Bounds;
            if (options.Fullscreen)
            {
                Left = bounds.Left;
                Top = bounds.Top;
                Width = bounds.Width;
                Height = bounds.Height;
            }
            else
            {
                Width = Math.Min(1280, bounds.Width * 0.82);
                Height = Math.Min(720, bounds.Height * 0.82);
                Left = bounds.Left + (bounds.Width - Width) / 2.0;
                Top = bounds.Top + (bounds.Height - Height) / 2.0;
            }

            BuildVisualTree();
            Content = _root;
            Loaded += OnLoaded;
            Closed += OnClosed;
            PreviewKeyDown += OnKeyDown;
            MouseLeftButtonDown += delegate { if (!_options.Fullscreen) DragMove(); };
        }

        private void BuildVisualTree()
        {
            _root.Background = Brushes.Black;

            _video.LoadedBehavior = MediaState.Manual;
            _video.UnloadedBehavior = MediaState.Manual;
            _video.Stretch = _options.FillScreen ? Stretch.UniformToFill : Stretch.Uniform;
            _video.Volume = _options.Volume;
            _video.ScrubbingEnabled = true;
            _video.MediaOpened += OnMediaOpened;
            _video.MediaEnded += OnMediaEnded;
            _video.MediaFailed += OnMediaFailed;
            _root.Children.Add(_video);

            _loadingCover.Background = Brushes.Black;
            _loadingCover.Opacity = 1;
            _root.Children.Add(_loadingCover);

            _hint.Text = "ESC  跳过";
            _hint.Foreground = new SolidColorBrush(Color.FromArgb(150, 238, 241, 243));
            _hint.FontFamily = new FontFamily("Microsoft YaHei UI");
            _hint.FontSize = 11;
            _hint.HorizontalAlignment = HorizontalAlignment.Right;
            _hint.VerticalAlignment = VerticalAlignment.Bottom;
            _hint.Margin = new Thickness(0, 0, 24, 22);
            _hint.Opacity = 0;
            _root.Children.Add(_hint);

            _progress.Height = 2;
            _progress.VerticalAlignment = VerticalAlignment.Bottom;
            _progress.HorizontalAlignment = HorizontalAlignment.Stretch;
            _progress.Background = new SolidColorBrush(Color.FromArgb(190, 220, 237, 244));
            _progress.RenderTransformOrigin = new Point(0, 0.5);
            _progress.RenderTransform = _progressScale;
            _root.Children.Add(_progress);

            _handoffMessage.Text = "正在启动 Codex…";
            _handoffMessage.Foreground = new SolidColorBrush(Color.FromRgb(224, 232, 236));
            _handoffMessage.FontFamily = new FontFamily("Microsoft YaHei UI Light, Microsoft YaHei UI");
            _handoffMessage.FontSize = 18;
            _handoffMessage.HorizontalAlignment = HorizontalAlignment.Center;
            _handoffMessage.VerticalAlignment = VerticalAlignment.Center;
            _handoffMessage.Margin = new Thickness(24);

            _handoffPanel.Background = Brushes.Black;
            _handoffPanel.Child = _handoffMessage;
            _handoffPanel.Visibility = Visibility.Collapsed;
            _root.Children.Add(_handoffPanel);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Log.Write("InkDragonCodex v1.3.0 starting.");
            string videoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _options.Video);
            if (!File.Exists(videoPath))
            {
                OnPlaybackError("找不到启动视频：" + videoPath);
                return;
            }

            Log.Write("Opening video: " + videoPath);
            _video.Source = new Uri(videoPath, UriKind.Absolute);
            if (_options.LaunchDelayMs <= 0)
            {
                LaunchCodexOnce();
            }
            else
            {
                var launchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_options.LaunchDelayMs) };
                launchTimer.Tick += delegate
                {
                    launchTimer.Stop();
                    LaunchCodexOnce();
                };
                launchTimer.Start();
            }
            _video.Play();

            _progressTimer.Interval = TimeSpan.FromMilliseconds(33);
            _progressTimer.Tick += OnProgressTick;
            _progressTimer.Start();

            _fallbackTimer.Interval = TimeSpan.FromSeconds(_options.MaxWaitSeconds);
            _fallbackTimer.Tick += delegate
            {
                _fallbackTimer.Stop();
                Log.Write("Playback fallback timeout reached.");
                Finish(false);
            };
            _fallbackTimer.Start();

            if (_options.AutoSkipMs > 0)
            {
                var skipTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_options.AutoSkipMs) };
                skipTimer.Tick += delegate
                {
                    skipTimer.Stop();
                    Finish(true);
                };
                skipTimer.Start();
            }

            AnimateOpacity(_hint, 0, 0.72, 0.6, 0.65, false);
            AnimateOpacity(_hint, 0.72, 0, 3.0, 0.8, false);
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            if (_video.NaturalDuration.HasTimeSpan)
                _naturalDuration = _video.NaturalDuration.TimeSpan;

            Log.Write(string.Format(CultureInfo.InvariantCulture,
                "Video opened: {0}x{1}, duration={2}, volume={3:0.00}",
                _video.NaturalVideoWidth, _video.NaturalVideoHeight,
                _naturalDuration.HasValue ? _naturalDuration.Value.ToString() : "unknown",
                _options.Volume));

            AnimateOpacity(_loadingCover, 1, 0, 0, 0.28, false);
        }

        private void OnProgressTick(object sender, EventArgs e)
        {
            if (!_naturalDuration.HasValue || _naturalDuration.Value.TotalMilliseconds <= 0) return;
            double ratio = Math.Max(0, Math.Min(1, _video.Position.TotalMilliseconds / _naturalDuration.Value.TotalMilliseconds));
            _progressScale.ScaleX = ratio;

            // App activation can steal keyboard focus while the splash remains
            // topmost. Reclaim it during playback so Esc always works instantly.
            if (!_finishing && !IsActive)
            {
                Activate();
                Focus();
                Keyboard.Focus(this);
            }

            if ((DateTime.UtcNow - _lastReadinessProbe).TotalMilliseconds >= 250)
            {
                _lastReadinessProbe = DateTime.UtcNow;
                ObserveCodexWindow();
            }

            double remaining = (_naturalDuration.Value - _video.Position).TotalSeconds;
            if (!_endFadeStarted && remaining <= _options.FadeSeconds && remaining > 0)
            {
                _endFadeStarted = true;
                AnimateOpacity(_root, 1, 0, 0, Math.Max(0.1, remaining), false);
            }
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            Log.Write("Video ended.");
            Finish(false);
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            OnPlaybackError("视频无法播放：" + (e.ErrorException == null ? "未知媒体错误" : e.ErrorException.Message));
        }

        private void OnPlaybackError(string message)
        {
            Log.Write(message);
            LaunchCodexOnce();
            MessageBox.Show(message + "\n\n请确认 Windows 媒体组件可用。Codex 仍会继续启动。",
                "墨龙 Codex 启动器", MessageBoxButton.OK, MessageBoxImage.Warning);
            Finish(false);
        }

        private void LaunchCodexOnce()
        {
            if (_launched || !_options.LaunchCodex) return;
            _launched = true;
            CodexLauncher.Start(_options.AppLaunch, _options.ProcessNames, _options.ForceLaunch);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Space || e.Key == Key.Enter)
            {
                e.Handled = true;
                Finish(true);
            }
        }

        private void Finish(bool skipped)
        {
            if (_finishing) return;
            _finishing = true;
            Log.Write(skipped ? "Animation skipped." : "Animation completed.");
            _progressTimer.Stop();
            _fallbackTimer.Stop();
            LaunchCodexOnce();
            try { _video.Stop(); } catch { }

            if (!_options.LaunchCodex)
            {
                CloseSplash(skipped ? 0.22 : 0.12);
                return;
            }

            if (skipped)
            {
                Log.Write("Skip requested: handing control to Codex immediately.");
                CodexLauncher.Start(_options.AppLaunch, _options.ProcessNames, true);
                WindowFinder.TryActivateCodexWindow(_options.ProcessNames);
                CloseSplash(0.05);
                return;
            }

            if (IsCodexInterfaceReady())
            {
                WindowFinder.TryActivateCodexWindow(_options.ProcessNames);
                Log.Write("Handoff completed immediately.");
                CloseSplash(0.05);
                return;
            }

            // The client can need longer than the video to create its main window.
            // Keep the splash alive until a real ChatGPT window exists.
            _root.BeginAnimation(UIElement.OpacityProperty, null);
            _root.Opacity = 1;
            BeginAnimation(Window.OpacityProperty, null);
            Opacity = 1;
            _handoffPanel.Visibility = Visibility.Visible;
            _handoffStartedAt = DateTime.UtcNow;
            _handoffRetried = false;
            Log.Write("Waiting for Codex main window.");

            // Re-issue activation once at the handoff boundary.
            CodexLauncher.Start(_options.AppLaunch, _options.ProcessNames, true);

            _handoffTimer.Interval = TimeSpan.FromMilliseconds(150);
            _handoffTimer.Tick += OnHandoffTick;
            _handoffTimer.Start();
        }

        private void OnHandoffTick(object sender, EventArgs e)
        {
            if (IsCodexInterfaceReady())
            {
                _handoffTimer.Stop();
                WindowFinder.TryActivateCodexWindow(_options.ProcessNames);
                Log.Write("Handoff completed after waiting.");
                CloseSplash(0.05);
                return;
            }

            double elapsed = (DateTime.UtcNow - _handoffStartedAt).TotalSeconds;
            if (!_handoffRetried && elapsed >= 5)
            {
                _handoffRetried = true;
                Log.Write("Retrying Codex activation during handoff.");
                CodexLauncher.Start(_options.AppLaunch, _options.ProcessNames, true);
            }

            if (elapsed >= _options.HandoffWaitSeconds)
            {
                _handoffTimer.Stop();
                Log.Write("Codex main window did not appear before handoff timeout.");
                _handoffMessage.Text = "Codex 启动超时\n请查看启动器日志";
                var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                closeTimer.Tick += delegate
                {
                    closeTimer.Stop();
                    CloseSplash(0.2);
                };
                closeTimer.Start();
            }
        }

        private void ObserveCodexWindow()
        {
            IntPtr handle = WindowFinder.FindCodexWindow(_options.ProcessNames);
            if (handle == IntPtr.Zero)
            {
                _candidateWindow = IntPtr.Zero;
                _candidateWindowSince = DateTime.MinValue;
                return;
            }

            if (handle != _candidateWindow)
            {
                _candidateWindow = handle;
                _candidateWindowSince = DateTime.UtcNow;
                Log.Write("Observed Codex main window; waiting for interface stability.");
            }
        }

        private bool IsCodexInterfaceReady()
        {
            ObserveCodexWindow();
            if (_candidateWindow == IntPtr.Zero || _candidateWindowSince == DateTime.MinValue) return false;
            if (!WindowFinder.IsWindowResponsive(_candidateWindow)) return false;
            return (DateTime.UtcNow - _candidateWindowSince).TotalSeconds >= _options.ReadyStabilitySeconds;
        }

        private void CloseSplash(double fadeSeconds)
        {
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromSeconds(Math.Max(0.01, fadeSeconds)));
            fade.Completed += delegate
            {
                Topmost = false;
                Hide();
                Close();
            };
            BeginAnimation(Window.OpacityProperty, fade);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _handoffTimer.Stop();
            try { _video.Close(); } catch { }
        }

        private static void AnimateOpacity(UIElement target, double from, double to, double begin, double duration, bool autoReverse)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                BeginTime = TimeSpan.FromSeconds(begin),
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
                AutoReverse = autoReverse
            };
            target.BeginAnimation(UIElement.OpacityProperty, animation);
        }
    }

    internal static class CodexLauncher
    {
        [ComImport]
        [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationActivationManager
        {
            [PreserveSig]
            int ActivateApplication(
                [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                [MarshalAs(UnmanagedType.LPWStr)] string arguments,
                uint options,
                out uint processId);
        }

        [ComImport]
        [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
        private class ApplicationActivationManager
        {
        }

        public static void Start(string target, string[] processNames, bool forceLaunch)
        {
            try
            {
                // Always ask Windows to activate the registered application.
                // This launches a closed client and focuses an existing client,
                // avoiding false positives from unrelated windows or background processes.
                bool directStarted = false;
                string executable = FindInstalledChatGptExecutable();
                if (!string.IsNullOrEmpty(executable))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = executable,
                            WorkingDirectory = System.IO.Path.GetDirectoryName(executable),
                            UseShellExecute = true
                        });
                        directStarted = true;
                        Log.Write("Started installed executable: " + executable);
                    }
                    catch (Exception directError)
                    {
                        Log.Write("Direct executable launch failed: " + directError.Message);
                    }
                }
                else
                {
                    Log.Write("Installed ChatGPT executable was not found in package registry.");
                }

                string configuredId = NormalizeAppId(target);
                string[] appIds = new[]
                {
                    configuredId,
                    "OpenAI.Codex_2p2nqsd0c76g0!App",
                    "com.openai.codex"
                };

                foreach (string appId in appIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    uint processId;
                    int result = ActivateRegisteredApplication(appId, out processId);
                    if (result >= 0)
                    {
                        Log.Write("Activated registered app: " + appId + ", pid=" + processId);
                        return;
                    }
                    Log.Write("Activation failed: " + appId + ", HRESULT=0x" + result.ToString("X8", CultureInfo.InvariantCulture));
                }

                if (directStarted)
                {
                    Log.Write("Direct executable was started; registered activation was unavailable.");
                    return;
                }

                // Last-resort shell activation for systems where COM activation is unavailable.
                if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = target,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
                }
                Log.Write("Started: " + target);
            }
            catch (Exception ex)
            {
                Log.Write("Launch failed: " + ex);
            }
        }

        private static int ActivateRegisteredApplication(string appId, out uint processId)
        {
            processId = 0;
            IApplicationActivationManager manager = null;
            try
            {
                manager = (IApplicationActivationManager)new ApplicationActivationManager();
                return manager.ActivateApplication(appId, null, 0, out processId);
            }
            catch (COMException ex)
            {
                return ex.ErrorCode;
            }
            finally
            {
                if (manager != null && Marshal.IsComObject(manager)) Marshal.FinalReleaseComObject(manager);
            }
        }

        private static string NormalizeAppId(string target)
        {
            const string prefix = "shell:AppsFolder\\";
            if (target != null && target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return target.Substring(prefix.Length);
            return target;
        }

        private static string FindInstalledChatGptExecutable()
        {
            const string packagesKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
            const string packagePrefix = "OpenAI.Codex_";
            string bestExecutable = null;
            Version bestVersion = new Version(0, 0);

            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(packagesKey))
                {
                    if (root == null) return null;
                    foreach (string packageName in root.GetSubKeyNames())
                    {
                        if (!packageName.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                        int versionEnd = packageName.IndexOf('_', packagePrefix.Length);
                        if (versionEnd <= packagePrefix.Length) continue;
                        Version version;
                        if (!Version.TryParse(packageName.Substring(packagePrefix.Length, versionEnd - packagePrefix.Length), out version))
                            continue;

                        using (RegistryKey package = root.OpenSubKey(packageName))
                        {
                            if (package == null) continue;
                            string packageRoot = package.GetValue("PackageRootFolder") as string;
                            if (string.IsNullOrWhiteSpace(packageRoot)) continue;
                            string candidate = System.IO.Path.Combine(packageRoot, "app", "ChatGPT.exe");
                            if (version > bestVersion)
                            {
                                bestVersion = version;
                                bestExecutable = candidate;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Package registry lookup failed: " + ex.Message);
            }
            return bestExecutable;
        }
    }

    internal static class WindowFinder
    {
        private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int command);
        [DllImport("user32.dll")]
        private static extern bool IsHungAppWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maximumCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public static IntPtr FindCodexWindow(string[] processNames)
        {
            foreach (string name in processNames ?? new string[0])
            {
                try
                {
                    foreach (Process process in Process.GetProcessesByName(name).OrderByDescending(p => p.StartTime))
                    {
                        process.Refresh();
                        if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
                    }
                }
                catch { }
            }

            return IntPtr.Zero;
        }

        public static void ActivateCodexWindow()
        {
            ActivateCodexWindow(new[] { "ChatGPT", "Codex" });
        }

        public static void ActivateCodexWindow(string[] processNames)
        {
            TryActivateCodexWindow(processNames);
        }

        public static bool IsWindowResponsive(IntPtr handle)
        {
            return handle != IntPtr.Zero && !IsHungAppWindow(handle);
        }

        public static bool TryActivateCodexWindow(string[] processNames)
        {
            IntPtr handle = FindCodexWindow(processNames);
            if (handle == IntPtr.Zero)
            {
                return false;
            }
            ShowWindowAsync(handle, 9);
            SetForegroundWindow(handle);
            Log.Write("Activated Codex window.");
            return true;
        }
    }

    internal static class Log
    {
        public static readonly string Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InkDragonCodex", "launcher.log");

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ", CultureInfo.InvariantCulture) + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
