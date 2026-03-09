using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using StickyMD.ViewModels;

namespace StickyMD.Views;

public partial class NoteEditorWindow : Window
{
    private bool _allowClose;

    public NoteEditorWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public event EventHandler? NewNoteRequested;

    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
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
        NewNoteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e)
    {
        WrapSelection("**", "**");
    }

    private void ItalicButton_Click(object sender, RoutedEventArgs e)
    {
        WrapSelection("*", "*");
    }

    private void UnderlineButton_Click(object sender, RoutedEventArgs e)
    {
        WrapSelection("<u>", "</u>");
    }

    private void StrikeButton_Click(object sender, RoutedEventArgs e)
    {
        WrapSelection("~~", "~~");
    }

    private void WrapSelection(string prefix, string suffix)
    {
        var sourceText = EditorTextBox.Text ?? string.Empty;
        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;

        if (selectionStart < 0 || selectionStart > sourceText.Length)
        {
            return;
        }

        if (selectionLength < 0 || selectionStart + selectionLength > sourceText.Length)
        {
            return;
        }

        var selectedText = EditorTextBox.SelectedText ?? string.Empty;
        var wrappedText = $"{prefix}{selectedText}{suffix}";

        var updated = sourceText.Remove(selectionStart, selectionLength)
            .Insert(selectionStart, wrappedText);

        EditorTextBox.Text = updated;

        if (selectionLength == 0)
        {
            EditorTextBox.SelectionStart = selectionStart + prefix.Length;
            EditorTextBox.SelectionLength = 0;
        }
        else
        {
            EditorTextBox.SelectionStart = selectionStart + prefix.Length;
            EditorTextBox.SelectionLength = selectionLength;
        }

        EditorTextBox.Focus();
    }
}
