"""Code-only migration gate against the confirmed 5P baseline.

Checks tracked existing content bytes via Git and direct SerializeField type/name
declarations. This supplements Unity's loaded-reference audit, not a C# parser.
New assets are allowed; intentional content migration requires a reviewed gate update.
"""
import json
import pathlib
import re
import subprocess
import hashlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
BASELINE = '7bfdfe5e'
CONTENT = {'.unity', '.prefab', '.asset', '.meta', '.mat', '.controller', '.overrideController', '.anim', '.inputactions'}

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT).decode('utf-8-sig')

changed = git('diff', '--name-only', '--diff-filter=DMRT', BASELINE, '--', 'Assets', 'Packages', 'ProjectSettings').splitlines()
failures = []
checked = []
repair_file = ROOT / 'Documentation/ContentRepairs.json'
repairs = {r['path']: r for r in json.loads(repair_file.read_text())['repairs']} if repair_file.exists() else {}
def canonical_hash(text):
    return hashlib.sha256(text.replace('\r\n', '\n').encode('utf-8')).hexdigest()
fields = re.compile(r'\[SerializeField\]\s+(?:private|public|protected)\s+([\w.<>\[\]]+)\s+(\w+)')
for name in changed:
    path = ROOT / name
    if path.suffix in CONTENT or name.startswith(('Packages/', 'ProjectSettings/')):
        repair = repairs.get(name)
        if not (repair and path.exists() and
                canonical_hash(git('show', BASELINE + ':' + name)) == repair['baseline_sha256'] and
                canonical_hash(path.read_text(encoding='utf-8-sig')) == repair['approved_sha256']):
            failures.append('Unreviewed serialized/configuration content changed: ' + name)
    if path.suffix == '.cs' and name.startswith(('Assets/AirflowPrototype/', 'Assets/WindrunnerProduction/')):
        old = set(fields.findall(git('show', BASELINE + ':' + name)))
        new = set(fields.findall(path.read_text(encoding='utf-8-sig'))) if path.exists() else set()
        if old - new:
            failures.append('Serialized field removed or changed: ' + name + ' ' + str(sorted(old - new)))
        checked.append(name)
print(json.dumps({'baseline': BASELINE, 'checked_source_contracts': checked, 'failures': failures}, indent=2))
raise SystemExit(bool(failures))
