using UnityEngine;

namespace AirflowPrototype
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AirChimeStartSeal : MonoBehaviour
    {
        [SerializeField] private AirChimeTrial trial;
        [SerializeField] private AirChimeTrialSettings settings;

        private BoxCollider _trigger;
        private LineRenderer _ring;
        private Material _runtimeMaterial;
        private bool _playerInside;

        public void Configure(
            AirChimeTrial newTrial,
            AirChimeTrialSettings newSettings)
        {
            trial = newTrial;
            settings = newSettings;

            if (Application.isPlaying)
                BuildIfNeeded();

            ApplyCollider();
        }

        private void Awake()
        {
            _trigger =
                GetComponent<BoxCollider>();

            BuildIfNeeded();
            ApplyCollider();
        }

        private void Update()
        {
            if (_ring == null ||
                settings == null)
            {
                return;
            }

            bool active =
                trial != null &&
                trial.IsActive;

            bool completed =
                trial != null &&
                trial.IsCompleted;

            float spin =
                active
                    ? 45f
                    : 16f;

            transform.Rotate(
                Vector3.up,
                spin *
                Time.unscaledDeltaTime,
                Space.World);

            Color color =
                completed
                    ? settings.completedColor
                    : active
                        ? settings.activeColor
                        : settings.inactiveColor;

            if (!active &&
                !completed)
            {
                color.a =
                    Mathf.Max(
                        color.a,
                        0.42f);
            }

            _ring.startColor = color;
            _ring.endColor = color;
        }

        private void OnTriggerEnter(
            Collider other)
        {
            PlayerMotor motor =
                other.GetComponentInParent<PlayerMotor>();

            if (motor == null)
                return;

            _playerInside = true;

            if (trial != null &&
                !trial.IsActive)
            {
                trial.StartTrial(
                    motor);
            }
        }

        private void OnTriggerExit(
            Collider other)
        {
            PlayerMotor motor =
                other.GetComponentInParent<PlayerMotor>();

            if (motor != null)
                _playerInside = false;
        }

        private void BuildIfNeeded()
        {
            if (settings == null ||
                _ring != null)
            {
                return;
            }

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            if (shader == null)
                shader =
                    Shader.Find(
                        "Sprites/Default");

            if (shader != null)
            {
                _runtimeMaterial =
                    new Material(shader);

                _runtimeMaterial.name =
                    "Runtime Air Chime Start Seal";
            }

            GameObject ringObject =
                new GameObject(
                    "Start Seal Ring");

            ringObject.transform.SetParent(
                transform,
                false);

            _ring =
                ringObject.AddComponent<LineRenderer>();

            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.alignment =
                LineAlignment.TransformZ;

            _ring.positionCount =
                Mathf.Max(
                    12,
                    settings.ringSegments);

            _ring.startWidth =
                settings.startSealRingWidth;

            _ring.endWidth =
                settings.startSealRingWidth;

            if (_runtimeMaterial != null)
                _ring.material = _runtimeMaterial;

            for (int i = 0;
                 i < _ring.positionCount;
                 i++)
            {
                float angle =
                    i /
                    (float)_ring.positionCount *
                    Mathf.PI *
                    2f;

                _ring.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) *
                        settings.startSealRadius,
                        0.025f,
                        Mathf.Sin(angle) *
                        settings.startSealRadius));
            }
        }

        private void ApplyCollider()
        {
            if (_trigger == null)
                _trigger =
                    GetComponent<BoxCollider>();

            if (_trigger == null ||
                settings == null)
            {
                return;
            }

            _trigger.isTrigger = true;

            float size =
                settings.startSealRadius *
                2f;

            _trigger.size =
                new Vector3(
                    size,
                    0.6f,
                    size);

            _trigger.center =
                new Vector3(
                    0f,
                    0.25f,
                    0f);
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }
    }
}
