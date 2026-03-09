using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using StickyMD.Services;
using StickyMD.ViewModels;
using StickyMD.Views;
using Forms = System.Windows.Forms;

namespace StickyMD;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "StickyMD.SingleInstance.Mutex";
    private const string ActivationPipeName = "StickyMD.SingleInstance.Activate";

    private static Mutex? _instanceMutex;

    private CancellationTokenSource? _activationListenerToken;
    private Forms.NotifyIcon? _trayIcon;

    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;
    private bool _isExitRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryAcquireSingleInstance())
        {
            await SignalPrimaryInstanceAsync();
            Shutdown();
            return;
        }

        _activationListenerToken = new CancellationTokenSource();
        _ = ListenForSecondaryInstanceSignalsAsync(_activationListenerToken.Token);

        try
        {
            var storageService = new NoteStorageService();
            var markdownService = new MarkdownService();

            _mainViewModel = new MainViewModel(storageService, markdownService);
            _mainWindow = new MainWindow(_mainViewModel);

            MainWindow = _mainWindow;

            InitializeTrayIcon();
            _mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"StickyMD 시작 중 오류가 발생했습니다.\n\n{ex.Message}",
                "StickyMD",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            CleanupSingleInstanceResources();
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _activationListenerToken?.Cancel();

        if (_mainWindow is not null)
        {
            await _mainWindow.EnsureSavedAsync();
        }

        _trayIcon?.Dispose();
        CleanupSingleInstanceResources();

        base.OnExit(e);
    }

    internal void RequestExit()
    {
        if (_isExitRequested)
        {
            return;
        }

        _isExitRequested = true;

        if (_mainWindow is not null)
        {
            _mainWindow.CloseForExit();
        }

        Shutdown();
    }

    private static bool TryAcquireSingleInstance()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        return createdNew;
    }

    private static async Task SignalPrimaryInstanceAsync()
    {
        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                ActivationPipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            await client.ConnectAsync(250);

            await using var writer = new StreamWriter(client) { AutoFlush = true };
            await writer.WriteLineAsync("SHOW");
        }
        catch
        {
            // 첫 인스턴스가 아직 준비 전이면 조용히 종료합니다.
        }
    }

    private async Task ListenForSecondaryInstanceSignalsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    ActivationPipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(server);
                var message = await reader.ReadLineAsync();

                if (string.Equals(message, "SHOW", StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.Invoke(ShowMainWindowFromTray);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(200, cancellationToken);
            }
        }
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Text = "StickyMD",
            Icon = System.Drawing.SystemIcons.Information,
            Visible = true
        };

        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("New Note", null, (_, _) => Dispatcher.Invoke(CreateNewNoteFromTray));
        contextMenu.Items.Add("Show Notes", null, (_, _) => Dispatcher.Invoke(ShowMainWindowFromTray));
        contextMenu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(RequestExit));

        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindowFromTray);
    }

    private void CreateNewNoteFromTray()
    {
        ShowMainWindowFromTray();
        _mainViewModel?.NewNoteCommand.Execute(null);
    }

    private void ShowMainWindowFromTray()
    {
        _mainWindow?.ShowFromTray();
    }

    private static void CleanupSingleInstanceResources()
    {
        if (_instanceMutex is null)
        {
            return;
        }

        try
        {
            _instanceMutex.ReleaseMutex();
        }
        catch
        {
            // 이미 해제된 경우 무시합니다.
        }

        _instanceMutex.Dispose();
        _instanceMutex = null;
    }
}
