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

        [SerializeField]
        private Image[] blockFills;

        [SerializeField]
        private bool useSeparatedLabel;

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
        public Image[] BlockFills => blockFills;

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
            Configure(
                configuredDiscomfortManager,
                configuredGameStateManager,
                configuredDiscomfortText,
                configuredDiscomfortSlider,
                configuredGameOverText,
                null,
                false);
        }

        public void Configure(
            DiscomfortManager configuredDiscomfortManager,
            GameStateManager configuredGameStateManager,
            Text configuredDiscomfortText,
            Slider configuredDiscomfortSlider,
            Text configuredGameOverText,
            Image[] configuredBlockFills,
            bool separatedLabel)
        {
            Unsubscribe();
            discomfortManager = configuredDiscomfortManager;
            gameStateManager = configuredGameStateManager;
            discomfortText = configuredDiscomfortText;
            discomfortSlider = configuredDiscomfortSlider;
            gameOverText = configuredGameOverText;
            blockFills = configuredBlockFills;
            useSeparatedLabel = separatedLabel;
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
                discomfortText.text = useSeparatedLabel
                    ? $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}"
                    : $"DISCOMFORT {Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}";
            }

            if (discomfortSlider != null)
            {
                discomfortSlider.minValue = 0f;
                discomfortSlider.maxValue = maximum;
                discomfortSlider.value = current;
            }

            UpdateBlockGauge(current, maximum);

            if (gameOverText != null)
            {
                gameOverText.text = "GAME OVER";
                gameOverText.gameObject.SetActive(isGameOver);
            }

            RefreshCount++;
        }

        public void UpdateBlockGauge(float current, float maximum)
        {
            if (blockFills == null || blockFills.Length == 0)
            {
                return;
            }

            float safeMaximum = Mathf.Max(0.01f, maximum);
            float filledBlocks = Mathf.Clamp01(current / safeMaximum) * blockFills.Length;
            for (int index = 0; index < blockFills.Length; index++)
            {
                Image fill = blockFills[index];
                if (fill == null)
                {
                    continue;
                }

                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = Mathf.Clamp01(filledBlocks - index);
                fill.color = GetBlockColor(index, blockFills.Length);
                fill.raycastTarget = false;
            }
        }

        private static Color GetBlockColor(int index, int blockCount)
        {
            float position = blockCount > 1 ? index / (float)(blockCount - 1) : 0f;
            if (position < 0.34f)
            {
                return new Color(0.2f, 0.78f, 0.35f);
            }

            if (position < 0.67f)
            {
                return new Color(0.95f, 0.82f, 0.2f);
            }

            if (position < 0.89f)
            {
                return new Color(1f, 0.5f, 0.12f);
            }

            return new Color(0.92f, 0.18f, 0.16f);
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

            if (blockFills == null || blockFills.Length == 0)
            {
                blockFills = FindBlockFills();
            }

            return HasResolvedReferences;
        }

        private Image[] FindBlockFills()
        {
            var result = new Image[10];
            Transform blocks = transform.Find("HUDRoot/HUDContainer/DiscomfortPanel/BlockGauge");
            if (blocks == null)
            {
                return result;
            }

            for (int index = 0; index < result.Length; index++)
            {
                result[index] = blocks.Find($"Block{index + 1:00}/Fill")?.GetComponent<Image>();
            }

            return result;
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
            foreach (T component in GetComponentsInChildren<T>(true))
            {
                if (component.name == childName)
                {
                    return component;
                }
            }

            return null;
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
