using System.Windows.Documents;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushConverter = System.Windows.Media.BrushConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using StickyMD.Models;
using StickyMD.Services;

namespace StickyMD.ViewModels;

public sealed class NoteViewModel : ViewModelBase
{
    private static readonly IReadOnlyDictionary<string, MediaBrush> ColorBrushes = new Dictionary<string, MediaBrush>(StringComparer.OrdinalIgnoreCase)
    {
        ["yellow"] = CreateBrush("#EFE6B5"),
        ["pink"] = CreateBrush("#EFD3E2"),
        ["blue"] = CreateBrush("#D8E6F7"),
        ["green"] = CreateBrush("#DCEED4"),
        ["purple"] = CreateBrush("#E4DBF5")
    };

    private readonly MarkdownService _markdownService;

    private string _content;
    private string _color;
    private DateTime _updatedAt;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private bool _isPreviewMode = true;
    private bool _hasPendingChanges;

    private FlowDocument? _previewDocument;
    private FlowDocument? _fullDocument;

    public NoteViewModel(NoteModel model, MarkdownService markdownService)
    {
        _markdownService = markdownService;

        Id = model.Id;
        CreatedAt = model.CreatedAt;

        _content = model.Content;
        _color = model.Color;
        _updatedAt = model.UpdatedAt;
        _x = model.X;
        _y = model.Y;
        _width = model.Width;
        _height = model.Height;
    }

    public event EventHandler? Changed;

    public string Id { get; }

    public DateTime CreatedAt { get; }

    public string Content
    {
        get => _content;
        set
        {
            if (!SetProperty(ref _content, value))
            {
                return;
            }

            InvalidateRenderCache();
            OnPropertyChanged(nameof(Title));
            MarkChanged();
        }
    }

    public string Color
    {
        get => _color;
        set
        {
            if (!SetProperty(ref _color, value))
            {
                return;
            }

            OnPropertyChanged(nameof(NoteColorBrush));
            MarkChanged();
        }
    }

    public MediaBrush NoteColorBrush => ResolveColorBrush(Color);

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        private set
        {
            if (!SetProperty(ref _updatedAt, value))
            {
                return;
            }

            OnPropertyChanged(nameof(UpdatedTimeText));
        }
    }

    public string UpdatedTimeText => UpdatedAt.ToLocalTime().ToString("HH:mm");

    public string Title => ExtractTitle(Content);

    public double X
    {
        get => _x;
        set
        {
            if (!SetProperty(ref _x, value))
            {
                return;
            }

            MarkChanged();
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (!SetProperty(ref _y, value))
            {
                return;
            }

            MarkChanged();
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            if (!SetProperty(ref _width, value))
            {
                return;
            }

            MarkChanged();
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            if (!SetProperty(ref _height, value))
            {
                return;
            }

            MarkChanged();
        }
    }

    public bool IsPreviewMode
    {
        get => _isPreviewMode;
        set => SetProperty(ref _isPreviewMode, value);
    }

    public bool HasPendingChanges
    {
        get => _hasPendingChanges;
        private set => SetProperty(ref _hasPendingChanges, value);
    }

    public FlowDocument PreviewDocument
    {
        get
        {
            _previewDocument ??= _markdownService.RenderPreview(Content);
            return _previewDocument;
        }
    }

    public FlowDocument FullDocument
    {
        get
        {
            _fullDocument ??= _markdownService.RenderFull(Content);
            return _fullDocument;
        }
    }

    public bool Contains(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return Content.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               Title.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    public void MarkSaved(DateTime savedAt)
    {
        UpdatedAt = savedAt;
        HasPendingChanges = false;
    }

    public NoteModel ToModel()
    {
        return new NoteModel
        {
            Id = Id,
            Content = Content,
            Color = Color,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height
        };
    }

    private void MarkChanged()
    {
        UpdatedAt = DateTime.UtcNow;
        HasPendingChanges = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void InvalidateRenderCache()
    {
        _previewDocument = null;
        _fullDocument = null;

        OnPropertyChanged(nameof(PreviewDocument));
        OnPropertyChanged(nameof(FullDocument));
    }

    private static MediaBrush ResolveColorBrush(string color)
    {
        if (ColorBrushes.TryGetValue(color, out var brush))
        {
            return brush;
        }

        return ColorBrushes["yellow"];
    }

    private static MediaBrush CreateBrush(string hex)
    {
        var brush = (MediaSolidColorBrush)new MediaBrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private static string ExtractTitle(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Untitled";
        }

        var lines = content.Split('\n');

        foreach (var line in lines)
        {
            var cleaned = line.Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            cleaned = cleaned
                .TrimStart('#', '-', '*', '>', ' ')
                .Replace("[ ]", string.Empty, StringComparison.Ordinal)
                .Replace("[x]", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("`", string.Empty, StringComparison.Ordinal)
                .Trim();

            if (cleaned.Length == 0)
            {
                continue;
            }

            return cleaned.Length <= 48
                ? cleaned
                : cleaned[..48] + "...";
        }

        return "Untitled";
    }
}
