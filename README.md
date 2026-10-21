# Jellyfin Library Statistics

A focused, read-only statistics plugin for **Jellyfin 10.11.11**. It is inspired by the library-analysis portion of [Jellyfin Helper](https://github.com/JellyPlugins/jellyfin-helper), without its cleanup, recommendation, discovery, Arr/Seerr, or web-client modification features.

## What it shows

- Total measured storage and media-file count.
- Per-library storage, movies, episodes, other videos, and audio tracks.
- Container, resolution, video codec, audio codec, and dynamic-range breakdowns.
- Movies and series episodes that Jellyfin has already grouped as multiple merged versions.
- The exact path and MediaInfo streams for every version in a duplicate group.
- The last completed scan after a server restart.

The dashboard closely follows Jellyfin Helper's compact layout, with Overview, Duplicates, Media formats, and Settings screens.

## Duplicate finder

The duplicate finder uses Jellyfin's merged media sources as its source of truth. A movie is listed when its Jellyfin item has two or more distinct media versions. For series, the same check is applied to each episode and the result is displayed under its series title. It does not infer duplicates from similar names, years, or filenames.

Results can be filtered by movie or series, library, title, episode, or path. Each version shows its indexed filesystem path, container, size, runtime, and video, audio, and subtitle stream details already known to Jellyfin.

## Library exclusions

Open **Dashboard → Plugins → Library Statistics → Settings**. Check every library that should be excluded, save, and run a new scan. Exclusions are stored by Jellyfin virtual-folder ID, so renaming a library does not silently include it again.

## Build

Requirements: .NET 9 SDK.

```powershell
./build.ps1
```

The script runs the tests and creates `dist/library-statistics_1.1.0.0.zip`.

## Manual installation

1. Build the plugin or extract the release zip.
2. Copy its files into `LibraryStatistics_1.1.0.0/` inside Jellyfin's plugins directory.
3. Restart Jellyfin.
4. Open Library Statistics from the dashboard/sidebar, choose exclusions, and select **Scan libraries**.

## How scanning works

The plugin queries Jellyfin's indexed video and audio items beneath each included virtual folder. It reads Jellyfin's static media sources and stored stream metadata for codecs, dimensions, dynamic range, merged versions, and storage totals. Files that are offline or unavailable remain counted but contribute zero measured bytes.

Audio-codec and other stream-based grouped sizes are attributed per media file. A file with more than one distinct audio codec contributes its full size to each codec, so those grouped sizes are useful for comparison but may exceed physical storage when added together.

## Compatibility

- Jellyfin 10.11.11
- .NET 9

## Origin and license

The product scope and dashboard organization are based on Jellyfin Helper by JellyPlugins. This project is distributed under GPL-3.0-or-later; see `LICENSE`.
