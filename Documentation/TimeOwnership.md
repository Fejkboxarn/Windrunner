# Engine time ownership

TimeScaleArbiter is regular C# state. Each pause or slowdown has a disposable request; release/update affects only that request. Strongest slowdown wins, pause dominates all slowdowns, and the final release restores the captured baseline scale and fixed step. Releasing one of two nested pauses cannot resume gameplay. fixedDeltaTime remains positive and follows the underlying slowdown ratio during pause.

UnityTimeAuthority is the single owned-code adapter that writes Unity's engine-global Time.timeScale/fixedDeltaTime. Its static scope reflects the engine-global clock, not player or gameplay state. It captures the baseline at the first request, supports old scenes without ProjectRoot and resets safely when entering Play Mode without domain reload. Owners release requests on disable/disposal.

AirPerfectTimingFeedback now delegates the original quality/hold/smoothstep recovery equations to HitStopEnvelope. Independent components have independent requests; retriggering one component restarts only its envelope as before. Standalone slowdown tuning is unchanged.

Overlap corrections (intentional): menu pause freezes hit-stop recovery and Sling's unscaled impact hold. Gameplay ability updates, swimming correction, camera motion and FOV recovery are suspended while a pause request exists. Hit-stop still allows the original unscaled camera/FOV/impact response when no menu pause is active. Releasing pause resumes outstanding slowdown instead of restoring a stale time scale. Disabling one hit-stop source cannot cancel another or unpause the game.

The owned runtime scan previously found only PauseMenuController and AirPerfectTimingFeedback writing engine time; both now use requests. No MMF_TimeScale/FreezeFrame/TimeManager recipe was found in owned generated scenes/assets/prefabs. Vendor/demo time effects must route through this boundary if later activated in gameplay; do not assume arbitrary external Time writers can coexist safely.

Validation: deterministic nested requests and 400 old-equation envelope scenarios, plus actual menu pause with two hit-stop components, nested pause, source disable and recovery. Manual acceptance still includes pausing during perfect Sling impact, cursor/camera feel and audible feedback.
