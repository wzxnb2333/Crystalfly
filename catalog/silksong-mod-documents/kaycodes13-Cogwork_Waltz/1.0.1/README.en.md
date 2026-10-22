# Cogwork Waltz

A Hollow Knight: Silksong mod which replaces the music in the Cogwork Dancers boss fight with [Cogwork Waltz by Joe Zeng](https://youtu.be/ZbU_AZupH_w), a rearrangement that puts the song in 3/8 time. Made with the author's permission.

Special thanks to [Fabulator_K](https://thunderstore.io/c/hollow-knight-silksong/p/Fabulator_K/) for recording the more natural-sounding rendition of the song which is used in the mod.

## Installation

Install via r2modman or Thunderstore Mod Manager. Cogwork Waltz' dependencies should be installed automatically.

For manual installation, extract the `.zip` file and place the resulting folder in the `BepInEx/plugins` directory for your Silksong install. You'll also have to manually install [WavLib](https://thunderstore.io/c/hollow-knight-silksong/p/SFGrenade/WavLib/).

## A Note on Gameplay

This mod, in addition to replacing the music, also implements a bug fix that stops the boss from desyncing from the music. It doesn't alter the intended movement speed in any way, but if there's any delay or game lag the boss will resume moving precisely on beat rather than being thrown off as it would in the vanilla game.

## Less Important Notes on Tempo and Timing

The rearrangement was made to be loopable and to (mostly) fit the tempo of the original music - which is very cool and is what spurred me to make this mod in the first place. However, there are some shenanigans that I wanted to write down for posterity.

The boss AI aims and moves on a specific pace. In each phase, movements are timed to be 1 second apart, 0.75 seconds, 0.75 seconds, and 1.25 seconds, respectively. The way the boss movement is coded means that the boss will always _slowly_ desync from the music, but that's the intention, at least.

But the intended boss tempo for each phase does NOT always match with the tempo of the music.

Phase 2 is where the mismatch is the messiest. It has the music at 75 bpm and the boss movement at 80 bpm. Zeng's rearrangement kept the original music's tempo for this part, and I didn't want to alter the boss AI; so instead I slightly sped up the rearrangement's phase 2 to match the boss.

Curiously in the original track the difference in phase 3's music tempo and boss tempo is offset from the beat in a rhythmically meaningful way. It's 120 bpm in the music and 80 bpm in boss movements. While this technically does mean the boss is not synced to the beat, it desyncs at a pace which creates a "4:3 polymeter" or "natural triplet feel" between the boss movements and the music's tempo. The rearrangement, however, was made to be synced with the boss instead.
