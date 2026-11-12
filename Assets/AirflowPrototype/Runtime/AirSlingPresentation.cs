using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>Synchronous Sling presentation commands. Owns no gameplay state,
    /// resource spending, movement authority or subscriptions. The controller
    /// retains serialized references and invokes commands in legacy event order.</summary>
    public sealed class AirSlingPresentation
    {
        private AirSlingSettings settings;
        private PlayerMovementVisuals movementVisuals;
        private PlayerSpeedFeedback speedFeedback;
        private PlayerOrbitCamera orbitCamera;
        private AirPerfectTimingFeedback perfectTimingFeedback;
        private AirSlingImpactAudioLibrary impactAudioLibrary;

        public void Configure(AirSlingSettings settings, PlayerMovementVisuals visuals,
            PlayerSpeedFeedback speed, PlayerOrbitCamera camera,
            AirPerfectTimingFeedback perfect, AirSlingImpactAudioLibrary audio)
        {
            this.settings = settings; movementVisuals = visuals; speedFeedback = speed;
            orbitCamera = camera; perfectTimingFeedback = perfect; impactAudioLibrary = audio;
        }

        public void Release()
        {
            if (settings.enableSlingFovProfile &&
                speedFeedback != null)
            {
                speedFeedback.PulseFovOverride(
                    settings.releaseFov,
                    settings.releaseFovResponse,
                    settings.releaseFovHoldDuration,
                    settings.releaseFovRecoveryResponse);
            }
        }

        public void PerfectRelease(float perfectQuality)
        {
                if (settings.enablePerfectReleaseTimeStop &&
                    perfectTimingFeedback != null)
                {
                    perfectTimingFeedback
                        .TriggerPerfectFeedback(
                            perfectQuality,
                            settings
                                .perfectReleaseTimeStopDuration,
                            settings
                                .perfectReleaseTimeStopRecovery,
                            settings
                                .perfectReleaseTimeScale);
                }
        }

        public void DashStart(AirSlingNode _trackedTarget)
        {
            if (settings.enableSlingFovProfile &&
                speedFeedback != null)
            {
                speedFeedback.SetFovOverride(
                    settings.dashFov,
                    settings.dashFovResponse);
            }

            if (settings.enableDashStartCameraKick &&
                orbitCamera != null)
            {
                orbitCamera.AddRewardImpulse(
                    _trackedTarget.LandingPosition,
                    1f,
                    settings.dashCameraKickBack,
                    settings.dashCameraKickUp,
                    settings.dashCameraKickRoll,
                    settings.dashCameraKickRecovery);
            }
        }

        public void ImpactEffects(AirSlingNode target)
        {
            target.PlayImpactVFX();
            target.PlayImpactAudio(
                impactAudioLibrary);
        }

        public void ImpactResponse()
        {
            if (settings.enableNodeImpactTimeStop &&
                perfectTimingFeedback != null)
            {
                perfectTimingFeedback
                    .TriggerPerfectFeedback(
                        1f,
                        settings
                            .nodeImpactTimeStopDuration,
                        settings
                            .nodeImpactTimeStopRecovery,
                        settings
                            .nodeImpactTimeScale);
            }

            if (movementVisuals != null)
            {
                movementVisuals.ClearTraversalBank(
                    settings.curveBankResponse);
            }
        }

        public void LaunchFov()
        {
            if (settings.enableSlingFovProfile &&
                speedFeedback != null)
            {
                speedFeedback.PulseFovOverride(
                    settings.launchFov,
                    settings.launchFovResponse,
                    settings.launchFovHoldDuration,
                    settings.launchFovRecoveryResponse);
            }
        }

        public void LaunchFlip()
        {
            if (settings.enableFinalLaunchFlip &&
                movementVisuals != null)
            {
                movementVisuals
                    .TriggerTraversalFlip(
                        settings
                            .finalLaunchFlipIntensity);
            }
        }

        public void UpdateCurveBank(AirSlingBezierPath path, float progress)
        {
            if (movementVisuals == null)
                return;

            if (!settings.enablePlayerCurveBank)
            {
                movementVisuals.ClearTraversalBank(
                    settings.curveBankResponse);

                return;
            }

            float previewT =
                Mathf.Clamp01(
                    progress +
                    settings.curveBankPreview);

            Vector3 tangent =
                path.EvaluateTangent(
                    progress);

            Vector3 preview =
                path.EvaluateTangent(
                    previewT);

            tangent.y = 0f;
            preview.y = 0f;

            if (tangent.sqrMagnitude <
                    0.0001f ||
                preview.sqrMagnitude <
                    0.0001f)
            {
                movementVisuals.ClearTraversalBank(
                    settings.curveBankResponse);

                return;
            }

            float signedAngle =
                Vector3.SignedAngle(
                    tangent.normalized,
                    preview.normalized,
                    Vector3.up);

            float bank01 =
                Mathf.Clamp(
                    signedAngle /
                    Mathf.Max(
                        1f,
                        settings.curveAngleForMaximumBank),
                    -1f,
                    1f);

            float bankDegrees =
                -bank01 *
                settings.maximumPlayerCurveBankDegrees;

            movementVisuals.SetTraversalBank(
                bankDegrees,
                settings.curveBankResponse);
        }

        public void UpdateSlingFovDuringDash(float progress)
        {
            if (!settings.enableSlingFovProfile ||
                speedFeedback == null)
            {
                return;
            }

            if (progress <
                settings.impactFovCompressionStart)
            {
                speedFeedback.SetFovOverride(
                    settings.dashFov,
                    settings.dashFovResponse);

                return;
            }

            float compression01 =
                Mathf.InverseLerp(
                    settings.impactFovCompressionStart,
                    1f,
                    progress);

            float targetFov =
                Mathf.Lerp(
                    settings.dashFov,
                    settings.impactCompressionFov,
                    compression01);

            speedFeedback.SetFovOverride(
                targetFov,
                settings.impactCompressionFovResponse);
        }

        public void UpdateTrackedNodeFacing(AirSlingNode _trackedTarget, Vector3 playerPosition, float unscaledDeltaTime)
        {
            if (!settings.makeNodeVisualFacePlayerDuringSling ||
                _trackedTarget == null)
            {
                return;
            }

            AirSlingNodeVisualAnimator visualAnimator =
                _trackedTarget
                    .GetComponent<AirSlingNodeVisualAnimator>();

            if (visualAnimator == null)
                return;

            visualAnimator.FaceWorldPosition(
                playerPosition,
                settings
                    .nodeVisualTurnSpeedDegreesPerSecond,
                unscaledDeltaTime);
        }

        public void Reset()
        {
            if (movementVisuals != null)
            {
                movementVisuals.ClearTraversalBank(
                    settings != null
                        ? settings.curveBankResponse
                        : 15f);
            }

            if (speedFeedback != null)
            {
                speedFeedback.ClearFovOverride(
                    settings != null
                        ? settings.launchFovRecoveryResponse
                        : 7f);
            }
        }
    }
}
