"""Read-only Suntemple source and production-world preservation check (streaming hashes)."""
import hashlib,json,pathlib,sys
root=pathlib.Path(__file__).resolve().parents[1]
failed=[]; checked=0; skipped=[]
for name in ['SuntempleSourceBaseline.json','SuntempleProtectedWorld.json']:
    for relative,expected in json.loads((root/'Documentation'/name).read_text(encoding='utf-8-sig')).items():
        path=root/relative
        if not path.exists() and '.unitypackage' in relative:
            skipped.append(relative) # Local preserved archive is deliberately not versioned.
            continue
        if not path.exists(): failed.append(relative+': missing');continue
        with path.open('rb') as file:actual=hashlib.file_digest(file,'sha256').hexdigest()
        checked+=1
        if actual.lower()!=expected.lower():failed.append(relative+': differs from pre-repair bytes')
print(json.dumps({'checked':checked,'failures':failed,'optional_local_archives_absent':skipped},indent=2))
sys.exit(bool(failed))
