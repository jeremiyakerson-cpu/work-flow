#!/usr/bin/env bash
# Renders the 4K video and adds a synthesized ambient soundtrack (needs ffmpeg).
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p out
D=58
ffmpeg -loglevel error -y \
  -f lavfi -i "sine=f=110:d=$D" -f lavfi -i "sine=f=164.81:d=$D" -f lavfi -i "sine=f=220:d=$D" \
  -f lavfi -i "sine=f=261.63:d=$D" -f lavfi -i "sine=f=329.63:d=$D" -f lavfi -i "sine=f=493.88:d=$D" \
  -filter_complex "[0]volume=0.9,tremolo=f=0.11:d=0.5[a];[1]volume=0.6,tremolo=f=0.17:d=0.7[b];[2]volume=0.5,tremolo=f=0.13:d=0.6[c];[3]volume=0.4,tremolo=f=0.23:d=0.8[d];[4]volume=0.3,tremolo=f=0.19:d=0.8[e];[5]volume=0.12,tremolo=f=0.29:d=0.9[f];[a][b][c][d][e][f]amix=inputs=6:normalize=0,lowpass=f=1800,aecho=0.8:0.7:420|780:0.35|0.25,volume=2.5,afade=t=in:d=2.5,afade=t=out:st=53.5:d=3.5,aformat=channel_layouts=stereo" \
  -ar 48000 out/ambient.wav
# Authored at 1920x1080; --scale=2 outputs 3840x2160.
npx remotion render FableCapabilities out/silent.mp4 --scale=2 --codec=h264 --crf=16 --image-format=jpeg --jpeg-quality=95
ffmpeg -loglevel error -y -i out/silent.mp4 -i out/ambient.wav -c:v copy -c:a aac -b:a 256k -shortest \
  -movflags +faststart out/fable-5-2-capabilities-4k.mp4
rm out/silent.mp4
echo "Wrote out/fable-5-2-capabilities-4k.mp4"
