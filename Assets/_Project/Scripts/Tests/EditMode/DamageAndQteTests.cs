using InElementum.Combat.Model;
using InElementum.Data.Enums;
using InElementum.Data.Settings;
using InElementum.Data.Stats;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class DamageAndQteTests
    {
        private readonly TestAssets _assets = new TestAssets();
        private CombatTuning _tuning;

        private static readonly StatBlock Tens = new StatBlock(10, 10, 10, 10, 10);

        [SetUp]
        public void SetUp()
        {
            _tuning = _assets.Tuning();
            _tuning.StrengthCoefficient = 1f;
            _tuning.DefenseCoefficient = 0.5f;
            _tuning.MinimumDamage = 1;
            _tuning.BaseCritChance = 0.05f;
            _tuning.CritChancePerLuck = 0.01f;
            _tuning.CritMultiplier = 1.5f;
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private DamageResult Calculate(float elementMultiplier = 1f, bool crit = false, float power = 10f)
        {
            var request = DamageRequest.Create(power, Tens, Tens);
            request.ElementMultiplier = elementMultiplier;
            request.IsCrit = crit;
            return DamageCalculator.Calculate(request, _tuning);
        }

        [Test]
        public void StrengthAddsAndDefenseSubtracts()
        {
            // (10 + 10 * 1) - 10 * 0.5
            Assert.That(Calculate().Amount, Is.EqualTo(15));
            Assert.That(Calculate().Reaction, Is.EqualTo(ElementReaction.Normal));
        }

        [Test]
        public void WeaknessMultipliesDamage()
        {
            var result = Calculate(2f);

            Assert.That(result.Amount, Is.EqualTo(30));
            Assert.That(result.Reaction, Is.EqualTo(ElementReaction.Weak));
        }

        [Test]
        public void ResistanceReducesDamageRoundingHalvesUp()
        {
            var result = Calculate(0.5f);

            Assert.That(result.Amount, Is.EqualTo(8));
            Assert.That(result.Reaction, Is.EqualTo(ElementReaction.Resist));
        }

        [Test]
        public void ImmunityDealsNothing()
        {
            var result = Calculate(0f);

            Assert.That(result.Amount, Is.EqualTo(0));
            Assert.That(result.Reaction, Is.EqualTo(ElementReaction.Immune));
        }

        [Test]
        public void CritMultipliesFinalDamage()
        {
            var result = Calculate(crit: true);

            Assert.That(result.Amount, Is.EqualTo(23));
            Assert.That(result.IsCrit, Is.True);
        }

        [Test]
        public void DamageNeverFallsBelowMinimum()
        {
            var request = DamageRequest.Create(0f, new StatBlock(), new StatBlock(0, 100, 0, 0, 0));

            Assert.That(DamageCalculator.Calculate(request, _tuning).Amount, Is.EqualTo(1));
        }

        [Test]
        public void MultiplierAndFlatReductionApplyToRawDamage()
        {
            var request = DamageRequest.Create(10f, Tens, Tens);
            request.Multiplier = 2f;
            request.FlatReduction = 5;

            // (10 + 10) * 2 - 5 - 5
            Assert.That(DamageCalculator.Calculate(request, _tuning).Amount, Is.EqualTo(30));
        }

        [Test]
        public void CritChanceGrowsWithLuck()
        {
            Assert.That(DamageCalculator.GetCritChance(Tens, _tuning), Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(DamageCalculator.RollCrit(Tens, _tuning, new FixedRandom(0.1f)), Is.True);
            Assert.That(DamageCalculator.RollCrit(Tens, _tuning, new FixedRandom(0.2f)), Is.False);
        }

        [Test]
        public void QteJudgesPerfectSuccessAndFail()
        {
            Assert.That(QteJudge.Judge(null, 0.2f, 0.25f), Is.EqualTo(QteResult.Fail));
            Assert.That(QteJudge.Judge(0.04f, 0.2f, 0.25f), Is.EqualTo(QteResult.Perfect));
            Assert.That(QteJudge.Judge(-0.15f, 0.2f, 0.25f), Is.EqualTo(QteResult.Success));
            Assert.That(QteJudge.Judge(0.25f, 0.2f, 0.25f), Is.EqualTo(QteResult.Fail));
        }

        [Test]
        public void ParryWindowIsTighterThanDodge()
        {
            Assert.That(QteJudge.JudgeDefense(DefenseInput.Dodge, 0.15f, _tuning), Is.EqualTo(DefenseOutcome.Dodged));
            Assert.That(QteJudge.JudgeDefense(DefenseInput.Parry, 0.15f, _tuning), Is.EqualTo(DefenseOutcome.Hit));
            Assert.That(QteJudge.JudgeDefense(DefenseInput.Parry, 0.05f, _tuning), Is.EqualTo(DefenseOutcome.Parried));
            Assert.That(QteJudge.JudgeDefense(DefenseInput.None, 0f, _tuning), Is.EqualTo(DefenseOutcome.Hit));
        }
    }
}
