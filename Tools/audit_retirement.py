"""Conservative retirement inventory. References are evidence, not proof of runtime use."""
import json,re,subprocess
from pathlib import Path
root=Path(__file__).resolve().parent.parent
assets=root/'Assets'
serialized={}; tokens={}; scripts=list(assets.rglob('*.cs')); total=0
for p in assets.rglob('*'):
 if p.suffix not in ('.unity','.prefab','.asset','.controller','.overrideController','.anim','.cs','.asmdef'): continue
 try:s=p.read_text(encoding='utf-8-sig')
 except (UnicodeError,OSError):continue
 total+=1; name=p.relative_to(root).as_posix()
 if p.suffix=='.cs':
  for word in set(re.findall(r'\b[A-Za-z_]\w*\b',s)):tokens.setdefault(word,[]).append(name)
 else:
  for guid in set(re.findall(r'guid: ([a-f0-9]{32})',s)):serialized.setdefault(guid,[]).append(name)
owned=[]; external={}
for p in scripts:
 name=p.relative_to(root).as_posix()
 if not name.startswith(('Assets/AirflowPrototype/','Assets/WindrunnerProduction/','Assets/PortableAnimation/')):
  external[p.relative_to(assets).parts[0]]=external.get(p.relative_to(assets).parts[0],0)+1;continue
 meta=Path(str(p)+'.meta');m=re.search(r'guid: (\w+)',meta.read_text()) if meta.exists() else None
 refs=serialized.get(m[1],[]) if m else []
 callers=[c for c in tokens.get(p.stem,[]) if c!=name]
 editor='/Editor/' in name; tests='/Tests/' in name
 legacy=any(x in p.stem for x in ('HumanoidAnimatorDriver','CustomAnimationDriver','RunningArmPose','ProceduralKnife','DualThrowPose','GroundedFootIK','InputStartupRepair','AirCastFeedback','AirTargetIndicator','AirChargeVisual','Batch','Migration'))
 authoring=p.stem.startswith('CharacterTuning') or p.stem in ('CharacterProductionDiagnostics','CharacterCompositionAudit','WindrunnerCandidateBuild')
 status='required' if tests or authoring or (not editor and not legacy and (refs or callers)) else 'replaceable' if legacy else 'uncertain'
 reason='Current runtime/test dependency; preserve until replacement contract is tested.' if status=='required' else 'Legacy compatibility or installation path; audit live ownership and callers before retirement.' if status=='replaceable' else 'No automatic obsolete verdict: inspect reflection, animation events and authoring workflow before removal.'
 owned.append(dict(path=name,classification=status,serialized_references=refs,source_mentions=callers,retirement_condition=reason))
data=[]
for folder in ('AirflowPrototype','WindrunnerProduction','PortableAnimation'):
 for p in (assets/folder).rglob('*'):
  if p.suffix not in ('.unity','.prefab','.asset','.controller','.overrideController','.anim','.mat','.inputactions'):continue
  meta=Path(str(p)+'.meta');m=re.search(r'guid: (\w+)',meta.read_text()) if meta.exists() else None
  refs=serialized.get(m[1],[]) if m else []
  mentions=tokens.get(p.stem,[])
  data.append(dict(path=p.relative_to(root).as_posix(),classification='required' if refs or mentions or p.suffix=='.unity' else 'uncertain',serialized_references=refs,source_name_mentions=mentions,retirement_condition='Retain referenced authoring/runtime assets. Zero static references is not proof of obsolescence: verify dynamic loads and installer-generated paths before deletion.'))
removed=subprocess.check_output(['git','diff','--name-only','--diff-filter=D','9a604f4','--','Assets'],cwd=root,text=True).splitlines()
retired=root/'Documentation/RetiredProductionComponents.json'
report=dict(scanned_text_assets=total,owned_scripts=owned,owned_data_assets=data,external_script_packages_retained=external,removed_files=removed,retired_scene_components=json.loads(retired.read_text()) if retired.exists() else [],notes='Initial audit performed before deletion. Current inventory includes reviewed retirements. Source mentions include comments/tooling; unknown dynamic reflection/resource callers remain a removal risk. Third-party assets are retained, not declared obsolete.')
retirement_report=json.loads((root/'Documentation/SourceRetirements.json').read_text())
retirement_reasons={r['path']:r['reason'] for group in ('scripts','assets') for r in retirement_report.get(group,[])}
report['removed_classification']=[dict(path=p,classification='obsolete',reason=retirement_reasons.get(p,retirement_reasons.get(p.removesuffix('.meta'),'Completed one-off placeholder installer; no callers or serialized GUID users.'))) for p in removed]
(root/'Documentation/LegacyRetirementInventory.json').write_text(json.dumps(report,indent=2)+'\n')
print(f'{len(owned)} owned scripts inventoried; {sum(external.values())} external scripts retained; {total} text assets searched.')
