using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using StickyMD.ViewModels;

namespace StickyMD.Views;

public partial class StickyNoteWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly NoteViewModel _noteViewModel;

    private bool _allowClose;
    private bool _isInitialized;

    public StickyNoteWindow(MainViewModel mainViewModel, NoteViewModel noteViewModel)
    {
        InitializeComponent();

        _mainViewModel = mainViewModel;
        _noteViewModel = noteViewModel;

        DataContext = _noteViewModel;

        Left = _noteViewModel.X;
        Top = _noteViewModel.Y;
        Width = _noteViewModel.Width;
        Height = _noteViewModel.Height;

        Loaded += StickyNoteWindow_Loaded;
        LocationChanged += StickyNoteWindow_BoundsChanged;
        SizeChanged += StickyNoteWindow_BoundsChanged;

        _noteViewModel.PropertyChanged += NoteViewModel_PropertyChanged;

        PreviewViewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(Hyperlink_RequestNavigate));
    }

    public string NoteId => _noteViewModel.Id;

    public void ShowFromManager()
    {
        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        Activate();
    }

    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    private void StickyNoteWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        UpdateEditorMode();

        if (!_noteViewModel.IsPreviewMode)
        {
            EditorTextBox.Focus();
            EditorTextBox.CaretIndex = EditorTextBox.Text.Length;
        }
    }

    private void StickyNoteWindow_BoundsChanged(object? sender, EventArgs e)
    {
        if (!_isInitialized)
        {
            return;
        }

        _noteViewModel.X = Left;
        _noteViewModel.Y = Top;
        _noteViewModel.Width = Width;
        _noteViewModel.Height = Height;
    }

    private void StickyNoteWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _noteViewModel.PropertyChanged -= NoteViewModel_PropertyChanged;
    }

    private void NoteViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NoteViewModel.IsPreviewMode))
        {
            UpdateEditorMode();
        }
    }

    private void UpdateEditorMode()
    {
        if (_noteViewModel.IsPreviewMode)
        {
            EditorTextBox.Visibility = Visibility.Collapsed;
            PreviewViewer.Visibility = Visibility.Visible;
            return;
        }

        PreviewViewer.Visibility = Visibility.Collapsed;
        EditorTextBox.Visibility = Visibility.Visible;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        _mainViewModel.NewNoteCommand.Execute(null);
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (MenuButton.ContextMenu is not { } contextMenu)
        {
            return;
        }

        contextMenu.PlacementTarget = MenuButton;
        contextMenu.IsOpen = true;
    }

    private void ColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string color)
        {
            return;
        }

        _mainViewModel.SetNoteColor(_noteViewModel, color);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private async void StickyNoteWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (!isCtrl)
        {
            return;
        }

        if (e.Key == Key.B)
        {
            ApplyBold();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.I)
        {
            ApplyItalic();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S)
        {
            await _mainViewModel.SaveNowAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.N)
        {
            _mainViewModel.NewNoteCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyBold();
    }

    private void ItalicButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyItalic();
    }

    private void CodeButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureEditMode();

        var selected = EditorTextBox.SelectedText ?? string.Empty;

        if (selected.Contains('\n'))
        {
            WrapSelection("```\n", "\n```");
            return;
        }

        WrapSelection("`", "`");
    }

    private void ListButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureEditMode();

        var selected = EditorTextBox.SelectedText;
        if (string.IsNullOrEmpty(selected))
        {
            InsertAtCaret("- ");
            return;
        }

        var lines = selected.Split('\n');
        var transformed = string.Join("\n", lines.Select(line => string.IsNullOrWhiteSpace(line) ? line : $"- {line}"));

        ReplaceSelection(transformed);
    }

    private void ApplyBold()
    {
        EnsureEditMode();
        WrapSelection("**", "**");
    }

    private void ApplyItalic()
    {
        EnsureEditMode();
        WrapSelection("*", "*");
    }

    private void EnsureEditMode()
    {
        if (_noteViewModel.IsPreviewMode)
        {
            _noteViewModel.IsPreviewMode = false;
        }

        EditorTextBox.Focus();
    }

    private void WrapSelection(string prefix, string suffix)
    {
        var source = EditorTextBox.Text ?? string.Empty;
        var start = EditorTextBox.SelectionStart;
        var length = EditorTextBox.SelectionLength;

        if (start < 0 || start > source.Length)
        {
            return;
        }

        if (length < 0 || start + length > source.Length)
        {
            return;
        }

        var selected = EditorTextBox.SelectedText ?? string.Empty;
        var wrapped = prefix + selected + suffix;

        var updated = source.Remove(start, length).Insert(start, wrapped);
        EditorTextBox.Text = updated;

        if (length == 0)
        {
            EditorTextBox.SelectionStart = start + prefix.Length;
            EditorTextBox.SelectionLength = 0;
        }
        else
        {
            EditorTextBox.SelectionStart = start + prefix.Length;
            EditorTextBox.SelectionLength = length;
        }
    }

    private void InsertAtCaret(string text)
    {
        var source = EditorTextBox.Text ?? string.Empty;
        var start = EditorTextBox.SelectionStart;

        if (start < 0 || start > source.Length)
        {
            return;
        }

        var updated = source.Insert(start, text);
        EditorTextBox.Text = updated;
        EditorTextBox.SelectionStart = start + text.Length;
        EditorTextBox.SelectionLength = 0;
    }

    private void ReplaceSelection(string replacement)
    {
        var source = EditorTextBox.Text ?? string.Empty;
        var start = EditorTextBox.SelectionStart;
        var length = EditorTextBox.SelectionLength;

        if (start < 0 || start > source.Length)
        {
            return;
        }

        if (length < 0 || start + length > source.Length)
        {
            return;
        }

        var updated = source.Remove(start, length).Insert(start, replacement);
        EditorTextBox.Text = updated;
        EditorTextBox.SelectionStart = start;
        EditorTextBox.SelectionLength = replacement.Length;
    }

    private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri is null)
        {
            return;
        }

        e.Handled = true;

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // 기본 브라우저 실행 실패 시 앱은 계속 동작합니다.
        }
    }
}
