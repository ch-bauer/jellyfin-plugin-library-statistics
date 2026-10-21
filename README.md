<div align="center">
  <img src="images/icon.png" alt="Library Statistics" width="128" />
  <h1>Library Statistics for Jellyfin (Proof of Concept)</h1>
  <p>Library storage, media-format statistics and merged-version duplicate analysis.</p>
</div>

> [!CAUTION]
> **This is a proof of concept, written with AI.** It is provided for testing, with no
> guarantee of support, bug fixes or troubleshooting. Test it before relying on it on a
> production server.

Library Statistics scans the media already indexed by Jellyfin and turns its stored
MediaInfo into a compact dashboard. It is read-only: it does not modify library items,
media files, watch history or merged versions.

The scope and dashboard are based on the library-analysis part of
[Jellyfin Helper](https://github.com/JellyPlugins/jellyfin-helper), without its cleanup,
recommendation, discovery, Arr/Seerr or web-client modification features.

## What it shows

- Jellyfin Helper-style category cards for movies, TV shows, music, other video and total
  storage.
- A storage-distribution bar that shows how much space each media category uses.
- A storage-based per-library breakdown for movies, TV shows, other video and audio.
- Per-library or whole-server donut charts for containers, resolutions, video codecs,
  audio codecs and dynamic range.
- A storage or media-file growth timeline reconstructed from Jellyfin's indexed dates.
- Scrollable Largest and Latest views for movies and aggregate series storage.
- Movies and series episodes that Jellyfin has grouped as multiple merged versions.
- The exact indexed path, container, size, runtime and video/audio/subtitle MediaInfo for
  every version.
- The latest completed scan after a Jellyfin restart.

## Merged-version duplicates

The duplicate view starts with Jellyfin's merged media sources, then also finds separate
Jellyfin items that share IMDb/TMDb/TVDb identity. When provider IDs are unavailable, it
uses a normalized movie title and year, or a series title plus season/episode coordinates.

A result is listed only when the matched items resolve to two or more distinct media paths.
Series are checked per episode and displayed under the series title. When Jellyfin returns
the same merged set through each underlying item, the plugin normalizes the paths so the
group and its statistics are counted only once.

Results can be filtered by media type, library, title, episode or path.

## Configuration

Open **Dashboard → Plugins → Library Statistics**.

- Use **Settings** to exclude libraries from scans.
- Select the circular scan button at the top to refresh the dashboard.
- Use **Duplicates** to compare merged and separately indexed duplicate versions.
- Use **Media formats** to switch the donut charts between the whole server and one
  library.

Exclusions are stored by Jellyfin virtual-folder ID, so renaming a library does not include
it again accidentally.

## Install from the plugin repository

Add this URL in **Dashboard → Plugins → Repositories**:

```text
https://raw.githubusercontent.com/ch-bauer/jellyfin-plugin-library-statistics/main/manifest.json
```

Install **Library Statistics** from the catalogue and restart Jellyfin.

## Manual install

Download the latest zip from
[Releases](https://github.com/ch-bauer/jellyfin-plugin-library-statistics/releases), extract
it into its own folder inside Jellyfin's `plugins` directory, and restart the server.

Common plugin directories are `/config/plugins/` in the official Docker image and
`%ProgramData%\Jellyfin\Server\plugins\` on Windows.

## Build

Requires the .NET 9 SDK.

```powershell
./build.ps1
```

The script runs the test suite and creates `dist/library-statistics_1.1.3.0.zip`.

GitHub Actions restores, builds, tests and retains an artifact for every push and pull
request. Pushing a matching `v*` tag builds the minimal Jellyfin zip, creates the GitHub
release, calculates its MD5 and updates `manifest.json` automatically.

## Compatibility

- Jellyfin **10.11.11**
- .NET 9

## License

GPL-3.0-or-later — see [LICENSE](LICENSE).
