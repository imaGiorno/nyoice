using Nyoice.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.UI
{
    [DisallowMultipleComponent]
    public sealed class DiscomfortUI : MonoBehaviour
    {
        [SerializeField]
        private DiscomfortManager discomfortManager;

        [SerializeField]
        private GameStateManager gameStateManager;

        [SerializeField]
        private Text discomfortText;

        [SerializeField]
        private Slider discomfortSlider;

        [SerializeField]
        private Text gameOverText;

        public string DisplayedText => discomfortText != null ? discomfortText.text : string.Empty;
        public bool IsGameOverVisible => gameOverText != null && gameOverText.gameObject.activeSelf;

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void Configure(
            DiscomfortManager configuredDiscomfortManager,
            GameStateManager configuredGameStateManager,
            Text configuredDiscomfortText,
            Slider configuredDiscomfortSlider,
            Text configuredGameOverText)
        {
            Unsubscribe();
            discomfortManager = configuredDiscomfortManager;
            gameStateManager = configuredGameStateManager;
            discomfortText = configuredDiscomfortText;
            discomfortSlider = configuredDiscomfortSlider;
            gameOverText = configuredGameOverText;
            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            float current = discomfortManager != null
                ? discomfortManager.CurrentDiscomfort
                : 0f;
            float maximum = discomfortManager != null
                ? discomfortManager.MaxDiscomfort
                : 100f;

            if (discomfortText != null)
            {
                discomfortText.text = $"DISCOMFORT {Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}";
            }

            if (discomfortSlider != null)
            {
                discomfortSlider.minValue = 0f;
                discomfortSlider.maxValue = maximum;
                discomfortSlider.value = current;
            }

            if (gameOverText != null)
            {
                gameOverText.text = "GAME OVER";
                gameOverText.gameObject.SetActive(
                    gameStateManager != null && gameStateManager.IsGameOver);
            }
        }

        private void Subscribe()
        {
            if (discomfortManager != null)
            {
                discomfortManager.ValueChanged += HandleValueChanged;
            }

            if (gameStateManager != null)
            {
                gameStateManager.GameOver += HandleGameOver;
            }
        }

        private void Unsubscribe()
        {
            if (discomfortManager != null)
            {
                discomfortManager.ValueChanged -= HandleValueChanged;
            }

            if (gameStateManager != null)
            {
                gameStateManager.GameOver -= HandleGameOver;
            }
        }

        private void HandleValueChanged(float value)
        {
            Refresh();
        }

        private void HandleGameOver()
        {
            Refresh();
        }
    }
}
