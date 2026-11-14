"""Bake the mod's culture voices into Pocket TTS voice-state files.

Each voice under module\\Voices\\<sex>\\<culture>\\<id>\\ carries a short English
reference clip (breeze-reference.wav). Pocket TTS clones from a clip, but only
with weights Kyutai gate behind a Hugging Face sign-in; what the clone produces
is a speaker state that the ungated, no-cloning model can load and speak with.
So the cloning happens once, here, and players never need the gated weights.

Writes <out>\\<sex>\\<culture>\\<id>\\pocket.safetensors (the same layout as
module\\Voices) plus <out>\\manifest.json naming the weights revision, the
pocket-tts version, the dtype, the trim and each file's sha256. It never writes
into module\\Voices or a deployed Modules folder, and never touches voice.json.

Run it with the Python the voice app (claude-voice) uses -- the one with
pocket-tts installed -- after `hf auth login` and accepting the terms at
https://huggingface.co/kyutai/pocket-tts:

    python tools\\bake_pocket_voices.py --out ..\\pocket-voices-pack --fp16 --max-seconds 8
    python tools\\bake_pocket_voices.py --out D:\\tmp\\pv --only empire_male_gruff nord_female_warm

fp16 halves the files. pocket-tts keeps a loaded state's dtype, so whoever
loads an fp16 file must upcast its floating tensors to float32 before
generating (int64 offsets stay as they are).
"""

import argparse
import datetime
import hashlib
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
VOICES = os.path.join(REPO, "module", "Voices")
CLIP = "breeze-reference.wav"
STATE = "pocket.safetensors"
LANGUAGE = "english"        # the clips are English, and so is the voice app's default model


def find_voices(root):
    """(sex, culture, id, dir) for every voice folder that has a reference clip."""
    found = []
    for sex in sorted(os.listdir(root)):
        sdir = os.path.join(root, sex)
        if not os.path.isdir(sdir):
            continue
        for culture in sorted(os.listdir(sdir)):
            cdir = os.path.join(sdir, culture)
            if not os.path.isdir(cdir):
                continue
            for vid in sorted(os.listdir(cdir)):
                vdir = os.path.join(cdir, vid)
                if os.path.isfile(os.path.join(vdir, CLIP)):
                    found.append((sex, culture, vid, vdir))
    return found


def refuse_bad_out(out):
    """The pack lives outside the mod: not in module\\Voices, not in the game."""
    o = os.path.normcase(os.path.abspath(out))
    v = os.path.normcase(os.path.abspath(VOICES))
    if o == v or o.startswith(v + os.sep) or v.startswith(o + os.sep):
        raise SystemExit(f"--out must not overlap module\\Voices: {out}")
    parts = [p.lower() for p in o.split(os.sep)]
    if "modules" in parts and any("bannerlord" in p for p in parts):
        raise SystemExit(f"--out must not be the game's Modules folder: {out}")


def read_clip(path):
    """Mono float32 tensor [1, T] and its rate."""
    from pocket_tts.data.audio import audio_read

    return audio_read(path)


def trim_quiet(audio, rate, max_seconds):
    """Cut to at most max_seconds, at the quietest 20 ms in the last 1.5 s before it.

    A cut mid-syllable leaves the clone a half-word to continue from; a cut in a
    breath does not. Falls back to the hard limit when nothing is quiet.
    """
    import torch

    limit = int(max_seconds * rate)
    if audio.shape[-1] <= limit:
        return audio, audio.shape[-1] / rate
    hop = int(0.02 * rate)
    lo = max(0, limit - int(1.5 * rate))
    seg = audio[0, lo:limit]
    n = seg.shape[-1] // hop
    if n < 2:
        return audio[..., :limit], limit / rate
    rms = seg[: n * hop].reshape(n, hop).pow(2).mean(dim=1).sqrt()
    best = int(torch.argmin(rms))
    cut = lo + best * hop + hop // 2
    return audio[..., :cut], cut / rate


