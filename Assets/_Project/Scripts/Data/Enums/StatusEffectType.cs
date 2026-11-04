namespace InElementum.Data.Enums
{
    public enum StatusEffectType
    {
        None = 0,
        Poison,
        Burn,
        Stun,
        Weakness,
        Soft,
        Thorns,
        StatBuff,
        StatDebuff,
        /// <summary>Enemy single-target attacks go to this unit. Lasts until its next turn starts.</summary>
        Taunt
    }
}
