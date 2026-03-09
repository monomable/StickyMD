using StickyMD.Models;

namespace StickyMD.Services;

public sealed class NoteService
{
    private readonly StorageService _storageService;
    private readonly object _syncRoot = new();
    private int _newNoteIndex;

    public NoteService(StorageService storageService)
    {
        _storageService = storageService;
    }

    public IReadOnlyList<Note> LoadNotes()
    {
        var notes = _storageService.LoadNotes();

        lock (_syncRoot)
        {
            _newNoteIndex = notes.Count;
        }

        return notes;
    }

    public Note CreateNote()
    {
        lock (_syncRoot)
        {
            var offset = (_newNoteIndex % 10) * 24;
            _newNoteIndex++;

            var note = new Note
            {
                Id = Guid.NewGuid().ToString("N"),
                Content = "# 오늘 할 일\n\n- [ ] 새 작업 추가",
                X = 120 + offset,
                Y = 120 + offset,
                Width = 320,
                Height = 260,
                UpdatedAt = DateTime.UtcNow
            };

            SaveNote(note);
            return note;
        }
    }

    public void SaveNote(Note note)
    {
        note.UpdatedAt = DateTime.UtcNow;
        _storageService.UpsertNote(note);
    }

    public void DeleteNote(string noteId)
    {
        _storageService.DeleteNote(noteId);
    }
}
