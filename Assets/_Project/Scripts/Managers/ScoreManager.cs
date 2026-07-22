using System;
using UnityEngine;

namespace Nyoice.Managers
{
    [DisallowMultipleComponent]
    public sealed class ScoreManager : MonoBehaviour
    {
        private const int BaseScore = 100;
        private const float ComboStepSeconds = 5f;

        private static readonly float[] ComboStages =
        {
            1.0f,
            1.5f,
            2.0f,
            2.5f,
            3.0f
        };

        [SerializeField]
        private DiscomfortManager discomfortManager;

        [SerializeField]
        private GameStateManager gameStateManager;

        private bool _isSubscribed;
        private int _comboStageIndex;

        public event Action ScoreChanged;

        public int CurrentScore { get; private set; }
        public float ComboMultiplier => ComboStages[_comboStageIndex];
        public int ComboStageIndex => _comboStageIndex;
        public int ProcessedNpcCount { get; private set; }
        public float NoAdjacencyElapsed { get; private set; }
        public float NoAdjacencySecondsPerCombo => ComboStepSeconds;
        public int BaseScoreValue => BaseScore;
        public bool IsComboTimingStarted { get; private set; }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        public void Configure(
            DiscomfortManager configuredDiscomfortManager,
            GameStateManager configuredGameStateManager)
        {
            Unsubscribe();
            discomfortManager = configuredDiscomfortManager;
            gameStateManager = configuredGameStateManager;
            Subscribe();
            ResetComboIfAdjacent();
        }

        public void AdvanceTime(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || !IsComboTimingStarted || IsBlocked || HasAdjacency() ||
                _comboStageIndex >= ComboStages.Length - 1)
            {
                return;
            }

            NoAdjacencyElapsed += deltaSeconds;
            bool changed = false;
            while (NoAdjacencyElapsed >= ComboStepSeconds &&
                   _comboStageIndex < ComboStages.Length - 1)
            {
                NoAdjacencyElapsed -= ComboStepSeconds;
                _comboStageIndex++;
                changed = true;
            }

            if (_comboStageIndex >= ComboStages.Length - 1)
            {
                NoAdjacencyElapsed = 0f;
            }

            if (changed)
            {
                ScoreChanged?.Invoke();
            }
        }

        public bool NotifyNpcFinished()
        {
            if (IsBlocked)
            {
                return false;
            }

            ProcessedNpcCount++;
            CurrentScore += Mathf.RoundToInt(BaseScore * ComboMultiplier);
            ScoreChanged?.Invoke();
            return true;
        }

        public bool NotifyUrinalUseStarted()
        {
            if (IsBlocked || IsComboTimingStarted)
            {
                return false;
            }

            IsComboTimingStarted = true;
            NoAdjacencyElapsed = 0f;
            ScoreChanged?.Invoke();
            return true;
        }

        public void ResetSession()
        {
            CurrentScore = 0;
            ProcessedNpcCount = 0;
            _comboStageIndex = 0;
            NoAdjacencyElapsed = 0f;
            IsComboTimingStarted = false;
            ScoreChanged?.Invoke();
        }

        private bool IsBlocked => gameStateManager == null || gameStateManager.IsGameOver;

        private bool HasAdjacency()
        {
            return discomfortManager != null && discomfortManager.AdjacentPairCount > 0;
        }

        private void Subscribe()
        {
            if (_isSubscribed || discomfortManager == null)
            {
                return;
            }

            discomfortManager.AdjacentPairCountChanged += HandleAdjacentPairCountChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (_isSubscribed && discomfortManager != null)
            {
                discomfortManager.AdjacentPairCountChanged -= HandleAdjacentPairCountChanged;
            }

            _isSubscribed = false;
        }

        private void HandleAdjacentPairCountChanged(int pairCount)
        {
            if (pairCount > 0)
            {
                ResetCombo();
            }
        }

        private void ResetComboIfAdjacent()
        {
            if (HasAdjacency())
            {
                ResetCombo();
            }
        }

        private void ResetCombo()
        {
            bool changed = _comboStageIndex != 0 || NoAdjacencyElapsed > 0f;
            _comboStageIndex = 0;
            NoAdjacencyElapsed = 0f;
            if (changed)
            {
                ScoreChanged?.Invoke();
            }
        }
    }
}
