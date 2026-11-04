namespace InElementum.Data.Enums
{
    /// <summary>How an ability or surge becomes available to the player.</summary>
    public enum UnlockSource
    {
        Default,
        SkillTree,
        RelationshipLevel3,
        RelationshipLevel5,
        MainStory,
        SideContent
    }

    public enum QteType
    {
        TimedPress,
        Mash,
        Sequence
    }

    public enum QteResult
    {
        None,
        Fail,
        Success,
        Perfect
    }
}
