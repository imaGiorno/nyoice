using System;
using Nyoice.Toilet;
using UnityEngine;

namespace Nyoice.Managers
{
    [DisallowMultipleComponent]
    public sealed class DiscomfortManager : MonoBehaviour
    {
        private const float MaximumDiscomfort = 100f;
        private const float LogInterval = 10f;

        [SerializeField]
        private UrinalController[] urinals;

        [SerializeField]
        private GameStateManager gameStateManager;

        [SerializeField, Min(0f)]
        private float discomfortPerAdjacentPairPerSecond = 10f;

        [SerializeField]
        private bool enableDebugLogs = true;

        private int _lastAdjacentPairCount = -1;
        private int _lastLoggedStep;

        public event Action<float> ValueChanged;
        public event Action<int> AdjacentPairCountChanged;

        public float CurrentDiscomfort { get; private set; }
        public float MaxDiscomfort => MaximumDiscomfort;
        public float DiscomfortPerAdjacentPairPerSecond => discomfortPerAdjacentPairPerSecond;
        public int AdjacentPairCount => CountAdjacentPairs();

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        public void Configure(
            UrinalController[] configuredUrinals,
            GameStateManager configuredGameStateManager)
        {
            urinals = configuredUrinals;
            gameStateManager = configuredGameStateManager;
            SortUrinals();
            ReportAdjacentPairChange(CountAdjacentPairs());
        }

        public void ConfigureRate(float pointsPerPairPerSecond)
        {
            discomfortPerAdjacentPairPerSecond = Mathf.Max(0f, pointsPerPairPerSecond);
        }

        public void AdvanceTime(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || gameStateManager == null || gameStateManager.IsGameOver)
            {
                return;
            }

            int adjacentPairs = CountAdjacentPairs();
            ReportAdjacentPairChange(adjacentPairs);
            if (adjacentPairs == 0)
            {
                return;
            }

            float increase = adjacentPairs * discomfortPerAdjacentPairPerSecond * deltaSeconds;
            SetCurrentDiscomfort(CurrentDiscomfort + increase);
        }

        public int CountAdjacentPairs()
        {
            int pairCount = 0;
            for (int number = 1; number < 8; number++)
            {
                UrinalController left = GetUrinal(number);
                UrinalController right = GetUrinal(number + 1);
                if (left != null && right != null && left.IsOccupied && right.IsOccupied)
                {
                    pairCount++;
                }
            }

            return pairCount;
        }

        private void SetCurrentDiscomfort(float value)
        {
            float clampedValue = Mathf.Clamp(value, 0f, MaximumDiscomfort);
            if (Mathf.Approximately(CurrentDiscomfort, clampedValue))
            {
                return;
            }

            CurrentDiscomfort = clampedValue;
            ValueChanged?.Invoke(CurrentDiscomfort);

            int currentStep = Mathf.FloorToInt(CurrentDiscomfort / LogInterval);
            if (currentStep > _lastLoggedStep)
            {
                _lastLoggedStep = currentStep;
                Log($"Discomfort: {CurrentDiscomfort:0.0} / {MaximumDiscomfort:0}");
            }

            if (CurrentDiscomfort >= MaximumDiscomfort)
            {
                Log("Discomfort reached maximum");
                gameStateManager.TriggerGameOver();
            }
        }

        private void ReportAdjacentPairChange(int adjacentPairs)
        {
            if (_lastAdjacentPairCount == adjacentPairs)
            {
                return;
            }

            _lastAdjacentPairCount = adjacentPairs;
            AdjacentPairCountChanged?.Invoke(adjacentPairs);
            Log($"Adjacent pairs: {adjacentPairs}");
            Log(
                $"Discomfort rate: {adjacentPairs * discomfortPerAdjacentPairPerSecond:0.0} per second");
        }

        private UrinalController GetUrinal(int number)
        {
            if (urinals == null)
            {
                return null;
            }

            foreach (UrinalController urinal in urinals)
            {
                if (urinal != null && urinal.UrinalNumber == number)
                {
                    return urinal;
                }
            }

            return null;
        }

        private void SortUrinals()
        {
            if (urinals == null)
            {
                return;
            }

            Array.Sort(
                urinals,
                (left, right) => GetUrinalNumber(left).CompareTo(GetUrinalNumber(right)));
        }

        private static int GetUrinalNumber(UrinalController urinal)
        {
            return urinal != null ? urinal.UrinalNumber : int.MaxValue;
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log(message, this);
            }
        }
    }
}
