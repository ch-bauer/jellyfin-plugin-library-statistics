using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LibraryStatistics.Configuration;

/// <summary>Plugin configuration.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the Jellyfin virtual-folder ids excluded from scans.</summary>
    public string[] ExcludedLibraryIds { get; set; } = [];
}
