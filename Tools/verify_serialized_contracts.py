"""Code-only migration gate against the confirmed 5P baseline.

Checks tracked existing content bytes via Git and direct SerializeField type/name
declarations. This supplements Unity's loaded-reference audit, not a C# parser.
New assets are allowed; intentional content migration requires a reviewed gate update.
"""
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
BASELINE = '7bfdfe5e'
CONTENT = {'.unity', '.prefab', '.asset', '.meta', '.mat', '.controller', '.overrideController', '.anim', '.inputactions'}

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT).decode('utf-8-sig')

changed = git('diff', '--name-only', '--diff-filter=DMRT', BASELINE, '--', 'Assets', 'Packages', 'ProjectSettings').splitlines()
failures = []
checked = []
fields = re.compile(r'\[SerializeField\]\s+(?:private|public|protected)\s+([\w.<>\[\]]+)\s+(\w+)')
for name in changed:
    path = ROOT / name
    if path.suffix in CONTENT or name.startswith(('Packages/', 'ProjectSettings/')):
        failures.append('Existing serialized/configuration content changed: ' + name)
    if path.suffix == '.cs' and name.startswith(('Assets/AirflowPrototype/', 'Assets/WindrunnerProduction/')):
        old = set(fields.findall(git('show', BASELINE + ':' + name)))
        new = set(fields.findall(path.read_text(encoding='utf-8-sig'))) if path.exists() else set()
        if old - new:
            failures.append('Serialized field removed or changed: ' + name + ' ' + str(sorted(old - new)))
        checked.append(name)
print(json.dumps({'baseline': BASELINE, 'checked_source_contracts': checked, 'failures': failures}, indent=2))
raise SystemExit(bool(failures))
