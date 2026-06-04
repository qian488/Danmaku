# Danmaku Mobile Build Size Notes

## Current High-Impact Assets

The largest source files in the current project are audio files under `Assets/Resources/Demos/Danmaku/Audio/BGM`:

- `home.ogg`: about 78 MB source file
- `game.ogg`: about 47 MB source file

Both BGM clips are kept as `Streaming` Vorbis assets for mobile and now use lower Unity import quality with background loading enabled. This reduces packaged audio contribution without changing loader code or replacing the source files.

## Conservative Settings Kept

- Danmaku SFX files already use Vorbis `CompressedInMemory` at quality `0.65`, so they were not blindly reprocessed.
- Major sprites already have Android platform overrides and compression enabled. They were not batch downscaled in this pass because character, boss, bullet, and UI readability need visual review in Unity before lowering max texture size.
- `Resources` remains in use to avoid changing the loading architecture during this mobile adaptation pass.

## Recommended Next Audit

After the next Android build, open Unity's Editor log and compare the asset contribution table before making further changes. Prioritize:

- BGM contribution after the new import settings.
- Any single texture over 2-3 MB in the build report.
- Unused plugin/example content that appears in the build.
- Whether moving non-startup resources out of `Resources` is worth the loading-code change.
