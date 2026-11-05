using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Adnd.Server.Hubs;
using Adnd.Server.Models;

namespace Adnd.Server.Telegram;

/// <summary>
/// Pure helpers: who may see a message, how a message, the combat card and a character sheet look in Telegram
/// (HTML parse mode), and the button data the bot hands out.
/// </summary>
public static partial class TelegramFormat
{
    /// <summary>Telegram's limit is 4,096 characters of visible text; HTML is clipped before it is built.</summary>
    public const int MaxText = 3800;

    public static string H(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    public static string Clip(string? text, int max) =>
        string.IsNullOrEmpty(text) || text.Length <= max ? text ?? string.Empty : text[..(max - 1)].TrimEnd() + "…";

    public static string Hash(string html, IReadOnlyList<IReadOnlyList<TgButton>>? keyboard) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html + "\n" + JsonSerializer.Serialize(keyboard ?? []))))[..32];

    // ── Who sees what ─────────────────────────────────────────────────────────

    /// <summary>
    /// Whether <paramref name="viewer"/> may see the message — the same rule as the chat history
    /// (<c>MessagesController</c>): no whisper routing, or routed from or to them; and a secret roll only for the
    /// roller and the game's creator. A null viewer is the table — a group chat — which sees neither whispers nor
    /// secret rolls.
    /// </summary>
    public static bool Visible(Message m, Player? viewer)
    {
        if (m.IsDeleted) return false;
        if (viewer is null) return m.WhisperFromId is null && m.WhisperToId is null && !m.IsSecret;
        var routed = m.WhisperFromId is not null || m.WhisperToId is not null;
        if (routed && m.WhisperFromId != viewer.Id && m.WhisperToId != viewer.Id) return false;
        return !m.IsSecret || m.PlayerId == viewer.Id || viewer.Role == PlayerRole.Creator;
    }

    // ── Messages ──────────────────────────────────────────────────────────────

    /// <summary>What a message needs from around it to be drawn.</summary>
    public sealed record MessageContext(
        string? SenderName,
        string? TargetName,
        /// <summary>Set when the chat does not follow this game: a header saying which game it is from.</summary>
        string? GameTitle,
        /// <summary>The viewer is the one a roll prompt was asked of, and it is still open.</summary>
        bool PromptOpen);

    public static readonly string[] PromptTypes = ["RollRequest", "RerollOffer", "RollDecline"];
    private static readonly string[] DiceTypes = ["DiceRoll", "SkillCheck", "AttackRoll"];

    public static string RenderMessage(Message m, MessageContext ctx)
    {
        var body = m.Type switch
        {
            "GM" => GmText(m, ctx),
            "Combat" => $"⚔️ <b>{Inline(m.Content)}</b>",
            "System" => $"ℹ️ <i>{Inline(m.Content)}</i>",
            "OOC" when m.WhisperFromId is not null => $"❔ <i>You asked the GM:</i> {Inline(m.Content)}",
            "OOC" => $"💬 <i>{H(ctx.SenderName ?? "Someone")} (OOC):</i> {Inline(m.Content)}",
            "Whisper" or "OOCWhisper" => WhisperText(m, ctx),
            var t when PromptTypes.Contains(t) => $"🎲 {(ctx.PromptOpen ? "<b>The GM asks you:</b> " : "")}{Inline(m.Content)}",
            var t when DiceTypes.Contains(t) => $"🎲 {(m.IsSecret ? "🔒 " : "")}{Inline(m.Content)}",
            "Chat" => $"<b>{H(ctx.SenderName ?? "Someone")}:</b> {Inline(m.Content)}",
            _ => ctx.SenderName is { } who ? $"<b>{H(who)}:</b> {Inline(m.Content)}" : Inline(m.Content),
        };
        return ctx.GameTitle is { } game ? $"🗺 <i>{H(game)}</i>\n{body}" : body;
    }

    private static string GmText(Message m, MessageContext ctx)
    {
        var text = Markdown(m.Content);
        return m.WhisperToId is not null ? $"🧙 <i>The GM, to you only:</i>\n{text}" : $"🧙 {text}";
    }

    private static string WhisperText(Message m, MessageContext ctx)
    {
        var ooc = m.Type == "OOCWhisper" ? " (OOC)" : "";
        var from = m.WhisperFromId is null ? "The GM" : ctx.SenderName ?? "Someone";
        var to = ctx.TargetName ?? "you";
        return $"🤫 <i>{H(from)} → {H(to)}{ooc}:</i> {Inline(m.Content)}";
    }

    private static string Inline(string text) => H(Clip(text, MaxText));

    /// <summary>The buttons of an open roll prompt; null for anything else.</summary>
    public static IReadOnlyList<IReadOnlyList<TgButton>>? PromptKeyboard(Message m, bool open)
    {
        if (!open || m.Metadata.ValueKind != JsonValueKind.Object) return null;
        var kind = m.Metadata.TryGetProperty("kind", out var k) ? k.GetString() : null;
        if (!m.Metadata.TryGetProperty("toolCallId", out var idEl) || !Guid.TryParse(idEl.GetString(), out var toolCallId))
            return null;
        var id = toolCallId.ToString("N");
        switch (kind)
        {
            case "rollRequest":
                return [[new TgButton("🎲 Roll", $"r:{id}:c"), new TgButton("Decline", $"r:{id}:d")]];
            case "rerollOffer":
                var rows = new List<IReadOnlyList<TgButton>>();
                if (m.Metadata.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array)
                {
                    var i = 0;
                    foreach (var o in options.EnumerateArray())
                    {
                        // Rows stored before v0.2.0 spell the names PascalCase.
                        var name = o.TryGetProperty("name", out var n) || o.TryGetProperty("Name", out n) ? n.GetString() : null;
                        var left = (o.TryGetProperty("usesRemaining", out var u) || o.TryGetProperty("UsesRemaining", out u))
                                   && u.ValueKind == JsonValueKind.Number ? $" ({u.GetInt32()} left)" : "";
                        rows.Add([new TgButton($"🔁 {Clip(name ?? "Reroll", 40)}{left}", $"r:{id}:o:{i}")]);
                        i++;
                    }
                }
                rows.Add([new TgButton("Keep the roll", $"r:{id}:k")]);
                return rows;
            default:
                return null;
        }
    }

    /// <summary>
    /// The GM's Markdown as Telegram HTML: bold, italics, code, headings as bold lines and list bullets. Escaped
    /// first, so nothing in the text can open a tag.
    /// </summary>
    public static string Markdown(string text)
    {
        var lines = H(Clip(text.Replace("\r\n", "\n"), MaxText)).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (HeadingPattern().Match(line) is { Success: true } h) line = $"<b>{h.Groups[1].Value}</b>";
            else if (BulletPattern().Match(line) is { Success: true } b) line = "• " + b.Groups[1].Value;
            else if (line.Trim() is "---" or "***") line = "";
            line = BoldPattern().Replace(line, "<b>$2</b>");
            line = ItalicPattern().Replace(line, "<i>$2</i>");
            line = CodePattern().Replace(line, "<code>$1</code>");
            lines[i] = line;
        }
        return string.Join('\n', lines).Trim();
    }

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(.+?)\s*#*$")] private static partial Regex HeadingPattern();
    [GeneratedRegex(@"^\s*[-*+]\s+(.+)$")] private static partial Regex BulletPattern();
    [GeneratedRegex(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1")] private static partial Regex BoldPattern();
    [GeneratedRegex(@"(?<![\w*])(\*|_)(?=\S)(.+?)(?<=\S)\1(?![\w*])")] private static partial Regex ItalicPattern();
    [GeneratedRegex(@"`([^`]+)`")] private static partial Regex CodePattern();

    // ── The fight ─────────────────────────────────────────────────────────────

    /// <summary>The combat card: zones in order, who stands where with their hit points, and whose turn it is.</summary>
    public static string CombatCard(CombatDto combat)
    {
        var sb = new StringBuilder();
        var over = combat.Status == nameof(CombatStatus.Finished);
        sb.Append($"⚔️ <b>{H(combat.Name)}</b> — {(over ? "the fight is over" : $"round {combat.CurrentRound}")}\n");
        var acting = !over && combat.CurrentTurnIndex >= 0 && combat.CurrentTurnIndex < combat.Participants.Count
            ? combat.Participants[combat.CurrentTurnIndex]
            : null;

        // Anyone standing nowhere known is shown in the first zone.
        var zones = combat.Zones.Count > 0 ? combat.Zones : [new CombatZoneDto("", "The field", [])];
        var known = zones.Select(z => z.Id).ToHashSet();
        string ZoneOf(ParticipantDto p) => p.ZoneId is { } z && known.Contains(z) ? z : zones[0].Id;
        foreach (var zone in zones)
        {
            var here = combat.Participants.Where(p => ZoneOf(p) == zone.Id).ToList();
            var tags = zone.Tags.Count > 0 ? $" <i>({H(string.Join(", ", zone.Tags))})</i>" : "";
            sb.Append($"\n📍 <b>{H(zone.Name)}</b>{tags}\n");
            if (here.Count == 0) sb.Append("   —\n");
            foreach (var p in here)
            {
                var marker = acting?.Id == p.Id ? "▶" : p.ParticipantType == nameof(CombatParticipantType.Character) ? "🛡" : "👹";
                var hp = p.HP <= 0 ? "💀 down" : $"{p.HP}/{p.MaxHP} HP";
                var conditions = p.Conditions.Count > 0 ? $" · {H(string.Join(", ", p.Conditions))}" : "";
                sb.Append($"   {marker} {H(p.DisplayName)} — {hp}, AC {p.AC}{conditions}\n");
            }
        }
        if (acting is not null) sb.Append($"\n▶ <b>{H(acting.DisplayName)}</b>'s turn.");
        return sb.ToString().TrimEnd();
    }

    // ── Character sheet ───────────────────────────────────────────────────────

    public static string Sheet(Character c)
    {
        var sb = new StringBuilder();
        sb.Append($"🧾 <b>{H(c.Name)}</b> — {H(c.Race)} {H(c.Class)} {c.Level}\n");
        sb.Append($"❤️ {c.CurrentHP}/{c.MaxHP} HP · 🛡 AC {c.ArmourClass} · ✨ {c.Experience} XP · prof +{c.ProficiencyBonus}\n");
        if (c.Attributes.ValueKind == JsonValueKind.Object)
            sb.Append(string.Join(" · ", c.Attributes.EnumerateObject()
                .Where(a => a.Value.ValueKind == JsonValueKind.Number)
                .Select(a => $"{H(a.Name.Length > 3 ? a.Name[..3].ToUpperInvariant() : a.Name.ToUpperInvariant())} {a.Value.GetInt32()}"))).Append('\n');
        var conditions = Strings(c.Conditions);
        if (conditions.Count > 0) sb.Append($"⚠️ {H(string.Join(", ", conditions))}\n");
        var items = Items(c.Inventory);
        if (items.Count > 0) sb.Append($"\n🎒 {H(string.Join(", ", items))}\n");
        if (c.Spells.ValueKind == JsonValueKind.Object)
            foreach (var group in c.Spells.EnumerateObject())
            {
                var spells = Strings(group.Value).Select(s => s.Split(" (")[0]).ToList();
                if (spells.Count > 0) sb.Append($"✨ <b>{H(group.Name)}:</b> {H(string.Join(", ", spells))}\n");
            }
        return Clip(sb.ToString().TrimEnd(), MaxText);
    }

    private static List<string> Strings(JsonElement e) =>
        e.ValueKind != JsonValueKind.Array ? [] :
        e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList();

    /// <summary>Inventory stacks <c>[{name, quantity}]</c>; older sheets hold bare strings.</summary>
    private static List<string> Items(JsonElement e) =>
        e.ValueKind != JsonValueKind.Array ? [] :
        e.EnumerateArray().Select(x => x.ValueKind switch
        {
            JsonValueKind.String => x.GetString(),
            JsonValueKind.Object when x.TryGetProperty("name", out var n) =>
                x.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number && q.GetInt32() > 1
                    ? $"{n.GetString()} ×{q.GetInt32()}"
                    : n.GetString(),
            _ => null,
        }).OfType<string>().ToList();

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>"/cmd@bot args" → ("cmd", "args"); null when the text is not a command (or is addressed to another bot).</summary>
    public static (string Command, string Args)? ParseCommand(string text, string? botUsername)
    {
        if (!text.StartsWith('/')) return null;
        var space = text.IndexOfAny([' ', '\n']);
        var head = space < 0 ? text[1..] : text[1..space];
        var args = space < 0 ? "" : text[(space + 1)..].Trim();
        var at = head.IndexOf('@');
        if (at >= 0)
        {
            if (botUsername is not null && !head[(at + 1)..].Equals(botUsername, StringComparison.OrdinalIgnoreCase)) return null;
            head = head[..at];
        }
        return head.Length == 0 ? null : (head.ToLowerInvariant(), args);
    }
}
