using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DuckMode.Core.Contracts;
using Flurl.Http;

namespace DuckMode.AI;

public class OpenAiClient : IAiClient
{
    // Đọc API key từ biến môi trường để tránh lộ key trong repo công khai
    // Set env var: OPENAI_API_KEY
    private static string? ApiKey => Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string Model = "gpt-3.5-turbo";

    public async Task<AiResponse> ChatAsync(IEnumerable<AiMessage> history, CancellationToken cancellationToken)
    {
        var messages = history.Select(m => new {
            role = m.Role == "user" ? "user" : "assistant",
            content = m.Content
        }).ToList();

        var req = new {
            model = Model,
            messages,
            max_tokens = 1024,
            temperature = 0.7
        };

        try
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                return new AiResponse("[OpenAI: Thiếu API key. Hãy đặt biến môi trường OPENAI_API_KEY rồi chạy lại ứng dụng.]");
            }

            var resp = await ApiUrl
                .WithTimeout(32)
                .WithHeader("Authorization", $"Bearer {ApiKey}")
                .AllowAnyHttpStatus()
                .SendJsonAsync(System.Net.Http.HttpMethod.Post, req, cancellationToken: cancellationToken)
                .ReceiveString();
            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                var message = err.GetProperty("message").GetString() ?? "unknown error";
                return new AiResponse($"[OpenAI lỗi: {message}]");
            }
            var choices = root.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var content = choices[0]
                                .GetProperty("message")
                                .GetProperty("content").GetString() ?? "";
                return new AiResponse(content.Trim());
            }
            return new AiResponse("[OpenAI: Không có phản hồi từ server.]");
        }
        catch (Exception ex)
        {
            return new AiResponse($"[OpenAI lỗi: {ex.Message}]");
        }
    }
}
