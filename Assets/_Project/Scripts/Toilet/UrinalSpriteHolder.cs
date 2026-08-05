using UnityEngine;

namespace Nyoice.Toilet
{
    [CreateAssetMenu(
        fileName = "UrinalSpriteHolder",
        menuName = "Nyoice/Toilet/Urinal Sprite Holder")]
    public sealed class UrinalSpriteHolder : ScriptableObject
    {
        [SerializeField]
        private Sprite urinalNormal;

        [SerializeField]
        private Sprite urinalSelected;

        public Sprite UrinalNormal => urinalNormal;
        public Sprite UrinalSelected => urinalSelected;
    }
}
