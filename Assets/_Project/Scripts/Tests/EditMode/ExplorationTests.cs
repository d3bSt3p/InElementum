using InElementum.Exploration;
using NUnit.Framework;
using UnityEngine;

namespace InElementum.Tests.EditMode
{
    public class ExplorationTests
    {
        private const float Dt = 0.02f;
        private TestAssets _assets;
        private MovementProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssets();
            _profile = _assets.Create<MovementProfile>();
            _profile.MoveSpeed = 8f;
            _profile.Acceleration = 80f;
            _profile.Deceleration = 100f;
            _profile.AirControl = 1f;
            _profile.JumpHeight = 3f;
            _profile.TimeToApex = 0.4f;
            _profile.FallGravityMultiplier = 2f;
            _profile.JumpCutMultiplier = 0.5f;
            _profile.MaxFallSpeed = 20f;
            _profile.CoyoteTime = 0.1f;
            _profile.JumpBufferTime = 0.1f;
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        /// <summary>
        /// Runs a body over flat ground at y = 0 for <paramref name="steps"/> steps and returns the highest point.
        /// The ground stops a falling body, as physics would.
        /// </summary>
        private static float Simulate(PlatformerMotor motor, int steps, ref Vector2 position, ref Vector2 velocity,
            System.Func<int, bool> jumpHeld, float moveX = 0f)
        {
            var highest = position.y;
            for (var i = 0; i < steps; i++)
            {
                var grounded = position.y <= 0.0001f;
                velocity = motor.Step(velocity, moveX, jumpHeld(i), grounded, Dt);
                position += velocity * Dt;
                if (position.y < 0f)
                {
                    position.y = 0f;
                    velocity.y = 0f;
                }

                highest = Mathf.Max(highest, position.y);
            }

            return highest;
        }

