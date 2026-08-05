using UnityEngine;

namespace Nyoice.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField]
        private AudioSource seSource;

        [SerializeField]
        private AudioClipHolder clipHolder;

        public AudioSource SeSource => seSource;
        public AudioClipHolder ClipHolder => clipHolder;

        public void Configure(AudioSource source, AudioClipHolder holder)
        {
            seSource = source;
            clipHolder = holder;
        }

        public void PlaySE(SoundEffectId id)
        {
            if (clipHolder == null || !clipHolder.TryGetClip(id, out AudioClip clip))
            {
                return;
            }

            PlaySE(clip);
        }

        public void PlaySE(AudioClip clip)
        {
            if (seSource == null || clip == null)
            {
                return;
            }

            seSource.PlayOneShot(clip);
        }

        public void StopSE()
        {
            if (seSource != null)
            {
                seSource.Stop();
            }
        }
    }
}
