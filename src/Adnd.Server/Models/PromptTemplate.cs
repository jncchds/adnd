namespace Adnd.Server.Models;

public class PromptTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
