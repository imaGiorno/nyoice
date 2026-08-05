using Nyoice.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.UI
{
    [DisallowMultipleComponent]
    public sealed class ScoreUI : MonoBehaviour
    {
        [SerializeField]
        private ScoreManager scoreManager;

        [SerializeField]
        private Text scoreText;

        [SerializeField]
        private Text comboText;

        [SerializeField]
        private Text processedCountText;

        [SerializeField]
        private bool useSeparatedLabels;

        private bool _isSubscribed;

        public string DisplayedScore => scoreText != null ? scoreText.text : string.Empty;
        public string DisplayedCombo => comboText != null ? comboText.text : string.Empty;
        public string DisplayedProcessedCount =>
            processedCountText != null ? processedCountText.text : string.Empty;
        public bool IsSubscribed => _isSubscribed;

        private void OnEnable()
        {
            EnsureRuntimeBindings();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(ScoreManager manager, Text configuredScoreText, Text configuredComboText)
        {
            Configure(manager, configuredScoreText, configuredComboText, null, false);
        }

        public void Configure(
            ScoreManager manager,
            Text configuredScoreText,
            Text configuredComboText,
            Text configuredProcessedCountText,
            bool separatedLabels)
        {
            Unsubscribe();
            scoreManager = manager;
            scoreText = configuredScoreText;
            comboText = configuredComboText;
            processedCountText = configuredProcessedCountText;
            useSeparatedLabels = separatedLabels;
            Subscribe();
            Refresh();
        }

        public bool EnsureRuntimeBindings()
        {
            scoreManager ??= FindAnyObjectByType<ScoreManager>(FindObjectsInactive.Include);
            scoreText ??= FindDescendantText("ScoreText");
            comboText ??= FindDescendantText("ComboText");
            if (useSeparatedLabels)
            {
                processedCountText ??= FindDescendantText("ProcessedCountText");
            }

            if (scoreManager == null || scoreText == null || comboText == null ||
                (useSeparatedLabels && processedCountText == null))
            {
                Unsubscribe();
                return false;
            }

            Subscribe();
            Refresh();
            return true;
        }

        public void Refresh()
        {
            int score = scoreManager != null ? scoreManager.CurrentScore : 0;
            float multiplier = scoreManager != null ? scoreManager.ComboMultiplier : 1f;
            int processedCount = scoreManager != null ? scoreManager.ProcessedNpcCount : 0;

            if (scoreText != null)
            {
                scoreText.text = useSeparatedLabels ? score.ToString() : $"SCORE {score}";
            }

            if (comboText != null)
            {
                comboText.text = useSeparatedLabels
                    ? $"\u00d7{multiplier:0.0}"
                    : $"COMBO \u00d7{multiplier:0.0}";
            }

            if (processedCountText != null)
            {
                processedCountText.text = $"{processedCount}\u4eba";
            }
        }

        private void Subscribe()
        {
            if (_isSubscribed || scoreManager == null)
            {
                return;
            }

            scoreManager.ScoreChanged += Refresh;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (_isSubscribed && scoreManager != null)
            {
                scoreManager.ScoreChanged -= Refresh;
            }

            _isSubscribed = false;
        }

        private Text FindDescendantText(string childName)
        {
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                if (text.name == childName)
                {
                    return text;
                }
            }

            return null;
        }
    }
}
