using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DuckMode.Core.Contracts;
using Flurl.Http;

namespace DuckMode.AI;

public class GeminiAiClient : IAiClient
{
    // Đọc API key từ biến môi trường để tránh lộ key trong repo công khai
    // Set env var: GEMINI_API_KEY
    private static string? ApiKey
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            try
            {
                var p = System.IO.Path.Combine(AppContext.BaseDirectory, "gemini.key");
                if (System.IO.File.Exists(p))
                {
                    var firstLine = System.IO.File.ReadLines(p).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(firstLine)) return firstLine.Trim();
                }
            }
            catch { }
            return null;
        }
    }
    private const string ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    public async Task<AiResponse> ChatAsync(IEnumerable<AiMessage> history, CancellationToken cancellationToken)
    {
        var req = new
        {
            contents = history.Select(msg => new
            {
                parts = new[] { new { text = msg.Content } }
            }).ToList()
        };

        try
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                return new AiResponse("[Gemini: Thiếu API key. Hãy đặt biến môi trường GEMINI_API_KEY rồi chạy lại ứng dụng.]");
            }

            var resp = await ApiUrl
                .WithTimeout(32)
                .WithHeader("X-goog-api-key", ApiKey)
                .SendJsonAsync(System.Net.Http.HttpMethod.Post, req, cancellationToken: cancellationToken)
                .ReceiveString();
            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                var message = err.GetProperty("message").GetString() ?? "unknown error";
                return new AiResponse($"[Gemini API lỗi: {message}]");
            }
            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var content = candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString() ?? "[Gemini không trả về kết quả.]";
                return new AiResponse(content.Trim());
            }
            return new AiResponse("[Gemini: Không có dữ liệu phản hồi hoặc hết quota.]");
        }
        catch (Exception ex)
        {
            return new AiResponse($"[Gemini lỗi: {ex.Message}. Kiểm tra key/API hoặc kết nối mạng.]");
        }
    }
}
