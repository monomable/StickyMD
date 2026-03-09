using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using StickyMD.Models;
using StickyMD.Services;
using StickyMD.Utils;

namespace StickyMD.ViewModels;

public sealed class NoteViewModel : ViewModelBase
{
    private readonly NoteService _noteService;
    private readonly DispatcherTimer _saveDebounceTimer;

    private string _content;
    private FlowDocument _previewDocument;
    private DateTime _updatedAt;
    private string _updatedTimeText;

    private readonly double _x;
    private readonly double _y;
    private readonly double _width;
    private readonly double _height;

    public NoteViewModel(Note note, NoteService noteService, Brush cardBackground)
    {
        _noteService = noteService;

        Id = note.Id;
        CardBackground = cardBackground;

        _content = note.Content;
        _previewDocument = MarkdownRenderer.Render(_content, compact: true, maxBlocks: 3);

        _updatedAt = note.UpdatedAt;
        _updatedTimeText = _updatedAt.ToLocalTime().ToString("HH:mm");

        _x = note.X;
        _y = note.Y;
        _width = note.Width;
        _height = note.Height;

        _saveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _saveDebounceTimer.Tick += SaveDebounceTimer_Tick;
    }

    public string Id { get; }

    public Brush CardBackground { get; }

    public string Content
    {
        get => _content;
        set
        {
            if (!SetProperty(ref _content, value))
            {
                return;
            }

            PreviewDocument = MarkdownRenderer.Render(_content, compact: true, maxBlocks: 3);
            ScheduleSave();
        }
    }

    public FlowDocument PreviewDocument
    {
        get => _previewDocument;
        private set => SetProperty(ref _previewDocument, value);
    }

    public string UpdatedTimeText
    {
        get => _updatedTimeText;
        private set => SetProperty(ref _updatedTimeText, value);
    }

    public bool Contains(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return Content.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    public void FlushSaveNow()
    {
        _saveDebounceTimer.Stop();
        SaveNow();
    }

    private void ScheduleSave()
    {
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void SaveDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _saveDebounceTimer.Stop();
        SaveNow();
    }

    private void SaveNow()
    {
        _updatedAt = DateTime.UtcNow;
        UpdatedTimeText = _updatedAt.ToLocalTime().ToString("HH:mm");

        _noteService.SaveNote(ToModel());
    }

    private Note ToModel()
    {
        return new Note
        {
            Id = Id,
            Content = Content,
            X = _x,
            Y = _y,
            Width = _width,
            Height = _height,
            UpdatedAt = _updatedAt
        };
    }
}
