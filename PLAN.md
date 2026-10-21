# Library Statistics plan

## Goal

Create a small Jellyfin 10.11.11 plugin that keeps only the useful, read-only library-analysis idea from Jellyfin Helper.

## Version 1.1 scope

- Admin-only Overview, Duplicates, Media formats, and Settings page.
- Manual scans of Jellyfin-indexed video and audio items.
- Stable library-ID exclusions, including mixed and music libraries.
- Overall and per-library storage and media counts.
- Container, resolution, video codec, audio codec, and dynamic-range breakdowns.
- Duplicate filtering based on Jellyfin's own merged movie and episode versions.
- Exact paths and per-version video, audio, and subtitle MediaInfo.
- Latest scan persisted across Jellyfin restarts.
- No scheduled jobs, filesystem cleanup, recommendations, watch history, Arr/Seerr, or web-client injection.

## Safety and performance

- Read-only: the plugin never changes library items or media files.
- One scan at a time; a second request receives HTTP 409.
- Uses Jellyfin's indexed items and media-stream metadata instead of probing media contents.
- File access is limited to measuring the primary indexed media path.
- Admin authorization protects scan results and settings endpoints.

## Follow-up candidates

- Scheduled scans and a growth timeline.
- CSV/JSON export.
- Per-library codec breakdown filtering.
- Largest-file drill-down.
- Localization.
