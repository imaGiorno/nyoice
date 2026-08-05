using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyoice.Audio
{
    [CreateAssetMenu(fileName = "AudioClipHolder", menuName = "Nyoice/Audio/Audio Clip Holder")]
    public sealed class AudioClipHolder : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField]
            private SoundEffectId id;

            [SerializeField]
            private AudioClip clip;

            public SoundEffectId Id => id;
            public AudioClip Clip => clip;
        }

        [SerializeField]
        private List<Entry> soundEffects = new List<Entry>();

        public IReadOnlyList<Entry> SoundEffects => soundEffects;

        public bool TryGetClip(SoundEffectId id, out AudioClip clip)
        {
            foreach (Entry entry in soundEffects)
            {
                if (entry != null && entry.Id == id)
                {
                    clip = entry.Clip;
                    return clip != null;
                }
            }

            clip = null;
            return false;
        }
    }
}
