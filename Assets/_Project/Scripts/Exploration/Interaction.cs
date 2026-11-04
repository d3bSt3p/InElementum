using System.Collections.Generic;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>Something the explored character can use with the Interact button: a sign, an NPC, a chest.</summary>
    public interface IInteractable
    {
        /// <summary>The verb in the prompt, such as "Read" or "Talk".</summary>
        string PromptVerb { get; }
        /// <summary>World point the prompt floats at.</summary>
        Vector3 PromptAnchor { get; }
        /// <summary>World position the reach is measured from.</summary>
        Vector3 Position { get; }
        /// <summary>How close the player must stand, in units.</summary>
        float Reach { get; }
        bool CanInteract { get; }
        void Interact(PlayerController2D player);
        /// <summary>The player walked out of reach or lost the controls; close anything left open.</summary>
        void EndInteraction();
    }

    /// <summary>Every enabled interactable, so the player can find the nearest without physics queries.</summary>
    public static class Interactables
    {
        private static readonly List<IInteractable> All = new List<IInteractable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        public static void Register(IInteractable interactable)
        {
            if (!All.Contains(interactable)) All.Add(interactable);
        }

        public static void Unregister(IInteractable interactable) => All.Remove(interactable);

        /// <summary>The closest interactable that can be used and has <paramref name="position"/> within its reach.</summary>
        public static IInteractable Nearest(Vector3 position)
        {
            IInteractable best = null;
            var bestDistance = float.MaxValue;
            foreach (var interactable in All)
            {
                if (!interactable.CanInteract) continue;
                var distance = Vector2.Distance(position, interactable.Position);
                if (distance > interactable.Reach || distance >= bestDistance) continue;
                best = interactable;
                bestDistance = distance;
            }

            return best;
        }
    }
}
