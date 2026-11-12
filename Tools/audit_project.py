"""Read-only source/content inventory; output is a report, never an asset rewrite."""
import collections
import hashlib
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets'

def main():
    sources = list(ASSETS.rglob('*.cs'))
    owned = [p for p in sources if p.parts[len(ASSETS.parts)] in ('AirflowPrototype', 'WindrunnerProduction', 'PortableAnimation')]
    guid_paths = collections.defaultdict(list)
    for p in ASSETS.rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})$', p.read_text(encoding='utf-8-sig'), re.M)
        if match:
            guid_paths[match[1]].append(str(p.relative_to(ROOT))[:-5])
    scene = ASSETS / 'AirflowPrototype/Generated/Batch1_Playground.unity'
    text = scene.read_text(encoding='utf-8-sig')
    blocks = re.split(r'^--- !u!', text, flags=re.M)[1:]
    objects = {}
    for block in blocks:
        header = re.match(r'(\d+) &(-?\d+)', block)
        if not header:
            continue
        if header[1] == '1':
            name = re.search(r'^  m_Name: (.*)$', block, re.M)
            objects[header[2]] = name[1] if name else '?'
    components = []
    for block in blocks:
        if not block.startswith('114 '):
            continue
        script = re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]+)', block)
        obj = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', block)
        enabled = re.search(r'm_Enabled: (\d)', block)
        paths = guid_paths.get(script[1], []) if script else []
        if any('AirflowPrototype' in p or 'WindrunnerProduction' in p for p in paths):
            components.append({'object': objects.get(obj[1], '?') if obj else '?', 'script': paths, 'enabled': enabled[1] if enabled else '?'})
    counts = collections.Counter(p.parts[len(ASSETS.parts)] for p in sources)
    fingerprint = {}
    tracked = subprocess.check_output(['git', 'ls-files', '-z', 'Assets', 'Packages', 'ProjectSettings'], cwd=ROOT).decode().split('\0')
    for name in tracked:
        if name and pathlib.Path(name).suffix in ('.unity', '.prefab', '.asset', '.meta', '.json'):
            p = ROOT / name
            if p.is_file():
                fingerprint[name] = hashlib.sha256(p.read_bytes()).hexdigest()
    report = {
        'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip(),
        'script_counts_by_root': dict(counts),
        'largest_owned_sources': sorted([{'path': str(p.relative_to(ROOT)), 'lines': len(p.read_text(encoding='utf-8-sig').splitlines())} for p in owned], key=lambda x: -x['lines'])[:30],
        'duplicate_asset_guids': {k:v for k,v in guid_paths.items() if len(v)>1},
        'playground_components': components,
        'serialized_fingerprints': fingerprint,
    }
    print(json.dumps(report, indent=2))

if __name__ == '__main__':
    main()
