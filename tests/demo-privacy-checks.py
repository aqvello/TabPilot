"""Unsafe or legacy recordings must be rejected before writing any public assets."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile

encoder = Path(__file__).resolve().parents[1] / "tools" / "encode-workflow-demo.py"
proof = {
    "Language": "en", "CurrentShortcutHintVerified": True,
    "IsolatedBrowserProfile": True, "Selection": "Passed",
    "SelectionVerified": True, "ForegroundVerified": True,
    "ChangedWindow": True, "TargetInitiallySelected": False,
    "PhysicalMonitors": 3, "FixtureMonitorsVerified": True,
    "CrossMonitorSelection": True, "TargetStayedOnOriginalScreen": True,
    "TargetBoundsUnchanged": True, "HighlightBoundsVerified": True,
    "HighlightElapsedMs": 2001, "NativeHighlightEdgesObserved": 4,
    "DesktopCapture": False,
}
cleanup = {
    "DemoWindowsClosed": True, "IsolatedBrowserExited": True,
    "TemporaryProfileDeleted": True, "ExistingBrowserWindowsUnchanged": True,
}
cases = [
    ("shared browser profile", "proof", "IsolatedBrowserProfile", False),
    ("legacy proof without isolation", "proof", "IsolatedBrowserProfile", None),
    ("browser still running", "cleanup", "IsolatedBrowserExited", False),
    ("profile left on disk", "cleanup", "TemporaryProfileDeleted", False),
    ("existing windows changed", "cleanup", "ExistingBrowserWindowsUnchanged", False),
]
for optimized in (False, True):
    for label, document, key, value in cases:
        with tempfile.TemporaryDirectory(prefix="tabpilot-export-check-") as temporary:
            root = Path(temporary)
            inputs = {"proof": dict(proof), "cleanup": dict(cleanup)}
            if value is None:
                del inputs[document][key]
            else:
                inputs[document][key] = value
            (root / "verification.json").write_text(json.dumps(inputs["proof"]), encoding="utf-8")
            (root / "cleanup.json").write_text(json.dumps(inputs["cleanup"]), encoding="utf-8")
            result = subprocess.run(
                [sys.executable] + (["-O"] if optimized else []) + [str(encoder), str(root), str(root / "public")],
                capture_output=True, text=True,
            )
            if result.returncode == 0 or (root / "public").exists():
                raise RuntimeError("Unsafe recording was exported: " + label)
            # Ensure rejection happened at the intended guard, not a missing frame or FFmpeg.
            expected = "KeyError" if value is None else "Recording privacy or workflow verification failed"
            if expected not in result.stderr:
                raise RuntimeError("Unexpected rejection: " + result.stderr)
            print("PASS " + label + (" (python -O)" if optimized else ""))
print("Demo privacy checks: 10 passed")
