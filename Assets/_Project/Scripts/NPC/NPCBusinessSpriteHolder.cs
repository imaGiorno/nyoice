using UnityEngine;

namespace Nyoice.NPC
{
    [CreateAssetMenu(
        fileName = "NPCBusinessSpriteHolder",
        menuName = "Nyoice/NPC/Business Sprite Holder")]
    public sealed class NPCBusinessSpriteHolder : ScriptableObject
    {
        [SerializeField]
        private Sprite npcBusinessFront01;

        [SerializeField]
        private Sprite npcBusinessFront02;

        [SerializeField]
        private Sprite npcBusinessLeft01;

        [SerializeField]
        private Sprite npcBusinessLeft02;

        [SerializeField]
        private Sprite npcBusinessBackPee;

        public Sprite NpcBusinessFront01 => npcBusinessFront01;
        public Sprite NpcBusinessFront02 => npcBusinessFront02;
        public Sprite NpcBusinessLeft01 => npcBusinessLeft01;
        public Sprite NpcBusinessLeft02 => npcBusinessLeft02;
        public Sprite NpcBusinessBackPee => npcBusinessBackPee;
    }
}
