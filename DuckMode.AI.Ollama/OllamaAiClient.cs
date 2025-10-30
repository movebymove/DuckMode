using System.Linq;
using DuckMode.Core.Contracts;
using Flurl.Http;

namespace DuckMode.AI.Ollama;

public class OllamaAiClient : IAiClient
{
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaAiClient(string baseUrl = "http://localhost:11434", string model = "mistral")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    private sealed record ChatMessage(string role, string content);
    private sealed record ChatResp(ChatMessage message);

    public async Task<AiResponse> ChatAsync(IEnumerable<AiMessage> history, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = _model,
            messages = history.Select(m => new ChatMessage(m.Role, m.Content)).ToArray(),
            stream = false
        };

        // Retry up to 3 times with auto-start
        Exception? lastException = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // Check Ollama status first
                var isRunning = await OllamaService.IsRunningAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"Attempt {attempt + 1}: Ollama is {(isRunning ? "running" : "not running")}");
                
                // Check and start Ollama if not running (only on first attempt)
                if (attempt == 0 && !isRunning)
                {
                    System.Diagnostics.Debug.WriteLine($"Ollama not running, attempting to start...");
                    var started = await OllamaService.StartOllamaAsync();
                    if (started)
                    {
                        // Wait longer for Ollama to fully initialize
                        await Task.Delay(3000, cancellationToken);
                        // Verify it's running now
                        isRunning = await OllamaService.IsRunningAsync(cancellationToken);
                        System.Diagnostics.Debug.WriteLine($"After start attempt, Ollama is {(isRunning ? "running" : "still not running")}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to start Ollama, but continuing anyway...");
                    }
                }

                // Ensure model exists before chat (only on first attempt)
                if (attempt == 0 && isRunning)
                {
                    var modelExists = await OllamaService.EnsureModelExistsAsync(_model, cancellationToken);
                    System.Diagnostics.Debug.WriteLine($"Model {_model} exists: {modelExists}");
                }

                if (!isRunning)
                {
                    throw new Exception("Ollama is not running");
                }

                System.Diagnostics.Debug.WriteLine($"Attempting to call /api/chat with model: {_model}");
                var resp = await $"{_baseUrl}/api/chat".WithTimeout(TimeSpan.FromSeconds(30))
                    .PostJsonAsync(body).ReceiveJson<ChatResp>();

                string content = resp.message?.content ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"Got response, content length: {content.Length}");
                
                if (string.IsNullOrEmpty(content))
                {
                    throw new Exception("Received empty response from Ollama");
                }
                
                return new AiResponse(content);
            }
            catch (FlurlHttpTimeoutException ex)
            {
                lastException = ex;
                System.Diagnostics.Debug.WriteLine($"Chat timeout on attempt {attempt + 1}: {ex.Message}");
                // Timeout - might be starting up (must catch before FlurlHttpException)
                if (attempt < 2)
                {
                    await Task.Delay(3000 * (attempt + 1), cancellationToken);
                    continue;
                }
            }
            catch (FlurlHttpException ex)
            {
                lastException = ex;
                var statusCode = ex.StatusCode?.ToString() ?? "null";
                System.Diagnostics.Debug.WriteLine($"Chat HTTP error on attempt {attempt + 1}: Status {statusCode}, {ex.Message}");
                
                // Check if it's a memory error
                string? errorBody = null;
                if (ex.Call?.Response != null)
                {
                    try
                    {
                        errorBody = await ex.Call.Response.GetStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Response body: {errorBody}");
                        
                        // Check for memory error
                        if (errorBody.Contains("requires more system memory") || errorBody.Contains("memory"))
                        {
                            throw new Exception($"Model {_model} cần nhiều RAM hơn máy hiện có. Vui lòng dùng model nhỏ hơn (ví dụ: qwen2.5:7b) hoặc đóng các ứng dụng khác.", ex);
                        }
                    }
                    catch (Exception innerEx) when (innerEx.Message.Contains("memory"))
                    {
                        throw; // Re-throw memory error
                    }
                    catch { }
                }
                
                // Ollama not running, model not found, or other HTTP error
                if (attempt < 2)
                {
                    // Only try to start if we got connection error (not 404/400)
                    if (ex.StatusCode == null || ex.StatusCode >= 500)
                    {
                        System.Diagnostics.Debug.WriteLine("Connection error detected, checking Ollama status...");
                        if (!await OllamaService.IsRunningAsync(cancellationToken))
                        {
                            await OllamaService.StartOllamaAsync();
                            await Task.Delay(2000, cancellationToken);
                        }
                    }
                    await Task.Delay(2000 * (attempt + 1), cancellationToken); // Exponential backoff
                    continue;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                System.Diagnostics.Debug.WriteLine($"Chat exception on attempt {attempt + 1}: {ex.GetType().Name} - {ex.Message}");
                if (attempt < 2)
                {
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }
            }
        }

        // All retries failed
        throw new Exception($"Failed to connect to Ollama after 3 attempts. Last error: {lastException?.Message}", lastException);
    }
}