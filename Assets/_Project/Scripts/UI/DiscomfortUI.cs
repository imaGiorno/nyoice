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

        private bool _isSubscribed;
        private bool _hasLoggedInitializationError;

        public string DisplayedText => discomfortText != null ? discomfortText.text : string.Empty;
        public bool IsGameOverVisible => gameOverText != null && gameOverText.gameObject.activeSelf;
        public bool IsSubscribed => _isSubscribed;
        public bool HasResolvedReferences => discomfortManager != null
            && gameStateManager != null
            && discomfortText != null
            && discomfortSlider != null
            && gameOverText != null;
        public int RefreshCount { get; private set; }

        private void Awake()
        {
            EnsureRuntimeBindings();
        }

        private void OnEnable()
        {
            EnsureRuntimeBindings();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

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
            _hasLoggedInitializationError = false;

            if (Application.isPlaying && isActiveAndEnabled)
            {
                Subscribe();
            }

            Refresh();
        }

        public bool EnsureRuntimeBindings()
        {
            bool resolved = ResolveReferences();
            if (!resolved)
            {
                if (!_hasLoggedInitializationError)
                {
                    _hasLoggedInitializationError = true;
                    Debug.LogError(
                        "DiscomfortUI could not resolve all required manager and UI references.",
                        this);
                }

                Unsubscribe();
                return false;
            }

            _hasLoggedInitializationError = false;
            Subscribe();
            Refresh();
            return true;
        }

        public void Refresh()
        {
            float current = discomfortManager != null
                ? discomfortManager.CurrentDiscomfort
                : 0f;
            float maximum = discomfortManager != null
                ? discomfortManager.MaxDiscomfort
                : 100f;
            bool isGameOver = gameStateManager != null && gameStateManager.IsGameOver;
            if (isGameOver)
            {
                current = maximum;
            }

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
                gameOverText.gameObject.SetActive(isGameOver);
            }

            RefreshCount++;
        }

        private void Subscribe()
        {
            if (_isSubscribed || discomfortManager == null || gameStateManager == null)
            {
                return;
            }

            discomfortManager.ValueChanged += HandleValueChanged;
            gameStateManager.GameOver += HandleGameOver;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
            {
                return;
            }

            if (discomfortManager != null)
            {
                discomfortManager.ValueChanged -= HandleValueChanged;
            }

            if (gameStateManager != null)
            {
                gameStateManager.GameOver -= HandleGameOver;
            }

            _isSubscribed = false;
        }

        private bool ResolveReferences()
        {
            if (discomfortManager == null)
            {
                discomfortManager = FindManager<DiscomfortManager>();
            }

            if (gameStateManager == null)
            {
                gameStateManager = FindManager<GameStateManager>();
            }

            if (discomfortText == null)
            {
                discomfortText = FindDirectChildComponent<Text>("DiscomfortText");
            }

            if (discomfortSlider == null)
            {
                discomfortSlider = FindDirectChildComponent<Slider>("DiscomfortSlider");
            }

            if (gameOverText == null)
            {
                gameOverText = FindDirectChildComponent<Text>("GameOverText");
            }

            return HasResolvedReferences;
        }

        private T FindManager<T>()
            where T : Component
        {
            T managerInValidationRoot = transform.root.GetComponentInChildren<T>(true);
            return managerInValidationRoot != null
                ? managerInValidationRoot
                : FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }

        private T FindDirectChildComponent<T>(string childName)
            where T : Component
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
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
