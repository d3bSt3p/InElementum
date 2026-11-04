using System;
using System.Collections.Generic;
using UnityEngine;

namespace InElementum.Data.Characters
{
    /// <summary>The poses a character's sprite set can hold. Combat and exploration ask for these by name.</summary>
    public enum CharacterAnimation
    {
        Idle,
        Run,
        Jump,
        Fall,
        Land,
        Attack,
        Ability,
        Surge,
        Counter,
        Parry,
        Dodge,
        Hurt,
        Death,
        /// <summary>The combat stance between actions, weapon drawn.</summary>
        BattleIdle
    }

    /// <summary>One flipbook: frames played at a fixed rate, looping or holding the last frame.</summary>
    [Serializable]
    public class SpriteClip
    {
        [SerializeField] private CharacterAnimation animation;
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField] [Min(1f)] private float framesPerSecond = 12f;
        [SerializeField] private bool loop;
        [Tooltip("The frame where a strike connects. Combat starts the strike this many frames before the hit lands.")]
        [SerializeField] [Min(0)] private int impactFrame;

        public SpriteClip(CharacterAnimation animation, Sprite[] frames, float framesPerSecond, bool loop, int impactFrame = 0)
        {
            this.animation = animation;
            this.frames = frames;
            this.framesPerSecond = framesPerSecond;
            this.loop = loop;
            this.impactFrame = impactFrame;
        }

        public CharacterAnimation Animation => animation;
        public IReadOnlyList<Sprite> Frames => frames;
        public float FramesPerSecond => framesPerSecond;
        public bool Loop => loop;
        public int ImpactFrame => impactFrame;
        public float Duration => frames.Length / framesPerSecond;
        public float ImpactTime => Mathf.Min(impactFrame, Mathf.Max(0, frames.Length - 1)) / framesPerSecond;
        public bool IsEmpty => frames == null || frames.Length == 0;
    }

    /// <summary>
    /// Every animation for one character look. Built from the placeholder sheets by the Character Art builder; a
    /// pose the set lacks falls back to a close one (a missing Fall plays Jump, a missing Surge plays Ability).
    /// </summary>
    [CreateAssetMenu(fileName = "Sprite Set", menuName = "In Elementum/Characters/Sprite Set", order = 5)]
    public class CharacterSpriteSet : ScriptableObject
    {
        [SerializeField] private List<SpriteClip> clips = new List<SpriteClip>();

        public IReadOnlyList<SpriteClip> Clips => clips;

        internal void SetClips(IEnumerable<SpriteClip> value) => clips = new List<SpriteClip>(value);

        /// <summary>The clip for <paramref name="animation"/>, or its nearest stand-in, or null when nothing fits.</summary>
        public SpriteClip Find(CharacterAnimation animation)
        {
            for (var current = (CharacterAnimation?)animation; current.HasValue; current = Fallback(current.Value))
            {
                foreach (var clip in clips)
                {
                    if (clip != null && clip.Animation == current.Value && !clip.IsEmpty) return clip;
                }
            }

            return null;
        }

        private static CharacterAnimation? Fallback(CharacterAnimation animation)
        {
            switch (animation)
            {
                case CharacterAnimation.Fall: return CharacterAnimation.Jump;
                case CharacterAnimation.Jump: return CharacterAnimation.Idle;
                case CharacterAnimation.Land: return CharacterAnimation.Idle;
                case CharacterAnimation.Run: return CharacterAnimation.Idle;
                case CharacterAnimation.Surge: return CharacterAnimation.Ability;
                case CharacterAnimation.Ability: return CharacterAnimation.Attack;
                case CharacterAnimation.Counter: return CharacterAnimation.Attack;
                case CharacterAnimation.Parry: return CharacterAnimation.Attack;
                case CharacterAnimation.Attack: return CharacterAnimation.Idle;
                case CharacterAnimation.Dodge: return CharacterAnimation.Jump;
                case CharacterAnimation.Hurt: return CharacterAnimation.Idle;
                case CharacterAnimation.Death: return CharacterAnimation.Hurt;
                case CharacterAnimation.BattleIdle: return CharacterAnimation.Idle;
                default: return null;
            }
        }
    }
}
