using System;
using InElementum.Combat.Model;
using InElementum.Core.UI;
using InElementum.Data.Containers;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// An enemy roaming the room. It patrols between two points, chases the player once it sees them (blocked by
    /// ground), lunges when close, and gives up after losing sight for a while. Touching the player starts its
    /// encounter with the enemy striking first; the player's field attack starts it with the party striking first.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class OverworldEnemy : MonoBehaviour
    {
        private enum Move
        {
            Walk,
            WindUp,
            Lunge
        }

        /// <summary>Raised when this enemy's fight should start, with the side that struck first.</summary>
        public static event Action<OverworldEnemy, BattleSide> EncounterTriggered;

        [SerializeField] private EncounterData encounter;
        [Tooltip("Unique within the room. Used to keep the enemy gone once beaten.")]
        [SerializeField] private string enemyId;

        [Header("Patrol")]
        [Tooltip("World points walked between. Leave both at zero to stand guard.")]
        [SerializeField] private Vector2 patrolPointA;
        [SerializeField] private Vector2 patrolPointB;
        [SerializeField] [Min(0f)] private float patrolSpeed = 2f;
        [Tooltip("Seconds to pause at each end of the patrol.")]
        [SerializeField] [Min(0f)] private float patrolPause = 0.6f;

        [Header("Sight")]
        [SerializeField] [Min(0f)] private float sightRange = 7f;
        [SerializeField] [Range(0f, 360f)] private float fieldOfView = 120f;
        [Tooltip("The player is noticed this close whichever way the enemy faces.")]
        [SerializeField] [Min(0f)] private float nearSenseRadius = 1.4f;
        [Tooltip("Seconds out of sight before the enemy gives up the chase.")]
        [SerializeField] [Min(0f)] private float loseSightTime = 2.5f;
        [SerializeField] private Vector2 eyeOffset = new Vector2(0f, 0.3f);

        [Header("Chase and lunge")]
        [SerializeField] [Min(0f)] private float chaseSpeed = 4.5f;
        [Tooltip("Lunges when the player is this close horizontally.")]
        [SerializeField] [Min(0f)] private float lungeRange = 2.4f;
        [SerializeField] [Min(0f)] private float lungeWindUp = 0.35f;
        [SerializeField] [Min(0f)] private float lungeSpeed = 11f;
        [SerializeField] [Min(0f)] private float lungeDuration = 0.22f;
        [SerializeField] [Min(0f)] private float lungeCooldown = 1.2f;

        [Header("Scene")]
        [Tooltip("Optional. The player is found on start when empty.")]
        [SerializeField] private Transform player;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private LayerMask playerMask;
        [Tooltip("Tinted while chasing and flipped to face the way it moves. Optional.")]
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color alertTint = new Color(1f, 0.45f, 0.4f);

        private Rigidbody2D _body;
        private BoxCollider2D _collider;
        private EnemyAwareness _awareness;
        private Vector3 _startPosition;
        private Color _baseColor = Color.white;
        private Move _move;
        private float _moveTimer;
        private float _nextLunge;
        private float _pauseUntil;
        private bool _headingToB = true;
        private Vector2 _lastSeen;

        public EncounterData Encounter { get => encounter; set => encounter = value; }
        public string EnemyId { get => enemyId; set => enemyId = value; }
        public AwarenessState Awareness => _awareness != null ? _awareness.State : AwarenessState.Patrol;
        /// <summary>+1 facing right, -1 facing left.</summary>
        public float Facing { get; private set; } = 1f;
        /// <summary>While true, touching the player does nothing, so a fight just left is not entered again at once.</summary>
        public static bool ContactSuppressed { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            EncounterTriggered = null;
            ContactSuppressed = false;
        }

        private WorldText _marker;

        private bool HasPatrol => patrolPointA != patrolPointB;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<BoxCollider2D>();
            _body.freezeRotation = true;
            _awareness = new EnemyAwareness(loseSightTime);
            _startPosition = transform.position;
            if (visual != null) _baseColor = visual.color;
            _marker = WorldUi.Add(new WorldText(boxed: false, fontSize: 22));
        }

        private void OnDestroy() => _marker?.Release();

        private void OnDisable()
        {
            if (_marker != null) _marker.Shown = false;
        }

        private void Start()
        {
            if (player == null)
            {
                var found = FindAnyObjectByType<PlayerController2D>();
                if (found != null) player = found.transform;
            }
        }

        /// <summary>Starts this enemy's fight with <paramref name="initiative"/> striking first.</summary>
        public void TriggerEncounter(BattleSide initiative)
        {
            if (!isActiveAndEnabled) return;
            EncounterTriggered?.Invoke(this, initiative);
        }

        /// <summary>Back to its starting spot, unaware, as after the player respawns.</summary>
        public void ResetToStart()
        {
            transform.position = _startPosition;
            if (_body != null)
            {
                _body.position = _startPosition;
                _body.linearVelocity = Vector2.zero;
            }

            _awareness?.Reset();
            _move = Move.Walk;
            _headingToB = true;
            _pauseUntil = 0f;
            if (visual != null) visual.color = _baseColor;
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            var sees = CanSeePlayer();
            if (sees) _lastSeen = player.position;
            _awareness.Update(sees, dt);

            var velocity = _body.linearVelocity;
            velocity.x = _awareness.State == AwarenessState.Chase ? ChaseSpeed(dt) : PatrolSpeed();
            if (_move == Move.Walk && Mathf.Abs(velocity.x) > 0.01f) Facing = Mathf.Sign(velocity.x);
            if (_move != Move.Lunge && Mathf.Abs(velocity.x) > 0.01f && Blocked(Mathf.Sign(velocity.x))) velocity.x = 0f;
            _body.linearVelocity = velocity;

            CheckContact();
        }

        private void Update()
        {
            if (visual == null) return;

            var scale = visual.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Facing;
            visual.transform.localScale = scale;

            var alert = _awareness.State == AwarenessState.Chase;
            var tint = alert ? alertTint : _baseColor;
            if (_move == Move.WindUp) tint = Color.Lerp(alertTint, Color.white, Mathf.PingPong(Time.time * 12f, 1f));
            visual.color = Color.Lerp(visual.color, tint, 12f * Time.deltaTime);
        }

        private float PatrolSpeed()
        {
            _move = Move.Walk;
            if (!HasPatrol || Time.time < _pauseUntil) return 0f;

            var target = _headingToB ? patrolPointB : patrolPointA;
            var dx = target.x - transform.position.x;
            if (Mathf.Abs(dx) < 0.1f || Blocked(Mathf.Sign(dx)))
            {
                _headingToB = !_headingToB;
                _pauseUntil = Time.time + patrolPause;
                return 0f;
            }

            return Mathf.Sign(dx) * patrolSpeed;
        }

        private float ChaseSpeed(float dt)
        {
            var target = player != null && _awareness.TimeUnseen <= 0f ? (Vector2)player.position : _lastSeen;
            var dx = target.x - transform.position.x;

            switch (_move)
            {
                case Move.WindUp:
                    _moveTimer -= dt;
                    if (_moveTimer > 0f) return 0f;
                    _move = Move.Lunge;
                    _moveTimer = lungeDuration;
                    return Facing * lungeSpeed;

                case Move.Lunge:
                    _moveTimer -= dt;
                    if (_moveTimer > 0f && !Blocked(Facing)) return Facing * lungeSpeed;
                    _move = Move.Walk;
                    _nextLunge = Time.time + lungeCooldown;
                    return 0f;
            }

            if (_awareness.TimeUnseen <= 0f && Mathf.Abs(dx) <= lungeRange && Time.time >= _nextLunge)
            {
                Facing = Mathf.Sign(dx);
                _move = Move.WindUp;
                _moveTimer = lungeWindUp;
                return 0f;
            }

            return Mathf.Abs(dx) < 0.2f ? 0f : Mathf.Sign(dx) * chaseSpeed;
        }

        private Vector2 Eye => (Vector2)transform.position + eyeOffset;

        private bool CanSeePlayer()
        {
            if (player == null || !player.gameObject.activeInHierarchy) return false;

            var target = (Vector2)player.position;
            if (!EnemyAwareness.InView(Eye, Facing, target, sightRange, fieldOfView, nearSenseRadius)) return false;
            return Physics2D.Linecast(Eye, target, groundMask).collider == null;
        }

        /// <summary>A wall ahead, or no floor ahead to step onto.</summary>
        private bool Blocked(float direction)
        {
            var bounds = _collider.bounds;
            var front = new Vector2(direction > 0f ? bounds.max.x : bounds.min.x, bounds.center.y);
            if (Physics2D.Raycast(front, new Vector2(direction, 0f), 0.15f, groundMask).collider != null) return true;

            var toe = new Vector2(front.x + direction * 0.2f, bounds.min.y + 0.05f);
            return Physics2D.Raycast(toe, Vector2.down, 0.4f, groundMask).collider == null;
        }

        private void CheckContact()
        {
            if (ContactSuppressed) return;
            var bounds = _collider.bounds;
            if (Physics2D.OverlapBox(bounds.center, (Vector2)bounds.size + Vector2.one * 0.05f, 0f, playerMask) != null)
                TriggerEncounter(BattleSide.Enemy);
        }

        /// <summary>A "!" over a chasing enemy, or "?" while it searches for a player it lost sight of.</summary>
        private void LateUpdate()
        {
            var chasing = _awareness != null && _awareness.State == AwarenessState.Chase;
            _marker.Shown = chasing;
            if (!chasing) return;

            _marker.Anchor = new Vector3(transform.position.x, _collider.bounds.max.y + 0.45f, 0f);
            _marker.Text = _awareness.IsSearching ? "?" : "!";
            _marker.Color = _awareness.IsSearching ? new Color(1f, 0.9f, 0.5f) : new Color(1f, 0.35f, 0.3f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            if (HasPatrol)
            {
                Gizmos.DrawLine(patrolPointA, patrolPointB);
                Gizmos.DrawWireSphere(patrolPointA, 0.15f);
                Gizmos.DrawWireSphere(patrolPointB, 0.15f);
            }

            var eye = (Vector2)transform.position + eyeOffset;
            var facing = Application.isPlaying ? Facing : 1f;
            var half = fieldOfView * 0.5f * Mathf.Deg2Rad;
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.6f);
            Gizmos.DrawLine(eye, eye + new Vector2(Mathf.Cos(half) * facing, Mathf.Sin(half)) * sightRange);
            Gizmos.DrawLine(eye, eye + new Vector2(Mathf.Cos(half) * facing, -Mathf.Sin(half)) * sightRange);
            Gizmos.DrawWireSphere(eye, nearSenseRadius);
        }
    }
}
