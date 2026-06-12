using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Character Creation ====================

    public async Task<CreateCharacterResponse> CreateCharacter(Guid gameId, Guid playerId, string characterJson)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return new CreateCharacterResponse { Success = false, Error = "Authentication required." };
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return new CreateCharacterResponse { Success = false, Error = "Not an active player." };
        }

        // Verify the playerId matches the current user
        if (player.Id != playerId)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Player ID mismatch." });
            return new CreateCharacterResponse { Success = false, Error = "Player ID mismatch." };
        }

        var characterData = JsonSerializer.Deserialize<CharacterCreateInput>(characterJson);
        if (characterData == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Invalid character data." });
            return new CreateCharacterResponse { Success = false, Error = "Invalid character data." };
        }

        var character = new Character
        {
            PlayerId = playerId,
            Name = characterData.Name,
            Class = characterData.Class,
            Level = characterData.Level ?? 1,
            ProficiencyBonus = GetProficiencyBonus(characterData.Level ?? 1),
            CurrentHP = characterData.CurrentHP ?? 10,
            MaxHP = characterData.MaxHP ?? 10,
            Attributes = JsonSerializer.SerializeToElement(characterData.Attributes),
            Skills = JsonSerializer.SerializeToElement(characterData.Skills ?? new { }),
            Inventory = JsonSerializer.SerializeToElement(characterData.Inventory ?? new { }),
            Spells = JsonSerializer.SerializeToElement(new { }),
            Conditions = JsonSerializer.SerializeToElement(new { }),
            CustomFields = JsonSerializer.SerializeToElement(new { systemId = characterData.SystemId }),
            UpdatedAt = DateTime.UtcNow
        };

        _context.Characters.Add(character);
        await _context.SaveChangesAsync();

        return new CreateCharacterResponse
        {
            Success = true,
            CharacterId = character.Id,
            Name = character.Name,
            Class = character.Class,
            Level = character.Level
        };
    }

    private static int GetProficiencyBonus(int level)
    {
        if (level <= 4) return 2;
        if (level <= 8) return 3;
        if (level <= 12) return 4;
        if (level <= 16) return 5;
        return 6;
    }

}
