---
name: extract-video-screenshots
description: Extract PNG screenshots from an existing video at 10-second intervals into branch-scoped UITestsScreenshots paths. Use when a recording already exists; this skill does not record video.
---

# Extract Video Screenshots

Given a video path, extract one frame at each 10-second timestamp into:

```text
<repository-root>/UITestsScreenshots/<branch-name>/screen_<HH-MM-SS>.png
```

Use the video supplied by the calling workflow; do not record another video or regenerate screenshots/baseline files. Resolve the repository root from the current Git worktree unless the caller supplies one. Use the caller's branch name when provided, otherwise the current checked-out branch. If the worktree is detached and no branch name was supplied, stop and ask for one.

Run `scripts/extract_video_screenshots.sh <video-path> [branch-name] [repository-root]`. It samples 00:00:10, 00:00:20, and so on through the last 10-second mark not past the video duration. The filename timestamp uses hyphens so it is portable. Branch slashes are preserved as nested directories beneath `UITestsScreenshots/`.

The helper refuses to overwrite any screenshot already present for that branch. Do not delete or rename existing outputs to make a run succeed; report the collision and stop. It never writes to `screenshots/baseline*.png`.

After extraction, report the source video, its duration, the output directory, and the number of PNGs created. If the video is shorter than 10 seconds, report that no 10-second frames were available.
