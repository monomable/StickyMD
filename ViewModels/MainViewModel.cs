using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using StickyMD.Helpers;
using StickyMD.Models;
using StickyMD.Services;

namespace StickyMD.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "StickyMD";

    private static readonly string[] ColorCycle =
    {
        "yellow",
        "pink",
        "blue",
        "green",
        "purple"
    };

    private readonly NoteStorageService _storageService;
    private readonly MarkdownService _markdownService;
    private readonly DispatcherTimer _saveDebounceTimer;
    private readonly ObservableCollection<NoteViewModel> _notes = new();

    private string _searchText = string.Empty;
    private NoteViewModel? _selectedNote;
    private bool _startWithWindows;
    private bool _suppressStartupWrite;
    private int _newNoteCounter;

    public MainViewModel(NoteStorageService storageService, MarkdownService markdownService)
    {
        _storageService = storageService;
        _markdownService = markdownService;

        FilteredNotes = CollectionViewSource.GetDefaultView(_notes);
        FilteredNotes.Filter = FilterNote;

        NewNoteCommand = new RelayCommand(_ => CreateAndOpenNewNote());
        OpenNoteCommand = new RelayCommand(param => OpenNote(param as NoteViewModel));
        DeleteNoteCommand = new RelayCommand(param => DeleteNote(param as NoteViewModel));
        ExitAppCommand = new RelayCommand(_ => ExitRequested?.Invoke(this, EventArgs.Empty));
        SaveNowCommand = new RelayCommand(async _ => await SaveNowAsync());

        _saveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _saveDebounceTimer.Tick += SaveDebounceTimer_Tick;

        LoadStartupOption();
    }

    public event EventHandler<NoteViewModel>? OpenNoteRequested;

    public event EventHandler<NoteViewModel>? NoteDeleted;

    public event EventHandler? ExitRequested;

    public ICommand NewNoteCommand { get; }

    public ICommand OpenNoteCommand { get; }

    public ICommand DeleteNoteCommand { get; }

    public ICommand ExitAppCommand { get; }

    public ICommand SaveNowCommand { get; }

    public ICollectionView FilteredNotes { get; }

    public IReadOnlyList<NoteViewModel> Notes => _notes;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            FilteredNotes.Refresh();
        }
    }

    public NoteViewModel? SelectedNote
    {
        get => _selectedNote;
        set => SetProperty(ref _selectedNote, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (!SetProperty(ref _startWithWindows, value))
            {
                return;
            }

            if (_suppressStartupWrite)
            {
                return;
            }

            UpdateStartupRegistration(value);
        }
    }

    public async Task InitializeAsync()
    {
        if (_notes.Count > 0)
        {
            return;
        }

        var loadedNotes = await _storageService.LoadAsync();

        var sorted = loadedNotes
            .OrderByDescending(note => note.UpdatedAt)
            .ToList();

        if (sorted.Count == 0)
        {
            sorted.Add(CreateDefaultModel(0));
        }

        for (var i = 0; i < sorted.Count; i++)
        {
            AddNoteInternal(sorted[i]);
        }

        SelectedNote = _notes[0];
        await SaveNowAsync();
    }

    public async Task SaveNowAsync()
    {
        _saveDebounceTimer.Stop();

        var dirtyNotes = _notes.Where(note => note.HasPendingChanges).ToList();
        if (dirtyNotes.Count == 0)
        {
            return;
        }

        var saveTime = DateTime.UtcNow;

        var models = _notes
            .Select(note =>
            {
                var model = note.ToModel();

                if (note.HasPendingChanges)
                {
                    model.UpdatedAt = saveTime;
                }

                return model;
            })
            .ToList();

        await _storageService.SaveAsync(models);

        foreach (var dirty in dirtyNotes)
        {
            dirty.MarkSaved(saveTime);
        }
    }

    public void SetNoteColor(NoteViewModel note, string color)
    {
        if (note is null)
        {
            return;
        }

        note.Color = color;
    }

    public void OpenSelectedNote()
    {
        if (SelectedNote is not null)
        {
            OpenNote(SelectedNote);
        }
    }

    private void CreateAndOpenNewNote()
    {
        var model = CreateDefaultModel(_newNoteCounter);
        _newNoteCounter++;

        var note = AddNoteInternal(model);

        SelectedNote = note;
        OpenNoteRequested?.Invoke(this, note);
    }

    private NoteViewModel AddNoteInternal(NoteModel model)
    {
        var note = new NoteViewModel(model, _markdownService);
        note.Changed += Note_Changed;

        _notes.Insert(0, note);
        FilteredNotes.Refresh();

        return note;
    }

    private void OpenNote(NoteViewModel? note)
    {
        if (note is null)
        {
            return;
        }

        SelectedNote = note;
        OpenNoteRequested?.Invoke(this, note);
    }

    private void DeleteNote(NoteViewModel? note)
    {
        if (note is null)
        {
            return;
        }

        note.Changed -= Note_Changed;
        _notes.Remove(note);
        FilteredNotes.Refresh();

        NoteDeleted?.Invoke(this, note);

        if (_notes.Count == 0)
        {
            CreateAndOpenNewNote();
            return;
        }

        if (ReferenceEquals(SelectedNote, note))
        {
            SelectedNote = _notes[0];
        }

        _ = SaveNowAsync();
    }

    private bool FilterNote(object item)
    {
        if (item is not NoteViewModel note)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return note.Contains(SearchText);
    }

    private void Note_Changed(object? sender, EventArgs e)
    {
        FilteredNotes.Refresh();

        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private async void SaveDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _saveDebounceTimer.Stop();
        await SaveNowAsync();
    }

    private static NoteModel CreateDefaultModel(int index)
    {
        var now = DateTime.UtcNow;
        var offset = (index % 8) * 24;

        return new NoteModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Content = "hello world!",
            Color = ColorCycle[index % ColorCycle.Length],
            CreatedAt = now,
            UpdatedAt = now,
            X = 140 + offset,
            Y = 140 + offset,
            Width = 380,
            Height = 320
        };
    }

    private void LoadStartupOption()
    {
        _suppressStartupWrite = true;
        StartWithWindows = IsStartupEnabled();
        _suppressStartupWrite = false;
    }

    private static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var value = key?.GetValue(RunValueName) as string;

            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    private static void UpdateStartupRegistration(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return;
            }

            if (!enabled)
            {
                key.DeleteValue(RunValueName, false);
                return;
            }

            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return;
            }

            key.SetValue(RunValueName, $"\"{executablePath}\"");
        }
        catch
        {
            // Keep app running even when registry write fails.
        }
    }
}