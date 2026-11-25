# Baked voices (OmniVoice)

`bake_voices.py` renders every staff line, player answer and PA announcement piece found in
`Assets/_Project/Data/Procedures/*.json`, plus the Logbook's phrase cards (player voice) and key
words (announcer voice), to `Assets/_Project/Resources/Voice/<key>.ogg`.
The game plays these files first, so voices work in any build without a TTS engine or network.

## Setup (once)

```sh
mkdir -p ~/.cache/seoul-sim && cd ~/.cache/seoul-sim
uv venv --python 3.11 omnivoice-venv
VIRTUAL_ENV=~/.cache/seoul-sim/omnivoice-venv uv pip install torch==2.8.0 torchaudio==2.8.0 omnivoice soundfile librosa
```

The first run downloads the OmniVoice model (`k2-fsa/OmniVoice`, 0.6B) and Whisper small, a few GB
in all, into the Hugging Face cache. On an Apple Silicon Mac it runs on the GPU (MPS).

## Bake

```sh
TOKENIZERS_PARALLELISM=false ~/.cache/seoul-sim/omnivoice-venv/bin/python -I Tools/voice/bake_voices.py
```

- Only lines without a file are rendered; `--force` re-renders everything; `--only nurse,doctor`
  limits a run to some voices. Files for lines that no longer exist are removed.
- Each character speaks in the voice of its anchor clip, `anchors/<voice>.wav`. A voice without an
  anchor is designed from its description in `VOICES` (several takes, the one Whisper hears best is
  kept). Delete an anchor and re-bake that voice with `--force --only <voice>` to give a character
  a new voice.
- Every take is transcribed with Whisper; one that isn't heard as the line is rendered again (up to
  three takes, the best is kept). Lines still unclear are listed in `bake_report.txt`.
- About 9 s a line on an M-series Mac; a full bake (about 830 lines) takes roughly two hours.

## Licence

OmniVoice's code is Apache-2.0, but its model weights are CC-BY-NC 4.0 (non-commercial, because of
its training data). That is fine for the competition demo; a commercial release must re-bake the
voices with a model licensed for commercial use. The voices are designed from text descriptions,
not cloned from anyone.
