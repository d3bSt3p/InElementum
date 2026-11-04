using System.Collections;
using InElementum.Combat.Interfaces;
using InElementum.Combat.Managers;
using InElementum.Combat.Model;
using InElementum.Core.Managers;
using InElementum.Core.Utils;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Combat.Characters
{
    /// <summary>
    /// The scene view of one <see cref="Model.Combatant"/>. It holds no rules: it plays movement and hit feedback,
    /// and fades when its combatant is incapacitated.
    /// </summary>
    public abstract class CharacterBase : MonoBehaviour, ICharacter
    {
        [Header("Base settings")]
        [SerializeField] private CharacterType characterType;
        [SerializeField] private Transform textSpawnRoot;

        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;
        private Vector3 _home;
        private SpriteAnimator _animator;
        private float _homeFacing = 1f;

        #region Cache
        public Combatant Combatant { get; private set; }
        public CharacterStats CharacterStats => Combatant?.Stats;
        public CharacterType CharacterType => characterType;
        /// <summary>True when this view plays sprite animations rather than standing still.</summary>
        public bool IsAnimated => _animator != null;
        public Transform TextSpawnRoot => textSpawnRoot != null ? textSpawnRoot : transform;

        /// <summary>
        /// Where feedback popups start: the authored text root when one is set, otherwise just above the top of
        /// the character's sprites.
        /// </summary>
        public Vector3 PopupOrigin
        {
            get
            {
                if (textSpawnRoot != null) return textSpawnRoot.position;

                var found = false;
                var bounds = default(Bounds);
                foreach (var spriteRenderer in _renderers ?? GetComponentsInChildren<SpriteRenderer>())
                {
                    if (spriteRenderer == null || !spriteRenderer.enabled || spriteRenderer.sprite == null) continue;
                    if (found) bounds.Encapsulate(spriteRenderer.bounds);
                    else bounds = spriteRenderer.bounds;
                    found = true;
                }

                return found
                    ? new Vector3(bounds.center.x, bounds.max.y + 0.25f, transform.position.z)
                    : transform.position + Vector3.up * 2f;
            }
        }

        protected FxManager FxManager => FxManager.Instance;
        protected AudioManager AudioManager => AudioManager.Instance;
        protected GameManager GameManager => GameManager.Instance;
        protected CombatManager CombatManager => CombatManager.Instance;
        protected UIManager UIManager => UIManager.Instance;
        #endregion

        public virtual void Bind(Combatant combatant)
        {
            Combatant = combatant;
            combatant.ViewRoot = transform;
            combatant.TextRoot = TextSpawnRoot;
            characterType = combatant.Side == BattleSide.Party ? CharacterType.Ally : CharacterType.Enemy;

            _home = transform.position;
            _renderers = GetComponentsInChildren<SpriteRenderer>();
            _baseColors = new Color[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
                _baseColors[i] = _renderers[i].color;

            // Sheets face right; the party stands on the left, so enemies turn around to face it.
            _animator = GetComponentInChildren<SpriteAnimator>();
            _homeFacing = combatant.Side == BattleSide.Party ? 1f : -1f;
            Face(_homeFacing);
            if (_animator != null) _animator.Play(combatant.IsAlive ? CharacterAnimation.BattleIdle : CharacterAnimation.Death, true);
            if (!combatant.IsAlive) SetTint(DownedTint);

            combatant.Stats.OnIncapacitated += OnIncapacitated;
            combatant.Stats.OnRevived += OnRevived;
        }

        protected virtual void OnDestroy()
        {
            if (Combatant == null) return;
            Combatant.Stats.OnIncapacitated -= OnIncapacitated;
            Combatant.Stats.OnRevived -= OnRevived;
        }

        /// <summary>A downed sprite already lies on the ground, so it needs less fading than a block does.</summary>
        private Color DownedTint => IsAnimated ? new Color(0.6f, 0.6f, 0.65f, 0.75f) : new Color(0.3f, 0.3f, 0.3f, 0.4f);

        protected virtual void OnIncapacitated()
        {
            SetTint(DownedTint);
            if (_animator != null) _animator.Play(CharacterAnimation.Death, true);
        }

        protected virtual void OnRevived()
        {
            SetTint(Color.white);
            if (_animator != null) _animator.Play(CharacterAnimation.BattleIdle, true);
        }

        #region Animation
        /// <summary>Steps toward <paramref name="target"/>, or hops in place when there is none.</summary>
        public IEnumerator Advance(CharacterBase target, float duration)
        {
            var moving = target != null && target != this;
            var end = moving ? Vector3.Lerp(_home, target.transform.position, 0.6f) : _home + Vector3.up * 0.4f;
            if (moving) Face(end.x - _home.x);
            PlayLiving(moving ? CharacterAnimation.Run : CharacterAnimation.Jump);
            yield return Move(_home, end, duration);
            PlayLiving(CharacterAnimation.BattleIdle);
        }

        public IEnumerator Return(float duration)
        {
            var from = transform.position;
            if (Mathf.Abs(from.x - _home.x) > 0.05f)
            {
                Face(_home.x - from.x);
                PlayLiving(CharacterAnimation.Run);
            }

            yield return Move(from, _home, duration);
            Face(_homeFacing);
            PlayLiving(CharacterAnimation.BattleIdle);
        }

        /// <summary>
        /// Starts a strike pose and returns the seconds until its impact frame, so the hit can land on it. Returns 0
        /// for a view without animations.
        /// </summary>
        public float PlayStrike(CharacterAnimation strike)
        {
            if (_animator == null || Combatant == null || !Combatant.IsAlive) return 0f;
            _animator.PlayOnce(strike, CharacterAnimation.BattleIdle);
            return _animator.ImpactTime(strike);
        }

        /// <summary>Flinches, unless the hit was the one that put this character down.</summary>
        public void PlayHurt()
        {
            if (_animator == null || Combatant == null || !Combatant.IsAlive) return;
            _animator.PlayOnce(CharacterAnimation.Hurt, CharacterAnimation.BattleIdle);
        }

        private void PlayLiving(CharacterAnimation animation)
        {
            if (_animator != null && (Combatant == null || Combatant.IsAlive)) _animator.Play(animation);
        }

        /// <summary>Turns the sprite toward <paramref name="direction"/>: positive is right.</summary>
        private void Face(float direction)
        {
            if (_animator != null && Mathf.Abs(direction) > 0.001f) _animator.FlipX = direction < 0f;
        }

        private Coroutine _dodgeRoutine;

        /// <summary>
        /// Darts <paramref name="offset"/> from home, dropping fading <paramref name="ghostColor"/> afterimages along
        /// the path, holds there until <paramref name="holdUntil"/> (a <see cref="Time.time"/>), then slides home.
        /// Starting a new dodge cancels one still in progress.
        /// </summary>
        public void PlayDodge(Vector3 offset, float dashDuration, float holdUntil, float returnDuration, Color ghostColor)
        {
            if (_dodgeRoutine != null) StopCoroutine(_dodgeRoutine);
            _dodgeRoutine = StartCoroutine(Dodge(offset, dashDuration, holdUntil, returnDuration, ghostColor));
        }

        private IEnumerator Dodge(Vector3 offset, float dashDuration, float holdUntil, float returnDuration, Color ghostColor)
        {
            const int afterimages = 3;
            const float afterimageLifetime = 0.3f;

            var from = transform.position;
            var to = _home + offset;
            var spawned = 0;
            if (_animator != null && Combatant != null && Combatant.IsAlive)
            {
                // A hop back, played to fit the dash and the hold before the hit.
                var length = Mathf.Max(dashDuration, holdUntil - Time.time);
                var clip = _animator.Play(CharacterAnimation.Dodge, true);
                if (clip > 0f && length > 0f) _animator.Play(CharacterAnimation.Dodge, true, clip / length);
            }

            for (var time = 0f; time < dashDuration; time += Time.deltaTime)
            {
                var t = time / dashDuration;
                if (spawned < afterimages && t >= (float)spawned / afterimages)
                {
                    // Earlier ghosts are fainter, so the trail reads as motion away from where the character stood.
                    var color = ghostColor;
                    color.a *= (spawned + 1f) / afterimages;
                    SpawnAfterimage(color, afterimageLifetime);
                    spawned++;
                }

                transform.position = Vector3.Lerp(from, to, 1f - (1f - t) * (1f - t));
                yield return null;
            }

            transform.position = to;
            while (Time.time < holdUntil) yield return null;
            PlayLiving(CharacterAnimation.BattleIdle);
            yield return Move(transform.position, _home, returnDuration);
            _dodgeRoutine = null;
        }

        /// <summary>Leaves a tinted copy of every visible sprite where it stands now, fading out over <paramref name="lifetime"/>.</summary>
        private void SpawnAfterimage(Color color, float lifetime)
        {
            if (_renderers == null) return;
            foreach (var source in _renderers)
            {
                if (source == null || !source.enabled || source.sprite == null) continue;

                var ghost = new GameObject($"{name} Afterimage").AddComponent<SpriteRenderer>();
                ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                ghost.transform.localScale = source.transform.lossyScale;
                ghost.sprite = source.sprite;
                ghost.sharedMaterial = source.sharedMaterial;
                ghost.drawMode = source.drawMode;
                if (source.drawMode != SpriteDrawMode.Simple) ghost.size = source.size;
                ghost.flipX = source.flipX;
                ghost.flipY = source.flipY;
                ghost.sortingLayerID = source.sortingLayerID;
                ghost.sortingOrder = source.sortingOrder - 1;
                ghost.color = color;

                StartCoroutine(FadeAfterimage(ghost, color, lifetime));
                // Safety net: the fade stops if this character is disabled mid-dodge.
                Destroy(ghost.gameObject, lifetime);
            }
        }

        private static IEnumerator FadeAfterimage(SpriteRenderer ghost, Color color, float lifetime)
        {
            var startAlpha = color.a;
            for (var time = 0f; time < lifetime && ghost != null; time += Time.deltaTime)
            {
                color.a = Mathf.Lerp(startAlpha, 0f, time / lifetime);
                ghost.color = color;
                yield return null;
            }
        }

        public IEnumerator Flash(Color color, float duration)
        {
            SetTint(color);
            yield return new WaitForSeconds(duration);
            SetTint(Combatant != null && !Combatant.IsAlive ? DownedTint : Color.white);
        }

        private IEnumerator Move(Vector3 from, Vector3 to, float duration)
        {
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, to, time / duration);
                yield return null;
            }

            transform.position = to;
        }

        /// <summary>Multiplies each sprite's authored color, so white restores it.</summary>
        protected void SetTint(Color tint)
        {
            if (_renderers == null) return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].color = _baseColors[i] * tint;
            }
        }
        #endregion

        public CharacterBase GetCharacterBase() => this;
        public CharacterType GetCharacterType() => CharacterType;
    }
}
