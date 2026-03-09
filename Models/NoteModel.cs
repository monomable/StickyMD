namespace StickyMD.Models;

public sealed class NoteModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Content { get; set; } = "hello world!";

    public string Color { get; set; } = "yellow";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public double X { get; set; } = 120;

    public double Y { get; set; } = 120;

    public double Width { get; set; } = 380;

    public double Height { get; set; } = 320;
}
