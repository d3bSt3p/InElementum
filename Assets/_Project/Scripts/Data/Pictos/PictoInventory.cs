using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;
using InElementum.Data.Progression;

namespace InElementum.Data.Pictos
{
    /// <summary>
    /// The party's pictos: which it owns, who wears each, how close each is to mastery, and which Luminas the party
    /// has learned. Each picto has at most one wearer, found through the <see cref="Loadout"/>s made here. Plain C#;
    /// persistence comes later.
    /// </summary>
    public class PictoInventory
    {
        private readonly List<PictoData> _owned = new List<PictoData>();
        private readonly HashSet<PictoData> _learned = new HashSet<PictoData>();
        private readonly Dictionary<PictoData, int> _mastery = new Dictionary<PictoData, int>();
        private readonly List<Loadout> _loadouts = new List<Loadout>();

        public IReadOnlyList<PictoData> Owned => _owned;
        public IEnumerable<PictoData> Learned => _learned;
        public IReadOnlyList<Loadout> Loadouts => _loadouts;

        /// <summary>Adds a picto to the party. False if it is null or already owned.</summary>
        public bool Add(PictoData picto)
        {
            if (picto == null || _owned.Contains(picto)) return false;

            _owned.Add(picto);
            return true;
        }

        public bool Owns(PictoData picto) => picto != null && _owned.Contains(picto);

        /// <summary>Makes the loadout for a party member, or returns the one they already have.</summary>
        public Loadout CreateLoadout(PartyMemberData member, int pictoSlots, CharacterProgress progress = null)
        {
            var existing = GetLoadout(member);
            if (existing != null) return existing;

            var loadout = new Loadout(member, pictoSlots, progress, this);
            _loadouts.Add(loadout);
            return loadout;
        }

        public Loadout GetLoadout(PartyMemberData member) =>
            member != null ? _loadouts.FirstOrDefault(loadout => loadout.Member == member) : null;

        /// <summary>The loadout wearing the picto, or null.</summary>
        public Loadout GetWearer(PictoData picto) => _loadouts.FirstOrDefault(loadout => loadout.IsWearing(picto));

        /// <summary>Victories won while wearing the picto.</summary>
        public int GetMastery(PictoData picto) => picto != null && _mastery.TryGetValue(picto, out var count) ? count : 0;

        public bool IsLearned(PictoData picto) => picto != null && _learned.Contains(picto);

        /// <summary>Teaches the picto's Lumina to the whole party. False if it was already learned.</summary>
        public bool Learn(PictoData picto) => picto != null && _learned.Add(picto);

        /// <summary>
        /// Adds a victory to the mastery of every picto worn by a member who fought, and learns each Lumina that reaches
        /// its picto's <see cref="PictoData.BattlesToMaster"/>. Returns the newly learned ones.
        /// </summary>
        public List<PictoData> RecordVictory(IEnumerable<PartyMemberData> participants)
        {
            var learned = new List<PictoData>();
            var fought = new HashSet<PartyMemberData>(participants.Where(member => member != null));

            foreach (var loadout in _loadouts.Where(loadout => fought.Contains(loadout.Member)))
            {
                foreach (var picto in loadout.Pictos)
                {
                    _mastery[picto] = GetMastery(picto) + 1;
                    if (_mastery[picto] >= picto.BattlesToMaster && Learn(picto))
                        learned.Add(picto);
                }
            }

            return learned;
        }
    }
}
