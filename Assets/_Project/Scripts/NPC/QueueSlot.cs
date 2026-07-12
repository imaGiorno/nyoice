using UnityEngine;

namespace Nyoice.NPC
{
    /// <summary>
    /// Represents one visible queue position and its current occupant.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QueueSlot : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int queueNumber;

        public int QueueNumber => queueNumber;
        public NPCController Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;

        public void Initialize(int number)
        {
            queueNumber = number;
        }

        public bool TryAssign(NPCController npc)
        {
            if (IsOccupied)
            {
                return false;
            }

            Occupant = npc;
            return true;
        }

        public void Clear(NPCController npc)
        {
            if (Occupant == npc)
            {
                Occupant = null;
            }
        }
    }
}
