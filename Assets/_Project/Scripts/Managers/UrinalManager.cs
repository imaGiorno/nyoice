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
        private bool enableDebugLogs = true;

        public UrinalController CurrentSelection { get; private set; }

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
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveSelection(-1);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveSelection(1);
            }

            if (Input.GetMouseButtonDown(0))
            {
                SelectFromScreenPosition(Input.mousePosition);
            }

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    SelectFromScreenPosition(touch.position);
                }
            }
        }

        public void Configure(
            UrinalController[] configuredUrinals,
            Camera camera,
            AudioSource source)
        {
            ClearSelection();
            urinals = configuredUrinals;
            inputCamera = camera;
            audioSource = source;
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
            if (urinal == null || !urinal.IsAvailable)
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
            Log($"Urinal{urinal.UrinalNumber:00} selected");
            return true;
        }

        public UrinalController ConfirmSelection(NPCController npc)
        {
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
            Log($"{npc.name} reserved Urinal{selected.UrinalNumber:00}");
            return selected;
        }

        public void MoveSelection(int direction)
        {
            if (direction == 0)
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
                return;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                return;
            }

            UrinalController urinal = hit.collider.GetComponentInParent<UrinalController>();
            SelectUrinal(urinal);
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

