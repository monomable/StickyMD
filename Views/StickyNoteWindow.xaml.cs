using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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

    private void StickyNoteWindow_Deactivated(object? sender, EventArgs e)
    {
        if (!_noteViewModel.IsPreviewMode)
        {
            _noteViewModel.IsPreviewMode = true;
        }
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

    private async void StickyNoteWindow_PreviewKeyDown(object sender, KeyEventArgs e)
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
        ToggleCode();
    }

    private void ListButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleList();
    }

    private void PreviewViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        _noteViewModel.IsPreviewMode = false;
        EditorTextBox.Focus();
        EditorTextBox.CaretIndex = EditorTextBox.Text.Length;
        e.Handled = true;
    }

    private void EditorTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is DependencyObject dependencyObject && IsDescendantOfWindow(dependencyObject, this))
        {
            return;
        }

        _noteViewModel.IsPreviewMode = true;
    }

    private void ApplyBold()
    {
        ToggleInlineStyle("**", "**");
    }

    private void ApplyItalic()
    {
        ToggleInlineStyle("*", "*");
    }

    private void ToggleCode()
    {
        EnsureEditMode();

        if (!TryGetSelectionInfo(out var source, out var start, out var length))
        {
            return;
        }

        var selected = length > 0 ? source.Substring(start, length) : string.Empty;
        var useBlockCode = selected.Contains('\n', StringComparison.Ordinal) ||
                           IsWrapped(selected, "```\n", "\n```");

        if (useBlockCode)
        {
            ToggleInlineStyle("```\n", "\n```");
            return;
        }

        ToggleInlineStyle("`", "`");
    }

    private void ToggleList()
    {
        EnsureEditMode();

        if (!TryGetSelectionInfo(out var source, out var start, out var length))
        {
            return;
        }

        if (length == 0)
        {
            ToggleCurrentLineListPrefix(source, start);
            return;
        }

        var selected = source.Substring(start, length);
        var lines = selected.Split('\n');

        var nonEmptyLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        if (nonEmptyLines.Count == 0)
        {
            return;
        }

        var removePrefix = nonEmptyLines.All(HasListPrefix);

        var transformed = string.Join("\n", lines.Select(line =>
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            return removePrefix ? RemoveListPrefix(line) : $"- {line}";
        }));

        ReplaceRange(source, start, length, transformed, start, transformed.Length);
    }

    private void ToggleInlineStyle(string prefix, string suffix)
    {
        EnsureEditMode();

        if (!TryGetSelectionInfo(out var source, out var start, out var length))
        {
            return;
        }

        if (length == 0)
        {
            if (HasOutsideWrapper(source, start, 0, prefix, suffix))
            {
                var updated = source
                    .Remove(start, suffix.Length)
                    .Remove(start - prefix.Length, prefix.Length);

                EditorTextBox.Text = updated;
                SetSelection(start - prefix.Length, 0);
                return;
            }

            var inserted = prefix + suffix;
            var appended = source.Insert(start, inserted);
            EditorTextBox.Text = appended;
            SetSelection(start + prefix.Length, 0);
            return;
        }

        var selected = source.Substring(start, length);

        if (IsWrapped(selected, prefix, suffix))
        {
            var unwrapped = selected.Substring(prefix.Length, selected.Length - prefix.Length - suffix.Length);
            ReplaceRange(source, start, length, unwrapped, start, unwrapped.Length);
            return;
        }

        if (HasOutsideWrapper(source, start, length, prefix, suffix))
        {
            var updated = source
                .Remove(start + length, suffix.Length)
                .Remove(start - prefix.Length, prefix.Length);

            EditorTextBox.Text = updated;
            SetSelection(start - prefix.Length, length);
            return;
        }

        var wrapped = prefix + selected + suffix;
        ReplaceRange(source, start, length, wrapped, start + prefix.Length, length);
    }

    private void ToggleCurrentLineListPrefix(string source, int caret)
    {
        var lineStart = source.LastIndexOf('\n', Math.Max(0, caret - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var lineEnd = source.IndexOf('\n', caret);
        if (lineEnd < 0)
        {
            lineEnd = source.Length;
        }

        var lineLength = lineEnd - lineStart;
        var line = source.Substring(lineStart, lineLength);

        var removePrefix = HasListPrefix(line);
        var replacement = removePrefix ? RemoveListPrefix(line) : $"- {line}";

        var updated = source.Remove(lineStart, lineLength).Insert(lineStart, replacement);
        EditorTextBox.Text = updated;

        int newCaret;
        if (removePrefix)
        {
            newCaret = caret >= lineStart + 2 ? caret - 2 : lineStart;
        }
        else
        {
            newCaret = caret + 2;
        }

        SetSelection(newCaret, 0);
    }

    private static bool HasListPrefix(string line)
    {
        return line.StartsWith("- ", StringComparison.Ordinal) ||
               line.StartsWith("* ", StringComparison.Ordinal);
    }

    private static string RemoveListPrefix(string line)
    {
        if (line.StartsWith("- ", StringComparison.Ordinal) ||
            line.StartsWith("* ", StringComparison.Ordinal))
        {
            return line[2..];
        }

        return line;
    }

    private static bool IsWrapped(string text, string prefix, string suffix)
    {
        return text.Length >= prefix.Length + suffix.Length &&
               text.StartsWith(prefix, StringComparison.Ordinal) &&
               text.EndsWith(suffix, StringComparison.Ordinal);
    }

    private static bool HasOutsideWrapper(string source, int start, int length, string prefix, string suffix)
    {
        return start >= prefix.Length &&
               start + length + suffix.Length <= source.Length &&
               source.AsSpan(start - prefix.Length, prefix.Length).SequenceEqual(prefix.AsSpan()) &&
               source.AsSpan(start + length, suffix.Length).SequenceEqual(suffix.AsSpan());
    }

    private bool TryGetSelectionInfo(out string source, out int start, out int length)
    {
        source = EditorTextBox.Text ?? string.Empty;
        start = EditorTextBox.SelectionStart;
        length = EditorTextBox.SelectionLength;

        if (start < 0 || start > source.Length)
        {
            return false;
        }

        if (length < 0 || start + length > source.Length)
        {
            return false;
        }

        return true;
    }

    private void ReplaceRange(string source, int start, int length, string replacement, int newSelectionStart, int newSelectionLength)
    {
        var updated = source.Remove(start, length).Insert(start, replacement);
        EditorTextBox.Text = updated;
        SetSelection(newSelectionStart, newSelectionLength);
    }

    private void SetSelection(int start, int length)
    {
        var totalLength = EditorTextBox.Text?.Length ?? 0;
        var safeStart = Math.Clamp(start, 0, totalLength);
        var safeLength = Math.Clamp(length, 0, totalLength - safeStart);

        EditorTextBox.SelectionStart = safeStart;
        EditorTextBox.SelectionLength = safeLength;
    }

    private void EnsureEditMode()
    {
        if (_noteViewModel.IsPreviewMode)
        {
            _noteViewModel.IsPreviewMode = false;
        }

        EditorTextBox.Focus();
    }

    private static bool IsDescendantOfWindow(DependencyObject element, DependencyObject root)
    {
        var current = element;
        while (current is not null)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
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
            // 疫꿸퀡???됰슢??怨? ??쎈뻬 ??쎈솭 ???源? ?④쑴????덉삂??몃빍??
        }
    }
}