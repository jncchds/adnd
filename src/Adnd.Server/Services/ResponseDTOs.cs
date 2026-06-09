namespace Adnd.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


public class OpenAIChatResponse
{
    public List<OpenAIChatChoice>? Choices { get; set; }
    public OpenAIUsage? Usage { get; set; }
}

public class OpenAIChatChoice
{
    public OpenAIChatMessage? Message { get; set; }
}

public class OpenAIChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ReasoningContent { get; set; }
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

public class OpenAIToolCall
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public OpenAIFunctionCall? Function { get; set; }
}

public class OpenAIFunctionCall
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

public class OpenAIUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class OpenAIEmbeddingResponse
{
    public List<OpenAIEmbeddingData>? Data { get; set; }
}

public class OpenAIEmbeddingData
{
    public float[]? Embedding { get; set; }
}

public class GoogleAIResponse
{
    public List<GoogleAICandidate>? Candidates { get; set; }
}

public class GoogleAICandidate
{
    public GoogleAIContent? Content { get; set; }
}

public class GoogleAIContent
{
    public List<GoogleAIPart>? Parts { get; set; }
}

public class GoogleAIPart
{
    public string? Text { get; set; }
    public GoogleAIFunctionCall? FunctionCall { get; set; }
}

public class GoogleAIFunctionCall
{
    public string? Name { get; set; }
    public Dictionary<string, object>? Args { get; set; }
}

public class GoogleAIEmbeddingResponse
{
    public GoogleAIEmbedding? Embedding { get; set; }
}

public class GoogleAIEmbedding
{
    public float[]? Values { get; set; }
}

// ==================== Null helpers ====================

internal class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

#pragma warning disable CS8767 // Nullability of reference types in type of indexer/Value setter doesn't match implicitly implemented member (null impl)
internal class NullConfiguration : IConfiguration
{
    public string this[string key] { get => string.Empty; set { } }
    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
    public IConfigurationSection GetSection(string key) => new NullConfigurationSection();
    public IChangeToken GetReloadToken() => new NullChangeToken();
    public void Bind(object obj) { }
}

internal class NullConfigurationSection : IConfigurationSection
{
    public string Key => "";
    public string Path => "";
    public string Value { get => string.Empty; set { } }
    public string this[string key] { get => string.Empty; set { } }
    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
    public IConfigurationSection GetSection(string key) => this;
    public IChangeToken GetReloadToken() => new NullChangeToken();
}
#pragma warning restore CS8767

internal class NullChangeToken : IChangeToken
{
    public bool HasChanged => false;
    public bool ActiveChangeCallbacks => false;
    public IDisposable RegisterChangeCallback(Action<object> callback, object? state) => null!;
    public void GetChangeToken() => throw new NotImplementedException();
}

// Ollama API response types
public class OllamaResponse
{
    public OllamaMessage? Message { get; set; }
    public bool Done { get; set; }
}

public class OllamaMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<OllamaToolCall>? ToolCalls { get; set; }
}

public class OllamaToolCall
{
    public string? Id { get; set; }
    public OllamaFunctionCall? Function { get; set; }
}

public class OllamaFunctionCall
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

public class OllamaEmbeddingResponse
{
    public float[]? Embedding { get; set; }
}
