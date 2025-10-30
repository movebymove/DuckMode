using System.Diagnostics;
using System.Linq;
using Flurl.Http;

namespace DuckMode.AI.Ollama;

public static class OllamaService
{
    private const string OllamaUrl = "http://localhost:11434";
    private const string DefaultModel = "qwen2.5:7b";

    public static async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await $"{OllamaUrl}/api/tags".WithTimeout(TimeSpan.FromSeconds(5)).GetAsync(cancellationToken: cancellationToken);
            return response.StatusCode == 200;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ollama IsRunning check failed: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> EnsureModelExistsAsync(string model = DefaultModel, CancellationToken cancellationToken = default)
    {
        if (!await IsRunningAsync(cancellationToken)) return false;

        try
        {
            var resp = await $"{OllamaUrl}/api/tags".WithTimeout(TimeSpan.FromSeconds(5)).GetJsonAsync<dynamic>(cancellationToken: cancellationToken);
            dynamic tags = resp;
            var models = tags.models as IEnumerable<dynamic> ?? Array.Empty<dynamic>();
            var hasModel = models.Any(m => m.name?.ToString().StartsWith(model) == true);
            
            if (!hasModel)
            {
                // Try to pull model
                await StartOllamaAsync();
                await Task.Delay(2000, cancellationToken); // Wait for Ollama to start
                
                var pullProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ollama",
                        Arguments = $"pull {model}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                pullProcess.Start();
                await pullProcess.WaitForExitAsync(cancellationToken);
                return pullProcess.ExitCode == 0;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> StartOllamaAsync()
    {
        try
        {
            // Check if already running
            if (await IsRunningAsync()) return true;

            // Try to start Ollama service
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            
            // Wait a bit for it to start
            await Task.Delay(3000);
            
            // Check if it's running now
            return await IsRunningAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start Ollama: {ex.Message}");
            return false;
        }
    }
}

