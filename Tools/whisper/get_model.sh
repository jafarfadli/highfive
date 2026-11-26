#!/bin/sh
# Downloads the Whisper model the game uses to hear the player say an answer out loud.
# ggml-base-q5_1 (about 57 MB, MIT licence, OpenAI Whisper weights converted by whisper.cpp).
set -e
cd "$(dirname "$0")/../.."
mkdir -p Assets/StreamingAssets/Whisper
curl -L --fail -o Assets/StreamingAssets/Whisper/ggml-base-q5_1.bin \
  https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base-q5_1.bin
echo "Saved Assets/StreamingAssets/Whisper/ggml-base-q5_1.bin"
