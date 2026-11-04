using System.Collections.Generic;
using UnityEngine;

namespace InElementum.Combat.Managers
{
    /// <summary>
    /// Moves the orthographic battle camera between shots: an overview of the whole field, a close-up on one
    /// character, or a frame around an attacker and its target. Shots are requested every frame or at action
    /// beats; the camera eases toward the latest one. <see cref="Punch"/> adds a short zoom kick and shake on hits.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CombatCamera : MonoBehaviour
    {
        [Header("Overview")]
        [SerializeField] [Min(0f)] private float overviewPadding = 1.4f;
        [SerializeField] [Min(1f)] private float minOverviewSize = 4.5f;

        [Header("Bodies")]
        [Tooltip("Room kept under each character's feet for its name plate and health bar.")]
        [SerializeField] [Min(0f)] private float plateDepth = 0.6f;
        [Tooltip("Size assumed for a character with no visible renderers.")]
        [SerializeField] [Min(0.1f)] private float defaultHeight = 2f;
        [SerializeField] [Min(0.1f)] private float defaultHalfWidth = 0.6f;

        [Header("Close-up")]
        [Tooltip("Orthographic size when focusing on one character.")]
        [SerializeField] [Min(0.5f)] private float focusSize = 3.6f;
        [Tooltip("World height above a character's feet that the close-up centres on.")]
        [SerializeField] private float focusHeight = 0.9f;

        [Header("Action")]
        [SerializeField] [Min(0f)] private float framePadding = 2.2f;
        [SerializeField] [Min(0.5f)] private float minFrameSize = 4.4f;
        [Tooltip("How far a hit's punch zooms in, in orthographic size units at strength 1.")]
        [SerializeField] [Min(0f)] private float punchZoom = 0.15f;

        [Tooltip("Minimum screen fractions kept clear at each edge (left, bottom, right, top). Overview and action shots keep " +
                 "characters out of them, and out of whatever the HUD reports through HudMargins.")]
        [SerializeField] private Vector4 safeArea = new Vector4(0.12f, 0.16f, 0.03f, 0.14f);

        [Header("Motion")]
        [SerializeField] [Min(0.01f)] private float smoothTime = 0.28f;
        [SerializeField] [Min(0f)] private float shakeDecay = 6f;

        private readonly List<Transform> _overviewSubjects = new List<Transform>();
        private readonly Dictionary<Transform, Vector3> _bodies = new Dictionary<Transform, Vector3>();
        private Camera _camera;
        private float _z;

        // The eased shot, kept apart from shake and punch so those never feed back into the ease.
        private Vector2 _position;
        private float _size;
        private Vector2 _targetPosition;
        private float _targetSize;
        private Vector2 _velocity;
        private float _sizeVelocity;

        private float _shake;
        private float _punch;

        // The last framed shot, kept so a change of HUD margins can refit it. Cleared by a close-up.
        private bool _hasFrame;
        private Bounds _frameBounds;
        private float _framePadding;
        private float _frameMinSize;
        private Vector4 _hudMargins;
        private int _snapFrame = -1;

        public Camera Camera => _camera;

        /// <summary>
        /// Screen fractions the HUD covers right now (left, bottom, right, top), kept clear on top of
        /// <see cref="safeArea"/>. The HUD sets it as the screen's shape changes. A change refits the current shot at
        /// once, and if the camera snapped this frame it snaps again, so the first shot of a battle starts clear.
        /// </summary>
        public Vector4 HudMargins
        {
            get => _hudMargins;
            set
            {
                if (value == _hudMargins) return;
                _hudMargins = value;
                if (_hasFrame) SetFrame(_frameBounds, _framePadding, _frameMinSize);
                if (_snapFrame == Time.frameCount) Snap();
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _z = transform.position.z;
            _position = _targetPosition = transform.position;
            _size = _targetSize = _camera.orthographicSize;
        }

        #region Shots
        /// <summary>Sets who the overview frames. Inactive or destroyed subjects are skipped.</summary>
        public void SetOverviewSubjects(IEnumerable<Transform> subjects)
        {
            _overviewSubjects.Clear();
            _overviewSubjects.AddRange(subjects);
            _bodies.Clear();
        }

        /// <summary>Frames every active overview subject.</summary>
        public void ShowOverview()
        {
            var bounds = default(Bounds);
            var any = false;
            foreach (var subject in _overviewSubjects)
            {
                if (subject == null || !subject.gameObject.activeInHierarchy) continue;
                Encapsulate(ref bounds, ref any, subject);
            }

            if (!any) return;
            SetFrame(bounds, overviewPadding, minOverviewSize);
        }

        /// <summary>
        /// Close-up on <paramref name="subject"/>, placed at <paramref name="screenX"/> across the screen (0 left, 1
        /// right) so the command menu has room beside it.
        /// </summary>
        public void Focus(Transform subject, float screenX = 0.5f, float sizeScale = 1f)
        {
            if (subject == null) return;

            var size = focusSize * sizeScale;
            var halfWidth = size * _camera.aspect;
            var point = (Vector2)subject.position + Vector2.up * focusHeight;
            _targetPosition = point + Vector2.right * (0.5f - screenX) * 2f * halfWidth;
            _targetSize = size;
            _hasFrame = false;
        }

        /// <summary>Frames all of <paramref name="subjects"/>, such as an attacker and its targets.</summary>
        public void Frame(params Transform[] subjects) => Frame((IEnumerable<Transform>)subjects);

        public void Frame(IEnumerable<Transform> subjects)
        {
            var bounds = default(Bounds);
            var any = false;
            foreach (var subject in subjects)
            {
                if (subject == null || !subject.gameObject.activeInHierarchy) continue;
                Encapsulate(ref bounds, ref any, subject);
            }

            if (any) SetFrame(bounds, framePadding, minFrameSize);
        }

        /// <summary>A brief zoom-in and shake, for a hit landing.</summary>
        public void Punch(float strength = 1f)
        {
            _punch = Mathf.Max(_punch, punchZoom * strength);
            _shake = Mathf.Max(_shake, 0.12f * strength);
        }

        /// <summary>Jumps straight to the current shot, skipping the ease.</summary>
        public void Snap()
        {
            _snapFrame = Time.frameCount;
            _velocity = Vector2.zero;
            _sizeVelocity = 0f;
            _position = _targetPosition;
            _size = _targetSize;
            transform.position = new Vector3(_position.x, _position.y, _z);
            _camera.orthographicSize = _size;
        }
        #endregion

        private void LateUpdate()
        {
            _position = Vector2.SmoothDamp(_position, _targetPosition, ref _velocity, smoothTime);
            _size = Mathf.SmoothDamp(_size, _targetSize, ref _sizeVelocity, smoothTime);

            _punch = Mathf.MoveTowards(_punch, 0f, Time.deltaTime * 1.5f);
            _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * shakeDecay * 0.1f);
            var shake = _shake > 0f ? Random.insideUnitCircle * _shake : Vector2.zero;

            transform.position = new Vector3(_position.x + shake.x, _position.y + shake.y, _z);
            _camera.orthographicSize = Mathf.Max(0.5f, _size - _punch);
        }

        /// <summary>
        /// Fits <paramref name="bounds"/> inside the area left clear by the safe area and the HUD, then centres it there.
        /// </summary>
        private void SetFrame(Bounds bounds, float padding, float minSize)
        {
            _hasFrame = true;
            _frameBounds = bounds;
            _framePadding = padding;
            _frameMinSize = minSize;

            var clear = Vector4.Max(safeArea, _hudMargins);
            bounds.Expand(padding * 2f);
            var usableWidth = Mathf.Max(0.2f, 1f - clear.x - clear.z);
            var usableHeight = Mathf.Max(0.2f, 1f - clear.y - clear.w);

            var size = Mathf.Max(minSize, bounds.extents.y / usableHeight, bounds.extents.x / (_camera.aspect * usableWidth));
            var halfWidth = size * _camera.aspect;

            // Shift so the bounds sit in the middle of the clear area rather than the middle of the screen.
            _targetPosition = new Vector2(
                bounds.center.x - (clear.x - clear.z) * halfWidth,
                bounds.center.y - (clear.y - clear.w) * size);
            _targetSize = size;
        }

        /// <summary>
        /// Adds a character's whole body to <paramref name="bounds"/>: its sprites' real width and height, plus a strip
        /// under the feet for the name plate. The body is measured once and kept, so idle animations and attack
        /// lunges don't make the shot breathe.
        /// </summary>
        private void Encapsulate(ref Bounds bounds, ref bool any, Transform subject)
        {
            var body = BodyOf(subject);
            var feet = (Vector2)subject.position;
            var min = new Vector3(feet.x - body.x, feet.y - body.z, 0f);
            var max = new Vector3(feet.x + body.x, feet.y + body.y, 0f);

            if (!any)
            {
                bounds = new Bounds(min, Vector3.zero);
                any = true;
            }
            else
            {
                bounds.Encapsulate(min);
            }

            bounds.Encapsulate(max);
        }

        /// <summary>
        /// A character's extent around its feet: half width (x), height above (y) and depth below (z). Falls back to a
        /// column <see cref="defaultHeight"/> tall when it has no visible renderers yet.
        /// </summary>
        private Vector3 BodyOf(Transform subject)
        {
            if (_bodies.TryGetValue(subject, out var cached)) return cached;

            var feet = subject.position;
            var halfWidth = defaultHalfWidth;
            var above = defaultHeight;
            var below = 0f;
            var measured = false;

            foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                    continue;

                var b = renderer.bounds;
                if (b.size.sqrMagnitude < 0.0001f) continue;
                if (!measured)
                {
                    halfWidth = above = below = 0f;
                    measured = true;
                }

                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(b.min.x - feet.x), Mathf.Abs(b.max.x - feet.x));
                above = Mathf.Max(above, b.max.y - feet.y);
                below = Mathf.Max(below, feet.y - b.min.y);
            }

            var body = new Vector3(halfWidth, above, below + plateDepth);
            if (measured) _bodies[subject] = body;
            return body;
        }
    }
}
