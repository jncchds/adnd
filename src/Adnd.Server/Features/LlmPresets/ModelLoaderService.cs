using System.Net.Http.Json;

namespace Adnd.Server.Features.LlmPresets;

public class ModelLoaderService
{
    private readonly HttpClient _http;

    public ModelLoaderService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(string[] ChatModels, string[] EmbeddingModels)> LoadModels(string provider, string? hostUrl = null)
    {
        return provider.ToLower() switch
        {
            "openai" => await LoadOpenAIModels(),
            "google" => await LoadGoogleModels(),
            "ollama" => await LoadOllamaModels(hostUrl),
            "lmstudio" => await LoadLmStudioModels(hostUrl),
            _ => (Array.Empty<string>(), Array.Empty<string>())
        };
    }

    private async Task<(string[], string[])> LoadOpenAIModels()
    {
        var response = await _http.GetStringAsync("https://api.openai.com/v1/models");
        // Parse JSON response, filter:
        // Chat: IDs containing "gpt" or "o"
        // Embedding: IDs containing "text-embedding"
        // Return (chatModels, embeddingModels)
        throw new NotImplementedException();
    }

    private async Task<(string[], string[])> LoadGoogleModels()
    {
        var apiKey = "placeholder"; // Will be fetched from preset's decrypted API key
        var response = await _http.GetStringAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}");
        // Parse JSON, filter by type: "chat" vs "embedding"
        throw new NotImplementedException();
    }

    private async Task<(string[], string[])> LoadOllamaModels(string? hostUrl)
    {
        var url = hostUrl ?? "http://localhost:11434/api/tags";
        var response = await _http.GetFromJsonAsync<OllamaTagsResponse>(url);
        var models = response?.Models?.Select(m => m.Name).ToArray() ?? Array.Empty<string>();
        return (models, models); // Ollama doesn't distinguish chat/embedding
    }

    private async Task<(string[], string[])> LoadLmStudioModels(string? hostUrl)
    {
        var url = (hostUrl ?? "http://localhost:1234") + "/v1/models";
        var response = await _http.GetFromJsonAsync<OpenAIModelsResponse>(url);
        var chatModels = response?.Data?.Where(m => m.Id.Contains("gpt") || m.Id.Contains("llama") || m.Id.Contains("mistral")).Select(m => m.Id).ToArray() ?? Array.Empty<string>();
        var embeddingModels = response?.Data?.Where(m => m.Id.Contains("embedding")).Select(m => m.Id).ToArray() ?? Array.Empty<string>();
        return (chatModels, embeddingModels);
    }
}

public class OllamaTagsResponse
{
    public OllamaModel[]? Models { get; set; }
}

public class OllamaModel
{
    public string? Name { get; set; }
}

public class OpenAIModelsResponse
{
    public OpenAIModel[]? Data { get; set; }
}

public class OpenAIModel
{
    public string? Id { get; set; }
}
