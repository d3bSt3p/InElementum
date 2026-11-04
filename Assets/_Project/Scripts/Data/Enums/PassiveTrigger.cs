namespace InElementum.Data.Enums
{
    /// <summary>When a picto or Lumina passive's effects fire. Effects never set off another passive.</summary>
    public enum PassiveTrigger
    {
        /// <summary>Always-on fields, such as extra attack hits. Any effects apply as the wearer joins the battle.</summary>
        Always,
        /// <summary>As the wearer joins the battle, at its start.</summary>
        OnBattleStart,
        /// <summary>Each time the wearer reaches Cast, after statuses tick, even if a stun then costs the turn.</summary>
        OnTurnStart,
        /// <summary>Each of the wearer's damaging hits that lands on an enemy. Single-enemy effects aim at that enemy.</summary>
        OnHit,
        /// <summary>Each enemy hit the wearer parries. Single-enemy effects aim at the attacker.</summary>
        OnParry,
        /// <summary>Each enemy hit the wearer dodges. Single-enemy effects aim at the attacker.</summary>
        OnDodge,
        /// <summary>Each enemy the wearer's hit fells. Single-enemy effects aim at a random enemy left standing.</summary>
        OnKill
    }
}
