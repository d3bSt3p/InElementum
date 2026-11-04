namespace InElementum.Data.Enums
{
    /// <summary>
    /// What a sound is for. <see cref="Containers.SoundProfileData"/> assets pick one, and the
    /// <see cref="Core.Managers.AudioManager"/> plays a random clip from the profile for it.
    /// Assets store these as numbers: only ever add new values at the end.
    /// </summary>
    public enum AudioActionType
    {
        // Combat actions (from NueDeck). A combat action's Audio Type picks one of these.
        Attack,
        Bite,
        Block,
        Button,
        Draw,
        Drink,
        EnemyDeath,
        Heal,
        Poison,
        Swing,
        Debuff,
        Buff,
        Plant,
        Tree,
        Water,
        Burn,

        // Combat feedback
        Dodge,
        QteSuccess,
        QteFail,
        Break,
        Victory,
        Defeat,
        EncounterStart,

        // Exploration
        Footstep,
        FootstepWater,
        Jump,
        Land,
        Interact,
        Checkpoint,
        CharacterSwitch,

        // Menus
        MenuOpen,
        MenuClose,
        MenuMove,
        MenuConfirm,
        MenuBack,
        MenuTab,
        EquipWeapon,
        EquipPicto,
        LearnSkill,
        Coin,

        // Loops: music and ambience channels
        MusicExploration,
        MusicBattle,
        AmbienceExploration
    }
}
