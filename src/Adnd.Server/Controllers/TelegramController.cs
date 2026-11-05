using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public record TelegramStatusDto(bool Enabled, bool Ready, string? Problem, string? BotUsername, bool Webhook);
public record TelegramAccountDto(TelegramStatusDto Bot, string? LinkedUsername, DateTimeOffset? LinkedAt);
public record TelegramCodeDto(string Code, DateTimeOffset ExpiresAt, string? Link, string? Command);
public record TelegramBindingDto(Guid Id, string ChatType, string ChatTitle, DateTimeOffset CreatedAt);
public record GameTelegramDto(TelegramStatusDto Bot, List<TelegramBindingDto> Bindings);

/// <summary>The signed-in user's Telegram account: whether the bot is on, the link, a one-time link code, unlinking.</summary>
[ApiController]
[Route("api/telegram")]
[Authorize]
public class TelegramController(
    AppDbContext db,
    TelegramOptions options,
    TelegramBotState bot,
    TelegramLinkService links,
    IUserIdProvider userIdProvider) : ControllerBase
{
    internal static TelegramStatusDto Status(TelegramOptions options, TelegramBotState bot) =>
        new(options.Enabled, bot.IsReady, bot.Problem, bot.Username, options.UseWebhook);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = userIdProvider.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return NotFound();
        return Ok(new TelegramAccountDto(Status(options, bot), user.TelegramUserId is null ? null : user.TelegramUsername, user.TelegramLinkedAt));
    }

    [HttpPost("link-code")]
    public async Task<IActionResult> CreateLinkCode()
    {
        if (Unavailable(this, options, bot) is { } unavailable) return unavailable;
        var code = await links.CreateCodeAsync(TelegramCodeKind.Account, userIdProvider.GetUserId(), null);
        return Ok(new TelegramCodeDto(code.Value, code.ExpiresAt, links.AccountLink(code.Value), $"/link {code.Value}"));
    }

    [HttpDelete("link")]
    public async Task<IActionResult> Unlink()
    {
        var userId = userIdProvider.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return NotFound();
        await links.UnlinkAsync(user);
        return NoContent();
    }

    internal static IActionResult? Unavailable(ControllerBase c, TelegramOptions options, TelegramBotState bot) =>
        !options.Enabled ? c.Conflict(new { error = "The Telegram bot is off: set TELEGRAM_BOT_TOKEN." })
        : !bot.IsReady ? c.Conflict(new { error = bot.Problem ?? "The Telegram bot is still starting. Try again in a moment." })
        : null;
}

/// <summary>The chats that play a game, for its creator: bind codes and disconnecting.</summary>
[Route("api/games/{gameId:guid}/telegram")]
public class GameTelegramController(
    AppDbContext db,
    TelegramOptions options,
    TelegramBotState bot,
    TelegramLinkService links,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid gameId)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;
        var bindings = await db.TelegramBindings.AsNoTracking()
            .Where(b => b.GameId == gameId && b.RevokedAt == null && b.ChatType != "private")
            .OrderBy(b => b.CreatedAt)
            .Select(b => new TelegramBindingDto(b.Id, b.ChatType, b.ChatTitle, b.CreatedAt))
            .ToListAsync();
        return Ok(new GameTelegramDto(TelegramController.Status(options, bot), bindings));
    }

    [HttpPost("codes")]
    public async Task<IActionResult> CreateCode(Guid gameId)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;
        if (TelegramController.Unavailable(this, options, bot) is { } unavailable) return unavailable;
        var code = await links.CreateCodeAsync(TelegramCodeKind.Bind, CurrentUserId, gameId);
        return Ok(new TelegramCodeDto(code.Value, code.ExpiresAt, links.GroupLink(code.Value), $"/bind {code.Value}"));
    }

    [HttpDelete("bindings/{bindingId:guid}")]
    public async Task<IActionResult> Revoke(Guid gameId, Guid bindingId)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;
        var binding = await db.TelegramBindings.FirstOrDefaultAsync(b => b.Id == bindingId && b.GameId == gameId && b.RevokedAt == null);
        if (binding is null) return NotFound(new { error = "No such chat." });
        await links.RevokeAsync(binding, "disconnected in Game Settings", tellChat: true);
        return NoContent();
    }
}

/// <summary>Where Telegram delivers updates when <c>ADND_HOSTNAME</c> is set; checked by the webhook secret header.</summary>
[ApiController]
[Route("api/telegram/webhook")]
[AllowAnonymous]
public class TelegramWebhookController(TelegramOptions options, TelegramBotService botService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        if (!options.UseWebhook) return NotFound();
        var header = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(header), Encoding.UTF8.GetBytes(options.WebhookSecret)))
            return Unauthorized();

        TgUpdate? update;
        try { update = await JsonSerializer.DeserializeAsync<TgUpdate>(Request.Body, TelegramApi.Json, HttpContext.RequestAborted); }
        catch (JsonException) { return BadRequest(); }
        if (update is not null) botService.Enqueue(update);
        // Answer at once; the update is handled in the background, in order.
        return Ok();
    }
}
