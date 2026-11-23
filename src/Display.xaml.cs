using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;

namespace TimerAlarmPlugin
{
    public partial class Display : Window
    {
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        const int GWL_EXSTYLE = -20;

        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WS_EX_TOPMOST = 0x00000008;
        
        private int stopRingingAfter;

        public Display()
        {
            InitializeComponent();

            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
        }

        public string GetTypeName()
        {
            try { return (string)Dispatcher.Invoke(() => TypeText.Text); }
            catch { return ""; }
        }

        public string GetTimeString()
        {
            try { return (string)Dispatcher.Invoke(() => TimeText.Text); }
            catch { return ""; }
        }

        public string Id { get; private set; }

        private double? desiredLeft = null;
        private double? desiredTop = null;

        private DispatcherTimer? uiTimer;
        private int remainingSeconds = 0;
        private bool isAlarm = false;
        private bool isPaused = false;

        private DispatcherTimer? ringingTimer;
        private DispatcherTimer? autoStopTimer;
        private MediaPlayer? mediaPlayer;

        public void ShowOverlay()
        {
            Left = desiredLeft ?? Screen.PrimaryScreen.WorkingArea.Left;
            Top = desiredTop ?? Screen.PrimaryScreen.WorkingArea.Top;

            Visibility = Visibility.Visible;
        }

        public void Start(int initialSeconds, bool alarm, int stopRingingAfter, Action<Display>? onFinished = null)
        {
            remainingSeconds = Math.Max(0, initialSeconds);
            isAlarm = alarm;
            this.stopRingingAfter = stopRingingAfter;

            TimeText.Text = FormatTime(remainingSeconds);

            uiTimer?.Stop();
            uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromSeconds(1);
            uiTimer.Tick += (s, e) =>
            {
                if (remainingSeconds > 0)
                {
                    remainingSeconds--;
                    TimeText.Text = FormatTime(remainingSeconds);
                }
                else
                {
                    uiTimer.Stop();
                    BeginRinging(onFinished);
                }
            };

            uiTimer.Start();
            isPaused = false;
        }

        public void Pause()
        {
            isPaused = true;
            try
            {
                uiTimer?.Stop();
            }
            catch { }
        }

        public void Resume()
        {
            if (!isPaused)
                return;

            isPaused = false;
            try
            {
                uiTimer?.Start();
            }
            catch { }
        }

        public bool IsPaused()
        {
            return isPaused;
        }
        
        private void StartFallbackSound()
        {
            ringingTimer?.Stop();

            ringingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            ringingTimer.Tick += (s, e) =>
                SystemSounds.Exclamation.Play();

            ringingTimer.Start();
        }

        private void BeginRinging(Action<Display>? onFinished)
        {
            var asmLocation = Assembly.GetExecutingAssembly().Location;
            var asmDir = Path.GetDirectoryName(asmLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
            
            var file = Directory
                .EnumerateFiles(asmDir, "alarm.*")
                .FirstOrDefault();

            if (file != null)
            {
                mediaPlayer = new MediaPlayer();

                mediaPlayer.MediaFailed += (s, e) =>
                {
                    mediaPlayer?.Close();
                    mediaPlayer = null;

                    StartFallbackSound();
                };

                mediaPlayer.Open(new Uri(file));

                mediaPlayer.MediaEnded += (s, e) =>
                {
                    mediaPlayer.Position = TimeSpan.Zero;
                    mediaPlayer.Play();
                };

                mediaPlayer.Play();
            }
            else
            {
                StartFallbackSound();
            }

            var msg = $"{TypeText.Text} {Id} is ringing.";
            var popup = new DismissWindow(msg);
            popup.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            autoStopTimer?.Stop();
            autoStopTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(stopRingingAfter)
            };

            autoStopTimer.Tick += (s, e) =>
            {
                autoStopTimer.Stop();
                autoStopTimer = null;

                ringingTimer?.Stop();
                ringingTimer = null;

                mediaPlayer?.Stop();
                mediaPlayer?.Close();
                mediaPlayer = null;
            };

            autoStopTimer.Start();

            popup.ShowDialog();

            autoStopTimer?.Stop();
            autoStopTimer = null;

            ringingTimer?.Stop();
            ringingTimer = null;

            try
            {
                mediaPlayer?.Stop();
                mediaPlayer?.Close();
                mediaPlayer = null;
            }
            catch { }

            onFinished?.Invoke(this);
        }
        
        private string FormatTime(int time)
        {
            int h = time / 3600;
            int m = (time % 3600) / 60;
            int s = time % 60;
            return $"{h:D2}:{m:D2}:{s:D2}";
        }

        public void SetId(string id)
        {
            Id = id;
            IdText.Text = id;
        }

        public void SetPosition(int stackIndex)
        {
            double gap = 0.0;
            double itemHeight = this.Height;

            desiredLeft = Screen.PrimaryScreen.WorkingArea.Left;
            desiredTop = Screen.PrimaryScreen.WorkingArea.Top + stackIndex * (itemHeight + gap);

            if (this.IsVisible)
            {
                this.Left = desiredLeft.Value;
                this.Top = desiredTop.Value;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            exStyle |= WS_EX_NOACTIVATE;
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_TOPMOST;

            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        protected override void OnClosed(EventArgs e)
        {
            uiTimer?.Stop();
            uiTimer = null;

            ringingTimer?.Stop();
            ringingTimer = null;

            try
            {
                mediaPlayer?.Stop();
                mediaPlayer?.Close();
            }
            catch { }

            mediaPlayer = null;

            base.OnClosed(e);
        }
    }
}