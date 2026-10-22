using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LibraryStatistics.Configuration;

/// <summary>Plugin configuration.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the Jellyfin virtual-folder ids excluded from scans.</summary>
    public string[] ExcludedLibraryIds { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether exactly one 4K and one 1080p version are allowed automatically.</summary>
    public bool AutoAllow4KAnd1080P { get; set; }
}
