using InElementum.Core.Managers;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// The explored character's sounds, read from what <see cref="PlayerController2D"/> is doing: footsteps by
    /// distance run (on water when standing in anything on the Water layer), a jump, a landing, and the field-attack
    /// swing. Added to the player by <see cref="PlayerController2D"/> when missing.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class PlayerAudio : MonoBehaviour
    {
        [Tooltip("Distance run between footsteps.")]
        [SerializeField] [Min(0.1f)] private float strideLength = 3.2f;
        [Tooltip("Horizontal speed below which the character counts as standing still.")]
        [SerializeField] [Min(0f)] private float runThreshold = 0.15f;
        [Tooltip("Fall speed a landing needs to make a sound, so stepping off a small ledge stays quiet.")]
        [SerializeField] [Min(0f)] private float landSpeed = 4f;
        [Tooltip("Colliders (triggers included) on these layers at the feet make footsteps splash.")]
        [SerializeField] private LayerMask waterMask;

        private PlayerController2D _controller;
        private Rigidbody2D _body;
        private Collider2D _collider;
        private bool _wasGrounded = true;
        private float _strideProgress;
        private float _fallSpeed;

        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (waterMask == 0) waterMask = LayerMask.GetMask("Water");
        }

        private void OnEnable()
        {
            _controller.FieldAttackStarted += OnFieldAttack;
            _wasGrounded = true;
            _strideProgress = 0f;
        }

        private void OnDisable() => _controller.FieldAttackStarted -= OnFieldAttack;

        private void OnFieldAttack() => AudioManager.Play(AudioActionType.Swing);

        private void Update()
        {
            if (_body == null || !_controller.ControlsEnabled)
            {
                _wasGrounded = _controller.IsGrounded;
                return;
            }

            var grounded = _controller.IsGrounded;
            var velocity = _body.linearVelocity;

            if (!grounded)
            {
                if (_wasGrounded && velocity.y > 0.1f) AudioManager.Play(AudioActionType.Jump);
                _fallSpeed = Mathf.Max(_fallSpeed, -velocity.y);
                // The first footstep after landing comes half a stride in.
                _strideProgress = strideLength * 0.5f;
            }
            else
            {
                if (!_wasGrounded)
                {
                    if (_fallSpeed >= landSpeed) AudioManager.Play(AudioActionType.Land);
                    _fallSpeed = 0f;
                }

                if (Mathf.Abs(velocity.x) > runThreshold)
                {
                    _strideProgress += Mathf.Abs(velocity.x) * Time.deltaTime;
                    if (_strideProgress >= strideLength)
                    {
                        _strideProgress -= strideLength;
                        AudioManager.Play(OnWater() ? AudioActionType.FootstepWater : AudioActionType.Footstep);
                    }
                }
                else
                {
                    // Starting to run makes a step soon, not a full stride later.
                    _strideProgress = strideLength * 0.7f;
                }
            }

            _wasGrounded = grounded;
        }

        private bool OnWater()
        {
            if (waterMask == 0) return false;
            var bounds = _collider != null ? _collider.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
            var feet = new Vector2(bounds.center.x, bounds.min.y);
            return Physics2D.OverlapBox(feet, new Vector2(bounds.size.x * 0.8f, 0.2f), 0f, waterMask) != null;
        }
    }
}
