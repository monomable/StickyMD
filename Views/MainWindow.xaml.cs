using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using StickyMD.ViewModels;
using StickyMD.Views;

namespace StickyMD.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Dictionary<string, StickyNoteWindow> _noteWindows = new();

    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;

        _viewModel.OpenNoteRequested += ViewModel_OpenNoteRequested;
        _viewModel.NoteDeleted += ViewModel_NoteDeleted;
        _viewModel.ExitRequested += ViewModel_ExitRequested;
    }

    public async Task EnsureSavedAsync()
    {
        await _viewModel.SaveNowAsync();
    }

    public void ShowFromTray()
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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void ViewModel_OpenNoteRequested(object? sender, NoteViewModel note)
    {
        if (_noteWindows.TryGetValue(note.Id, out var existingWindow))
        {
            existingWindow.ShowFromManager();
            return;
        }

        var window = new StickyNoteWindow(_viewModel, note)
        {
            Owner = this
        };

        window.Closed += StickyWindow_Closed;
        _noteWindows[note.Id] = window;

        window.Show();
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
        if (System.Windows.Application.Current is App app)
        {
            app.RequestExit();
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

    private void NoteList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel.OpenSelectedNote();
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
            Hide();
            return;
        }

        await EnsureSavedAsync();
    }
}
