using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using HeyMac.App.AppLock;
using HeyMac.Engine;
using HeyMac.Core.Models;
using HeyMac.Core.Recognition;
using HeyMac.Core.Security;

namespace HeyMac.App;

public partial class MainWindow : Window
{
    private readonly EngineController _engine;
    private readonly AppLockManager _appLock;
    private readonly HeyMacRuntime? _runtime;
    private readonly ObservableCollection<string> _protectedApps = new();

    public MainWindow(EngineController engine, AppLockManager appLock, HeyMacRuntime? runtime)
    {
        InitializeComponent();
        _engine = engine;
        _appLock = appLock;
        _runtime = runtime;
        AppLockList.ItemsSource = _protectedApps;
        RefreshStatus();
        RefreshAppLockList();
    }

    private void RefreshStatus()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeyMac", "store");
        bool enrolled = File.Exists(Path.Combine(dir, SecureStore.CentroidKey));
        bool hasPassword = File.Exists(Path.Combine(dir, SecureStore.LoginPasswordKey));
        bool modelsOk = _runtime?.ModelsAvailable ?? false;

        if (!modelsOk)
        {
            StatusText.Text = "Face models missing";
            StatusDetail.Text = "Place arcface.onnx + antispoof.onnx in the models folder (see PORT_GUIDE.md).";
            StatusText.Foreground = Brushes.Red;
            EnrollButton.IsEnabled = false;
            EnableUnlockCheck.IsEnabled = false;
        }
        else
        {
            StatusText.Text = enrolled ? "Enrolled — face verification ready" : "Not enrolled";
            StatusDetail.Text = hasPassword ? "Lock screen password saved." : "No lock screen password saved.";
            StatusText.Foreground = enrolled ? Brushes.Green : Brushes.Orange;
            EnrollButton.IsEnabled = true;
            EnableUnlockCheck.IsEnabled = enrolled && hasPassword;
        }
        EnrollButton.Content = enrolled ? "Re-enroll Face" : "Enroll Face";
    }

    private async void EnrollButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runtime == null)
        {
            EnrollProgress.Text = "Runtime not available — face models missing.";
            EnrollProgress.Foreground = Brushes.Red;
            return;
        }

        try
        {
            EnrollProgress.Text = "Enrolling… look at the camera.";
            EnrollProgress.Foreground = Brushes.Orange;

            bool ok = await Task.Run(() =>
                _runtime.Enroller().Run(
                    shouldContinue: () => !Dispatcher.HasShutdownStarted,
                    onProgress: p =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            switch (p.Kind)
                            {
                                case EnrollmentProgressKind.SampleCaptured:
                                    EnrollProgress.Text = $"Captured {p.Count}/{p.Target} samples (liveness {p.Liveness:P0}).";
                                    break;
                                case EnrollmentProgressKind.RejectedNotLive:
                                    EnrollProgress.Text = $"Frame rejected (not live, {p.Liveness:P0}). Keep your face centered.";
                                    break;
                                case EnrollmentProgressKind.Finished:
                                    EnrollProgress.Text = $"Enrolled. Agreement: min {p.MinAgreement:F2}, mean {p.MeanAgreement:F2}.";
                                    break;
                                case EnrollmentProgressKind.Failed:
                                    EnrollProgress.Text = $"Failed: {p.Failure}";
                                    EnrollProgress.Foreground = Brushes.Red;
                                    break;
                            }
                        });
                    }));

            RefreshStatus();
            if (ok)
            {
                EnrollProgress.Text = "Enrollment complete.";
                EnrollProgress.Foreground = Brushes.Green;
                MessageBox.Show("Face enrolled successfully.", "Hey Mac", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            EnrollProgress.Text = $"Enrollment unavailable: {ex.Message}";
            EnrollProgress.Foreground = Brushes.Red;
        }
    }

    private void SavePassword_Click(object sender, RoutedEventArgs e)
    {
        string password = PasswordBox.Password;
        if (string.IsNullOrEmpty(password))
        {
            PasswordStatus.Text = "Enter a password first.";
            PasswordStatus.Foreground = Brushes.Red;
            return;
        }

        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HeyMac", "store");
            var store = new SecureStore(new DpapiKeyProvider(), dir);
            store.Save(System.Text.Encoding.UTF8.GetBytes(password), SecureStore.LoginPasswordKey);
            PasswordStatus.Text = "Password saved (AES-GCM encrypted, DPAPI-protected key).";
            PasswordStatus.Foreground = Brushes.Green;
            PasswordBox.Password = "";
            RefreshStatus();
        }
        catch (Exception ex)
        {
            PasswordStatus.Text = $"Save failed: {ex.Message}";
            PasswordStatus.Foreground = Brushes.Red;
        }
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runtime == null || !_runtime.ModelsAvailable)
        {
            TestResult.Text = "Face verification unavailable — ONNX models missing.";
            TestResult.Foreground = Brushes.Red;
            return;
        }

        TestResult.Text = "Starting camera verification… look at the camera.";
        TestResult.Foreground = Brushes.Orange;

        _ = Task.Run(() =>
        {
            var outcome = _runtime.Verifier().Run(TimeSpan.FromSeconds(8), requiredConsecutive: 2);
            Dispatcher.Invoke(() =>
            {
                TestResult.Text = outcome.Failure != null
                    ? $"Verification: {outcome.Failure}"
                    : $"Verification: {(outcome.Matched ? "MATCHED" : "NO MATCH")} — {outcome.Summary}";
                TestResult.Foreground = outcome.Matched ? Brushes.Green : Brushes.Orange;
            });
        });
    }

    private void RefreshAppLockList()
    {
        _protectedApps.Clear();
        foreach (var app in _appLock.Settings.Apps.Where(a => a.Enabled))
        {
            _protectedApps.Add($"{(app.MatchByProcessName ? "proc:" : "title:")}{app.Identifier} " +
                               $"({app.Mode}, {app.Policy})");
        }
        AppLockStatus.Text = $"{_protectedApps.Count} protected app(s) configured.";
    }

    private void AddAppLock_Click(object sender, RoutedEventArgs e)
    {
        var id = AppLockProcessBox.Text.Trim();
        if (string.IsNullOrEmpty(id))
        {
            AppLockStatus.Text = "Enter a process name or window title.";
            AppLockStatus.Foreground = Brushes.Red;
            return;
        }

        var mode = AppLockModeBox.SelectedIndex == 1 ? ShieldMode.BlurDisplay : ShieldMode.BlurWindow;

        _appLock.AddOrUpdate(new ProtectedApp
        {
            Identifier = id,
            MatchByProcessName = true,
            Mode = mode,
            Policy = RelockPolicy.Immediate,
            Enabled = true
        });

        AppLockProcessBox.Clear();
        RefreshAppLockList();
        AppLockStatus.Text = $"Added {id} to app lock.";
        AppLockStatus.Foreground = Brushes.Green;
    }

    private void StartAppLockButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_runtime == null)
            {
                AppLockStatus.Text = "App lock requires face models.";
                AppLockStatus.Foreground = Brushes.Red;
                return;
            }

            IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            _appLock.Start(hwnd, _runtime.Verifier());
            AppLockStatus.Text = "App lock watcher started.";
            AppLockStatus.Foreground = Brushes.Green;
        }
        catch (Exception ex)
        {
            AppLockStatus.Text = $"App lock start failed: {ex.Message}";
            AppLockStatus.Foreground = Brushes.Red;
        }
    }
}
