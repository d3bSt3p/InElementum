using UnityEngine;

namespace InElementum.Core.Utils
{
    /// <summary>
    /// A short freeze of game time for impact: <see cref="Play"/> holds <see cref="Time.timeScale"/> at 0 for a few
    /// hundredths of a second, then puts back the scale it found. Anything else that changes the time scale (a menu
    /// pausing, the character wheel slowing time) should call <see cref="Release"/> first, so the freeze never hands
    /// them a scale of 0 to save, nor overwrites theirs when it ends.
    /// </summary>
    public static class HitStop
    {
        private static float _until;
        private static float _scaleBefore = 1f;
        private static Runner _runner;

        /// <summary>True while a freeze holds time.</summary>
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _until = 0f;
            _scaleBefore = 1f;
            _runner = null;
            Active = false;
        }

        /// <summary>
        /// Freezes time for <paramref name="seconds"/> of real time, or stretches a freeze already running. Does nothing
        /// while time is already stopped by something else, such as a pause menu.
        /// </summary>
        public static void Play(float seconds)
        {
            if (seconds <= 0f || !Application.isPlaying) return;
            if (!Active)
            {
                if (Time.timeScale <= 0f) return;
                _scaleBefore = Time.timeScale;
                Active = true;
                EnsureRunner();
            }

            _until = Mathf.Max(_until, Time.unscaledTime + seconds);
            Time.timeScale = 0f;
        }

        /// <summary>Ends any freeze now, restoring the time scale it found.</summary>
        public static void Release()
        {
            if (!Active) return;
            Active = false;
            // Leave the scale alone if someone else has set one since.
            if (Time.timeScale <= 0f) Time.timeScale = _scaleBefore;
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            var host = new GameObject("Hit Stop") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<Runner>();
        }

        /// <summary>Ends the freeze on time; runs on real time, since game time is stopped.</summary>
        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (Active && Time.unscaledTime >= _until) Release();
            }

            private void OnDestroy()
            {
                Release();
                if (_runner == this) _runner = null;
            }
        }
    }
}
