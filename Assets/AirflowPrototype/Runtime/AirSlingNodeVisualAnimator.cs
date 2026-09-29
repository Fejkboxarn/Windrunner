using UnityEngine;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class AirSlingNodeVisualAnimator : MonoBehaviour
    {
        [Header("Bear / Visual")]
        [SerializeField] private Animator animator;

        [Tooltip(
            "The visual object hidden after the death animation delay. " +
            "Usually the bear prefab root. If empty, the Animator GameObject is used.")]
        [SerializeField] private GameObject visualRoot;

        [Header("Animator Parameters")]
        [Tooltip("Bool parameter used for the idle state.")]
        [SerializeField] private string idleBoolParameter = "Idle";

        [Tooltip("Bool parameter used for the walking state.")]
        [SerializeField] private string walkBoolParameter = "Walk";

        [Tooltip("Bool parameter used for the death state.")]
        [SerializeField] private string dieBoolParameter = "Die";

        [Tooltip("Trigger parameter fired whenever the node takes non-lethal damage.")]
        [SerializeField] private string hurtTriggerParameter = "Hurt";

        private bool _dead;
        private bool _walking;

        public Animator Animator =>
            animator;

        public GameObject VisualRoot =>
            visualRoot;

        // Kept for API compatibility with the wander component.
        // Hurt timing is now fully owned by the Animator Controller.
        public bool IsHurtPlaying =>
            false;

        public bool IsDead =>
            _dead;

        public void Configure(
            Animator newAnimator,
            GameObject newVisualRoot)
        {
            if (newAnimator != null)
                animator = newAnimator;

            if (newVisualRoot != null)
                visualRoot = newVisualRoot;

            ResolveVisualRoot();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator =
                    GetComponentInChildren<Animator>(
                        true);
            }

            ResolveVisualRoot();
        }

        public void SetVisible(
            bool visible)
        {
            ResolveVisualRoot();

            if (visualRoot != null)
                visualRoot.SetActive(visible);
        }

        public void PlayIdle()
        {
            if (_dead)
                return;

            _walking = false;

            SetBoolIfPresent(
                walkBoolParameter,
                false);

            SetBoolIfPresent(
                idleBoolParameter,
                true);

            SetBoolIfPresent(
                dieBoolParameter,
                false);
        }

        public void PlayWalk()
        {
            if (_dead)
                return;

            _walking = true;

            SetBoolIfPresent(
                idleBoolParameter,
                false);

            SetBoolIfPresent(
                walkBoolParameter,
                true);

            SetBoolIfPresent(
                dieBoolParameter,
                false);
        }

        public void PlayHurt()
        {
            if (_dead)
                return;

            TriggerIfPresent(
                hurtTriggerParameter);

            // Preserve the underlying locomotion bools. Your Animator
            // transitions decide how Hurt exits back to Idle/Walk.
            if (_walking)
            {
                SetBoolIfPresent(
                    idleBoolParameter,
                    false);

                SetBoolIfPresent(
                    walkBoolParameter,
                    true);
            }
            else
            {
                SetBoolIfPresent(
                    walkBoolParameter,
                    false);

                SetBoolIfPresent(
                    idleBoolParameter,
                    true);
            }
        }

        public void PlayDie()
        {
            _dead = true;
            _walking = false;

            SetBoolIfPresent(
                idleBoolParameter,
                false);

            SetBoolIfPresent(
                walkBoolParameter,
                false);

            SetBoolIfPresent(
                dieBoolParameter,
                true);
        }

        public void ResetForRespawn()
        {
            _dead = false;
            _walking = false;

            SetVisible(
                true);

            ResetTriggerIfPresent(
                hurtTriggerParameter);

            SetBoolIfPresent(
                dieBoolParameter,
                false);

            SetBoolIfPresent(
                walkBoolParameter,
                false);

            SetBoolIfPresent(
                idleBoolParameter,
                true);
        }

        private void SetBoolIfPresent(
            string parameterName,
            bool value)
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(
                    parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (!HasParameter(
                    hash,
                    AnimatorControllerParameterType.Bool))
            {
                return;
            }

            animator.SetBool(
                hash,
                value);
        }

        private void TriggerIfPresent(
            string parameterName)
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(
                    parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (!HasParameter(
                    hash,
                    AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            animator.SetTrigger(
                hash);
        }

        private void ResetTriggerIfPresent(
            string parameterName)
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(
                    parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (!HasParameter(
                    hash,
                    AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            animator.ResetTrigger(
                hash);
        }

        private bool HasParameter(
            int parameterHash,
            AnimatorControllerParameterType type)
        {
            if (animator == null)
                return false;

            AnimatorControllerParameter[] parameters =
                animator.parameters;

            for (int i = 0;
                 i < parameters.Length;
                 i++)
            {
                AnimatorControllerParameter parameter =
                    parameters[i];

                if (parameter.nameHash ==
                        parameterHash &&
                    parameter.type ==
                        type)
                {
                    return true;
                }
            }

            return false;
        }

        public void FaceWorldPosition(
            Vector3 worldPosition,
            float degreesPerSecond,
            float deltaTime)
        {
            ResolveVisualRoot();

            if (visualRoot == null ||
                degreesPerSecond <= 0f ||
                deltaTime <= 0f)
            {
                return;
            }

            Vector3 toTarget =
                worldPosition -
                visualRoot.transform.position;

            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <
                0.0001f)
            {
                return;
            }

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    toTarget.normalized,
                    Vector3.up);

            visualRoot.transform.rotation =
                Quaternion.RotateTowards(
                    visualRoot.transform.rotation,
                    targetRotation,
                    degreesPerSecond *
                    deltaTime);
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot == null &&
                animator != null)
            {
                visualRoot =
                    animator.gameObject;
            }
        }
    }
}
