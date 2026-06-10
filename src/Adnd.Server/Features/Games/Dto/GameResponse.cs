namespace Adnd.Server.Features.Games.Dto;

public class GameResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid SystemId { get; set; }
    public string SystemName { get; set; } = string.Empty;
    public string SystemSlug { get; set; } = string.Empty;
    public Guid CreatorId { get; set; }
    public string CreatorDisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PlotSeed { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
