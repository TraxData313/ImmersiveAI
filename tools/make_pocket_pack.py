"""Zip the baked Pocket voices into a voice pack and write module\\Voices\\pocket-pack.json.

bake_pocket_voices.py leaves <pack>\\<sex>\\<culture>\\<id>\\pocket.safetensors and a
manifest.json. This turns that folder into the one zip claude-voice downloads
(its docs\\voice-packs.md is the contract) and writes the small description the
mod ships at the top of module\\Voices, so the voice app knows where to fetch it.

The zip is deterministic: the same states give the same bytes, and so the same
sha256, every time -- entries sorted, stored (fp16 weights do not compress),
fixed timestamps and attributes, nothing but the pocket.safetensors files, no
wrapper folder. Every state is checked against the manifest's sha256 first, and
every voice in module\\Voices must be in the pack (and the other way round), so
a renamed or missing voice stops the build instead of shipping a pack that
claude-voice would refuse whole.

    python tools\\make_pocket_pack.py
    python tools\\make_pocket_pack.py --pack ..\\pocket-voices-pack --zip ..\\pocket-voices-pack-dist\\pocket-voices-983151f.zip

The zip goes OUTSIDE the repo; only pocket-pack.json is committed. Publish the
zip at --url (a GitHub release asset) before shipping a mod that carries it.
"""

import argparse
import hashlib
import json
import os
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
VOICES = os.path.join(REPO, "module", "Voices")
PARENT = os.path.dirname(REPO)
STATE = "pocket.safetensors"
CLIP = "breeze-reference.wav"
PACK_FILE = "pocket-pack.json"
NAME = "immersiveai-calradia"
RELEASE = "https://github.com/TraxData313/ImmersiveAI/releases/download/{tag}/{tag}.zip"
FIXED_TIME = (1980, 1, 1, 0, 0, 0)      # the earliest a zip can say; any fixed value would do


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for block in iter(lambda: fh.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def voice_folders(root):
    """'sex/culture/id' for every voice under module\\Voices that has a clip to bake."""
    found = set()
    for sex in os.listdir(root):
        sdir = os.path.join(root, sex)
        if sex.startswith("_") or not os.path.isdir(sdir):
            continue
        for culture in os.listdir(sdir):
            cdir = os.path.join(sdir, culture)
            if not os.path.isdir(cdir):
                continue
            for vid in os.listdir(cdir):
                if os.path.isfile(os.path.join(cdir, vid, CLIP)):
                    found.add(f"{sex}/{culture}/{vid}")
    return found


def inside(path, folder):
    p, f = os.path.normcase(os.path.abspath(path)), os.path.normcase(os.path.abspath(folder))
    return p == f or p.startswith(f + os.sep)


def load_manifest(pack):
    with open(os.path.join(pack, "manifest.json"), encoding="utf-8") as fh:
        m = json.load(fh)
    w = m.get("weights") or {}
    if len(str(w.get("revision", ""))) != 40 or not w.get("repo") or not w.get("language"):
        raise SystemExit("manifest.json does not name the weights (repo, 40-digit revision, language)")
    if m.get("failures"):
        raise SystemExit(f"manifest.json records failed bakes: {m['failures']}")
    return m


def check_states(pack, manifest, voices_root):
    """[(rel voice path, file on disk)] sorted, every one verified."""
    rows = []
    for v in manifest["voices"]:
        rel = v["file"]
        if not rel.endswith("/" + STATE) or rel.count("/") != 3:
            raise SystemExit(f"unexpected file in manifest: {rel}")
        path = os.path.join(pack, *rel.split("/"))
        if not os.path.isfile(path):
            raise SystemExit(f"missing: {path}")
        if os.path.getsize(path) != v["bytes"] or sha256(path) != v["sha256"]:
            raise SystemExit(f"{rel} does not match manifest.json -- re-bake it")
        rows.append((rel[: -len("/" + STATE)], path))
    rows.sort()
    names = [r for r, _ in rows]
    if len(set(n.lower() for n in names)) != len(names):
        raise SystemExit("a voice is listed twice in manifest.json")
    have = voice_folders(voices_root)
    extra, missing = sorted(set(names) - have), sorted(have - set(names))
    if extra:
        raise SystemExit(f"baked but not in {voices_root} (renamed?): {', '.join(extra)}")
    if missing:
        raise SystemExit(f"in {voices_root} but not baked: {', '.join(missing)}")
    return rows


def write_zip(rows, dest):
    os.makedirs(os.path.dirname(os.path.abspath(dest)), exist_ok=True)
    tmp = dest + ".tmp"
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_STORED) as z:
        for rel, path in rows:
            info = zipfile.ZipInfo(f"{rel}/{STATE}", date_time=FIXED_TIME)
            info.compress_type = zipfile.ZIP_STORED
            info.create_system = 3                       # unix, so the mode below means one thing
            info.external_attr = 0o100644 << 16
            with open(path, "rb") as fh:
                z.writestr(info, fh.read())
    os.replace(tmp, dest)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.strip().splitlines()[0])
    ap.add_argument("--pack", default=os.path.join(PARENT, "pocket-voices-pack"),
                    help="folder bake_pocket_voices.py wrote (default ..\\pocket-voices-pack)")
    ap.add_argument("--zip", help="zip to write (default ..\\pocket-voices-pack-dist\\pocket-voices-<rev7>.zip)")
    ap.add_argument("--url", help="where the zip will be downloadable (default the GitHub release asset)")
    ap.add_argument("--voices", default=VOICES, help="voice tree the pack covers (default module\\Voices)")
    ap.add_argument("--json", help=f"description to write (default <voices>\\{PACK_FILE})")
    args = ap.parse_args(argv)

    manifest = load_manifest(args.pack)
    w = manifest["weights"]
    tag = f"pocket-voices-{w['revision'][:7]}"
    dest = args.zip or os.path.join(PARENT, "pocket-voices-pack-dist", tag + ".zip")
    if inside(dest, REPO):
        raise SystemExit(f"the zip belongs outside the repo: {dest}")

    rows = check_states(args.pack, manifest, args.voices)
    write_zip(rows, dest)
    size, digest = os.path.getsize(dest), sha256(dest)

    doc = {
        "schema": "claude-voice/pocket-pack",
        "version": 1,
        "name": NAME,
        "url": args.url or RELEASE.format(tag=tag),
        "sha256": digest,
        "bytes": size,
        "weights": {"repo": w["repo"], "revision": w["revision"], "language": w["language"]},
        "voices": [rel for rel, _ in rows],
    }
    out = args.json or os.path.join(args.voices, PACK_FILE)
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
        fh.write("\n")
    print(f"{len(rows)} voices -> {dest}\n  {size} bytes, sha256 {digest}\n  wrote {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
