using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

public interface IConfigurationService
{
    AppConfig GetConfiguration();
}
