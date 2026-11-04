using System;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// Platformer movement rules, with no physics engine: given the body's velocity, the stick, the jump button, and
    /// whether it stands on ground, works out the next velocity. Handles acceleration, gravity with a faster fall,
    /// coyote time, jump buffering, and variable jump height. The player controller feeds it from a Rigidbody2D and
    /// lets physics resolve collisions.
    /// </summary>
    public sealed class PlatformerMotor
    {
        private readonly MovementProfile _profile;
        private float _coyoteTimer;
        private float _bufferTimer;
        private bool _canCutJump;

        public PlatformerMotor(MovementProfile profile)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
        }

        /// <summary>True for the step a jump took off on.</summary>
        public bool JumpedThisStep { get; private set; }
        /// <summary>Whether the last step counted the body as standing: grounded and not already rising from a jump.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>Records a jump press. It is spent by the next step that can jump, within the buffer time.</summary>
        public void PressJump() => _bufferTimer = Mathf.Max(_profile.JumpBufferTime, 0.0001f);

        /// <param name="velocity">The body's velocity now, after collisions.</param>
        /// <param name="moveX">Horizontal input, -1 to 1.</param>
        /// <param name="jumpHeld">Whether jump is still held. Letting go while rising cuts the jump short.</param>
        /// <param name="touchingGround">Whether the feet touch ground this step.</param>
        public Vector2 Step(Vector2 velocity, float moveX, bool jumpHeld, bool touchingGround, float deltaTime)
        {
            JumpedThisStep = false;

            // Feet still overlap the ground for a step or two after takeoff; a rising body is not standing.
            IsGrounded = touchingGround && velocity.y <= 0.01f;
            if (IsGrounded)
            {
                _coyoteTimer = _profile.CoyoteTime;
                _canCutJump = false;
            }

            velocity.x = Run(velocity.x, Mathf.Clamp(moveX, -1f, 1f), deltaTime);

            if (_bufferTimer > 0f && (IsGrounded || _coyoteTimer > 0f))
            {
                velocity.y = _profile.JumpVelocity;
                _bufferTimer = 0f;
                _coyoteTimer = 0f;
                _canCutJump = true;
                IsGrounded = false;
                JumpedThisStep = true;
            }

            if (_canCutJump && !jumpHeld && velocity.y > 0f)
            {
                velocity.y *= _profile.JumpCutMultiplier;
                _canCutJump = false;
            }

            if (velocity.y <= 0f) _canCutJump = false;

            var gravity = _profile.Gravity * (velocity.y < 0f ? _profile.FallGravityMultiplier : 1f);
            velocity.y = Mathf.Max(velocity.y - gravity * deltaTime, -_profile.MaxFallSpeed);

            if (!IsGrounded) _coyoteTimer = Mathf.Max(0f, _coyoteTimer - deltaTime);
            _bufferTimer = Mathf.Max(0f, _bufferTimer - deltaTime);
            return velocity;
        }

        /// <summary>Forgets buffered jumps and coyote time, for a teleport or a cutscene.</summary>
        public void Reset()
        {
            _coyoteTimer = 0f;
            _bufferTimer = 0f;
            _canCutJump = false;
            JumpedThisStep = false;
        }

        private float Run(float speed, float moveX, float deltaTime)
        {
            var target = moveX * _profile.MoveSpeed;
            var pushing = Mathf.Abs(moveX) > 0.01f && (Mathf.Approximately(speed, 0f) || Mathf.Sign(target) == Mathf.Sign(speed));
            var rate = pushing ? _profile.Acceleration : _profile.Deceleration;
            if (!IsGrounded) rate *= _profile.AirControl;
            return Mathf.MoveTowards(speed, target, rate * deltaTime);
        }
    }
}
