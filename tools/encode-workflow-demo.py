"""Encode the verified dedicated-Chrome workflow; never access personal media."""
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

def require(condition):
    if not condition:
        raise ValueError("Recording privacy or workflow verification failed; no assets exported")


source = Path(sys.argv[1]).resolve()
assets = Path(sys.argv[2]).resolve()
proof = json.loads((source / "verification.json").read_text(encoding="utf-8-sig"))
language = proof["Language"]
require(language in ("zh-CN", "en"))
require(proof["CurrentShortcutHintVerified"])
require(proof["IsolatedBrowserProfile"])
suffix = "zh" if language == "zh-CN" else "en"
cleanup = json.loads((source / "cleanup.json").read_text(encoding="utf-8-sig"))
require(proof["Selection"] == "Passed" and proof["SelectionVerified"] and proof["ForegroundVerified"])
require(proof["ChangedWindow"] and not proof["TargetInitiallySelected"])
require(proof["PhysicalMonitors"] == 3 and proof["FixtureMonitorsVerified"])
require(proof["CrossMonitorSelection"] and proof["TargetStayedOnOriginalScreen"] and proof["TargetBoundsUnchanged"])
require(proof["HighlightBoundsVerified"] and proof["HighlightElapsedMs"] >= 2000)
require(proof["NativeHighlightEdgesObserved"] == 4 and not proof["DesktopCapture"])
require(cleanup["DemoWindowsClosed"])
require(cleanup["IsolatedBrowserExited"] and cleanup["TemporaryProfileDeleted"])
require(cleanup["ExistingBrowserWindowsUnchanged"])
count = proof["Frames"]
observed = proof["RecordedHighlightFrames"]
after_hold = 24  # Final page hold specified by WorkflowDemo.Run.
start = count - observed - after_hold
require(start == 53 and observed >= 2)
frames = list(range(start))
# Normalize the observed highlight to its verified duration at 10 fps.
highlight_frames = round(proof["HighlightElapsedMs"] / 100)
frames += [start + min(observed - 1, i * observed // highlight_frames) for i in range(highlight_frames)]
frames += list(range(start + observed, count))
assets.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory(prefix="gif-frames-", dir=source) as temporary:
    sequence = Path(temporary)
    for i, original in enumerate(frames):
        shutil.copyfile(source / f"frame-{original:03d}.png", sequence / f"frame-{i:03d}.png")
    subprocess.run([
        "ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-framerate", "10",
        "-i", str(sequence / "frame-%03d.png"), "-frames:v", str(len(frames)),
        "-filter_complex", "[0:v]split[a][b];[a]palettegen=stats_mode=full:reserve_transparent=0[p];[b][p]paletteuse=dither=bayer:bayer_scale=4",
        "-loop", "0", str(assets / f"demo-{suffix}.gif"),
    ], check=True)
for original, published in [("before.png", f"workflow-before-{suffix}.png"), ("after.png", f"workflow-after-{suffix}.png")]:
    shutil.copyfile(source / original, assets / published)
public_proof = {
    "DemoContent": "fictional local pages",
    "IsolatedBrowserProfile": True,
    "TemporaryProfileDeleted": True,
    "ExistingBrowserWindowsUnchanged": True,
    "ThreeMonitorWorkflowVerified": True,
    "SelectionVerified": bool(proof["SelectionVerified"]),
    "ForegroundVerified": bool(proof["ForegroundVerified"]),
    "HighlightVerified": bool(proof["HighlightBoundsVerified"] and proof["HighlightElapsedMs"] >= 2000),
    "DemoWindowsClosed": bool(cleanup["DemoWindowsClosed"]),
    "Presentation": "illustrated monitor overview and focus zoom",
}
for key in ("Version", "Language"):
    if key in proof:
        public_proof[key] = proof[key]
(assets / f"workflow-verification-{suffix}.json").write_text(json.dumps(public_proof, indent=2) + "\n", encoding="utf-8")
print(f"Encoded {language}: {len(frames)} frames, {len(frames) / 10:.1f}s, including {highlight_frames / 10:.1f}s verified highlight.")
