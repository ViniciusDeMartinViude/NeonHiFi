# NeonHiFi

A Windows desktop app that turns your PC into an 80s-style hi-fi stereo system — VU meters, a graphic equalizer, a spectrum "screen", and a bank of chunky retro buttons — while adding a few things the original hardware never had: a live now-playing display (artist, album art) pulled from Spotify, and real digital signal processing on whatever audio your machine is playing.

> Status: **early development** — architecture and backlog defined, implementation not yet started. See [Issues](../../issues) and [Milestones](../../milestones) for the current plan.

## What it does

NeonHiFi doesn't play music itself. Instead, it sits alongside whatever *is* playing audio on your PC (Spotify, a browser, anything) and:

1. **Captures** the system's audio output via WASAPI loopback.
2. **Processes** it through a configurable graphic EQ and optional retro-flavored effects (bass boost, stereo width, warmth/saturation).
3. **Renders** VU meters and a spectrum analyzer reacting to that live signal, on a faux-CRT "screen".
4. **Outputs** the processed signal to your headphones.
5. **Displays** the currently playing track's title, artist, and album art, sourced from the Spotify Web API.

This split exists because Spotify (like all major streaming services) doesn't expose raw decoded audio through its public API — only metadata and remote-control commands. NeonHiFi works around that the same way a real hi-fi would: it doesn't know what track is playing, it just reacts to the signal, with Spotify's API layered on top purely for the "now playing" display.

## Features (planned)

- [ ] Real-time WASAPI loopback audio capture
- [ ] Multi-band graphic equalizer (biquad filter bank)
- [ ] VU meters with realistic attack/decay ballistics
- [ ] Spectrum analyzer (FFT-driven)
- [ ] Retro skeuomorphic UI (brushed metal/wood panel look, chunky buttons, faders)
- [ ] Spotify "now playing" panel (track/artist/album art) via Spotify Web API
- [ ] EQ presets (save/load)
- [ ] Standalone Windows installer

## Tech stack

- **C# / .NET** (WPF for UI)
- **[NAudio](https://github.com/naudio/NAudio)** for WASAPI capture/output and DSP
- **[SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET)** for Spotify Web API access
- Windows-only (WASAPI is a Windows audio API)

## Getting started

Build/run instructions will be added once the initial project scaffolding (see Phase 0 in the [backlog](../../milestones)) is in place.

You will need a [Spotify Developer](https://developer.spotify.com/dashboard) app (Client ID) to use the now-playing feature — see [CLAUDE.md](CLAUDE.md) for how credentials are configured once that lands.

## Development

This project is developed with the help of [Claude Code](https://claude.com/claude-code). See [CLAUDE.md](CLAUDE.md) for architecture notes, conventions, and how the backlog (GitHub Issues + Milestones) is organized.

## License

[MIT](LICENSE)
