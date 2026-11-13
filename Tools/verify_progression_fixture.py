"""Targeted fixture usability gate against the accepted pre-task checkpoint."""
from pathlib import Path
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = "5761575b"
FIXTURE = "Assets/WindrunnerProduction/Progression/Fixtures/"


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True)


def blocks(text):
    return {key: (kind, body) for kind, key, body in
            re.findall(r"--- !u!(\d+) &(\d+)\n(.*?)(?=--- !u!|\Z)", text, re.S)}


failures = []
content = {".unity", ".prefab", ".asset", ".mat", ".anim", ".controller", ".inputactions", ".meta"}
for name in git("diff", "--no-renames", "--name-only", BASE, "--", "Assets", "Packages", "ProjectSettings").splitlines():
    if not name.startswith(FIXTURE) and (Path(name).suffix in content or name.startswith(("Packages/", "ProjectSettings/"))):
        failures.append("Changed non-fixture serialized asset: " + name)
name = FIXTURE + "ProgressionFixture.unity"
old = blocks(git("show", BASE + ":" + name))
new = blocks((ROOT / name).read_text(encoding="utf-8-sig"))
for key, (kind, body) in old.items():
    if key not in new:
        failures.append("Removed existing scene object/component: " + key)
        continue
    if kind == "114":
        def normalize(value):
            # Display prompts and serialization of an existing empty prerequisite are intentional.
            return re.sub(r"^  (?:prompt:.*|requiredUnlockId: *)\n", "", value, flags=re.M)
        if normalize(body) != normalize(new[key][1]):
            failures.append("Changed existing gameplay component binding/configuration: " + key)
    if kind in ("54", "65", "136", "143", "135") and body != new[key][1]:
        failures.append("Changed existing physics component: " + key)

report = {"baseline": BASE, "original_fixture_objects_retained": len(old),
          "current_fixture_objects": len(new), "failures": failures}
output = ROOT / "Logs/ProductionMigration/fixture-serialized-audit.json"
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2))
raise SystemExit(bool(failures))
