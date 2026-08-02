using System.Text.Json;
using System.Text.RegularExpressions;

namespace Adnd.Server.Services.Llm;

public static class JsonSchemas
{
    public static readonly JsonElement Array =
        JsonSerializer.Deserialize<JsonElement>("{\"type\":\"array\"}");
    public static readonly JsonElement Object =
        JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\",\"additionalProperties\":true}");
}

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

    /// <summary>
    /// Extracts a JSON array from model output.
    /// </summary>
    /// <param name="expectedProperties">
    /// When the model wraps the array in an object, only these property names are accepted.
    /// Previously this returned the *first* array-valued property whatever its name, so a
    /// narrative response that happened to be JSON with any array field was misread as
    /// tool calls. Pass null to keep the permissive behaviour.
    /// </param>
    public static bool TryExtractArray(string text, out JsonElement array, params string[]? expectedProperties)
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

            // Model wrapped the array in an object (e.g. {"plotThreads": [...]} or {"threads": [...]})
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Array) continue;

                    if (expectedProperties is { Length: > 0 } &&
                        !expectedProperties.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                        continue;

                    array = prop.Value.Clone();
                    return true;
                }
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