def to_dtype(state, half):
    """Floating tensors to fp16 when asked; integer offsets untouched."""
    if not half:
        return state
    return {m: {k: (t.half() if t.is_floating_point() else t) for k, t in d.items()}
            for m, d in state.items()}


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for block in iter(lambda: fh.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def weights_source(language):
    """The pinned hf:// path the package config loads cloning weights from."""
    import yaml
    from pocket_tts.utils.config import CONFIGS_DIR

    with open(CONFIGS_DIR / f"{language}.yaml", encoding="utf-8") as fh:
        cfg = yaml.safe_load(fh)
    src = cfg.get("weights_path", "")
    repo_path, _, rev = src.partition("@")
    return src, repo_path, rev


def load_model(language=LANGUAGE):
    from pocket_tts import TTSModel

    model = TTSModel.load_model(language=language)
    # The package falls back to the no-cloning weights without a word when the
    # gated ones cannot be had; baking against those would fail per voice.
    if not getattr(model, "has_voice_cloning", False):
        raise SystemExit(
            "pocket-tts loaded the no-cloning weights. Accept the terms at "
            "https://huggingface.co/kyutai/pocket-tts and run `hf auth login`.")
    return model


def bake_one(model, clip, dest, half, max_seconds):
    """Clone from clip, write dest atomically. Returns (seconds used, bytes)."""
    import torch
    from pocket_tts import export_model_state
    from pocket_tts.data.audio_utils import convert_audio

    audio, rate = read_clip(clip)
    used = audio.shape[-1] / rate
    if max_seconds:
        audio, used = trim_quiet(audio, rate, max_seconds)
    audio = convert_audio(audio, rate, model.config.mimi.sample_rate, 1)
    with torch.no_grad():
        state = model.get_state_for_audio_prompt(audio)
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    tmp = dest + ".tmp"
    export_model_state(to_dtype(state, half), tmp)
    os.replace(tmp, dest)
    return used, os.path.getsize(dest)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.strip().splitlines()[0])
    ap.add_argument("--out", required=True, help="pack folder (outside the mod)")
    ap.add_argument("--only", nargs="+", metavar="ID", help="just these voice ids")
    ap.add_argument("--fp16", action="store_true", help="store floating tensors as float16")
    ap.add_argument("--max-seconds", type=float, default=0,
                    help="trim each clip to at most N seconds, at a quiet point")
    ap.add_argument("--voices", default=VOICES, help="voice tree to read (default: module\\Voices)")
    args = ap.parse_args(argv)

    refuse_bad_out(args.out)
    voices = find_voices(args.voices)
    if args.only:
        want = {v.lower() for v in args.only}
        voices = [v for v in voices if v[2].lower() in want]
        missing = want - {v[2].lower() for v in voices}
        if missing:
            raise SystemExit("no such voice: " + ", ".join(sorted(missing)))
    if not voices:
        raise SystemExit(f"no voices with {CLIP} under {args.voices}")

    from importlib.metadata import version

    src, repo_path, rev = weights_source(LANGUAGE)
    print(f"loading pocket-tts {version('pocket-tts')} ({LANGUAGE}, {src}) ...", flush=True)
    model = load_model()

    os.makedirs(args.out, exist_ok=True)
    rows, failures = [], []
    began = time.monotonic()
    for i, (sex, culture, vid, vdir) in enumerate(voices, 1):
        rel = os.path.join(sex, culture, vid, STATE)
        dest = os.path.join(args.out, rel)
        try:
            used, size = bake_one(model, os.path.join(vdir, CLIP), dest,
                                  args.fp16, args.max_seconds)
        except Exception as exc:
            failures.append({"id": vid, "error": f"{type(exc).__name__}: {exc}"})
            print(f"[{i}/{len(voices)}] {vid}: FAILED {exc}", flush=True)
            continue
        rows.append({"id": vid, "sex": sex, "culture": culture,
                     "file": rel.replace(os.sep, "/"), "bytes": size,
                     "sha256": sha256(dest), "referenceSeconds": round(used, 3)})
        print(f"[{i}/{len(voices)}] {vid}: {used:.1f}s -> {size // 1024} KB", flush=True)

    manifest = {
        "weights": {"repo": repo_path.removeprefix("hf://").split("/languages/")[0],
                    "path": repo_path, "revision": rev, "language": LANGUAGE},
        "pocketTts": version("pocket-tts"),
        "dtype": "float16" if args.fp16 else "float32",
        "maxSeconds": args.max_seconds or None,
        "loadNote": "float16 files must have floating tensors upcast to float32 before generating",
        "createdUtc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "count": len(rows),
        "totalBytes": sum(r["bytes"] for r in rows),
        "voices": rows,
        "failures": failures,
    }
    # --only runs merge into an existing manifest rather than shrinking it.
    mpath = os.path.join(args.out, "manifest.json")
    if args.only and os.path.exists(mpath):
        with open(mpath, encoding="utf-8") as fh:
            old = json.load(fh)
        same = (old.get("dtype"), old.get("maxSeconds"), old.get("weights", {}).get("revision")) == \
               (manifest["dtype"], manifest["maxSeconds"], rev)
        if same:
            # A row whose voice folder is gone (a renamed voice) goes too, file and all,
            # so the pack never carries a state for a voice the mod no longer has.
            done = {r["id"] for r in rows}
            live = {f"{s}/{c}/{v}/{STATE}" for s, c, v, _ in find_voices(args.voices)}
            kept = []
            for r in old.get("voices", []):
                if r["id"] in done:
                    continue
                if r["file"] not in live:
                    stale = os.path.join(args.out, *r["file"].split("/"))
                    if os.path.exists(stale):
                        os.remove(stale)
                        try:
                            os.rmdir(os.path.dirname(stale))
                        except OSError:
                            pass
                    print(f"dropped {r['id']}: no longer in {args.voices}", flush=True)
                    continue
                kept.append(r)
            rows = kept + rows
            rows.sort(key=lambda r: r["file"])
            manifest.update(voices=rows, count=len(rows), totalBytes=sum(r["bytes"] for r in rows))
    with open(mpath, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2)
        fh.write("\n")
    print(f"{len(rows)} baked, {len(failures)} failed, "
          f"{manifest['totalBytes'] / 1e6:.1f} MB, {time.monotonic() - began:.0f}s -> {args.out}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
