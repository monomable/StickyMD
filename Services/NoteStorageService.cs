using System.IO;
using System.Text.Json;
using StickyMD.Models;

namespace StickyMD.Services;

public sealed class NoteStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly string _notesFilePath;

    public NoteStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDirectory = Path.Combine(appData, "StickyMD");

        Directory.CreateDirectory(appDirectory);
        _notesFilePath = Path.Combine(appDirectory, "notes.json");
    }

    public async Task<IReadOnlyList<NoteModel>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_notesFilePath))
            {
                return Array.Empty<NoteModel>();
            }

            await using var stream = new FileStream(
                _notesFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var container = await JsonSerializer.DeserializeAsync<NotesContainer>(stream, JsonOptions, cancellationToken);
            return container?.Notes ?? new List<NoteModel>();
        }
        catch
        {
            // 손상된 파일 등 예외 상황에서는 앱 안정성을 위해 빈 목록으로 복구합니다.
            return Array.Empty<NoteModel>();
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveAsync(IEnumerable<NoteModel> notes, CancellationToken cancellationToken = default)
    {
        var container = new NotesContainer
        {
            Notes = notes.ToList()
        };

        var tempPath = _notesFilePath + ".tmp";

        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, container, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(_notesFilePath))
            {
                File.Replace(tempPath, _notesFilePath, null);
            }
            else
            {
                File.Move(tempPath, _notesFilePath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            _ioLock.Release();
        }
    }

    private sealed class NotesContainer
    {
        public List<NoteModel> Notes { get; set; } = new();
    }
}
