using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// How the explored character runs and jumps. The target is Hollow Knight: quick to full speed, quick to stop,
    /// a fast fall, and a short hop on a tapped jump. Jump arcs are set by height and time to apex, and gravity
    /// follows from them.
    /// </summary>
    [CreateAssetMenu(fileName = "Movement Profile", menuName = "In Elementum/Exploration/Movement Profile", order = 0)]
    public class MovementProfile : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("Top running speed, units per second.")]
        [SerializeField] [Min(0f)] private float moveSpeed = 8f;
        [Tooltip("Speed gained per second while a direction is held.")]
        [SerializeField] [Min(0f)] private float acceleration = 90f;
        [Tooltip("Speed lost per second with no direction held, or when turning around.")]
        [SerializeField] [Min(0f)] private float deceleration = 120f;
        [Tooltip("Multiplies acceleration and deceleration in the air.")]
        [SerializeField] [Range(0f, 1f)] private float airControl = 0.85f;

        [Header("Jump")]
        [Tooltip("Apex of a held jump, in units.")]
        [SerializeField] [Min(0.1f)] private float jumpHeight = 3.4f;
        [Tooltip("Seconds from takeoff to the apex of a held jump.")]
        [SerializeField] [Min(0.05f)] private float timeToApex = 0.38f;
        [Tooltip("Gravity multiplier while falling, so the fall is quicker than the rise.")]
        [SerializeField] [Min(1f)] private float fallGravityMultiplier = 1.8f;
        [Tooltip("Upward speed kept when jump is released early, for a short hop.")]
        [SerializeField] [Range(0f, 1f)] private float jumpCutMultiplier = 0.4f;
        [SerializeField] [Min(0f)] private float maxFallSpeed = 22f;

        [Header("Forgiveness (seconds)")]
        [Tooltip("A jump still works this long after running off a ledge.")]
        [SerializeField] [Min(0f)] private float coyoteTime = 0.1f;
        [Tooltip("A jump pressed this long before landing happens on landing.")]
        [SerializeField] [Min(0f)] private float jumpBufferTime = 0.12f;

        public float MoveSpeed { get => moveSpeed; internal set => moveSpeed = value; }
        public float Acceleration { get => acceleration; internal set => acceleration = value; }
        public float Deceleration { get => deceleration; internal set => deceleration = value; }
        public float AirControl { get => airControl; internal set => airControl = value; }
        public float JumpHeight { get => jumpHeight; internal set => jumpHeight = value; }
        public float TimeToApex { get => timeToApex; internal set => timeToApex = value; }
        public float FallGravityMultiplier { get => fallGravityMultiplier; internal set => fallGravityMultiplier = value; }
        public float JumpCutMultiplier { get => jumpCutMultiplier; internal set => jumpCutMultiplier = value; }
        public float MaxFallSpeed { get => maxFallSpeed; internal set => maxFallSpeed = value; }
        public float CoyoteTime { get => coyoteTime; internal set => coyoteTime = value; }
        public float JumpBufferTime { get => jumpBufferTime; internal set => jumpBufferTime = value; }

        /// <summary>Rising gravity, units per second squared: 2h / t².</summary>
        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
        /// <summary>Takeoff speed that reaches <see cref="JumpHeight"/> under <see cref="Gravity"/>: 2h / t.</summary>
        public float JumpVelocity => 2f * jumpHeight / timeToApex;
    }
}
