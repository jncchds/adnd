using System.Text.Json;
using System.Text.RegularExpressions;

namespace Adnd.Server.Services.Llm;

public static class JsonExtract
{
    private static readonly Regex FenceRegex = new(@"```(?:json)?\s*([\s\S]*?)```", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryExtract(string text, out JsonDocument? doc)
    {
        doc = null;

        try
        {
            doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException) { }

        var match = FenceRegex.Match(text);
        if (match.Success)
        {
            try
            {
                doc = JsonDocument.Parse(match.Groups[1].Value.Trim());
                return true;
            }
            catch (JsonException) { }
        }

        var firstBracket = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '{' or '[')
            {
                firstBracket = i;
                break;
            }
        }

        if (firstBracket >= 0)
        {
            try
            {
                doc = JsonDocument.Parse(text[firstBracket..]);
                return true;
            }
            catch (JsonException) { }
        }

        return false;
    }

    public static bool TryExtractArray(string text, out JsonElement array)
    {
        array = default;
        if (!TryExtract(text, out var doc) || doc is null)
            return false;

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                array = doc.RootElement.Clone();
                return true;
            }
        }

        return false;
    }

    public static bool TryExtractObject(string text, out JsonElement obj)
    {
        obj = default;
        if (!TryExtract(text, out var doc) || doc is null)
            return false;

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                obj = doc.RootElement.Clone();
                return true;
            }
        }

        return false;
    }
}
