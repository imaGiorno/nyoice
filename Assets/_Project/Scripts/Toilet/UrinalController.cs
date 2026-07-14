using Nyoice.NPC;
using UnityEngine;

namespace Nyoice.Toilet
{
    [DisallowMultipleComponent]
    public sealed class UrinalController : MonoBehaviour
    {
        private static readonly Color DisabledColor = new Color(0.38f, 0.4f, 0.42f);

        [SerializeField, Range(1, 8)]
        private int urinalNumber = 1;

        [SerializeField]
        private UrinalState state = UrinalState.Available;

        [SerializeField]
        private Transform movePoint;

        [SerializeField]
        private Transform usePoint;

        [SerializeField]
        private GameObject highlight;

        [SerializeField]
        private Renderer bodyRenderer;

        private Color _availableColor = Color.white;

        public int UrinalNumber => urinalNumber;
        public UrinalState State => state;
        public Transform MovePoint => movePoint;
        public Transform UsePoint => usePoint;
        public NPCController ReservedBy { get; private set; }
        public bool IsAvailable => state == UrinalState.Available;
        public bool IsSelected => highlight != null && highlight.activeSelf;

        public void Configure(
            int number,
            Transform movePointTransform,
            Transform usePointTransform,
            GameObject highlightObject,
            Renderer renderer)
        {
            urinalNumber = number;
            movePoint = movePointTransform;
            usePoint = usePointTransform;
            highlight = highlightObject;
            bodyRenderer = renderer;

            if (bodyRenderer != null)
            {
                _availableColor = bodyRenderer.sharedMaterial != null
                    ? bodyRenderer.sharedMaterial.color
                    : Color.white;
            }

            ApplyPresentation();
        }

        public bool Reserve(NPCController npc)
        {
            if (!IsAvailable || npc == null)
            {
                return false;
            }

            ReservedBy = npc;
            state = UrinalState.Reserved;
            ApplyPresentation();
            return true;
        }

        public bool Occupy(NPCController npc)
        {
            if (state != UrinalState.Reserved || ReservedBy != npc)
            {
                return false;
            }

            state = UrinalState.Occupied;
            ApplyPresentation();
            return true;
        }

        public void Release(NPCController npc)
        {
            if (ReservedBy != npc)
            {
                return;
            }

            ReservedBy = null;
            state = UrinalState.Available;
            ApplyPresentation();
        }

        public void SetSelected(bool selected)
        {
            if (highlight != null)
            {
                highlight.SetActive(selected && IsAvailable);
            }
        }

        private void ApplyPresentation()
        {
            SetSelected(false);
            if (bodyRenderer == null)
            {
                return;
            }

            bodyRenderer.material.color = IsAvailable ? _availableColor : DisabledColor;
        }
    }
}

