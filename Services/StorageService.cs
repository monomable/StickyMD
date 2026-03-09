using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using StickyMD.Models;

namespace StickyMD.Services;

public sealed class StorageService
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly string _appDirectory;
    private readonly string _notesDirectory;
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly object _syncRoot = new();

    public StorageService()
    {
        _appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StickyMD");

        _notesDirectory = Path.Combine(_appDirectory, "notes");
        _databasePath = Path.Combine(_appDirectory, "stickymd.db");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        };

        _connectionString = builder.ToString();
    }

    public string NotesDirectory => _notesDirectory;

    public void Initialize()
    {
        Directory.CreateDirectory(_notesDirectory);

        lock (_syncRoot)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS notes (
                    id TEXT PRIMARY KEY,
                    content TEXT NOT NULL,
                    x REAL NOT NULL,
                    y REAL NOT NULL,
                    width REAL NOT NULL,
                    height REAL NOT NULL,
                    updated_at TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();

            ImportMarkdownFilesWithoutRows(connection);
        }
    }

    public List<Note> LoadNotes()
    {
        lock (_syncRoot)
        {
            var notes = new List<Note>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, content, x, y, width, height, updated_at
                FROM notes
                ORDER BY updated_at ASC;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var updatedAtText = reader.GetString(6);
                if (!DateTime.TryParse(
                        updatedAtText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var updatedAt))
                {
                    updatedAt = DateTime.UtcNow;
                }

                notes.Add(new Note
                {
                    Id = reader.GetString(0),
                    Content = reader.GetString(1),
                    X = reader.GetDouble(2),
                    Y = reader.GetDouble(3),
                    Width = reader.GetDouble(4),
                    Height = reader.GetDouble(5),
                    UpdatedAt = updatedAt
                });
            }

            return notes;
        }
    }

    public void UpsertNote(Note note)
    {
        lock (_syncRoot)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO notes (id, content, x, y, width, height, updated_at)
                VALUES ($id, $content, $x, $y, $width, $height, $updatedAt)
                ON CONFLICT(id) DO UPDATE SET
                    content = excluded.content,
                    x = excluded.x,
                    y = excluded.y,
                    width = excluded.width,
                    height = excluded.height,
                    updated_at = excluded.updated_at;
                """;

            command.Parameters.AddWithValue("$id", note.Id);
            command.Parameters.AddWithValue("$content", note.Content ?? string.Empty);
            command.Parameters.AddWithValue("$x", note.X);
            command.Parameters.AddWithValue("$y", note.Y);
            command.Parameters.AddWithValue("$width", note.Width);
            command.Parameters.AddWithValue("$height", note.Height);
            command.Parameters.AddWithValue("$updatedAt", note.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();

            SaveMarkdownFile(note);
        }
    }

    public void DeleteNote(string noteId)
    {
        lock (_syncRoot)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM notes WHERE id = $id;";
            command.Parameters.AddWithValue("$id", noteId);
            command.ExecuteNonQuery();

            var filePath = Path.Combine(_notesDirectory, $"{noteId}.md");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private void SaveMarkdownFile(Note note)
    {
        var filePath = Path.Combine(_notesDirectory, $"{note.Id}.md");
        File.WriteAllText(filePath, note.Content ?? string.Empty, Utf8NoBom);
    }

    private void ImportMarkdownFilesWithoutRows(SqliteConnection connection)
    {
        var files = Directory.GetFiles(_notesDirectory, "*.md");
        if (files.Length == 0)
        {
            return;
        }

        var importedCount = 0;
        foreach (var file in files)
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            using var existsCommand = connection.CreateCommand();
            existsCommand.CommandText = "SELECT COUNT(1) FROM notes WHERE id = $id;";
            existsCommand.Parameters.AddWithValue("$id", id);

            var exists = Convert.ToInt32(existsCommand.ExecuteScalar() ?? 0) > 0;
            if (exists)
            {
                continue;
            }

            var offset = (importedCount % 10) * 24;
            importedCount++;

            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                """
                INSERT INTO notes (id, content, x, y, width, height, updated_at)
                VALUES ($id, $content, $x, $y, $width, $height, $updatedAt);
                """;

            insertCommand.Parameters.AddWithValue("$id", id);
            insertCommand.Parameters.AddWithValue("$content", File.ReadAllText(file, Encoding.UTF8));
            insertCommand.Parameters.AddWithValue("$x", 120 + offset);
            insertCommand.Parameters.AddWithValue("$y", 120 + offset);
            insertCommand.Parameters.AddWithValue("$width", 320);
            insertCommand.Parameters.AddWithValue("$height", 260);
            insertCommand.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            insertCommand.ExecuteNonQuery();
        }
    }
}
