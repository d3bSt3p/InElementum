using UnityEngine;

namespace InElementum.Exploration
{
    public enum AwarenessState
    {
        Patrol,
        Chase
    }

    /// <summary>
    /// An overworld enemy's attention: it starts chasing the moment it sees the player and gives up after not seeing
    /// them for <see cref="LoseSightTime"/> seconds. Plain C#; the enemy component supplies whether it can see.
    /// </summary>
    public sealed class EnemyAwareness
    {
        public EnemyAwareness(float loseSightTime)
        {
            LoseSightTime = Mathf.Max(0f, loseSightTime);
        }

        public float LoseSightTime { get; }
        public AwarenessState State { get; private set; } = AwarenessState.Patrol;
        /// <summary>Seconds since the player was last seen while chasing. 0 while in sight.</summary>
        public float TimeUnseen { get; private set; }
        /// <summary>Chasing, but the player is out of sight right now.</summary>
        public bool IsSearching => State == AwarenessState.Chase && TimeUnseen > 0f;

        /// <summary>Advances by <paramref name="deltaTime"/>. True when the state changed.</summary>
        public bool Update(bool canSeePlayer, float deltaTime)
        {
            var before = State;
            if (canSeePlayer)
            {
                State = AwarenessState.Chase;
                TimeUnseen = 0f;
            }
            else if (State == AwarenessState.Chase)
            {
                TimeUnseen += deltaTime;
                if (TimeUnseen >= LoseSightTime)
                {
                    State = AwarenessState.Patrol;
                    TimeUnseen = 0f;
                }
            }

            return State != before;
        }

        public void Reset()
        {
            State = AwarenessState.Patrol;
            TimeUnseen = 0f;
        }

        /// <summary>
        /// Whether <paramref name="target"/> is inside the view: within <paramref name="range"/> and
        /// <paramref name="fieldOfView"/> degrees centred on the facing direction, or within
        /// <paramref name="nearRadius"/> whichever way the enemy faces. Line of sight is checked separately.
        /// </summary>
        /// <param name="facing">+1 facing right, -1 facing left.</param>
        public static bool InView(Vector2 eye, float facing, Vector2 target, float range, float fieldOfView, float nearRadius = 0f)
        {
            var toTarget = target - eye;
            var distance = toTarget.magnitude;
            if (distance <= nearRadius) return true;
            if (distance > range) return false;
            if (distance < 0.0001f) return true;

            var forward = new Vector2(facing >= 0f ? 1f : -1f, 0f);
            return Vector2.Angle(forward, toTarget) <= fieldOfView * 0.5f;
        }
    }
}
