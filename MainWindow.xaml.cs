using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using StickyMD.Services;
using StickyMD.ViewModels;
using StickyMD.Views;

namespace StickyMD;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private NoteEditorWindow? _editorWindow;

    public MainWindow(NoteService noteService)
    {
        InitializeComponent();

        _viewModel = new MainViewModel(noteService);
        _viewModel.EditorRequested += ViewModel_EditorRequested;
        _viewModel.CloseRequested += ViewModel_CloseRequested;

        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        LocationChanged += MainWindow_PositionChanged;
        SizeChanged += MainWindow_PositionChanged;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.FlushAllNotes();

        if (_editorWindow is not null)
        {
            _editorWindow.CloseForShutdown();
        }

        base.OnClosing(e);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize();
        EnsureEditorWindow();
        ShowEditorWindow();
    }

    private void ViewModel_EditorRequested(object? sender, NoteViewModel e)
    {
        EnsureEditorWindow();
        ShowEditorWindow();
    }

    private void ViewModel_CloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void MainWindow_PositionChanged(object? sender, EventArgs e)
    {
        PositionEditorWindow();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        PositionEditorWindow();
    }

    private void EnsureEditorWindow()
    {
        if (_editorWindow is not null)
        {
            return;
        }

        _editorWindow = new NoteEditorWindow(_viewModel)
        {
            Owner = this
        };

        _editorWindow.NewNoteRequested += EditorWindow_NewNoteRequested;
        _editorWindow.Closed += EditorWindow_Closed;

        PositionEditorWindow();
    }

    private void ShowEditorWindow()
    {
        if (_editorWindow is null)
        {
            return;
        }

        PositionEditorWindow();

        if (!_editorWindow.IsVisible)
        {
            _editorWindow.Show();
        }
    }

    private void PositionEditorWindow()
    {
        if (_editorWindow is null || !_editorWindow.IsLoaded)
        {
            return;
        }

        _editorWindow.Left = Left;
        _editorWindow.Top = Top + ActualHeight + 18;
    }

    private void EditorWindow_NewNoteRequested(object? sender, EventArgs e)
    {
        _viewModel.CreateNoteCommand.Execute(null);
        ShowEditorWindow();
    }

    private void EditorWindow_Closed(object? sender, EventArgs e)
    {
        if (_editorWindow is null)
        {
            return;
        }

        _editorWindow.NewNoteRequested -= EditorWindow_NewNoteRequested;
        _editorWindow.Closed -= EditorWindow_Closed;
        _editorWindow = null;
    }
}
