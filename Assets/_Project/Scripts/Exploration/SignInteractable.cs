using InElementum.Core.Managers;
using InElementum.Data.Enums;
using InElementum.Core.UI;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// Sample interactable: a sign, or an NPC stub, that shows a line of text over itself until the player walks
    /// off or presses Interact again.
    /// </summary>
    public class SignInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string verb = "Read";
        [SerializeField] [TextArea(2, 5)] private string message = "Hello, traveller.";
        [SerializeField] [Min(0.1f)] private float reach = 1.4f;
        [Tooltip("Where the prompt and message float, relative to this object.")]
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private Color messageColor = Color.white;

        private WorldText _label;

        public bool IsOpen { get; private set; }
        public string Message { get => message; set => message = value; }

        public string PromptVerb => IsOpen ? "Close" : verb;
        public Vector3 PromptAnchor => transform.position + labelOffset + (IsOpen ? Vector3.down * 0.55f : Vector3.zero);
        public Vector3 Position => transform.position;
        public float Reach => reach;
        public bool CanInteract => isActiveAndEnabled;

        private void Awake() => _label = WorldUi.Add(new WorldText(boxed: true));

        private void OnDestroy() => _label?.Release();

        private void OnEnable() => Interactables.Register(this);

        private void OnDisable()
        {
            Interactables.Unregister(this);
            IsOpen = false;
            if (_label != null) _label.Shown = false;
        }

        public void Interact(PlayerController2D player)
        {
            IsOpen = !IsOpen;
            if (IsOpen) AudioManager.Play(AudioActionType.Interact);
        }

        public void EndInteraction() => IsOpen = false;

        private void LateUpdate()
        {
            _label.Shown = IsOpen;
            if (!IsOpen) return;
            _label.Anchor = transform.position + labelOffset + Vector3.up * 0.6f;
            _label.Text = message;
            _label.Color = messageColor;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
