#!/usr/bin/env bash
set -euo pipefail

usage() {
  printf 'Usage: %s <video-path> [branch-name] [repository-root]\n' "$0" >&2
}

if [[ $# -lt 1 || $# -gt 3 ]]; then
  usage
  exit 2
fi

video_arg=$1
branch_name=${2:-}
repo_hint=${3:-$PWD}

if [[ "$video_arg" = /* ]]; then
  video_path=$video_arg
else
  video_path="$PWD/$video_arg"
fi

if [[ ! -f "$video_path" ]]; then
  printf 'Video file not found: %s\n' "$video_path" >&2
  exit 1
fi

for tool_name in ffmpeg ffprobe git awk; do
  if ! command -v "$tool_name" >/dev/null 2>&1; then
    printf 'Required command is unavailable: %s\n' "$tool_name" >&2
    exit 1
  fi
done

if ! repo_root=$(git -C "$repo_hint" rev-parse --show-toplevel 2>/dev/null); then
  printf 'Not inside a Git worktree: %s\n' "$repo_hint" >&2
  exit 1
fi

if [[ -z "$branch_name" ]]; then
  branch_name=$(git -C "$repo_root" branch --show-current)
fi
if [[ -z "$branch_name" ]]; then
  printf 'No branch name was supplied and the worktree is detached.\n' >&2
  exit 1
fi
if ! git -C "$repo_root" check-ref-format --branch "$branch_name" >/dev/null 2>&1; then
  printf 'Invalid Git branch name: %s\n' "$branch_name" >&2
  exit 1
fi

if ! video_stream=$(ffprobe -v error -select_streams v:0 -show_entries stream=codec_type -of csv=p=0 "$video_path") || [[ -z "$video_stream" ]]; then
  printf 'No readable video stream found in: %s\n' "$video_path" >&2
  exit 1
fi
if ! duration=$(ffprobe -v error -show_entries format=duration -of default=nokey=1:noprint_wrappers=1 "$video_path") || [[ ! "$duration" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
  printf 'Could not determine the video duration: %s\n' "$video_path" >&2
  exit 1
fi

duration_seconds=$(awk -v value="$duration" 'BEGIN { print int(value) }')
branch_dir="$repo_root/UITestsScreenshots/$branch_name"
timestamps=()
filenames=()

for ((second = 10; second <= duration_seconds; second += 10)); do
  timecode=$(printf '%02d-%02d-%02d' "$((second / 3600))" "$(((second / 60) % 60))" "$((second % 60))")
  filename="screen_${timecode}.png"
  if [[ -e "$branch_dir/$filename" ]]; then
    printf 'Refusing to overwrite existing screenshot: %s\n' "$branch_dir/$filename" >&2
    exit 1
  fi
  timestamps+=("$second")
  filenames+=("$filename")
done

if [[ ${#timestamps[@]} -eq 0 ]]; then
  printf 'Video duration is %ss; no 10-second frames are available.\n' "$duration"
  exit 0
fi

stage_dir=$(mktemp -d "${TMPDIR:-/tmp}/extract-video-screenshots.XXXXXX")
cleanup() {
  rm -rf "$stage_dir"
}
trap cleanup EXIT

for index in "${!timestamps[@]}"; do
  second=${timestamps[$index]}
  filename=${filenames[$index]}
  ffmpeg -hide_banner -loglevel error -nostdin -ss "$second" -i "$video_path" \
    -map 0:v:0 -frames:v 1 -update 1 "$stage_dir/$filename"
done

mkdir -p "$branch_dir"
for filename in "${filenames[@]}"; do
  if [[ -e "$branch_dir/$filename" ]]; then
    printf 'Refusing to overwrite existing screenshot: %s\n' "$branch_dir/$filename" >&2
    exit 1
  fi
done
for filename in "${filenames[@]}"; do
  mv "$stage_dir/$filename" "$branch_dir/$filename"
done

printf 'Extracted %d screenshots from %s (%ss) into %s\n' \
  "${#filenames[@]}" "$video_path" "$duration" "$branch_dir"