        #region Run
        [Test]
        public void Running_ReachesTopSpeedAndNoFurther()
        {
            var motor = new PlatformerMotor(_profile);
            var velocity = Vector2.zero;
            for (var i = 0; i < 10; i++) velocity = motor.Step(velocity, 1f, false, true, Dt);

            Assert.That(velocity.x, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void Running_TakesAccelerationTime()
        {
            var motor = new PlatformerMotor(_profile);
            var velocity = motor.Step(Vector2.zero, 1f, false, true, Dt);

            Assert.That(velocity.x, Is.EqualTo(80f * Dt).Within(0.001f));
        }

        [Test]
        public void Releasing_DeceleratesToAStop()
        {
            var motor = new PlatformerMotor(_profile);
            var velocity = new Vector2(8f, 0f);
            velocity = motor.Step(velocity, 0f, false, true, Dt);
            Assert.That(velocity.x, Is.EqualTo(8f - 100f * Dt).Within(0.001f));

            for (var i = 0; i < 10; i++) velocity = motor.Step(velocity, 0f, false, true, Dt);
            Assert.That(velocity.x, Is.EqualTo(0f));
        }

        [Test]
        public void AirControl_ScalesAcceleration()
        {
            _profile.AirControl = 0.5f;
            var motor = new PlatformerMotor(_profile);
            var velocity = motor.Step(new Vector2(0f, -1f), 1f, false, false, Dt);

            Assert.That(velocity.x, Is.EqualTo(80f * 0.5f * Dt).Within(0.001f));
        }
        #endregion

        #region Jump
        [Test]
        public void Gravity_And_JumpVelocity_FollowHeightAndApexTime()
        {
            Assert.That(_profile.Gravity, Is.EqualTo(2f * 3f / (0.4f * 0.4f)).Within(0.001f));
            Assert.That(_profile.JumpVelocity, Is.EqualTo(2f * 3f / 0.4f).Within(0.001f));
        }

        [Test]
        public void HeldJump_ReachesJumpHeight()
        {
            var motor = new PlatformerMotor(_profile);
            var position = Vector2.zero;
            var velocity = Vector2.zero;
            motor.PressJump();
            var highest = Simulate(motor, 60, ref position, ref velocity, _ => true);

            Assert.That(highest, Is.EqualTo(3f).Within(0.2f));
        }

        [Test]
        public void TappedJump_IsCutShort()
        {
            var motor = new PlatformerMotor(_profile);
            var position = Vector2.zero;
            var velocity = Vector2.zero;
            motor.PressJump();
            var highest = Simulate(motor, 60, ref position, ref velocity, step => step < 2);

            Assert.That(highest, Is.LessThan(3f * 0.5f));
            Assert.That(highest, Is.GreaterThan(0.1f));
        }

        [Test]
        public void Falling_IsFasterThanRising()
        {
            var motor = new PlatformerMotor(_profile);
            var rising = motor.Step(new Vector2(0f, 5f), 0f, true, false, Dt);
            var falling = motor.Step(new Vector2(0f, -5f), 0f, true, false, Dt);

            Assert.That(5f - rising.y, Is.EqualTo(_profile.Gravity * Dt).Within(0.001f));
            Assert.That(-5f - falling.y, Is.EqualTo(_profile.Gravity * 2f * Dt).Within(0.001f));
        }

        [Test]
        public void Falling_IsCappedAtMaxFallSpeed()
        {
            var motor = new PlatformerMotor(_profile);
            var velocity = motor.Step(new Vector2(0f, -19.9f), 0f, false, false, Dt);

            Assert.That(velocity.y, Is.EqualTo(-20f));
        }

        [Test]
        public void Jump_CannotBeRepeatedWhileRising()
        {
            var motor = new PlatformerMotor(_profile);
            motor.PressJump();
            var velocity = motor.Step(Vector2.zero, 0f, true, true, Dt);
            Assert.That(motor.JumpedThisStep, Is.True);

            // Feet still overlap the ground just after takeoff.
            motor.PressJump();
            var again = motor.Step(velocity, 0f, true, true, Dt);

            Assert.That(motor.JumpedThisStep, Is.False);
            Assert.That(again.y, Is.LessThan(velocity.y));
        }
        #endregion

        #region Coyote time and buffering
        [Test]
        public void CoyoteTime_AllowsAJumpJustAfterLeavingALedge()
        {
            var motor = new PlatformerMotor(_profile);
            motor.Step(Vector2.zero, 1f, false, true, Dt);
            var velocity = motor.Step(Vector2.zero, 1f, false, false, Dt);
            velocity = motor.Step(velocity, 1f, false, false, Dt);

            motor.PressJump();
            motor.Step(velocity, 1f, true, false, Dt);

            Assert.That(motor.JumpedThisStep, Is.True);
        }

        [Test]
        public void CoyoteTime_RunsOut()
        {
            var motor = new PlatformerMotor(_profile);
            var velocity = Vector2.zero;
            motor.Step(velocity, 1f, false, true, Dt);
            for (var i = 0; i < 6; i++) velocity = motor.Step(velocity, 1f, false, false, Dt);

            motor.PressJump();
            motor.Step(velocity, 1f, true, false, Dt);

            Assert.That(motor.JumpedThisStep, Is.False);
        }

        [Test]
        public void JumpBuffer_JumpsOnLanding()
        {
            var motor = new PlatformerMotor(_profile);
            var velocity = new Vector2(0f, -5f);
            motor.PressJump();
            velocity = motor.Step(velocity, 0f, true, false, Dt);
            velocity = motor.Step(velocity, 0f, true, false, Dt);
            Assert.That(motor.JumpedThisStep, Is.False);

            velocity.y = 0f;
            motor.Step(velocity, 0f, true, true, Dt);

            Assert.That(motor.JumpedThisStep, Is.True);
        }

        [Test]
        public void JumpBuffer_ExpiresBeforeALateLanding()
        {
            var motor = new PlatformerMotor(_profile);
            var velocity = new Vector2(0f, -5f);
            motor.PressJump();
            for (var i = 0; i < 6; i++) velocity = motor.Step(velocity, 0f, true, false, Dt);

            velocity.y = 0f;
            motor.Step(velocity, 0f, true, true, Dt);

            Assert.That(motor.JumpedThisStep, Is.False);
        }
        #endregion

        #region Awareness
        [Test]
        public void Awareness_ChasesOnSight()
        {
            var awareness = new EnemyAwareness(2f);
            Assert.That(awareness.State, Is.EqualTo(AwarenessState.Patrol));

            Assert.That(awareness.Update(true, 0.1f), Is.True);
            Assert.That(awareness.State, Is.EqualTo(AwarenessState.Chase));
            Assert.That(awareness.IsSearching, Is.False);
        }

        [Test]
        public void Awareness_GivesUpAfterLosingSightLongEnough()
        {
            var awareness = new EnemyAwareness(2f);
            awareness.Update(true, 0.1f);

            awareness.Update(false, 1.5f);
            Assert.That(awareness.State, Is.EqualTo(AwarenessState.Chase));
            Assert.That(awareness.IsSearching, Is.True);

            awareness.Update(false, 0.5f);
            Assert.That(awareness.State, Is.EqualTo(AwarenessState.Patrol));
        }

        [Test]
        public void Awareness_SeeingAgainRestartsTheCountdown()
        {
            var awareness = new EnemyAwareness(2f);
            awareness.Update(true, 0.1f);
            awareness.Update(false, 1.5f);
            awareness.Update(true, 0.1f);
            awareness.Update(false, 1.5f);

            Assert.That(awareness.State, Is.EqualTo(AwarenessState.Chase));
        }

        [Test]
        public void Awareness_UnseenPlayerIsNeverChased()
        {
            var awareness = new EnemyAwareness(2f);
            awareness.Update(false, 10f);
            Assert.That(awareness.State, Is.EqualTo(AwarenessState.Patrol));
        }

        [Test]
        public void View_CoversRangeAndFieldOfViewAhead()
        {
            var eye = Vector2.zero;
            Assert.That(EnemyAwareness.InView(eye, 1f, new Vector2(5f, 1f), 7f, 120f), Is.True, "ahead, in range");
            Assert.That(EnemyAwareness.InView(eye, 1f, new Vector2(8f, 0f), 7f, 120f), Is.False, "too far");
            Assert.That(EnemyAwareness.InView(eye, 1f, new Vector2(-3f, 0f), 7f, 120f), Is.False, "behind");
            Assert.That(EnemyAwareness.InView(eye, -1f, new Vector2(-3f, 0f), 7f, 120f), Is.True, "facing left");
            Assert.That(EnemyAwareness.InView(eye, 1f, new Vector2(1f, 3f), 7f, 120f), Is.False, "above the cone");
        }

        [Test]
        public void View_NearRadiusSeesBehind()
        {
            Assert.That(EnemyAwareness.InView(Vector2.zero, 1f, new Vector2(-1f, 0f), 7f, 120f, 1.4f), Is.True);
            Assert.That(EnemyAwareness.InView(Vector2.zero, 1f, new Vector2(-2f, 0f), 7f, 120f, 1.4f), Is.False);
        }
        #endregion
    }
}
