using System;
using InElementum.Combat.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InElementum.Exploration
{
    /// <summary>
    /// The explored character: runs and jumps through <see cref="PlatformerMotor"/>, and swings a field attack that
    /// starts combat with the party striking first. A thin adapter: the motor holds the movement rules, the
    /// Rigidbody2D resolves collisions.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class PlayerController2D : MonoBehaviour
    {
        [SerializeField] private MovementProfile profile;
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("Layers the feet stand on and sight lines are blocked by.")]
        [SerializeField] private LayerMask groundMask;
        [Tooltip("Layers the field attack can hit.")]
        [SerializeField] private LayerMask enemyMask;
        [Tooltip("Flipped to face the way the character moves. Optional.")]
        [SerializeField] private Transform visual;

        [Header("Field attack")]
        [Tooltip("Shown while the swing is out. Optional.")]
        [SerializeField] private GameObject slashVisual;
        [Tooltip("Hitbox centre, in front of the character, for a character facing right.")]
        [SerializeField] private Vector2 attackOffset = new Vector2(0.9f, 0.1f);
        [SerializeField] private Vector2 attackSize = new Vector2(1.2f, 1.1f);
        [Tooltip("Seconds the hitbox stays out.")]
        [SerializeField] [Min(0.01f)] private float attackActiveTime = 0.15f;
        [Tooltip("Seconds from one swing to the next.")]
        [SerializeField] [Min(0f)] private float attackCooldown = 0.35f;

        [Header("Ground check")]
        [Tooltip("Height of the box below the feet that counts as standing.")]
        [SerializeField] [Min(0.01f)] private float groundProbeDepth = 0.08f;

        private Rigidbody2D _body;
        private BoxCollider2D _collider;
        private PlatformerMotor _motor;
        private ExplorationInput _input;
        private float _attackUntil = float.NegativeInfinity;
        private float _nextAttack;
        private bool _controlsEnabled = true;
        private readonly Collider2D[] _hits = new Collider2D[8];

        /// <summary>+1 facing right, -1 facing left.</summary>
        public float Facing { get; private set; } = 1f;
        public bool IsGrounded => _motor != null && _motor.IsGrounded;
        public bool IsAttacking => Time.time < _attackUntil;
        public ExplorationInput Input => _input;
        public bool ControlsEnabled => _controlsEnabled;
        /// <summary>
        /// While true the character stands still and ignores move, jump, and attack, but the input map stays live, so
        /// a menu such as the character wheel can read it.
        /// </summary>
        public bool InputLocked { get; set; }
        public LayerMask GroundMask => groundMask;

        /// <summary>Raised when a swing starts, for sound and effects.</summary>
        public event Action FieldAttackStarted;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<BoxCollider2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // No friction, so the character never sticks to walls mid-jump.
            _collider.sharedMaterial = new PhysicsMaterial2D("Player (frictionless)") { friction = 0f, bounciness = 0f };

            if (profile == null)
            {
                Debug.LogWarning("[PlayerController2D] No Movement Profile assigned; using defaults.", this);
                profile = ScriptableObject.CreateInstance<MovementProfile>();
            }

            _motor = new PlatformerMotor(profile);
            _input = new ExplorationInput(inputActions);
            if (slashVisual != null) slashVisual.SetActive(false);
            if (GetComponent<PlayerAudio>() == null) gameObject.AddComponent<PlayerAudio>();
        }

        private void OnEnable()
        {
            if (_controlsEnabled) _input?.Enable();
        }

        private void OnDisable()
        {
            _input?.Disable();
            _attackUntil = float.NegativeInfinity;
            if (slashVisual != null) slashVisual.SetActive(false);
        }

        private void OnDestroy() => _input?.Dispose();

        /// <summary>Hands the controls to the player or takes them away, for combat and transitions.</summary>
        public void SetControlsEnabled(bool enabled)
        {
            _controlsEnabled = enabled;
            if (enabled && isActiveAndEnabled) _input.Enable();
            else _input.Disable();

            if (!enabled)
            {
                _body.linearVelocity = Vector2.zero;
                _motor.Reset();
            }
        }

        /// <summary>Moves the character to <paramref name="position"/> at rest.</summary>
        public void Warp(Vector3 position)
        {
            transform.position = position;
            _body.position = position;
            _body.linearVelocity = Vector2.zero;
            _motor.Reset();
        }

        private void Update()
        {
            _input.UpdateDevice();
            if (!_controlsEnabled) return;
            if (InputLocked)
            {
                if (slashVisual != null && slashVisual.activeSelf) slashVisual.SetActive(false);
                return;
            }

            if (_input.JumpPressed) _motor.PressJump();

            var moveX = _input.Move.x;
            if (Mathf.Abs(moveX) > 0.2f) Facing = Mathf.Sign(moveX);
            if (visual != null)
            {
                var scale = visual.localScale;
                scale.x = Mathf.Abs(scale.x) * Facing;
                visual.localScale = scale;
            }

            if (_input.FieldAttackPressed && Time.time >= _nextAttack)
            {
                _attackUntil = Time.time + attackActiveTime;
                _nextAttack = Time.time + attackCooldown;
                FieldAttackStarted?.Invoke();
            }

            if (slashVisual != null && slashVisual.activeSelf != IsAttacking) slashVisual.SetActive(IsAttacking);
        }

        private void FixedUpdate()
        {
            var acting = _controlsEnabled && !InputLocked;
            var moveX = acting ? _input.Move.x : 0f;
            var jumpHeld = acting && _input.JumpHeld;
            _body.linearVelocity = _motor.Step(_body.linearVelocity, moveX, jumpHeld, TouchingGround(), Time.fixedDeltaTime);

            if (IsAttacking) CheckAttackHits();
        }

        private bool TouchingGround()
        {
            var bounds = _collider.bounds;
            var center = new Vector2(bounds.center.x, bounds.min.y - groundProbeDepth * 0.5f);
            var size = new Vector2(bounds.size.x * 0.9f, groundProbeDepth);
            return Physics2D.OverlapBox(center, size, 0f, groundMask) != null;
        }

        private Vector2 AttackCenter => (Vector2)transform.position + new Vector2(attackOffset.x * Facing, attackOffset.y);

        private void CheckAttackHits()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = enemyMask, useTriggers = true };
            var count = Physics2D.OverlapBox(AttackCenter, attackSize, 0f, filter, _hits);
            for (var i = 0; i < count; i++)
            {
                var enemy = _hits[i].GetComponentInParent<OverworldEnemy>();
                if (enemy == null) continue;
                enemy.TriggerEncounter(BattleSide.Party);
                _attackUntil = float.NegativeInfinity;
                return;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.6f);
            var facing = Application.isPlaying ? Facing : 1f;
            Gizmos.DrawWireCube((Vector2)transform.position + new Vector2(attackOffset.x * facing, attackOffset.y), attackSize);
        }
    }
}
