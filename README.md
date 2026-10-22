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

- Jellyfin Helper-style category cards for movies, TV shows, music, trickplay, subtitles
  and all measured files.
- A storage-distribution bar and per-library table for video, audio, subtitles, images,
  trickplay, metadata and other sidecar files.
- Per-library or whole-server donut charts for containers, detailed resolution tiers,
  video codecs, video and music audio codecs, bitrate tiers, dynamic range, and audio and
  subtitle languages.
- Expandable format rows that reveal the exact matching file paths.
- A cumulative storage and media-file timeline built from Jellyfin's indexed media dates.
- Scrollable Largest and Latest views for movies and aggregate series storage.
- Movies and series episodes that Jellyfin has grouped as multiple merged versions.
- The exact indexed path, container, size, runtime and video/audio/subtitle MediaInfo for
  every version.
- The latest completed scan after a Jellyfin restart.
- Fast background scans with visible phase and percentage progress. The normal scan uses
  Jellyfin's index; a separate full storage scan also measures images, metadata,
  subtitles and trickplay.
- Persistent duplicate review states. Any result can be marked allowed, and an optional
  rule automatically allows exactly one 4K plus one 1080p version.
- Series-level expandable duplicate groups and a separate view for ambiguous multipart
  episodes.

## Merged-version duplicates

The duplicate view starts with Jellyfin's merged media sources, then also finds separate
Jellyfin items using metadata detected by Jellyfin. Movies match by IMDb/TMDb/TVDb ID or,
when those IDs are absent, by title and year. Episodes require the series identity,
Jellyfin's parent-season number, episode number and episode title. When either coordinate
is missing, the item is eligible only through Jellyfin's own merged media sources and is
not cross-matched to a separate item.

File and folder names are never duplicate criteria. Paths are used only to verify that a
metadata match represents distinct physical versions and to show where those versions
are stored.

A result is listed only when the matched items resolve to two or more distinct media paths.
Series are checked per episode and displayed under the series title. When Jellyfin returns
the same merged set through each underlying item, the plugin normalizes the paths so the
group and its statistics are counted only once.

Results can be filtered by media type, library, title, episode or path.
Enable **Only Jellyfin merged versions** to exclude the additional provider-ID and
title-based duplicate matches.

Review decisions are stored in an atomically replaced state database with automatic
backup recovery. A decision applies only to the exact version set that was reviewed;
adding or replacing a version returns the group to review. Paths identify the version
set but are never used to decide whether two items are duplicates.

## Configuration

Open **Dashboard → Plugins → Library Statistics**.

- Use **Settings** to exclude libraries from scans.
- Select the circular scan button at the top to refresh the dashboard.
- Use **Run full storage scan** in Settings when sidecar and trickplay totals also need to
  be refreshed.
- Use **Duplicates** to compare merged and separately indexed duplicate versions, toggle
  the exact 4K + 1080p allowance, and show or hide synchronized MediaInfo panels.
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

The script runs the test suite and creates `dist/library-statistics_1.3.0.3.zip`.

GitHub Actions restores, builds, tests and retains an artifact for every push and pull
request. Pushing a matching `v*` tag builds the minimal Jellyfin zip, creates the GitHub
release, calculates its MD5 and updates `manifest.json` automatically.

## Compatibility

- Jellyfin **10.11.11**
- .NET 9

## License

GPL-3.0-or-later — see [LICENSE](LICENSE).
