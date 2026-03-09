namespace StickyMD.Models;

public sealed class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Content { get; set; } = "# 오늘 할 일\n\n- [ ] 새 작업 추가";

    public double X { get; set; } = 120;

    public double Y { get; set; } = 120;

    public double Width { get; set; } = 320;

    public double Height { get; set; } = 260;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
