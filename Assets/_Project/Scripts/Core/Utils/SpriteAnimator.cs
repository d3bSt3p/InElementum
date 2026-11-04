using InElementum.Data.Characters;
using UnityEngine;

namespace InElementum.Core.Utils
{
    /// <summary>
    /// Flips a <see cref="SpriteRenderer"/> through the clips of a <see cref="CharacterSpriteSet"/>. Looping clips
    /// cycle; the rest hold their last frame, or hand over to a follow-up clip when played with one.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private CharacterSpriteSet spriteSet;
        [SerializeField] private CharacterAnimation startAnimation = CharacterAnimation.Idle;

        private SpriteRenderer _renderer;
        private SpriteClip _clip;
        private float _time;
        private float _speed = 1f;
        private CharacterAnimation? _then;

        public CharacterAnimation Current { get; private set; }
        public SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();

        /// <summary>Swapping the set keeps the current pose, so a character can change looks mid-stride.</summary>
        public CharacterSpriteSet SpriteSet
        {
            get => spriteSet;
            set
            {
                if (spriteSet == value) return;
                spriteSet = value;
                var time = _time;
                var then = _then;
                Play(Current, true);
                _time = time;
                _then = then;
                Refresh();
            }
        }

        /// <summary>True once a non-looping clip has shown its last frame for a full frame.</summary>
        public bool IsFinished => _clip == null || (!_clip.Loop && _time >= _clip.Duration);

        public bool FlipX
        {
            get => Renderer.flipX;
            set => Renderer.flipX = value;
        }

        private void Awake()
        {
            if (_clip == null) Play(startAnimation, true);
        }

        /// <summary>
        /// Starts <paramref name="animation"/>; does nothing if it is already playing, unless <paramref name="restart"/>.
        /// Returns the clip's length in seconds at <paramref name="speed"/>, or 0 when the set has nothing to show.
        /// </summary>
        public float Play(CharacterAnimation animation, bool restart = false, float speed = 1f)
        {
            _then = null;
            _speed = Mathf.Max(0.01f, speed);
            if (!restart && Current == animation && _clip != null) return _clip.Duration / _speed;

            Current = animation;
            _clip = spriteSet != null ? spriteSet.Find(animation) : null;
            _time = 0f;
            Refresh();
            return _clip != null ? _clip.Duration / _speed : 0f;
        }

        /// <summary>Plays <paramref name="animation"/> from the start, then <paramref name="then"/>.</summary>
        public float PlayOnce(CharacterAnimation animation, CharacterAnimation then, float speed = 1f)
        {
            var duration = Play(animation, true, speed);
            _then = then;
            return duration;
        }

        /// <summary>Seconds from the start of <paramref name="animation"/> to its impact frame.</summary>
        public float ImpactTime(CharacterAnimation animation, float speed = 1f)
        {
            var clip = spriteSet != null ? spriteSet.Find(animation) : null;
            return clip != null ? clip.ImpactTime / Mathf.Max(0.01f, speed) : 0f;
        }

        private void Update()
        {
            if (_clip == null) return;

            _time += Time.deltaTime * _speed;
            if (_then.HasValue && !_clip.Loop && _time >= _clip.Duration)
            {
                Play(_then.Value, true);
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_clip == null || _clip.IsEmpty) return;

            var count = _clip.Frames.Count;
            var frame = Mathf.FloorToInt(_time * _clip.FramesPerSecond);
            frame = _clip.Loop ? frame % count : Mathf.Min(frame, count - 1);
            Renderer.sprite = _clip.Frames[Mathf.Max(0, frame)];
        }
    }
}
