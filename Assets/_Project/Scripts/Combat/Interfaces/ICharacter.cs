using InElementum.Combat.Characters;
using InElementum.Data.Enums;

namespace InElementum.Combat.Interfaces
{
    public interface ICharacter
    {
        public CharacterBase GetCharacterBase();
        public CharacterType GetCharacterType();
    }
}