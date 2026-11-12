# Windows warning audit

The original RC1 BuildReport was read through Unity's GetLatestReport before rebuilding. Its **21 warning entries** are preserved verbatim in BuildWarnings.json; this corrects the earlier generic description, which mixed compiler warnings with build warnings. All 21 were inspected.

| Entries | Origin | Severity and disposition |
|---|---|---|
| 1 | Imported Shadow_Dagger mesh used by a particle renderer | Medium compatibility risk: Read/Write disabled; Unity currently enables it for this build. Import-setting migration needs memory/particle validation. Asset not recreated. |
| 1 | 41 imported environment meshes used by MeshColliders | Medium future-version risk: collision prebaking settings incomplete. Unity currently bakes them. Requires targeted importer/collider validation; bulk reimport was not performed. |
| 2 | Unity Core debug occlusion shaders | Low: all subshaders stripped due pipeline tags. Debug shaders are not the production URP materials; no evidence of gameplay render failure. |
| 3 | FEEL optional Cinemachine / PostProcessing Resources | Medium packaging debt: missing scripts on MM_6D_Shake, MMDefaultPostProcessingProfile and MMDefaultPostProcessingVolume. These vendor Resources are included despite not being referenced by the enabled gameplay scene. Do not confuse the gameplay integrity gate with an audit of every packaged vendor resource. Compatible dependency installation or a reviewed packaging exclusion is still needed; no vendor asset was deleted. |
| 1 | INab progress-bar shader | Medium visual edge risk: pow with potentially negative base (DX11). Requires authored ShaderGraph correction and appearance comparison. |
| 2 | Toony Colors Pro shader | Medium visual edge risk: derivatives inside varying loops (DX11 and DX12). Vendor shader update/render comparison required. |
| 6 | Raygeas Waterfall, Billboard, Surface, Water, Vegetation, Mountains | Low/medium upgrade debt: deprecated _FORWARD_PLUS keyword, potential compilation cost. |
| 5 | Raygeas Waterfall, Billboard, Surface, Water, Vegetation | Low/medium upgrade debt: deprecated UnityGBuffer include. |

Project-owned compiler warnings are separate: removed the unused nonserialized AirChimeStartSeal occupancy flag and empty exit handler. The legacy serialized resource-capacity field is retained for compatibility with a narrowly scoped CS0414 suppression and explanatory comment. Existing FindFirstObjectByType and sorted-search calls remain in legacy runtime fallbacks/editor setup tools; replacing them blindly with unordered searches can change selected objects. They are recorded architectural debt, not claimed fixed. FEEL BinaryFormatter/API analyzer warnings are external code and were not rewritten.

Subsequent builds record every warning message in their JSON report. Incremental compiler/shader caches change emitted counts; a lower count alone is not proof that warnings were resolved.
