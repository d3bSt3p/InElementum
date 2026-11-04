using System.Collections.Generic;
using InElementum.Data.Characters;
using InElementum.Data.Items;
using InElementum.Data.Pictos;
using InElementum.Data.Progression;

namespace InElementum.Combat.Model
{
    public enum BattleOutcome
    {
        Victory,
        Defeat
    }

    public sealed class BattleRewards
    {
        public int Experience { get; internal set; }
        public int Currency { get; internal set; }
        public List<(MaterialData material, int amount)> Materials { get; } = new List<(MaterialData, int)>();
        public List<PictoData> Pictos { get; } = new List<PictoData>();

        /// <summary>Totals every defeated enemy's <see cref="RewardTable"/>, rolling currency and drop chances.</summary>
        public static BattleRewards Roll(IEnumerable<Combatant> enemies, IRandomSource random)
        {
            var rewards = new BattleRewards();

            foreach (var enemy in enemies)
            {
                var table = enemy.Enemy != null ? enemy.Enemy.Rewards : null;
                if (table == null) continue;

                rewards.Experience += table.Experience;
                rewards.Currency += random.Range(table.CurrencyMin, table.CurrencyMax + 1);

                foreach (var drop in table.MaterialDrops)
                {
                    if (drop.Material != null && random.Value() < drop.Chance)
                        rewards.Materials.Add((drop.Material, drop.Amount));
                }

                if (table.PictoDrop != null)
                    rewards.Pictos.Add(table.PictoDrop);
            }

            return rewards;
        }
    }

    public sealed class BattleResult
    {
        public BattleResult(BattleOutcome outcome, BattleRewards rewards, IReadOnlyList<Combatant> participants)
        {
            Outcome = outcome;
            Rewards = rewards;
            Participants = participants;
        }

        public BattleOutcome Outcome { get; }
        /// <summary>Null on defeat.</summary>
        public BattleRewards Rewards { get; }
        /// <summary>Every party member who fought. Drives XP and picto mastery, later relationships.</summary>
        public IReadOnlyList<Combatant> Participants { get; }
        /// <summary>Every pair of participants, on a victory. Each pair was raised through <see cref="RelationshipService.FoughtTogether"/>.</summary>
        public List<PartyPair> FoughtTogether { get; } = new List<PartyPair>();
    }
}
