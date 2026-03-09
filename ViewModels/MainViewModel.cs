using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using StickyMD.Models;
using StickyMD.Services;

namespace StickyMD.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private static readonly Brush[] CardBrushPalette =
    {
        CreateFrozenBrush("#EFE6B5"),
        CreateFrozenBrush("#EFD3E2"),
        CreateFrozenBrush("#D8EAD7"),
        CreateFrozenBrush("#D7E4EF")
    };

    private readonly NoteService _noteService;
    private readonly ObservableCollection<NoteViewModel> _notes = new();
    private readonly ICollectionView _filteredNotes;

    private string _searchText = string.Empty;
    private NoteViewModel? _selectedNote;

    public MainViewModel(NoteService noteService)
    {
        _noteService = noteService;

        _filteredNotes = CollectionViewSource.GetDefaultView(_notes);
        _filteredNotes.Filter = FilterNote;

        CreateNoteCommand = new RelayCommand(_ => CreateNote());
        DeleteNoteCommand = new RelayCommand(param => DeleteNote(param as NoteViewModel));
        CloseAppCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler<NoteViewModel>? EditorRequested;

    public event EventHandler? CloseRequested;

    public ICommand CreateNoteCommand { get; }

    public ICommand DeleteNoteCommand { get; }

    public ICommand CloseAppCommand { get; }

    public ICollectionView FilteredNotes => _filteredNotes;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            _filteredNotes.Refresh();
        }
    }

    public NoteViewModel? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (!SetProperty(ref _selectedNote, value))
            {
                return;
            }

            if (value is not null)
            {
                EditorRequested?.Invoke(this, value);
            }
        }
    }

    public void Initialize()
    {
        if (_notes.Count > 0)
        {
            return;
        }

        var notes = _noteService.LoadNotes()
            .OrderByDescending(note => note.UpdatedAt)
            .ToList();

        if (notes.Count == 0)
        {
            notes.Add(_noteService.CreateNote());
        }

        for (var i = 0; i < notes.Count; i++)
        {
            _notes.Add(CreateViewModel(notes[i], i));
        }

        SelectedNote = _notes[0];
    }

    public void FlushAllNotes()
    {
        foreach (var note in _notes)
        {
            note.FlushSaveNow();
        }
    }

    private void CreateNote()
    {
        var note = _noteService.CreateNote();
        var noteViewModel = CreateViewModel(note, _notes.Count);

        _notes.Insert(0, noteViewModel);
        _filteredNotes.Refresh();

        SelectedNote = noteViewModel;
    }

    private void DeleteNote(NoteViewModel? noteViewModel)
    {
        if (noteViewModel is null)
        {
            return;
        }

        _noteService.DeleteNote(noteViewModel.Id);
        _notes.Remove(noteViewModel);
        _filteredNotes.Refresh();

        if (_notes.Count == 0)
        {
            CreateNote();
            return;
        }

        if (ReferenceEquals(SelectedNote, noteViewModel))
        {
            SelectedNote = _notes[0];
        }
    }

    private bool FilterNote(object item)
    {
        if (item is not NoteViewModel noteViewModel)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return noteViewModel.Contains(SearchText);
    }

    private NoteViewModel CreateViewModel(Note note, int index)
    {
        var brush = CardBrushPalette[index % CardBrushPalette.Length];
        return new NoteViewModel(note, _noteService, brush);
    }

    private static Brush CreateFrozenBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
