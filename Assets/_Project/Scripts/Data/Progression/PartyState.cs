using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Pictos;
using InElementum.Data.Settings;
using InElementum.Data.SkillTrees;
using InElementum.Data.Weapons;

namespace InElementum.Data.Progression
{
    public readonly struct LevelUp
    {
        public LevelUp(PartyMemberData member, int level, int levelsGained)
        {
            Member = member;
            Level = level;
            LevelsGained = levelsGained;
        }

        public PartyMemberData Member { get; }
        /// <summary>The level reached.</summary>
        public int Level { get; }
        public int LevelsGained { get; }
    }

    /// <summary>What a victory changed for the party.</summary>
    public sealed class VictoryReport
    {
        public List<LevelUp> LevelUps { get; } = new List<LevelUp>();
        /// <summary>Picto drops the party did not own yet.</summary>
        public List<PictoData> NewPictos { get; } = new List<PictoData>();
        public List<PictoData> LearnedLuminas { get; } = new List<PictoData>();
    }

    /// <summary>
    /// The party's progression between battles: each member's level, loadout, and skill tree, and the shared picto
    /// and weapon inventories.
    /// Plain C#. The debug battle builds one; Phase 3's party service will own it.
    /// </summary>
    public class PartyState
    {
        private readonly CombatTuning _combatTuning;
        private readonly ProgressionTuning _progressionTuning;
        private readonly Dictionary<PartyMemberData, CharacterProgress> _progress = new Dictionary<PartyMemberData, CharacterProgress>();
        private readonly Dictionary<PartyMemberData, SkillTreeState> _skillTrees = new Dictionary<PartyMemberData, SkillTreeState>();

        public PartyState(CombatTuning combatTuning, ProgressionTuning progressionTuning)
        {
            _combatTuning = combatTuning != null ? combatTuning : throw new ArgumentNullException(nameof(combatTuning));
            _progressionTuning = progressionTuning != null ? progressionTuning : throw new ArgumentNullException(nameof(progressionTuning));
        }

        public PictoInventory Pictos { get; } = new PictoInventory();
        /// <summary>The party's weapons, their levels, and who holds each.</summary>
        public WeaponInventory Weapons { get; } = new WeaponInventory();
        /// <summary>Relationship levels between members, which Fusion Surges and relationship abilities need.</summary>
        public RelationshipService Relationships { get; } = new RelationshipService();
        /// <summary>Which enemy weaknesses and resistances the party has found.</summary>
        public ElementKnowledge Elements { get; } = new ElementKnowledge();
        public IEnumerable<PartyMemberData> Members => _progress.Keys;

        /// <summary>
        /// Adds a member at <paramref name="level"/>. Their default pictos join the inventory, and they wear each one no
        /// other member already wears. Their starting weapons join the party too, and they hold the first one nobody
        /// else holds. Does nothing if they are already in the party.
        /// </summary>
        public void AddMember(PartyMemberData member, int level = 1)
        {
            if (member == null || _progress.ContainsKey(member)) return;

            var progress = new CharacterProgress(_progressionTuning, level);
            _progress.Add(member, progress);
            if (member.SkillTree != null) _skillTrees.Add(member, new SkillTreeState(member.SkillTree, progress));

            var loadout = Pictos.CreateLoadout(member, _combatTuning.PictoSlots, progress);
            foreach (var picto in member.DefaultPictos.Where(picto => picto != null))
            {
                Pictos.Add(picto);
                loadout.Equip(picto);
            }

            foreach (var weapon in member.StartingWeapons.Where(weapon => weapon != null))
            {
                Weapons.Add(weapon);
                if (Weapons.GetWeapon(member) == null) Weapons.Equip(member, weapon);
            }
        }

        public CharacterProgress GetProgress(PartyMemberData member) =>
            member != null && _progress.TryGetValue(member, out var progress) ? progress : null;

        public Loadout GetLoadout(PartyMemberData member) => Pictos.GetLoadout(member);

        /// <summary>The member's skill tree progress, or null when they have no tree.</summary>
        public SkillTreeState GetSkillTree(PartyMemberData member) =>
            member != null && _skillTrees.TryGetValue(member, out var tree) ? tree : null;

        /// <summary>Abilities and Surges every member has unlocked through their skill tree.</summary>
        public IEnumerable<CombatActionData> SkillTreeActions => _skillTrees.Values.SelectMany(tree => tree.UnlockedActions);

        /// <summary>
        /// Grants every participant the battle's <paramref name="experience"/>, adds a victory to the mastery of the
        /// pictos they wore, then adds the picto drops to the inventory. Members not in the party are ignored.
        /// </summary>
        public VictoryReport ApplyVictory(IEnumerable<PartyMemberData> participants, int experience, IEnumerable<PictoData> pictoDrops)
        {
            var report = new VictoryReport();
            var fought = participants.Where(member => member != null && _progress.ContainsKey(member)).Distinct().ToList();

            foreach (var member in fought)
            {
                var progress = _progress[member];
                var gained = progress.AddXp(experience);
                if (gained > 0) report.LevelUps.Add(new LevelUp(member, progress.Level, gained));
            }

            report.LearnedLuminas.AddRange(Pictos.RecordVictory(fought));

            if (pictoDrops != null)
            {
                foreach (var picto in pictoDrops)
                {
                    if (Pictos.Add(picto)) report.NewPictos.Add(picto);
                }
            }

            return report;
        }

        public static PartyState FromMembers(IEnumerable<PartyMemberData> members, CombatTuning combatTuning,
            ProgressionTuning progressionTuning)
        {
            var party = new PartyState(combatTuning, progressionTuning);
            foreach (var member in members)
                party.AddMember(member);

            return party;
        }
    }
}
