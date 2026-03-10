using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using StickyMD.Services;
using StickyMD.ViewModels;
using StickyMD.Views;

namespace StickyMD;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "StickyMD.SingleInstance.Mutex";
    private const string ActivationPipeName = "StickyMD.SingleInstance.Activate";

    private static Mutex? _instanceMutex;

    private CancellationTokenSource? _activationListenerToken;
    private MainWindow? _mainWindow;
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
            var mainViewModel = new MainViewModel(storageService, markdownService);

            _mainWindow = new MainWindow(mainViewModel);
            MainWindow = _mainWindow;

            await _mainWindow.InitializeAsync(showManager: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start StickyMD.\n\n{ex.Message}",
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
            // Ignore when the primary instance is still starting.
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
                    Dispatcher.Invoke(ShowMainWindowFromActivation);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                try
                {
                    await Task.Delay(200, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void ShowMainWindowFromActivation()
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
            // Ignore if already released.
        }

        _instanceMutex.Dispose();
        _instanceMutex = null;
    }
}