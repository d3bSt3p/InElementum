using System.Linq;
using InElementum.Combat.Actions;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class PortSmokeTests
    {
        [Test]
        public void RuntimeAssemblyHoldsNoNueDeckOrDeckbuilderTypes()
        {
            var removed = new[]
            {
                "CollectionManager", "HandController", "CardBase", "Card3D", "CardUI", "DeckData", "MapManager",
                "EncounterButton", "ShopCanvas", "RewardCanvas", "InventoryCanvas", "InformationCanvas",
                "CardData", "CardActionBase", "CardActionProcessor", "CardActionData", "CardActionType",
                "EnemyActionBase", "EnemyActionProcessor", "EnemyActionType", "StatusType", "AllyCharacterData"
            };

            var types = typeof(EffectBase).Assembly.GetTypes();

            Assert.That(types.Where(t => t.Namespace != null && t.Namespace.StartsWith("NueGames")), Is.Empty);
            Assert.That(types.Where(t => removed.Contains(t.Name)), Is.Empty);
        }
    }
}
