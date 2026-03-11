NEON SHIFT — MUSIC FILES
========================

Place your audio files here with these exact filenames:

  Easy.mp3      (or .ogg / .wav) — plays during Easy mode
  Medium.mp3    — plays during Medium mode  
  Hard.mp3      — plays during Hard mode
  Endless.mp3   — plays during Endless mode
  Menu.mp3      — plays on the Main Menu (optional)

Unity supported formats: .mp3, .ogg, .wav, .aiff

The game loads them automatically via Resources.Load<AudioClip>("Music/<ModeName>").
No code changes needed — just drop the files here and Unity will pick them up.

Volume is set to 0.7 in-game and 0.55 on the menu.
