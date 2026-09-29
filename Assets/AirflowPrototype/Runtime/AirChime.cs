using UnityEngine;

namespace AirflowPrototype
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class AirChime : MonoBehaviour
    {
        [SerializeField] private int sequenceIndex;
        [SerializeField] private AirChimeTrial trial;
        [SerializeField] private AirChimeTrialSettings settings;

        private SphereCollider _trigger;
        private LineRenderer _outerRing;
        private LineRenderer _innerRing;
        private Material _runtimeMaterial;
        private AudioSource _audioSource;

        private bool _active;
        private bool _completed;
        private float _visualWeight;

        private static AudioClip[] _toneCache;

        public int SequenceIndex => sequenceIndex;
        public bool IsActive => _active;
        public bool IsCompleted => _completed;

        public void Configure(
            AirChimeTrial newTrial,
            AirChimeTrialSettings newSettings,
            int newSequenceIndex)
        {
            trial = newTrial;
            settings = newSettings;
            sequenceIndex = newSequenceIndex;

            if (Application.isPlaying)
            {
                BuildIfNeeded();
                RefreshVisualImmediate();
            }

            ApplyCollider();
        }

        public void SetState(
            bool active,
            bool completed)
        {
            _active = active;
            _completed = completed;
        }

        public void PlayCompletionPulse()
        {
            _active = false;
            _completed = true;
            _visualWeight = 1.35f;

            if (settings != null &&
                settings.enableProceduralChimeAudio)
            {
                PlayTone(true);
            }
        }

        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            if (Application.isPlaying)
                BuildIfNeeded();

            ApplyCollider();
        }

        private void Update()
        {
            if (settings == null)
                return;

            BuildIfNeeded();

            float dt = Time.unscaledDeltaTime;
            float targetWeight =
                _active
                    ? 1f
                    : _completed
                        ? 0.32f
                        : 0.08f;

            _visualWeight =
                Mathf.Lerp(
                    _visualWeight,
                    targetWeight,
                    1f - Mathf.Exp(-settings.stateResponse * dt));

            float rotationSpeed =
                _active
                    ? settings.activeRotationSpeed
                    : settings.idleRotationSpeed;

            transform.Rotate(
                Vector3.forward,
                rotationSpeed * dt,
                Space.Self);

            float pulse =
                _active
                    ? 1f +
                      Mathf.Sin(
                          Time.unscaledTime *
                          settings.activePulseSpeed *
                          Mathf.PI * 2f) *
                      settings.activePulseAmount
                    : 1f;

            if (_outerRing != null)
            {
                _outerRing.transform.localScale =
                    Vector3.one * pulse;
            }

            if (_innerRing != null)
            {
                _innerRing.transform.localScale =
                    Vector3.one *
                    Mathf.Lerp(0.72f, 0.88f, _visualWeight);
            }

            ApplyVisualColor();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_active ||
                trial == null)
            {
                return;
            }

            PlayerMotor motor =
                other.GetComponentInParent<PlayerMotor>();

            if (motor == null)
                return;

            trial.TryPassChime(
                this,
                motor);
        }

        private void BuildIfNeeded()
        {
            if (settings == null)
                return;

            if (_runtimeMaterial == null)
            {
                Shader shader =
                    Shader.Find("Universal Render Pipeline/Unlit");

                if (shader == null)
                    shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    _runtimeMaterial =
                        new Material(shader);

                    _runtimeMaterial.name =
                        "Runtime Air Chime Material";
                }
            }

            if (_outerRing == null)
            {
                _outerRing =
                    CreateRing(
                        "Outer Ring",
                        settings.chimeRadius,
                        settings.ringWidth);
            }

            if (_innerRing == null)
            {
                _innerRing =
                    CreateRing(
                        "Inner Ring",
                        settings.chimeRadius * 0.72f,
                        settings.ringWidth * 0.65f);

                _innerRing.transform.localRotation =
                    Quaternion.Euler(0f, 0f, 45f);
            }

            if (_audioSource == null)
            {
                _audioSource =
                    GetComponent<AudioSource>();

                if (_audioSource == null)
                {
                    _audioSource =
                        gameObject.AddComponent<AudioSource>();
                }

                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 1f;
                _audioSource.rolloffMode =
                    AudioRolloffMode.Linear;
                _audioSource.minDistance = 2f;
                _audioSource.maxDistance = 28f;
            }
        }

        private LineRenderer CreateRing(
            string ringName,
            float radius,
            float width)
        {
            GameObject go =
                new GameObject(ringName);

            go.transform.SetParent(
                transform,
                false);

            LineRenderer line =
                go.AddComponent<LineRenderer>();

            line.useWorldSpace = false;
            line.loop = true;
            line.alignment =
                LineAlignment.TransformZ;

            line.positionCount =
                Mathf.Max(12, settings.ringSegments);

            line.startWidth = width;
            line.endWidth = width;

            if (_runtimeMaterial != null)
                line.material = _runtimeMaterial;

            int count = line.positionCount;

            for (int i = 0;
                 i < count;
                 i++)
            {
                float angle =
                    i /
                    (float)count *
                    Mathf.PI *
                    2f;

                line.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f));
            }

            return line;
        }

        private void ApplyCollider()
        {
            if (_trigger == null)
                _trigger = GetComponent<SphereCollider>();

            if (_trigger == null ||
                settings == null)
            {
                return;
            }

            _trigger.isTrigger = true;
            _trigger.radius =
                settings.chimeRadius * 0.82f;
        }

        private void RefreshVisualImmediate()
        {
            _visualWeight =
                _active
                    ? 1f
                    : _completed
                        ? 0.32f
                        : 0.08f;

            ApplyVisualColor();
        }

        private void ApplyVisualColor()
        {
            if (settings == null)
                return;

            Color color =
                _active
                    ? settings.activeColor
                    : _completed
                        ? settings.completedColor
                        : settings.inactiveColor;

            if (_active &&
                trial != null &&
                trial.IsFinalChime(this))
            {
                color =
                    Color.Lerp(
                        settings.activeColor,
                        settings.finalColor,
                        0.55f);
            }

            color.a *=
                Mathf.Clamp(
                    _visualWeight,
                    0.12f,
                    1f);

            if (_outerRing != null)
            {
                _outerRing.startColor = color;
                _outerRing.endColor = color;
            }

            if (_innerRing != null)
            {
                Color inner =
                    color;

                inner.a *= 0.55f;

                _innerRing.startColor = inner;
                _innerRing.endColor = inner;
            }
        }

        internal void PlaySequenceTone()
        {
            if (settings == null ||
                !settings.enableProceduralChimeAudio)
            {
                return;
            }

            PlayTone(false);
        }

        private void PlayTone(
            bool completion)
        {
            BuildToneCache();

            if (_audioSource == null ||
                _toneCache == null ||
                _toneCache.Length == 0)
            {
                return;
            }

            int index =
                completion
                    ? _toneCache.Length - 1
                    : Mathf.Clamp(
                        sequenceIndex,
                        0,
                        _toneCache.Length - 2);

            _audioSource.PlayOneShot(
                _toneCache[index],
                completion
                    ? settings.completionVolume
                    : settings.chimeVolume);
        }

        private void BuildToneCache()
        {
            if (_toneCache != null &&
                _toneCache.Length >= 9)
            {
                return;
            }

            _toneCache =
                new AudioClip[9];

            for (int i = 0;
                 i < _toneCache.Length;
                 i++)
            {
                float semitone =
                    i * 2f;

                float frequency =
                    settings.baseFrequency *
                    Mathf.Pow(
                        2f,
                        semitone / 12f);

                _toneCache[i] =
                    CreateBellTone(
                        frequency,
                        settings.chimeDuration,
                        "Air Chime " + i);
            }
        }

        private static AudioClip CreateBellTone(
            float frequency,
            float duration,
            string clipName)
        {
            const int sampleRate = 44100;

            int samples =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        duration *
                        sampleRate));

            float[] data =
                new float[samples];

            for (int i = 0;
                 i < samples;
                 i++)
            {
                float t =
                    i /
                    (float)sampleRate;

                float normalized =
                    t /
                    Mathf.Max(
                        0.001f,
                        duration);

                float envelope =
                    Mathf.Exp(
                        -5.2f *
                        normalized);

                float fundamental =
                    Mathf.Sin(
                        Mathf.PI *
                        2f *
                        frequency *
                        t);

                float shimmer =
                    Mathf.Sin(
                        Mathf.PI *
                        2f *
                        frequency *
                        2.01f *
                        t) *
                    0.28f;

                data[i] =
                    (fundamental + shimmer) *
                    envelope *
                    0.42f;
            }

            AudioClip clip =
                AudioClip.Create(
                    clipName,
                    samples,
                    1,
                    sampleRate,
                    false);

            clip.SetData(
                data,
                0);

            return clip;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }
    }
}
