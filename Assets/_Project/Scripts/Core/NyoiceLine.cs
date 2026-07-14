using Nyoice.NPC;
using UnityEngine;

namespace Nyoice.Core
{
    /// <summary>
    /// Confirms an NPC's urinal destination when the NPC crosses this trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NyoiceLine : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            NPCController npc = other.GetComponentInParent<NPCController>();
            if (npc != null)
            {
                npc.HandleNyoiceLineCrossed();
            }
        }
    }
}

