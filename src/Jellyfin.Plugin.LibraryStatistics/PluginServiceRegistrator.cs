using Jellyfin.Plugin.LibraryStatistics.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.LibraryStatistics;

/// <summary>Registers plugin services.</summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<StatisticsStore>();
        serviceCollection.AddSingleton<LibraryStorageScanner>();
        serviceCollection.AddSingleton<LibraryStatisticsService>();
    }
}
