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

        private bool _isSubscribed;

        public string DisplayedScore => scoreText != null ? scoreText.text : string.Empty;
        public string DisplayedCombo => comboText != null ? comboText.text : string.Empty;
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
            Unsubscribe();
            scoreManager = manager;
            scoreText = configuredScoreText;
            comboText = configuredComboText;
            Subscribe();
            Refresh();
        }

        public bool EnsureRuntimeBindings()
        {
            scoreManager ??= FindAnyObjectByType<ScoreManager>(FindObjectsInactive.Include);
            scoreText ??= FindChildText("ScoreText");
            comboText ??= FindChildText("ComboText");
            if (scoreManager == null || scoreText == null || comboText == null)
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
            if (scoreText != null)
            {
                scoreText.text = $"SCORE {scoreManager?.CurrentScore ?? 0}";
            }

            if (comboText != null)
            {
                float multiplier = scoreManager != null ? scoreManager.ComboMultiplier : 1f;
                comboText.text = $"COMBO ×{multiplier:0.0}";
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

        private Text FindChildText(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Text>() : null;
        }
    }
}
