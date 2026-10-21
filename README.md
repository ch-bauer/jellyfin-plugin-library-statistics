# Jellyfin Library Statistics

A focused, read-only statistics plugin for **Jellyfin 10.11.11**. It is inspired by the library-analysis portion of [Jellyfin Helper](https://github.com/JellyPlugins/jellyfin-helper), without its cleanup, recommendation, discovery, Arr/Seerr, or web-client modification features.

## What it shows

- Total measured storage and media-file count.
- Per-library storage, movies, episodes, other videos, and audio tracks.
- Container, resolution, video codec, audio codec, and dynamic-range breakdowns.
- The last completed scan after a server restart.

The dashboard follows Jellyfin Helper's Overview / Codecs / Settings organization, reduced to the three screens this plugin needs.

## Library exclusions

Open **Dashboard → Plugins → Library Statistics → Settings**. Check every library that should be excluded, save, and run a new scan. Exclusions are stored by Jellyfin virtual-folder ID, so renaming a library does not silently include it again.

## Build

Requirements: .NET 9 SDK.

```powershell
./build.ps1
```

The script runs the tests and creates `dist/library-statistics_1.0.0.0.zip`.

## Manual installation

1. Build the plugin or extract the release zip.
2. Copy its files into `LibraryStatistics_1.0.0.0/` inside Jellyfin's plugins directory.
3. Restart Jellyfin.
4. Open Library Statistics from the dashboard/sidebar, choose exclusions, and select **Scan libraries**.

## How scanning works

The plugin queries Jellyfin's indexed video and audio items beneath each included virtual folder. It reads stored media-stream metadata for codecs, dimensions, and dynamic range, and reads the primary indexed file's length for storage totals. Files that are offline or unavailable remain counted but contribute zero measured bytes.

Audio-codec and other stream-based grouped sizes are attributed per media file. A file with more than one distinct audio codec contributes its full size to each codec, so those grouped sizes are useful for comparison but may exceed physical storage when added together.

## Compatibility

- Jellyfin 10.11.11
- .NET 9

## Origin and license

The product scope and dashboard organization are based on Jellyfin Helper by JellyPlugins. This project is distributed under GPL-3.0-or-later; see `LICENSE`.
