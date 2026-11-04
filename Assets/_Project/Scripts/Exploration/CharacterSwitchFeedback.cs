using System;
using InElementum.Combat.Characters;
using InElementum.Core.Managers;
using InElementum.Data.Enums;
using InElementum.Core.Utils;
using InElementum.Data.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace InElementum.Exploration
{
    /// <summary>
    /// The punch of switching the explored character: a brief freeze of game time (hit stop), a flash of the new
    /// character's sprite that fades from white into their element's color, a stretch-and-settle pop, a short camera
    /// shake, and the character's switch sound. Everything runs on real time, so it plays through the freeze. The
    /// <see cref="ExplorationHud"/> adds a burst of the element's color behind the HUD at the same moment.
    /// Added by the <see cref="CharacterSwitcher"/> when the scene does not already have one, with the defaults below.
    /// </summary>
    [RequireComponent(typeof(CharacterSwitcher))]
    public class CharacterSwitchFeedback : MonoBehaviour
    {
        private const string FlashShaderPath = "Shaders/SpriteFlash";

        [Header("Hit stop")]
        [Tooltip("Real seconds game time stands still on a switch. 0 for none.")]
        [SerializeField] [Min(0f)] private float hitStopSeconds = 0.07f;

        [Header("Flash")]
        [Tooltip("Real seconds the sprite's flash takes to fade.")]
        [SerializeField] [Min(0.01f)] private float flashSeconds = 0.28f;
        [Tooltip("How far the flash has turned from white to the element's color as it fades, 0 to 1.")]
        [SerializeField] [Range(0f, 1f)] private float flashElementTint = 0.6f;

        [Header("Pop")]
        [Tooltip("Width and height the sprite starts at, as a fraction of its own, before settling back.")]
        [SerializeField] private Vector2 popStretch = new Vector2(0.78f, 1.22f);
        [Tooltip("Real seconds the pop takes to settle.")]
        [SerializeField] [Min(0.01f)] private float popSeconds = 0.22f;

        [Header("Camera shake")]
        [Tooltip("Largest camera offset, in world units.")]
        [SerializeField] [Min(0f)] private float shakeStrength = 0.12f;
        [Tooltip("Real seconds the shake takes to die out.")]
        [SerializeField] [Min(0.01f)] private float shakeSeconds = 0.2f;
        [Tooltip("Shakes per second.")]
        [SerializeField] [Min(1f)] private float shakeFrequency = 32f;

        [Header("Sound")]
        [Tooltip("Played on a switch to a character with no Switch Sound of their own. Optional.")]
        [SerializeField] private AudioClip defaultSwitchSound;
        [Tooltip("Volume of the switch sound when no AudioManager is in the scene to play it.")]
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;

        private CharacterSwitcher _switcher;
        private SpriteRenderer _sprite;
        private SpriteRenderer _flash;
        private Vector3 _spriteScale = Vector3.one;
        private AudioSource _audio;
        private PartyMemberData _last;
        private Color _element = Color.white;
        private float _playedAt = float.NegativeInfinity;

        private Camera _shakenCamera;
        private Vector3 _shakeOffset;
        private bool _shakeApplied;
        private float _shakeSeed;

        /// <summary>
        /// Raised on each switch with the new character, after the built-in effects start: the hook for anything
        /// further, such as particles or a voice line.
        /// </summary>
        public event Action<PartyMemberData> Played;

        private void Awake() => _switcher = GetComponent<CharacterSwitcher>();

        private void OnEnable()
        {
            _switcher.Switched += OnSwitched;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            _switcher.Switched -= OnSwitched;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            UndoShake();
            _playedAt = float.NegativeInfinity;
            ApplyPop(1f);
            if (_flash != null) _flash.enabled = false;
        }

        private void OnSwitched(PartyMemberData member)
        {
            // The first character of the room is not a switch.
            var first = _last == null;
            _last = member;
            if (first || member == null) return;
            Play(member);
        }

        /// <summary>Plays the switch effects for <paramref name="member"/> coming out.</summary>
        public void Play(PartyMemberData member)
        {
            FindSprite();
            _element = PlaceholderCharacter.ElementColor(member != null ? member.Element : Data.Enums.Element.None);
            _playedAt = Time.unscaledTime;
            _shakeSeed = UnityEngine.Random.value * 100f;
            HitStop.Play(hitStopSeconds);
            PlaySound(member != null && member.SwitchSound != null ? member.SwitchSound : defaultSwitchSound);
            LateUpdate();
            Played?.Invoke(member);
        }

        private void LateUpdate()
        {
            var elapsed = Time.unscaledTime - _playedAt;
            if (elapsed < 0f || elapsed > Mathf.Max(flashSeconds, popSeconds, shakeSeconds) + 0.05f)
            {
                if (_flash != null && _flash.enabled) _flash.enabled = false;
                return;
            }

            UpdateFlash(elapsed / flashSeconds);
            ApplyPop(elapsed / popSeconds);
        }

        #region Sprite
        /// <summary>The explored character's sprite: the one the animator drives, found once it exists.</summary>
        private void FindSprite()
        {
            if (_sprite != null) return;
            var animator = GetComponentInChildren<SpriteAnimator>();
            _sprite = animator != null ? animator.Renderer : GetComponentInChildren<SpriteRenderer>();
            if (_sprite == null) return;
            _spriteScale = _sprite.transform.localScale;

            var shader = Resources.Load<Shader>(FlashShaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"[CharacterSwitchFeedback] No flash shader at Resources/{FlashShaderPath}; switching will not flash the sprite.", this);
                return;
            }

            var flashObject = new GameObject("Switch Flash") { layer = _sprite.gameObject.layer };
            flashObject.transform.SetParent(_sprite.transform, false);
            _flash = flashObject.AddComponent<SpriteRenderer>();
            _flash.sharedMaterial = new Material(shader) { name = "Sprite Flash (runtime)" };
            _flash.enabled = false;
        }

        private void UpdateFlash(float t)
        {
            if (_flash == null || _sprite == null) return;
            var on = t < 1f && _sprite.sprite != null;
            _flash.enabled = on;
            if (!on) return;

            // Matches the sprite each frame, since the animator flips through frames under it.
            _flash.sprite = _sprite.sprite;
            _flash.flipX = _sprite.flipX;
            _flash.flipY = _sprite.flipY;
            _flash.sortingLayerID = _sprite.sortingLayerID;
            _flash.sortingOrder = _sprite.sortingOrder + 1;
            var color = Color.Lerp(Color.white, _element, flashElementTint * t);
            color.a = 1f - t * t;
            _flash.color = color;
        }

        /// <summary>Stretches the sprite tall and thin, then settles it with a small overshoot.</summary>
        private void ApplyPop(float t)
        {
            if (_sprite == null) return;
            t = Mathf.Clamp01(t);
            var settle = EaseOutBack(t);
            var x = Mathf.LerpUnclamped(popStretch.x, 1f, settle);
            var y = Mathf.LerpUnclamped(popStretch.y, 1f, settle);
            _sprite.transform.localScale = Vector3.Scale(_spriteScale, new Vector3(x, y, 1f));
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.9f;
            var u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
        #endregion

        #region Shake
        // The camera is moved only while it renders, then put back, so whatever drives it (Cinemachine, a follow
        // script) never sees the offset.
        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            UndoShake();
            if (camera.cameraType != CameraType.Game || camera != Camera.main) return;

            var elapsed = Time.unscaledTime - _playedAt;
            if (elapsed < 0f || elapsed >= shakeSeconds || shakeStrength <= 0f) return;

            var fade = 1f - elapsed / shakeSeconds;
            var time = elapsed * shakeFrequency;
            var offset = new Vector2(
                Mathf.PerlinNoise(_shakeSeed, time) * 2f - 1f,
                Mathf.PerlinNoise(_shakeSeed + 37f, time) * 2f - 1f) * (shakeStrength * fade * fade);

            _shakenCamera = camera;
            _shakeOffset = offset;
            _shakeApplied = true;
            camera.transform.position += _shakeOffset;
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _shakenCamera) UndoShake();
        }

        private void UndoShake()
        {
            if (!_shakeApplied) return;
            if (_shakenCamera != null) _shakenCamera.transform.position -= _shakeOffset;
            _shakeApplied = false;
            _shakenCamera = null;
        }
        #endregion

        private void PlaySound(AudioClip clip)
        {
            if (clip == null)
            {
                // No clip on the member or here: the shared Character Switch profile, if there is one.
                AudioManager.Play(AudioActionType.CharacterSwitch);
                return;
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOneShot(clip);
                return;
            }

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }

            _audio.PlayOneShot(clip, volume);
        }
    }
}
