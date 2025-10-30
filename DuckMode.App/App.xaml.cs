using System.Windows;
using DuckMode.AI;
using DuckMode.Core.Contracts;
using DuckMode.Data;
using DuckMode.Notifications;
using DuckMode.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DuckMode.App.Tray;
using DuckMode.Core.Services;
using DuckMode.AI.Ollama;
using Flurl.Http; // Force load Flurl.Http assembly

namespace DuckMode.App;

public partial class App : Application
{
    public static IHost? HostInstance { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HostInstance = new HostBuilder()
            .ConfigureServices((ctx, services) =>
            {
                var dataPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "DuckMode", "duckmode.db");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dataPath)!);

                services.AddSingleton(new LiteDbContext(dataPath));
                services.AddSingleton<ITaskRepository, TaskRepository>();
                services.AddSingleton<IReminderRepository, ReminderRepository>();
                services.AddSingleton<IConversationRepository, ConversationRepository>();
                services.AddSingleton<ISettingsService, SettingsService>();

                // Đăng ký ToastNotificationService gốc trước
                services.AddSingleton<ToastNotificationService>();
                // Đăng ký NotificationServiceAppWrapper là INotificationService (WPF-handling)
                services.AddSingleton<INotificationService>(sp =>
                    new NotificationServiceAppWrapper(sp.GetRequiredService<ToastNotificationService>()));

                services.AddSingleton<ReminderScheduler>(sp =>
                    new ReminderScheduler(
                        sp.GetRequiredService<IReminderRepository>(),
                        sp.GetRequiredService<ITaskRepository>(),
                        sp.GetRequiredService<INotificationService>()));
                services.AddSingleton<IReminderScheduler>(sp => sp.GetRequiredService<ReminderScheduler>());
                services.AddSingleton<INlpTaskExtractor, SimpleViNlpTaskExtractor>();
                services.AddSingleton<IAiClient>(_ => new DuckMode.AI.GeminiAiClient());

                services.AddSingleton<MainWindow>();
                services.AddSingleton<TrayIcon>();
            })
            .Build();

        HostInstance.Start();

        // Auto-connect to Ollama on startup
        _ = Task.Run(async () =>
        {
            try
            {
                var isRunning = await DuckMode.AI.Ollama.OllamaService.IsRunningAsync();
                if (!isRunning)
                {
                    System.Diagnostics.Debug.WriteLine("Ollama not running, attempting to start...");
                    await DuckMode.AI.Ollama.OllamaService.StartOllamaAsync();
                }
                // Ensure model exists
                await DuckMode.AI.Ollama.OllamaService.EnsureModelExistsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ollama startup error: {ex.Message}");
            }
        });

        var main = HostInstance.Services.GetRequiredService<MainWindow>();
        _ = HostInstance.Services.GetRequiredService<TrayIcon>();
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        HostInstance?.Dispose();
        base.OnExit(e);
    }
}

