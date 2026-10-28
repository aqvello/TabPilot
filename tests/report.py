"""Export only public software versions and a mixed-scaling test conclusion."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "artifacts" / "private"
baseline = json.loads((EVIDENCE / "baseline.json").read_text(encoding="utf-8-sig"))
monitors = baseline["Monitors"]
chrome = next(w["Version"] for w in baseline["Windows"] if w["Eligible"])
mixed = len(monitors) == 3 and len({m["ScalePercent"] for m in monitors}) > 1
summary = {
    "environment": {"os": baseline["OsVersion"], "chrome": chrome},
    "three_monitor_mixed_scaling_tested": mixed,
    "conclusion": "Passing controlled samples observed; Windows foreground activation may still fail.",
    "limitations": ["Enumeration completeness is unknown", "Complex collapsed groups are not fully tested"],
}
path = ROOT / "artifacts" / "validation-summary.json"
path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Exported software versions and mixed-scaling conclusion only.")
