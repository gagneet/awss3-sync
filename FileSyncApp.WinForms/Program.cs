using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Services;
using FileSyncApp.S3.Services;
using FileSyncApp.WinForms.Forms;
using FileSyncApp.WinForms.Presenters;
using FileSyncApp.WinForms.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Enrichers.Sensitive;

namespace FileSyncApp.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IConfigurationService, ConfigurationService>();

                Log.Logger = new LoggerConfiguration()
                    .Enrich.WithSensitiveDataMasking(new SensitiveDataEnricherOptions())
                    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                services.AddLogging(builder => builder.AddSerilog());

                services.AddSingleton<ICredentialService, CredentialService>();
                services.AddSingleton<IDatabaseService, DatabaseService>();

                // Specific Auth Implementations
                services.AddSingleton<CognitoAuthService>();
                services.AddSingleton<LocalAuthService>(_ => new LocalAuthService("users.json"));

                // Unified Auth Service
                services.AddSingleton<IAuthService>(sp =>
                    new UnifiedAuthService(
                        sp.GetRequiredService<CognitoAuthService>(),
                        sp.GetRequiredService<LocalAuthService>()));

                services.AddSingleton<IFileStorageService, S3FileStorageService>();
                services.AddSingleton<MetadataCache>(sp =>
                    new MetadataCache("sync_metadata.db", sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MetadataCache>>()));
                services.AddSingleton<ISyncEngine, SyncEngine>();
                services.AddSingleton<ISyncSchedulerService, SyncSchedulerService>();

                services.AddTransient<MainForm>();
                services.AddTransient<LoginForm>();
                services.AddTransient<SettingsForm>();
                services.AddTransient<FileSyncPresenter>();
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
            var configService = sp.GetRequiredService<IConfigurationService>();
            var config        = configService.GetConfiguration();
            if (string.IsNullOrWhiteSpace(config.AWS.BucketName))
            {
                MessageBox.Show(
                    "AWS BucketName is not configured.\n\n" +
                    "Please edit appsettings.json (or open Settings after login) and set:\n" +
                    "  AWS.AccessKey, AWS.SecretKey, AWS.Region, AWS.BucketName",
                    "Configuration Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            var loginForm = sp.GetRequiredService<LoginForm>();
            if (loginForm.ShowDialog() == DialogResult.OK)
            {
                var mainForm = sp.GetRequiredService<MainForm>();
                var presenter = new FileSyncPresenter(
                    mainForm,
                    sp.GetRequiredService<IFileStorageService>(),
                    sp.GetRequiredService<ISyncEngine>(),
                    sp.GetRequiredService<IAuthService>());

                Application.Run(mainForm);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            MessageBox.Show($"Application error: {ex.Message}");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
