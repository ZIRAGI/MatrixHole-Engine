using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MatrixHole
{
    public partial class SteamAuthWindow : Window
    {
        private Task<(bool ok, string? steamId, string? error)>? _authTask;
        private DispatcherTimer? _pollTimer;

        public SteamAuthWindow()
        {
            InitializeComponent();
            LoadLogo();
        }

        private void LoadLogo()
        {
            try
            {
                var logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo_new.png");
                if (System.IO.File.Exists(logoPath))
                {
                    LogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(logoPath));
                }
            }
            catch { }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            LoginBtn.IsEnabled = false;
            StatusText.Text = "Opening Steam login in your browser...";
            StatusText.Foreground = System.Windows.Media.Brushes.Orange;

            // Start auth in background
            _authTask = Task.Run(async () => await Core.SteamAuth.AuthenticateAsync());

            // Poll every 1.5s
            _pollTimer = new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Normal, OnPollTimerTick, Dispatcher);
            _pollTimer.Start();
        }

        private void OnPollTimerTick(object? sender, EventArgs e)
        {
            if (_authTask == null) return;

            if (!_authTask.IsCompleted) return;

            _pollTimer?.Stop();

            var result = _authTask.GetAwaiter().GetResult();
            if (result.ok && !string.IsNullOrEmpty(result.steamId))
            {
                StatusText.Text = $"✅ Authorized as SteamID {result.steamId}";
                StatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;

                // Brief delay so user sees success
                var closeTimer = new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Normal, (s, args) =>
                {
                    ((DispatcherTimer)s!).Stop();
                    DialogResult = true;
                    Close();
                }, Dispatcher);
                closeTimer.Start();
            }
            else
            {
                LoginBtn.IsEnabled = true;
                StatusText.Text = $"❌ Failed: {result.error ?? "Unknown error"}";
                StatusText.Foreground = System.Windows.Media.Brushes.Crimson;
            }
        }
    }
}
