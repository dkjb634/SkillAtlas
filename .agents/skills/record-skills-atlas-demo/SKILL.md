---
name: record-skills-atlas-demo
description: Record a polished browser video of the Skills Atlas single- and multiple-repository search, filtering, expansion, and similar-skills workflow. Use when asked to capture this application's functionality as a demo video; do not use for screenshots or generic browser testing.
---

# Record the Skills Atlas demo

Create one video showing the prescribed workflow against the local web application. Treat the recording as the deliverable: keep it free of terminal windows, automation overlays, credentials, and unrelated browser chrome.

## Prepare

1. Work from the repository root containing `SkillsAtlas.Web/SkillsAtlas.Web.csproj`.
2. Confirm the .NET SDK and a browser automation facility that can save video are available. Prefer the environment's browser-control tool. If it cannot record video, use Playwright with a video-enabled browser context. Do not substitute screenshots or a trace for the video.
3. Create a temporary directory outside the repository for the SQLite database, process log, browser profile, and intermediate video. Use a clean database so previously saved skills do not affect the demonstration.
4. Start the app as a background process with:

   ```bash
   SKILLS_ATLAS_DATABASE=<temporary-directory>/skills.db \
   dotnet run --project SkillsAtlas.Web/SkillsAtlas.Web.csproj --no-launch-profile \
     --urls http://127.0.0.1:<available-port>
   ```

   Capture its output. Wait until the listening URL responds successfully before opening the browser. If startup fails, report the relevant log output and stop.
5. Use a desktop viewport of at least 1440×900 at normal zoom. Open the app at the listening URL and wait until its main heading and controls are ready.

## Record

Start video capture before the first meaningful interaction. Use accessible roles, labels, and visible text to find controls; use element IDs only as a fallback. Keep pointer movement deliberate, allow short pauses after visible state changes, and scroll the activated control or result into view when necessary.

Perform this sequence exactly:

1. In the initial single-repository search textbox, type `https://github.com/JetBrains/kotlin` and click **Search**.
2. Wait for the loading state to finish and for a non-empty result list to appear. Network-backed repository scans can take time; wait on the UI state rather than using a fixed timeout.
3. Open two different result rows, one at a time. After each click, wait until its full-description region is visible and pause briefly so the expanded content can be read.
4. Switch to **Multiple repositories**.
5. Add both of these repository URLs to its repository list, using the view's add action after each URL when required:

   - `https://github.com/JetBrains/intellij-community`
   - `https://github.com/JetBrains/compose-multiplatform`

   Before continuing, verify that both exact URLs are visibly present in the list box.
6. Click **Search** in the multiple-repository view. Wait until loading finishes and a non-empty set of results for the newly added repositories is visible.
7. Open two different new result rows, one at a time. For each, wait for and briefly show its full description.
8. Type `ana` into the skills filtering textbox. Verify the visible result count or rows shrink and every remaining visible skill name contains `ana`, case-insensitively.
9. Open **Similar skills**. Wait until its loading state finishes and either the similar-skill groups or the explicit empty-state result is visible.
10. Hold the final view unchanged for 5 seconds, then stop video capture.

Do not click links that navigate to GitHub. Do not accelerate or cut out loading states after capture; they demonstrate that the app is working.

## Verify and deliver

- Save the final recording in a stable location under `artifacts/recordings/` in the repository, creating the directories if needed. Use an informative UTC-stamped filename such as `skills-atlas-demo-YYYYMMDDTHHMMSSZ.webm`; use `.mp4` instead when that is the recorder's native reliable format.
- Verify that the file exists, has non-zero size, is playable, and has a video stream. Confirm its duration includes the 5-second final hold.
- If an interaction fails, stop capture, retain the failed recording outside `artifacts/recordings/` for diagnosis, and report the exact missing control or unmet UI condition. Do not present a partial recording as complete.
- Stop the app process that this run started. Remove temporary profiles, database, logs, and incomplete recordings, but never remove pre-existing files or unrelated processes.
- Report the final video path, container format, duration, dimensions, and file size.
