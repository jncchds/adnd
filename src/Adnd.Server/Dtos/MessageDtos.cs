using System.Text.Json;
using Adnd.Server.Models;

namespace Adnd.Server.Dtos;

/// <summary>
/// A chat message as returned by the history endpoint. Mirrors the SignalR MessageDto
/// field-for-field so live and historical messages render identically, and carries the
/// author's display name so the client does not have to resolve it per message.
/// </summary>
public record MessageHistoryDto(
    Guid Id,
    Guid SessionId,
    Guid? PlayerId,
    string Content,
    string Type,
    JsonElement? Metadata,
    bool IsOOC,
    Guid? WhisperFromId,
    Guid? WhisperToId,
    string? WhisperTarget,
    DateTimeOffset CreatedAt,
    string? PlayerDisplayName)
{
    public static MessageHistoryDto From(Message m, string? displayName) => new(
        m.Id, m.SessionId, m.PlayerId, m.Content, m.Type,
        m.Metadata.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : m.Metadata,
        m.IsOOC, m.WhisperFromId, m.WhisperToId, m.WhisperTarget, m.CreatedAt, displayName);
}

public record MessagePageDto(
    IReadOnlyList<MessageHistoryDto> Items,
    bool HasMore,
    DateTimeOffset? NextCursor);
