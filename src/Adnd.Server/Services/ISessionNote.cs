using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Service for session note CRUD operations.
/// Notes are GM-only (Creator/GM roles can read/write; players cannot).
/// </summary>
public interface ISessionNoteService
{
    Task<List<SessionNoteResponse>> GetNotesAsync(Guid gameId, Guid sessionId);
    Task<SessionNoteResponse> CreateNoteAsync(Guid gameId, Guid sessionId, Guid creatorId, string title, string content);
    Task<SessionNoteResponse> UpdateNoteAsync(Guid gameId, Guid noteId, Guid updaterId, string title, string content);
    Task DeleteNoteAsync(Guid gameId, Guid noteId, Guid deleterId);
}

public class SessionNoteService : ISessionNoteService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SessionNoteService> _logger;

    public SessionNoteService(AppDbContext context, ILogger<SessionNoteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<SessionNoteResponse>> GetNotesAsync(Guid gameId, Guid sessionId)
    {
        return (await _context.SessionNotes
            .Where(sn => sn.Session.GameId == gameId && sn.SessionId == sessionId)
            .OrderByDescending(sn => sn.UpdatedAt ?? sn.CreatedAt)
            .Select(sn => new SessionNoteResponse
            {
                Id = sn.Id,
                SessionId = sn.SessionId,
                CreatorId = sn.CreatorId,
                CreatorName = sn.Creator != null ? (sn.Creator.DisplayName ?? sn.Creator.Email ?? "Unknown") : "Unknown",
                Title = sn.Title,
                Content = sn.Content,
                CreatedAt = sn.CreatedAt,
                UpdatedAt = sn.UpdatedAt
            })
            .ToListAsync());
    }

    public async Task<SessionNoteResponse> CreateNoteAsync(Guid gameId, Guid sessionId, Guid creatorId, string title, string content)
    {
        var note = new SessionNote
        {
            SessionId = sessionId,
            CreatorId = creatorId,
            Title = title,
            Content = content
        };

        _context.SessionNotes.Add(note);
        await _context.SaveChangesAsync();

        return new SessionNoteResponse
        {
            Id = note.Id,
            SessionId = note.SessionId,
            CreatorId = note.CreatorId,
            CreatorName = note.Creator?.DisplayName ?? note.Creator?.Email ?? "Unknown",
            Title = note.Title,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };
    }

    public async Task<SessionNoteResponse> UpdateNoteAsync(Guid gameId, Guid noteId, Guid updaterId, string title, string content)
    {
        var note = await _context.SessionNotes
            .Include(sn => sn.Session)
            .FirstOrDefaultAsync(sn => sn.Id == noteId && sn.Session.GameId == gameId);

        if (note == null)
            throw new KeyNotFoundException("Session note not found.");

        note.Title = title;
        note.Content = content;
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new SessionNoteResponse
        {
            Id = note.Id,
            SessionId = note.SessionId,
            CreatorId = note.CreatorId,
            CreatorName = note.Creator?.DisplayName ?? note.Creator?.Email ?? "Unknown",
            Title = note.Title,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };
    }

    public async Task DeleteNoteAsync(Guid gameId, Guid noteId, Guid deleterId)
    {
        var note = await _context.SessionNotes
            .Include(sn => sn.Session)
            .FirstOrDefaultAsync(sn => sn.Id == noteId && sn.Session.GameId == gameId);

        if (note == null)
            throw new KeyNotFoundException("Session note not found.");

        if (note.CreatorId != deleterId)
            throw new UnauthorizedAccessException("Only the creator can delete their notes.");

        _context.SessionNotes.Remove(note);
        await _context.SaveChangesAsync();
    }
}

public class SessionNoteResponse
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid CreatorId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
