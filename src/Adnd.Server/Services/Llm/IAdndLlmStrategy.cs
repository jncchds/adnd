using System.Text.Json;

namespace Adnd.Server.Services.Llm;

/// <summary>
/// Thin strategy interface for formatting LLM HTTP requests and parsing responses.
/// Each provider (Ollama, OpenAI, Google, OpenAI-Compatible) has its own strategy.
/// The actual HTTP execution stays in BaseLLMProvider / provider implementations.
/// </summary>
public interface IAdndLlmStrategy
{
    string ProviderType { get; }

    HttpRequestMessage BuildCompletionRequest(
        string endpoint,
        string model,
        string systemPrompt,
        string userPrompt,
        LLMOptions opts);

    HttpRequestMessage BuildToolCallRequest(
        string endpoint,
        string model,
        string systemPrompt,
        string userPrompt,
        IEnumerable<ToolDefinition> tools,
        LLMOptions opts);

    LLMToolCallResult ParseToolCallResponse(string responseBody);

    string ParseCompletionResponse(string responseBody);
}
