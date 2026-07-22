using System.Collections.Generic;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEngine;

namespace Nyoice.Managers
{
    [DisallowMultipleComponent]
    public sealed class UrinalManager : MonoBehaviour
    {
        private const int UrinalCount = 8;

        [SerializeField]
        private UrinalController[] urinals;

        [SerializeField]
        private Camera inputCamera;

        [SerializeField]
        private AudioSource audioSource;

        [SerializeField]
        private AudioClip selectionClip;

        [SerializeField]
        private GameStateManager gameStateManager;

        [SerializeField]
        private bool enableDebugLogs = true;

        public UrinalController CurrentSelection { get; private set; }
        public NPCController ActiveSelectionNpc { get; private set; }
        public bool IsInputEnabled => gameStateManager == null || !gameStateManager.IsGameOver;

        private void Awake()
        {
            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (selectionClip == null)
            {
                selectionClip = CreateSelectionClip();
            }
        }

        private void Update()
        {
            if (!IsInputEnabled)
            {
                return;
            }

            bool leftPressed = Input.GetKeyDown(KeyCode.LeftArrow);
            bool rightPressed = Input.GetKeyDown(KeyCode.RightArrow);
            bool mousePressed = Input.GetMouseButtonDown(0);
            bool touchPressed = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;

            if (ActiveSelectionNpc == null)
            {
                LogIgnoredInput(leftPressed, rightPressed, mousePressed, touchPressed);
                return;
            }

            if (leftPressed)
            {
                Log("LeftArrow pressed");
                MoveSelection(-1);
                ConfirmActiveSelection();
            }
            else if (rightPressed)
            {
                Log("RightArrow pressed");
                MoveSelection(1);
                ConfirmActiveSelection();
            }

            if (touchPressed)
            {
                Vector2 touchPosition = Input.GetTouch(0).position;
                Log($"Screen tap detected at ({touchPosition.x:0}, {touchPosition.y:0})");
                SelectFromScreenPosition(touchPosition);
            }
            else if (mousePressed)
            {
                Vector2 mousePosition = Input.mousePosition;
                Log($"Screen click detected at ({mousePosition.x:0}, {mousePosition.y:0})");
                SelectFromScreenPosition(mousePosition);
            }
        }

        public void Configure(
            UrinalController[] configuredUrinals,
            Camera camera,
            AudioSource source)
        {
            ClearSelection();
            ActiveSelectionNpc = null;
            urinals = configuredUrinals;
            inputCamera = camera != null ? camera : Camera.main;
            audioSource = source;

            if (inputCamera == null)
            {
                Log("Main Camera reference is not available");
            }
        }

        public void ConfigureGameState(GameStateManager configuredGameStateManager)
        {
            if (gameStateManager != null)
            {
                gameStateManager.GameOver -= HandleGameOver;
            }

            gameStateManager = configuredGameStateManager;
            if (gameStateManager != null)
            {
                gameStateManager.GameOver += HandleGameOver;
                if (gameStateManager.IsGameOver)
                {
                    HandleGameOver();
                }
            }
        }

        private void OnDestroy()
        {
            if (gameStateManager != null)
            {
                gameStateManager.GameOver -= HandleGameOver;
            }
        }

        public IReadOnlyList<UrinalController> GetAvailableByPriority()
        {
            var available = new List<UrinalController>(UrinalCount);
            for (int number = UrinalCount; number >= 1; number--)
            {
                UrinalController urinal = GetUrinal(number);
                if (urinal != null && urinal.IsAvailable)
                {
                    available.Add(urinal);
                }
            }

            return available;
        }

        public UrinalController GetAutomaticSelection()
        {
            IReadOnlyList<UrinalController> available = GetAvailableByPriority();
            return available.Count > 0 ? available[0] : null;
        }

        public bool SelectUrinal(UrinalController urinal)
        {
            if (!IsInputEnabled || ActiveSelectionNpc == null || urinal == null || !urinal.IsAvailable)
            {
                return false;
            }

            if (CurrentSelection == urinal)
            {
                return true;
            }

            ClearSelection();
            CurrentSelection = urinal;
            CurrentSelection.SetSelected(true);
            PlaySelectionSound();
            Log($"Urinal{urinal.UrinalNumber:00} selected for {ActiveSelectionNpc.name}");
            return true;
        }

        public bool BeginSelection(NPCController npc)
        {
            if (!IsInputEnabled || npc == null ||
                (ActiveSelectionNpc != null && ActiveSelectionNpc != npc))
            {
                return false;
            }

            ClearSelection();
            ActiveSelectionNpc = npc;
            Log($"{npc.name} started urinal selection");
            return true;
        }

