using NUnit.Framework;

namespace MLin.Audio.Editor.Tests
{
    public class DuckingControllerTests
    {
        [Test]
        public void TriggeredRule_ReachesFullDuckAfterAttack()
        {
            DuckingController controller = new DuckingController(new[] { new DuckingRule(null, null, -12f, 0.2f, 0.5f) });

            controller.TriggerRule(0);
            controller.Update(0.1f);
            float halfway = controller.GetRuleGain(0);
            controller.Update(0.1f);

            float fullDuck = AudioMath.DecibelsToLinear(-12f);
            Assert.That(halfway, Is.InRange(fullDuck + 0.01f, 0.99f), "still moving after half the attack");
            Assert.AreEqual(fullDuck, controller.GetRuleGain(0), 0.0001f);
        }

        [Test]
        public void ReleasedRule_RecoversAfterReleaseTime()
        {
            DuckingController controller = new DuckingController(new[] { new DuckingRule(null, null, -12f, 0f, 0.4f) });
            controller.TriggerRule(0);
            controller.Update(0.016f);

            controller.ClearTriggers();
            controller.Update(0.2f);
            Assert.Less(controller.GetRuleGain(0), 1f);

            controller.Update(0.2f);
            Assert.AreEqual(1f, controller.GetRuleGain(0), 0.0001f);
        }

        [Test]
        public void ZeroAttack_DucksImmediately()
        {
            DuckingController controller = new DuckingController(new[] { new DuckingRule(null, null, -6f, 0f, 0f) });

            controller.TriggerRule(0);
            controller.Update(0.016f);

            Assert.AreEqual(AudioMath.DecibelsToLinear(-6f), controller.GetRuleGain(0), 0.0001f);
        }

        [Test]
        public void UntriggeredRule_StaysAtFullVolume()
        {
            DuckingController controller = new DuckingController(new[] { new DuckingRule(null, null, -20f, 0f, 0f) });

            controller.Update(1f);

            Assert.AreEqual(1f, controller.GetRuleGain(0));
        }

        [Test]
        public void NoRulesAndNullGroup_GiveFullGain()
        {
            DuckingController controller = new DuckingController(null);

            Assert.IsFalse(controller.HasRules);
            Assert.AreEqual(1f, controller.GetGain(null));
        }

        [Test]
        public void RuleWithoutGroups_IsInvalid()
        {
            Assert.IsFalse(new DuckingRule(null, null, -8f, 0.1f, 0.5f).IsValid);
        }
    }
}
