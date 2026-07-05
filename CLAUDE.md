# CLAUDE.md

Guidance for Claude Code (or any agent) working in this repository.

## Project overview

NeonHiFi is a Windows desktop app that looks like an 80s hi-fi stereo system (VU meters, graphic EQ, spectrum "screen", chunky retro buttons) and adds two modern features:

1. Real DSP (EQ + effects) applied to whatever audio the PC is currently outputting, captured via WASAPI loopback.
2. A "now playing" panel (track/artist/album art) sourced from the Spotify Web API.

**Important constraint driving the architecture:** Spotify's public API does not expose raw/decoded audio — only metadata and remote-control commands. This app never decodes Spotify streams itself. It captures whatever the OS is already outputting (loopback capture) and treats Spotify purely as a metadata source. Do not design features that assume direct access to Spotify's audio stream.

## Tech stack

- **C# / .NET** (latest LTS), **WPF** for the UI
- **NAudio** for WASAPI loopback capture, WASAPI output, and low-level DSP building blocks
- **SpotifyAPI-NET** for OAuth + Web API calls (currently-playing polling, album art URLs)
- Windows-only by design (WASAPI). Do not add cross-platform abstraction layers unless explicitly asked — YAGNI here.

## Project structure

```
NeonHiFi.sln
src/
  NeonHiFi.App/       WPF UI: windows, custom controls (VU meter, EQ fader, LCD panel), view models
  NeonHiFi.Audio/      Capture, DSP (biquad EQ, FFT analyzer), output pipeline — no UI references
  NeonHiFi.Spotify/    OAuth flow, API client wrapper, now-playing polling, album art caching
tests/
  NeonHiFi.Audio.Tests/  Unit tests for DSP math (filter coefficients, FFT correctness) — this is the part that's actually unit-testable
docs/
  (architecture notes, if they grow beyond this file)
```

Keep `NeonHiFi.Audio` free of WPF/UI dependencies — it should be testable headless. The UI layer subscribes to events/observables it exposes.

## Code style

Formatting and naming conventions (4-space indent, file-scoped namespaces, `_camelCase` private fields, `PascalCase` members, `IInterface` naming, etc.) are codified in [`.editorconfig`](.editorconfig) at the repo root. Run `dotnet format` before committing to apply/verify them — CI does not currently enforce this separately, so it's on the honor system for now.

## Real-time audio conventions

- **Never block the audio callback thread.** WASAPI capture/output callbacks must return quickly — no UI calls, no logging to disk, no `await` on I/O inside them. Marshal data to the UI thread via a lock-free ring buffer or a `Dispatcher.BeginInvoke`, not the other way around.
- VU/spectrum rendering should run at a UI-friendly rate (~30-60fps), decoupled from the audio thread's block size.
- Prefer `double`/`float` math consistent with NAudio's `ISampleProvider` (float samples) throughout the DSP chain to avoid unnecessary conversions.

## Secrets

Spotify Client ID/Secret must never be committed. Use a local `appsettings.Development.json` or user secrets (`dotnet user-secrets`), both gitignored. If you add a settings file, add a corresponding `.example` template with placeholder values.

## Backlog & workflow

The backlog lives in **GitHub Issues**, grouped into **Milestones** (one per development phase: Phase 0 Setup, Phase 1 Audio Engine, Phase 2 Visualization, Phase 3 Retro UI, Phase 4 Spotify Integration, Phase 5 Polish/Packaging) and tagged with `epic:*` labels.

When picking up work:
1. `gh issue list --milestone "Phase N: ..."` to see what's next in the current phase — work roughly in phase order, since later phases depend on earlier ones (e.g., visualization needs the audio engine's data first).
2. Read the full issue body before starting — each one includes context and acceptance criteria, not just a title.
3. Reference the issue number in commit messages (e.g., `Add WASAPI loopback capture service (#6)`).
4. Don't bundle multiple unrelated issues into one commit/PR.
5. If an issue's acceptance criteria turn out to be wrong or incomplete once you're in the code, update the issue rather than silently deviating.

## Build & test

Solution scaffolding is in place (.NET 10 SDK, `net10.0-windows` for the WPF app, `net10.0` for the libraries/tests). From the repo root:

```
dotnet build                                  # builds NeonHiFi.sln (App, Audio, Spotify, Audio.Tests)
dotnet test                                   # runs NeonHiFi.Audio.Tests
dotnet run --project src/NeonHiFi.App         # launches the WPF app
```

`NeonHiFi.App` references `NeonHiFi.Audio` and `NeonHiFi.Spotify`. `NeonHiFi.Audio.Tests` references `NeonHiFi.Audio` only. `NeonHiFi.Audio` and `NeonHiFi.Spotify` have no WPF/UI dependencies — keep it that way.

## Verifying changes

Since this is a real-time audio + WPF UI app, type-checking and unit tests only verify the DSP math, not the actual listening/visual experience. When a change affects audio processing or the UI, run the app and manually confirm: audio plays through headphones correctly, EQ changes are audible, VU/spectrum react to real audio, and the Spotify panel updates. State plainly if a change couldn't be manually verified (e.g., no headphones/Spotify account available in the current environment).
