using System.Text.Json;
using Jellyfin.Plugin.LibraryStatistics.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Persists the most recent scan result across server restarts.</summary>
public sealed class StatisticsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly string _path;
    private readonly ILogger<StatisticsStore> _logger;
    private readonly object _sync = new();

    /// <summary>Initializes a new instance of the <see cref="StatisticsStore"/> class.</summary>
    public StatisticsStore(IApplicationPaths applicationPaths, ILogger<StatisticsStore> logger)
    {
        _path = Path.Combine(applicationPaths.DataPath, "library-statistics-latest.json");
        _logger = logger;
    }

    /// <summary>Saves a complete scan result using an atomic replacement.</summary>
    public void Save(StatisticsResult result)
    {
        lock (_sync)
        {
            var temporaryPath = _path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(result, JsonOptions));
                File.Move(temporaryPath, _path, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogWarning(ex, "Library Statistics: could not save the latest result.");
            }
            finally
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    // Best-effort cleanup after a failed atomic replacement.
                }
            }
        }
    }

    /// <summary>Loads the most recently saved result.</summary>
    public StatisticsResult? Load()
    {
        lock (_sync)
        {
            try
            {
                return File.Exists(_path)
                    ? JsonSerializer.Deserialize<StatisticsResult>(File.ReadAllText(_path), JsonOptions)
                    : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogWarning(ex, "Library Statistics: could not load the latest result.");
                return null;
            }
        }
    }
}
