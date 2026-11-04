using InElementum.Combat.Characters;
using UnityEngine;

namespace InElementum.Combat.Interfaces
{
    /// <summary>What effects need from a fighter. The view transforms may be null, so effects run without a scene.</summary>
    public interface ICombatant
    {
        CharacterStats Stats { get; }
        Transform ViewRoot { get; }
        Transform TextRoot { get; }
    }
}
