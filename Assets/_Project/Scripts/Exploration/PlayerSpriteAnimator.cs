using InElementum.Core.Utils;
using InElementum.Data.Characters;
using InElementum.Encounters;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// Poses the explored character's sprite from what <see cref="PlayerController2D"/> is doing: idle, run, jump,
    /// fall, a short landing crouch, and the field-attack swing. Wears the explored character's sprite set (see
    /// <see cref="CharacterSwitcher"/>), or the party leader's when there is no switcher.
    /// </summary>
    [RequireComponent(typeof(SpriteAnimator))]
    public class PlayerSpriteAnimator : MonoBehaviour
    {
        [Tooltip("Read for movement and attacks. Found in the parents when empty.")]
        [SerializeField] private PlayerController2D controller;
        [Tooltip("Wear the explored character's look: the character switcher's pick, else the active party's leader.")]
        [SerializeField] private bool followPartyLeader = true;
        [Tooltip("Horizontal speed below which the character counts as standing still.")]
        [SerializeField] [Min(0f)] private float runThreshold = 0.15f;
        [Tooltip("Vertical speed above which an airborne character counts as rising.")]
        [SerializeField] private float riseThreshold = 0.1f;
        [Tooltip("Longest the landing crouch holds before running or idling takes over.")]
        [SerializeField] [Min(0f)] private float landHold = 0.18f;

        private SpriteAnimator _animator;
        private Rigidbody2D _body;
        private CharacterSwitcher _switcher;
        private bool _wasGrounded = true;
        private float _landUntil;
        private float _attackUntil;

        private void Awake()
        {
            _animator = GetComponent<SpriteAnimator>();
            if (controller == null) controller = GetComponentInParent<PlayerController2D>();
            if (controller != null) _body = controller.GetComponent<Rigidbody2D>();
            _switcher = GetComponentInParent<CharacterSwitcher>();
        }

        private void OnEnable()
        {
            if (controller != null) controller.FieldAttackStarted += OnFieldAttack;
        }

        private void OnDisable()
        {
            if (controller != null) controller.FieldAttackStarted -= OnFieldAttack;
        }

        private void OnFieldAttack()
        {
            _attackUntil = Time.time + _animator.PlayOnce(CharacterAnimation.Attack, CharacterAnimation.Idle);
        }

        private void Update()
        {
            if (followPartyLeader) FollowLeader();
            if (controller == null || _body == null) return;

            var grounded = controller.IsGrounded;
            var velocity = _body.linearVelocity;
            var justLanded = grounded && !_wasGrounded;
            _wasGrounded = grounded;

            if (Time.time < _attackUntil) return;

            if (!controller.ControlsEnabled)
            {
                _animator.Play(CharacterAnimation.Idle);
                return;
            }

            if (!grounded)
            {
                _animator.Play(velocity.y > riseThreshold ? CharacterAnimation.Jump : CharacterAnimation.Fall);
                return;
            }

            var moving = Mathf.Abs(velocity.x) > runThreshold;
            if (justLanded)
            {
                _landUntil = Time.time + landHold;
                _animator.Play(CharacterAnimation.Land, true);
            }

            if (Time.time < _landUntil && !moving) return;
            _animator.Play(moving ? CharacterAnimation.Run : CharacterAnimation.Idle);
        }

        private void FollowLeader()
        {
            if (_switcher == null) _switcher = GetComponentInParent<CharacterSwitcher>();
            var member = _switcher != null ? _switcher.Current : null;
            if (member == null)
            {
                var party = EncounterDirector.Instance != null ? EncounterDirector.Instance.Party : null;
                if (party == null || party.Active.Count == 0) return;
                member = party.Active[0];
            }

            var set = member != null ? member.SpriteSet : null;
            if (set != null && _animator.SpriteSet != set) _animator.SpriteSet = set;
        }
    }
}
