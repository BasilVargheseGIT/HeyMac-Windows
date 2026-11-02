using System.Windows;
using System.Windows.Forms;
using HeyMac.App.AppLock;
using HeyMac.Core.Models;
using HeyMac.Core.Recognition;
using HeyMac.Engine;
using HeyMac.Engine.Win32;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace HeyMac.App;

public partial class App : Application
{
    private readonly NotifyIcon _tray = new();
    private readonly EngineSettings _settings = new();
    private readonly EngineController _engine;
    private readonly AppLockManager _appLock;
    private readonly HeyMacRuntime? _runtime;
    private MainWindow? _mainWindow;

    public App()
    {
        // Tray icon (HeyMac's "Dynamic Notch Island" / menu bar equivalent on Windows).
        _tray.Icon = System.Drawing.SystemIcons.Information;
        _tray.Text = "Hey Mac";
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowMainWindow();

        string storeDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeyMac", "store");

        string modelsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "models");
        string arcFace = System.IO.Path.Combine(modelsDir, "arcface.onnx");
        string antiSpoof = System.IO.Path.Combine(modelsDir, "antispoof.onnx");

        // Build the runtime once. If ONNX models are missing, HeyMacRuntime falls back
        // to a stub pipeline and ModelsAvailable will be false (UI guides the user).
        _runtime = new HeyMacRuntime(storeDir, arcFace, antiSpoof, cameraDeviceIndex: 0);

        _engine = new EngineController(_settings, _ => { });
        _appLock = new AppLockManager(_runtime.BackgroundStore);

        // Wire a live environment + start the background lock-screen watcher.
        var env = new LiveLockScreenEnvironment(_runtime.BackgroundStore);
        _engine.Start(env, _runtime.Verifier(), new KeystrokeInjector());
    }

    internal MainWindow? MainWindowInstance => _mainWindow;
    internal AppLockManager AppLockManager => _appLock;
    internal HeyMacRuntime? Runtime => _runtime;
    internal bool ModelsAvailable => _runtime?.ModelsAvailable ?? false;

    private void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = new MainWindow(_engine, _appLock, _runtime);
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _engine.Stop();
        _appLock.Controller?.Dispose();
        _runtime?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Live Windows environment: lock-screen detection + stored password from SecureStore.
    /// </summary>
    public sealed class LiveLockScreenEnvironment : ILockScreenEnvironment
    {
        private readonly LockScreenDetector _detector = new();
        private readonly SecureStore _store;

        public LiveLockScreenEnvironment(SecureStore store) => _store = store;

        public bool? IsLocked() => _detector.IsLocked();
        public bool DisplayIsAwake() => true; // simple: assume awake when locked
        public string LoadPassword()
        {
            byte[] data = _store.Load(SecureStore.LoginPasswordKey);
            return System.Text.Encoding.UTF8.GetString(data);
        }
        public void WakeDisplay() => _detector.WakeDisplay();
        public void Sleep(TimeSpan interval) => System.Threading.Thread.Sleep(interval);
    }
}
