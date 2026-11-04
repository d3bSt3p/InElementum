using System.Collections.Generic;

namespace InElementum.Encounters
{
    /// <summary>What has happened in a room since it was entered: which overworld enemies are beaten. Plain C#.</summary>
    public sealed class RoomSession
    {
        private readonly HashSet<string> _defeated = new HashSet<string>();

        public IReadOnlyCollection<string> Defeated => _defeated;

        public bool IsDefeated(string enemyId) => !string.IsNullOrEmpty(enemyId) && _defeated.Contains(enemyId);

        /// <summary>Records a beaten enemy. Enemies without an id cannot be tracked and are ignored.</summary>
        public void MarkDefeated(string enemyId)
        {
            if (!string.IsNullOrEmpty(enemyId)) _defeated.Add(enemyId);
        }
    }
}
