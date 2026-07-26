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
                if (IsReservationCandidate(urinal))
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
            if (!IsInputEnabled || ActiveSelectionNpc == null || urinal == null)
            {
                return false;
            }

            if (ActiveSelectionNpc.TargetUrinal == urinal)
            {
                return true;
            }

            if (ActiveSelectionNpc.TargetUrinal == null)
            {
                if (!IsReservationCandidate(urinal))
                {
                    return false;
                }

                if (ActiveSelectionNpc.CanAcceptUrinalSelection)
                {
                    if (!urinal.Reserve(ActiveSelectionNpc))
                    {
                        return false;
                    }

                    if (!ActiveSelectionNpc.AcceptUrinalAssignment(urinal))
                    {
                        urinal.Release(ActiveSelectionNpc);
                        return false;
                    }
                }

                ShowAssignment(urinal);
                return true;
            }

            return TryChangeAssignment(ActiveSelectionNpc, urinal);
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

            if (npc.TargetUrinal != null)
            {
                return npc.TargetUrinal;
            }

            UrinalController selected = CurrentSelection;
            if (!npc.CanAcceptUrinalSelection || !IsReservationCandidate(selected) ||
                !selected.Reserve(npc))
            {
                return null;
            }

            if (!npc.AcceptUrinalAssignment(selected))
            {
                selected.Release(npc);
                return null;
            }

            ShowAssignment(selected);
            return selected;
        }

        public bool ConfirmActiveSelection()
        {
            NPCController npc = ActiveSelectionNpc;
            return npc != null && ConfirmSelection(npc) != null;
        }

        public bool TryAssignAutomatic(NPCController npc)
        {
            if (!IsInputEnabled || npc == null || ActiveSelectionNpc != npc ||
                !npc.CanAcceptUrinalSelection)
            {
                return false;
            }

            UrinalController selected = GetAutomaticSelection();
            if (selected == null || !selected.Reserve(npc))
            {
                return false;
            }

            if (!npc.AcceptUrinalAssignment(selected))
            {
                selected.Release(npc);
                return false;
            }

            ShowAssignment(selected);
            Log($"{npc.name} automatically reserved Urinal{selected.UrinalNumber:00}");
            return true;
        }

        public bool TryChangeAssignment(NPCController npc, UrinalController replacement)
        {
            if (!IsInputEnabled || npc == null || npc != ActiveSelectionNpc ||
                !npc.CanChangeUrinalAssignment || replacement == null)
            {
                return false;
            }

            UrinalController previous = npc.TargetUrinal;
            if (replacement == previous)
            {
                return true;
            }

            if (!IsReservationCandidate(replacement) || !replacement.Reserve(npc))
            {
                return false;
            }

            if (!npc.ReplaceUrinalAssignment(previous, replacement))
            {
                replacement.Release(npc);
                return false;
            }

            if (!previous.Release(npc))
            {
                npc.RestoreUrinalAssignment(replacement, previous);
                replacement.Release(npc);
                ShowAssignment(previous);
                return false;
            }

            ShowAssignment(replacement);
            PlaySelectionSound();
            Log($"{npc.name} reassigned to Urinal{replacement.UrinalNumber:00}");
            return true;
        }

        public void MoveSelection(int direction)
        {
            if (!IsInputEnabled || direction == 0)
            {
                return;
            }

            int step = direction < 0 ? -1 : 1;
            int number = ActiveSelectionNpc != null && ActiveSelectionNpc.TargetUrinal != null
                ? ActiveSelectionNpc.TargetUrinal.UrinalNumber
                : UrinalCount;
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
                if (IsReservationCandidate(candidate))
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
            if (urinal == null)
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

        private static bool IsReservationCandidate(UrinalController urinal)
        {
            return urinal != null && urinal.IsAvailable && urinal.ReservedBy == null &&
                   urinal.MovePoint != null && urinal.UsePoint != null;
        }

        private void ShowAssignment(UrinalController urinal)
        {
            ClearSelection();
            CurrentSelection = urinal;
            CurrentSelection?.SetSelected(true);
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
