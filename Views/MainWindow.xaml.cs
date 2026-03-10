using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using StickyMD.ViewModels;

namespace StickyMD.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Dictionary<string, StickyNoteWindow> _noteWindows = new();

    private bool _allowClose;
    private bool _isInitialized;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.OpenNoteRequested += ViewModel_OpenNoteRequested;
        _viewModel.NoteDeleted += ViewModel_NoteDeleted;
        _viewModel.ExitRequested += ViewModel_ExitRequested;
    }

    public async Task InitializeAsync(bool showManager)
    {
        if (!_isInitialized)
        {
            await _viewModel.InitializeAsync();

            foreach (var note in _viewModel.Notes.ToList())
            {
                OpenOrShowNoteWindow(note, activate: false);
            }

            _isInitialized = true;
        }

        if (showManager)
        {
            ShowFromTray();
            return;
        }

        if (IsVisible)
        {
            Hide();
        }
    }

    public async Task EnsureSavedAsync()
    {
        await _viewModel.SaveNowAsync();
    }

    public void ShowFromTray()
    {
        ShowInTaskbar = true;

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;

        foreach (var window in _noteWindows.Values.ToList())
        {
            window.CloseForShutdown();
        }

        Close();
    }

    private void ViewModel_OpenNoteRequested(object? sender, NoteViewModel note)
    {
        OpenOrShowNoteWindow(note, activate: true);
    }

    private void ViewModel_NoteDeleted(object? sender, NoteViewModel note)
    {
        if (!_noteWindows.TryGetValue(note.Id, out var window))
        {
            return;
        }

        window.CloseForShutdown();
    }

    private void ViewModel_ExitRequested(object? sender, EventArgs e)
    {
        if (Application.Current is App app)
        {
            app.RequestExit();
        }
    }

    private void OpenOrShowNoteWindow(NoteViewModel note, bool activate)
    {
        if (_noteWindows.TryGetValue(note.Id, out var existingWindow))
        {
            existingWindow.ShowFromManager(activate);
            return;
        }

        var window = new StickyNoteWindow(_viewModel, note)
        {
            ShowActivated = activate
        };

        window.Closed += StickyWindow_Closed;
        _noteWindows[note.Id] = window;

        window.Show();

        // Always reset to default behavior for later explicit show calls.
        window.ShowActivated = true;

        if (activate)
        {
            window.Activate();
        }
    }

    private void StickyWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not StickyNoteWindow window)
        {
            return;
        }

        window.Closed -= StickyWindow_Closed;
        _noteWindows.Remove(window.NoteId);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsButton.ContextMenu is not { } contextMenu)
        {
            return;
        }

        contextMenu.PlacementTarget = SettingsButton;
        contextMenu.IsOpen = true;
    }

    private void CloseBoardButton_Click(object sender, RoutedEventArgs e)
    {
        MinimizeToTaskbar();
    }

    private void NoteList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel.OpenSelectedNote();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            e.Handled = true;
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            MinimizeToTaskbar();
            return;
        }

        await EnsureSavedAsync();
    }

    private void MinimizeToTaskbar()
    {
        ShowInTaskbar = true;

        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Minimized;
    }
}