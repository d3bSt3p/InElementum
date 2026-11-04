using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Data.Stats;

namespace InElementum.Data.Pictos
{
    /// <summary>Why a picto or Lumina could not be equipped. <see cref="None"/> means it was.</summary>
    public enum LoadoutError
    {
        None,
        Missing,
        AlreadyEquipped,
        SlotsFull,
        NotOwned,
        WornByAnother,
        /// <summary>Another worn picto or equipped Lumina already gives the same passive.</summary>
        DuplicateEffect,
        /// <summary>The Lumina's own picto is worn by this character.</summary>
        PictoWorn,
        NotLearned,
        NotEnoughLuminaPoints
    }

    /// <summary>
    /// What one party member wears: up to <see cref="PictoSlots"/> pictos, plus any learned Luminas their Lumina points
    /// cover. A loadout made by a <see cref="PictoInventory"/> also enforces ownership, one wearer per picto, and which
    /// Luminas the party has learned. A standalone loadout skips ownership and cannot equip Luminas.
    /// </summary>
    public class Loadout
    {
        private readonly List<PictoData> _pictos = new List<PictoData>();
        private readonly List<PictoData> _luminas = new List<PictoData>();

        public Loadout(int pictoSlots, CharacterProgress progress = null)
            : this(null, pictoSlots, progress, null)
        {
        }

        internal Loadout(PartyMemberData member, int pictoSlots, CharacterProgress progress, PictoInventory inventory)
        {
            Member = member;
            PictoSlots = pictoSlots;
            Progress = progress;
            Inventory = inventory;
        }

        public PartyMemberData Member { get; }
        public int PictoSlots { get; }
        /// <summary>Supplies the Lumina point budget. Null means no points.</summary>
        public CharacterProgress Progress { get; }
        /// <summary>Null for a standalone loadout.</summary>
        public PictoInventory Inventory { get; }

        public IReadOnlyList<PictoData> Pictos => _pictos;
        /// <summary>Equipped Luminas, listed by the picto they were learned from.</summary>
        public IReadOnlyList<PictoData> Luminas => _luminas;

        public int LuminaPoints => Progress?.LuminaPoints ?? 0;
        public int LuminaPointsSpent => _luminas.Sum(lumina => lumina.LuminaCost);
        public int LuminaPointsLeft => LuminaPoints - LuminaPointsSpent;

        /// <summary>Stat modifiers from worn pictos. Luminas give none.</summary>
        public IEnumerable<StatModifier> Modifiers => _pictos.SelectMany(picto => picto.Modifiers);

        /// <summary>Every passive in effect, from worn pictos and equipped Luminas.</summary>
        public IEnumerable<PassiveData> Passives =>
            _pictos.Concat(_luminas).Select(source => source.Passive).Where(passive => passive != null);

        public int ExtraAttackHits => Passives.Sum(passive => passive.ExtraAttackHits);

        public bool IsWearing(PictoData picto) => _pictos.Contains(picto);
        public bool HasLumina(PictoData picto) => _luminas.Contains(picto);

        #region Pictos
        public LoadoutError CanEquip(PictoData picto)
        {
            if (picto == null) return LoadoutError.Missing;
            if (_pictos.Contains(picto)) return LoadoutError.AlreadyEquipped;

            if (Inventory != null)
            {
                if (!Inventory.Owns(picto)) return LoadoutError.NotOwned;

                var wearer = Inventory.GetWearer(picto);
                if (wearer != null && wearer != this) return LoadoutError.WornByAnother;
            }

            if (_pictos.Count >= PictoSlots) return LoadoutError.SlotsFull;
            if (GivesPassive(picto.Passive)) return LoadoutError.DuplicateEffect;
            return LoadoutError.None;
        }

        public LoadoutError Equip(PictoData picto)
        {
            var error = CanEquip(picto);
            if (error == LoadoutError.None) _pictos.Add(picto);
            return error;
        }

        public bool Unequip(PictoData picto) => _pictos.Remove(picto);

        /// <summary>
        /// Puts <paramref name="picto"/> in slot <paramref name="slot"/>, as the menu does. A picto already in that slot is
        /// taken off; a picto this character already wears moves there. With <paramref name="takeFromWearer"/>, a picto
        /// another member wears is taken from them. Nothing changes when the result is an error.
        /// </summary>
        public LoadoutError EquipAt(int slot, PictoData picto, bool takeFromWearer = false)
        {
            if (picto == null) return LoadoutError.Missing;
            if (slot < 0 || slot >= PictoSlots) return LoadoutError.SlotsFull;

            var current = _pictos.IndexOf(picto);
            if (current >= 0)
            {
                _pictos.RemoveAt(current);
                _pictos.Insert(System.Math.Min(slot, _pictos.Count), picto);
                return LoadoutError.None;
            }

            Loadout previousWearer = null;
            var previousIndex = -1;
            if (takeFromWearer && Inventory != null)
            {
                var wearer = Inventory.GetWearer(picto);
                if (wearer != null && wearer != this)
                {
                    previousWearer = wearer;
                    previousIndex = wearer._pictos.IndexOf(picto);
                    wearer._pictos.RemoveAt(previousIndex);
                }
            }

            PictoData replaced = null;
            if (slot < _pictos.Count)
            {
                replaced = _pictos[slot];
                _pictos.RemoveAt(slot);
            }

            var error = CanEquip(picto);
            if (error == LoadoutError.None)
            {
                _pictos.Insert(System.Math.Min(slot, _pictos.Count), picto);
                return error;
            }

            if (replaced != null) _pictos.Insert(slot, replaced);
            if (previousWearer != null) previousWearer._pictos.Insert(previousIndex, picto);
            return error;
        }
        #endregion

        #region Luminas
        public LoadoutError CanEquipLumina(PictoData picto)
        {
            if (picto == null) return LoadoutError.Missing;
            if (_luminas.Contains(picto)) return LoadoutError.AlreadyEquipped;
            if (Inventory == null || !Inventory.IsLearned(picto)) return LoadoutError.NotLearned;
            if (_pictos.Contains(picto)) return LoadoutError.PictoWorn;
            if (GivesPassive(picto.Passive)) return LoadoutError.DuplicateEffect;
            if (picto.LuminaCost > LuminaPointsLeft) return LoadoutError.NotEnoughLuminaPoints;
            return LoadoutError.None;
        }

        public LoadoutError EquipLumina(PictoData picto)
        {
            var error = CanEquipLumina(picto);
            if (error == LoadoutError.None) _luminas.Add(picto);
            return error;
        }

        public bool UnequipLumina(PictoData picto) => _luminas.Remove(picto);
        #endregion

        private bool GivesPassive(PassiveData passive) => passive != null && Passives.Contains(passive);

        /// <summary>A standalone loadout wearing the member's default pictos, as many as fit.</summary>
        public static Loadout FromDefaults(PartyMemberData member, CombatTuning tuning, CharacterProgress progress = null)
        {
            var loadout = new Loadout(tuning.PictoSlots, progress);
            foreach (var picto in member.DefaultPictos)
                loadout.Equip(picto);

            return loadout;
        }
    }
}
