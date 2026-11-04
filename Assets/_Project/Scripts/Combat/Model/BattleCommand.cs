using InElementum.Data.Actions;

namespace InElementum.Combat.Model
{
    public enum CommandType
    {
        Attack,
        Ability,
        Item,
        Surge,
        Skip
    }

    /// <summary>The player's choice for the actor waiting at the Cast boundary. One action per turn.</summary>
    public sealed class BattleCommand
    {
        private BattleCommand(CommandType type, CombatActionData action, Combatant target)
        {
            Type = type;
            Action = action;
            Target = target;
        }

        public CommandType Type { get; }
        /// <summary>Null for Attack (the actor's basic attack is used) and Skip.</summary>
        public CombatActionData Action { get; }
        /// <summary>The chosen target for single-target effects. Null for Skip, Self, and all-target actions.</summary>
        public Combatant Target { get; }

        public static BattleCommand Attack(Combatant target) => new BattleCommand(CommandType.Attack, null, target);
        public static BattleCommand Ability(AbilityData ability, Combatant target) => new BattleCommand(CommandType.Ability, ability, target);
        public static BattleCommand Item(ItemData item, Combatant target) => new BattleCommand(CommandType.Item, item, target);
        public static BattleCommand Surge(SurgeData surge, Combatant target) => new BattleCommand(CommandType.Surge, surge, target);
        public static BattleCommand Skip() => new BattleCommand(CommandType.Skip, null, null);

        public override string ToString() => Action != null ? $"{Type} {Action.DisplayName}" : Type.ToString();
    }
}
