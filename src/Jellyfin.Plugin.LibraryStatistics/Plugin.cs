using Jellyfin.Plugin.LibraryStatistics.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.LibraryStatistics;

/// <summary>The Library Statistics plugin.</summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>Gets the active plugin instance.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Library Statistics";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("102f0695-0752-4348-95ac-01f2825db6cb");

    /// <inheritdoc />
    public override string Description =>
        "Read-only library statistics and merged-version duplicate analysis with exact paths and MediaInfo.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = "LibraryStatistics",
                DisplayName = "Library Statistics",
                EnableInMainMenu = true,
                MenuIcon = "analytics",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        ];
    }
}
