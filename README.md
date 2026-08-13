# RoundEndSounds Fixed

A maintained/fixed fork of [Stimayk/RoundEndSounds].

## Changes

- Fixed localization keys being displayed as raw text
- Added Turkish localization
- Removed hardcoded Russian UI strings
- Improved localization fallback behavior
- Updated build output for language files
- Fixed per-player volume by updating the active SoundEvent's `public.volume` parameter
- Volume levels now apply to the exact SoundEvent GUID sent to each player
- Kept direct `sounds/...` playback for compatibility (CS2 does not expose per-play volume for raw paths)

## Important volume setup

Use a SoundEvent name (for example `dp_1.1`) in each `Sounds.*.Sound` value if you want the `!res` volume control to work. The SoundEvent must be defined in a compiled and mounted `.vsndevts` addon and listed in `SoundEventFiles`.

Raw values beginning with `sounds/` are still playable, but CS2's `play` command has no server-controlled per-play volume. `snd_toolvolume` is a tools-only client setting and is intentionally no longer modified. Replace raw paths with their SoundEvent aliases for adjustable volume.

The raw path from the plugin's original Verkkars example is migrated to its built-in CS2 SoundEvent automatically, including in an existing config.

## Credits

Original plugin by Stimayk:
https://github.com/Stimayk/RoundEndSounds

This repository contains modifications/fixes made to the original project.
