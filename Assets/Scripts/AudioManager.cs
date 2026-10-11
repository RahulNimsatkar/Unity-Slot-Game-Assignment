using UnityEngine;

namespace SlotGame
{
    /// <summary>
    /// Thin wrapper around two AudioSources: one for one-shot effects and a
    /// dedicated looping source for the reel spin sound.
    ///
    /// All clips are procedurally generated at edit time by the scene builder
    /// (no third-party audio, no licensing headaches) and assigned in the scene.
    /// Every Play method is null-safe so audio can never break gameplay.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Sources (assigned by the scene builder)")]
        [Tooltip("Source for one-shot effects (clicks, stops, jingles).")]
        [SerializeField] private AudioSource _sfxSource;

        [Tooltip("Dedicated looping source for the reel spin sound.")]
        [SerializeField] private AudioSource _loopSource;

        [Header("Clips")]
        [SerializeField] private AudioClip _buttonClick;
        [SerializeField] private AudioClip _leverPull;
        [SerializeField] private AudioClip _reelStop;
        [SerializeField] private AudioClip _winJingle;
        [SerializeField] private AudioClip _jackpotJingle;
        [SerializeField] private AudioClip _loseSting;
        [SerializeField] private AudioClip _spinLoop;

        public void PlayClick() => Play(_buttonClick);
        public void PlayLever() => Play(_leverPull);
        public void PlayReelStop() => Play(_reelStop);
        public void PlayWin() => Play(_winJingle);
        public void PlayJackpot() => Play(_jackpotJingle);
        public void PlayLose() => Play(_loseSting);

        /// <summary>Starts the looping reel sound (called when a spin begins).</summary>
        public void StartSpinLoop()
        {
            if (_loopSource == null || _spinLoop == null)
                return;

            _loopSource.clip = _spinLoop;
            _loopSource.loop = true;
            if (!_loopSource.isPlaying)
                _loopSource.Play();
        }

        /// <summary>Stops the looping reel sound (called when all reels stopped).</summary>
        public void StopSpinLoop()
        {
            if (_loopSource != null)
                _loopSource.Stop();
        }

        private void Play(AudioClip clip)
        {
            if (_sfxSource != null && clip != null)
                _sfxSource.PlayOneShot(clip);
        }
    }
}
