using InElementum.Core.UI;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// Finds the nearest <see cref="IInteractable"/> in reach, shows its prompt over it, and uses it on Interact.
    /// The prompt is the GDD's minimal UI: the button and a verb, nothing else.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Color promptColor = new Color(1f, 0.95f, 0.8f);

        private PlayerController2D _player;
        private IInteractable _engaged;
        private WorldPrompt _prompt;

        /// <summary>The interactable whose prompt is showing, or null.</summary>
        public IInteractable Current { get; private set; }

        private void Awake()
        {
            _player = GetComponent<PlayerController2D>();
            _prompt = WorldUi.Add(new WorldPrompt());
        }

        private void OnDestroy() => _prompt?.Release();

        private void OnDisable()
        {
            _engaged?.EndInteraction();
            _engaged = null;
            Current = null;
            if (_prompt != null) _prompt.Shown = false;
        }

        private void Update()
        {
            Current = _player.ControlsEnabled && !_player.InputLocked ? Interactables.Nearest(transform.position) : null;

            if (_engaged != null && _engaged != Current)
            {
                _engaged.EndInteraction();
                _engaged = null;
            }

            if (Current != null && _player.Input.InteractPressed)
            {
                Current.Interact(_player);
                _engaged = Current;
            }

            _prompt.Shown = Current != null && _player.Input != null;
            if (!_prompt.Shown) return;
            _prompt.Anchor = Current.PromptAnchor;
            _prompt.Set(_player.Input.InteractGlyph, _player.Input.UsingGamepad, Current.PromptVerb, promptColor);
        }
    }
}
