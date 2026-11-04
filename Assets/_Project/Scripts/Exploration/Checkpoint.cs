using System;
using InElementum.Core.UI;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// Checkpoint stub: touching it makes it the respawn point and restores the party (the GDD's full heal). The
    /// menu with Save, Teleport, and Skill Trees comes with the level toolkit (4.1).
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        /// <summary>Raised when the player steps onto a checkpoint.</summary>
        public static event Action<Checkpoint> Reached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reached = null;

        [SerializeField] private Vector2 triggerSize = new Vector2(1.2f, 2f);
        [SerializeField] private Vector2 triggerOffset = new Vector2(0f, 1f);
        [Tooltip("Where the player stands after respawning, relative to this object.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private LayerMask playerMask;
        [Tooltip("Lit while this is the respawn point. Optional.")]
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color idleColor = new Color(0.45f, 0.5f, 0.6f);
        [SerializeField] private Color litColor = new Color(0.55f, 0.95f, 1f);

        private bool _playerInside;
        private float _messageUntil;
        private WorldText _message;

        public Vector3 SpawnPosition => transform.position + spawnOffset;
        public bool IsLit { get; private set; }

        private void Awake()
        {
            _message = WorldUi.Add(new WorldText(boxed: false));
            _message.Text = "Checkpoint: party restored";
        }

        private void OnDestroy() => _message?.Release();

        private void OnDisable()
        {
            if (_message != null) _message.Shown = false;
        }

        private void Start() => SetLit(IsLit);

        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (visual != null) visual.color = lit ? litColor : idleColor;
        }

        /// <summary>Shows "Party restored" over the checkpoint for a moment.</summary>
        public void ShowRestored() => _messageUntil = Time.time + 2f;

        private void FixedUpdate()
        {
            var inside = Physics2D.OverlapBox((Vector2)transform.position + triggerOffset, triggerSize, 0f, playerMask) != null;
            if (inside && !_playerInside) Reached?.Invoke(this);
            _playerInside = inside;
        }

        private void LateUpdate()
        {
            _message.Shown = Time.time < _messageUntil;
            _message.Anchor = transform.position + Vector3.up * 2.6f;
            _message.Color = litColor;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 1f, 1f, 0.5f);
            Gizmos.DrawWireCube((Vector2)transform.position + triggerOffset, triggerSize);
            Gizmos.DrawWireSphere(SpawnPosition, 0.2f);
        }
    }
}