        public bool EndSelection(NPCController npc)
        {
            if (npc == null || ActiveSelectionNpc != npc)
            {
                return false;
            }

            ClearSelection();
            ActiveSelectionNpc = null;
            Log($"{npc.name} ended urinal selection");
            return true;
        }

        public UrinalController ConfirmSelection(NPCController npc)
        {
            if (!IsInputEnabled || npc == null || ActiveSelectionNpc != npc)
            {
                return null;
            }

            UrinalController selected = CurrentSelection;
            if (selected == null || !selected.IsAvailable)
            {
                selected = GetAutomaticSelection();
            }

            if (selected == null || !selected.Reserve(npc))
            {
                ClearSelection();
                return null;
            }

            ClearSelection();
            Log($"{npc.name} confirmed Urinal{selected.UrinalNumber:00}");
            Log($"{npc.name} reserved Urinal{selected.UrinalNumber:00}");
            return selected;
        }

        public bool ConfirmActiveSelection()
        {
            NPCController npc = ActiveSelectionNpc;
            if (npc == null || CurrentSelection == null)
            {
                return false;
            }

            UrinalController confirmedUrinal = ConfirmSelection(npc);
            if (confirmedUrinal == null)
            {
                return false;
            }

            if (npc.AcceptUrinalAssignment(confirmedUrinal))
            {
                return true;
            }

            confirmedUrinal.Release(npc);
            return false;
        }

        public void MoveSelection(int direction)
        {
            if (!IsInputEnabled || direction == 0)
            {
                return;
            }

            if (CurrentSelection == null || !CurrentSelection.IsAvailable)
            {
                SelectUrinal(GetAutomaticSelection());
                return;
            }

            int step = direction < 0 ? -1 : 1;
            int number = CurrentSelection.UrinalNumber;
            for (int attempt = 0; attempt < UrinalCount; attempt++)
            {
                number += step;
                if (number < 1)
                {
                    number = UrinalCount;
                }
                else if (number > UrinalCount)
                {
                    number = 1;
                }

                UrinalController candidate = GetUrinal(number);
                if (candidate != null && candidate.IsAvailable)
                {
                    SelectUrinal(candidate);
                    return;
                }
            }
        }

        private void SelectFromScreenPosition(Vector2 screenPosition)
        {
            Camera camera = inputCamera != null ? inputCamera : Camera.main;
            if (camera == null)
            {
                Log("Click did not hit a selectable urinal: Main Camera is not available");
                return;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                Log("Click did not hit a selectable urinal");
                return;
            }

            UrinalController urinal = hit.collider.GetComponentInParent<UrinalController>();
            if (urinal == null || !urinal.IsAvailable)
            {
                Log("Click did not hit a selectable urinal");
                return;
            }

            Log($"Click hit Urinal{urinal.UrinalNumber:00}");
            if (SelectUrinal(urinal))
            {
                ConfirmActiveSelection();
            }
        }

        private void LogIgnoredInput(
            bool leftPressed,
            bool rightPressed,
            bool mousePressed,
            bool touchPressed)
        {
            if (leftPressed)
            {
                Log("LeftArrow pressed but ignored because there is no ActiveSelectionNpc");
            }

            if (rightPressed)
            {
                Log("RightArrow pressed but ignored because there is no ActiveSelectionNpc");
            }

            if (mousePressed || touchPressed)
            {
                Log("Screen input ignored because there is no ActiveSelectionNpc");
            }
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

        private void ClearSelection()
        {
            if (CurrentSelection != null)
            {
                CurrentSelection.SetSelected(false);
                CurrentSelection = null;
            }
        }

        private void HandleGameOver()
        {
            ClearSelection();
            ActiveSelectionNpc = null;
            Log("Urinal selection input disabled because game is over");
        }

        private void PlaySelectionSound()
        {
            if (audioSource != null && selectionClip != null)
            {
                audioSource.PlayOneShot(selectionClip);
            }
        }

        private static AudioClip CreateSelectionClip()
        {
            const int sampleRate = 44100;
            const int sampleCount = 2205;
            var samples = new float[sampleCount];

            for (int index = 0; index < samples.Length; index++)
            {
                float time = index / (float)sampleRate;
                float fade = 1f - (index / (float)samples.Length);
                samples[index] = Mathf.Sin(2f * Mathf.PI * 880f * time) * 0.12f * fade;
            }

            AudioClip clip = AudioClip.Create("NyoiceSelection", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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
