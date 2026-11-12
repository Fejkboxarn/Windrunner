"""Source inventory of ownership risks, not an automatic obsolete-code verdict."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
categories = {
    "scene_discovery": r"Find(?:Any|First|Objects|Object)\w*ByType|Camera\.main|GameObject\.Find",
    "global_service_access": r"(?:ProjectRoot|WindrunnerInputHost|WindrunnerFeedbackHost)\.Active",
    "engine_clock_write": r"Time\.(?:timeScale|fixedDeltaTime)\s*=",
    "physical_move": r"\b(?:controller|Controller)\.Move\(",
    "pose_write": r"(?:localPosition|localRotation|position|rotation)\s*=|applyRootMotion\s*=",
    "runtime_material": r"new Material\(|\.material\b",
}
files = sorted(set(path for folder in ("Assets/AirflowPrototype/Runtime", "Assets/WindrunnerProduction", "Assets/PortableAnimation/Runtime")
                   for path in (root / folder).rglob("*.cs")
                   if not {"Editor", "Tests"}.intersection(path.relative_to(root).parts)))
findings = {category: [] for category in categories}
for path in files:
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if line.lstrip().startswith("//"):
            continue
        for category, pattern in categories.items():
            if re.search(pattern, line):
                findings[category].append({"file": path.relative_to(root).as_posix(), "line": number, "source": line.strip()})
report = {"scope": "Owned runtime sources; static matches require ownership review", "files": len(files), "findings": findings}
output = root / "Logs/ProductionMigration/architecture-audit.json"
output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"files": len(files), "matches": {key: len(value) for key, value in findings.items()}}, indent=2))
