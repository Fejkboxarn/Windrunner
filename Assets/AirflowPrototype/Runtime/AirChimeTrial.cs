using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirChimeTrial : MonoBehaviour
    {
        [SerializeField] private PlayerMotor player;
        [SerializeField] private AirChimeTrialSettings settings;
        [SerializeField] private List<AirChime> chimes = new List<AirChime>();

        private bool _active;
        private bool _completed;
        private int _currentIndex;
        private float _trialElapsed;
        private float _groundedTimer;

        public bool IsActive => _active;
        public bool IsCompleted => _completed;
        public int CurrentIndex => _currentIndex;
        public int ChimeCount => chimes != null ? chimes.Count : 0;

        public event Action TrialStarted;
        public event Action<int> ChimePassed;
        public event Action TrialReset;
        public event Action TrialCompleted;

        public void Configure(
            PlayerMotor newPlayer,
            AirChimeTrialSettings newSettings,
            List<AirChime> newChimes)
        {
            player = newPlayer;
            settings = newSettings;
            chimes = newChimes ?? new List<AirChime>();

            SortAndConfigureChimes();
            SetWaitingState();
        }

        private void Awake()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerMotor>();

            SortAndConfigureChimes();
            SetWaitingState();
        }

        private void Update()
        {
            if (!_active ||
                _completed ||
                player == null ||
                settings == null)
            {
                return;
            }

            _trialElapsed += Time.deltaTime;

            if (!settings.resetWhenGrounded ||
                _currentIndex <= 0 ||
                _trialElapsed < settings.groundedResetGrace)
            {
                _groundedTimer = 0f;
                return;
            }

            if (player.IsGrounded)
            {
                _groundedTimer += Time.deltaTime;

                if (_groundedTimer >=
                    settings.groundedResetConfirmTime)
                {
                    ResetTrial();
                }
            }
            else
            {
                _groundedTimer = 0f;
            }
        }

        public void StartTrial(
            PlayerMotor triggeringPlayer)
        {
            if (triggeringPlayer != null)
                player = triggeringPlayer;

            if (chimes == null ||
                chimes.Count == 0)
            {
                return;
            }

            _active = true;
            _completed = false;
            _currentIndex = 0;
            _trialElapsed = 0f;
            _groundedTimer = 0f;

            RefreshChimeStates();

            TrialStarted?.Invoke();
        }

        public void ResetTrial()
        {
            _active = false;
            _completed = false;
            _currentIndex = 0;
            _trialElapsed = 0f;
            _groundedTimer = 0f;

            SetWaitingState();

            TrialReset?.Invoke();
        }

        public void TryPassChime(
            AirChime chime,
            PlayerMotor triggeringPlayer)
        {
            if (!_active ||
                _completed ||
                chime == null ||
                triggeringPlayer == null ||
                chimes == null ||
                _currentIndex < 0 ||
                _currentIndex >= chimes.Count)
            {
                return;
            }

            if (player != null &&
                triggeringPlayer != player)
            {
                return;
            }

            AirChime expected =
                chimes[_currentIndex];

            if (chime != expected)
                return;

            chime.PlaySequenceTone();

            int passedIndex =
                _currentIndex;

            _currentIndex++;

            ChimePassed?.Invoke(
                passedIndex);

            if (_currentIndex >= chimes.Count)
            {
                CompleteTrial();
                return;
            }

            RefreshChimeStates();
        }

        public bool IsFinalChime(
            AirChime chime)
        {
            return
                chime != null &&
                chimes != null &&
                chimes.Count > 0 &&
                chimes[chimes.Count - 1] == chime;
        }

        private void CompleteTrial()
        {
            _active = false;
            _completed = true;
            _groundedTimer = 0f;

            if (chimes != null &&
                chimes.Count > 0)
            {
                AirChime finalChime =
                    chimes[chimes.Count - 1];

                if (finalChime != null)
                    finalChime.PlayCompletionPulse();
            }

            RefreshChimeStates();

            TrialCompleted?.Invoke();
        }

        private void SortAndConfigureChimes()
        {
            if (chimes == null)
                chimes = new List<AirChime>();

            chimes.RemoveAll(
                chime => chime == null);

            if (chimes.Count == 0)
            {
                AirChime[] found =
                    GetComponentsInChildren<AirChime>(true);

                chimes.AddRange(found);
            }

            chimes.Sort(
                (a, b) =>
                    a.SequenceIndex.CompareTo(
                        b.SequenceIndex));

            for (int i = 0;
                 i < chimes.Count;
                 i++)
            {
                if (chimes[i] != null)
                {
                    chimes[i].Configure(
                        this,
                        settings,
                        i);
                }
            }
        }

        private void SetWaitingState()
        {
            if (chimes == null)
                return;

            for (int i = 0;
                 i < chimes.Count;
                 i++)
            {
                AirChime chime =
                    chimes[i];

                if (chime == null)
                    continue;

                chime.SetState(
                    false,
                    false);
            }
        }

        private void RefreshChimeStates()
        {
            if (chimes == null)
                return;

            for (int i = 0;
                 i < chimes.Count;
                 i++)
            {
                AirChime chime =
                    chimes[i];

                if (chime == null)
                    continue;

                bool completed =
                    _completed ||
                    i < _currentIndex;

                bool active =
                    _active &&
                    !_completed &&
                    i == _currentIndex;

                chime.SetState(
                    active,
                    completed);
            }
        }
    }
}
