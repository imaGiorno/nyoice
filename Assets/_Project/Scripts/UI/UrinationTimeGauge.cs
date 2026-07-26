using Nyoice.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.UI
{
    [DisallowMultipleComponent]
    public sealed class UrinationTimeGauge : MonoBehaviour
    {
        public const int SegmentCount = 5;
        public const float SecondsPerSegment = 2f;

        [SerializeField]
        private NPCController npc;

        [SerializeField]
        private Canvas gaugeCanvas;

        [SerializeField]
        private Image[] segmentFills;

        public NPCController Npc => npc;
        public Canvas GaugeCanvas => gaugeCanvas;
        public Image[] SegmentFills => segmentFills;
        public bool IsGaugeVisible => gaugeCanvas != null && gaugeCanvas.enabled;
        public float DisplayedTime { get; private set; }

        private void Awake()
        {
            if (npc == null)
            {
                npc = GetComponentInParent<NPCController>();
            }

            if (gaugeCanvas == null)
            {
                gaugeCanvas = GetComponent<Canvas>();
            }

            Refresh();
        }

        private void LateUpdate()
        {
            if (npc == null || npc.IsGameOver)
            {
                return;
            }

            Refresh();
        }

        public void Configure(NPCController configuredNpc, Canvas canvas, Image[] fills)
        {
            npc = configuredNpc;
            gaugeCanvas = canvas;
            segmentFills = fills;
            Refresh();
        }

        public void Refresh()
        {
            if (npc == null || gaugeCanvas == null || segmentFills == null ||
                segmentFills.Length != SegmentCount)
            {
                return;
            }

            bool visible = npc.IsPresentationVisible &&
                           npc.State != NPCState.ReadyToLeave &&
                           npc.State != NPCState.Leaving &&
                           npc.State != NPCState.Finished;
            gaugeCanvas.enabled = visible;
            if (!visible)
            {
                return;
            }

            DisplayedTime = npc.IsUrinationStarted
                ? npc.RemainingUrinationTime
                : npc.AssignedUrinationDuration;
            ApplyTime(DisplayedTime);
        }

        public void ApplyTime(float timeSeconds)
        {
            DisplayedTime = Mathf.Max(0f, timeSeconds);
            if (segmentFills == null)
            {
                return;
            }

            for (int index = 0; index < segmentFills.Length; index++)
            {
                if (segmentFills[index] != null)
                {
                    segmentFills[index].fillAmount = CalculateFillAmount(DisplayedTime, index);
                }
            }
        }

        public static float CalculateFillAmount(float timeSeconds, int segmentIndex)
        {
            return Mathf.Clamp01((timeSeconds / SecondsPerSegment) - segmentIndex);
        }
    }
}
