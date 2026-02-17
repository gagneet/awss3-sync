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
                    .Enrich.WithSensitiveDataMasking()
                    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                services.AddLogging(builder => builder.AddSerilog());

                services.AddSingleton<ICredentialService, CredentialService>();
                services.AddSingleton<IDatabaseService, DatabaseService>();
                services.AddSingleton<IAuthService, CognitoAuthService>();
                services.AddSingleton<IFileStorageService, S3FileStorageService>();
                services.AddSingleton<ISyncEngine, SyncEngine>();

                services.AddTransient<MainForm>();
                services.AddTransient<LoginForm>();
                services.AddTransient<FileSyncPresenter>();
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
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
