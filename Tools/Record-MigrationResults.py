"""Refresh committed evidence from actual local Unity XML and build reports."""
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
logs = root / "Logs/ProductionMigration"
destination = root / "Documentation/TestResults.json"
result = json.loads(destination.read_text(encoding="utf-8-sig"))
runs = {run["label"]: run for run in result["runs"]}
for path in sorted(logs.glob("*.xml")):
    tree = ET.parse(path).getroot()
    if tree.tag != "test-run":
        continue
    run = {"label": path.stem, "xml_sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
    run.update({key: tree.get(key) for key in (
        "result", "total", "passed", "failed", "skipped", "duration", "start-time", "end-time")})
    run["tests"] = [{"name": case.get("name"), "result": case.get("result"),
                     "failure": case.findtext("failure/message")}
                    for case in tree.iter("test-case")]
    runs[path.stem] = run
result["runs"] = sorted(runs.values(), key=lambda run: (run.get("start-time") or "", run["label"]))
paths = subprocess.check_output(["git", "diff", "--name-only", "7bfdfe5e", "--", "Assets"], cwd=root, text=True).splitlines()
paths += subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "Assets"], cwd=root, text=True).splitlines()
result["source_sha256_at_final_validation"] = {
    name: hashlib.sha256((root / name).read_bytes()).hexdigest()
    for name in sorted(set(paths))
    if Path(name).suffix in (".cs", ".asmdef") and (root / name).is_file()
}
result["builds"] = [json.loads(path.read_text(encoding="utf-8-sig"))
                    for path in sorted(logs.glob("*-build.json"))]
result["player_smoke_runs"] = [json.loads(path.read_text(encoding="utf-8-sig"))
                               for path in sorted(logs.glob("*-player.json"))]
result["rendered_profiles"] = [json.loads(path.read_text(encoding="utf-8-sig"))
                               for path in sorted(logs.glob("rendered-profile-*.json"))]
result["evidence_log_sha256"] = {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
                                 for path in sorted(logs.glob("*.log"))}
destination.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"Recorded {len(result['runs'])} actual test runs and {len(result['builds'])} build reports.")
