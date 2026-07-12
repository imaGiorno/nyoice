using UnityEngine;

namespace Nyoice.Core
{
    /// <summary>
    /// Marks the boundary where an NPC's urinal destination becomes final.
    /// Once an NPC crosses this line, its destination must not be changed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NyoiceLine : MonoBehaviour
    {
    }
}
