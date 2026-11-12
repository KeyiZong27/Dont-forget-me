using System;
using NUnit.Framework;

namespace DontForgetMe.Tests
{
    [TestFixture]
    public sealed class OpeningTests
    {
        [Test]
        public void EntireOpeningCanFinishWithoutGivingWaterOrKillingAnyone()
        {
            OpeningState state = ReadyForRepair();
            Assert.That(state.StartRepair(), Is.True);
            state.TickRepair(14);
            Assert.That(state.Phase, Is.EqualTo(OpeningPhase.Regroup));
            Assert.That(state.Finish(true), Is.True);
            Assert.That(state.Phase, Is.EqualTo(OpeningPhase.Complete));
            Assert.That(state.GaveWater, Is.False);
        }

        [Test]
        public void GivingWaterIsOptionalAndOnlyHappensOnce()
        {
            OpeningState state = new OpeningState();
            Assert.That(state.GiveWater(), Is.True);
            Assert.That(state.GiveWater(), Is.False);
            state.MeetZhou();
            Assert.That(state.GiveWater(), Is.False);
            Assert.That(state.GaveWater, Is.True);
        }

        [Test]
        public void StagesCannotBeSkipped()
        {
            OpeningState state = new OpeningState();
            Assert.That(state.Reunite(), Is.False);
            Assert.That(state.EnterAlley(), Is.False);
            Assert.That(state.StartRepair(), Is.False);
            Assert.That(state.Finish(true), Is.False);
            state.TickRepair(100);
            Assert.That(state.RepairProgress, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(OpeningPhase.Street));
        }

        [Test]
        public void DuplicateCrateCannotStartRepair()
        {
            OpeningState state = ReadyForDebris();
            Assert.That(state.ClearDebris("left"), Is.True);
            Assert.That(state.ClearDebris("left"), Is.False);
            Assert.That(state.StartRepair(), Is.False);
            Assert.That(state.ClearDebris("right"), Is.True);
            Assert.That(state.StartRepair(), Is.True);
            Assert.That(state.StartRepair(), Is.False);
        }

        [Test]
        public void RepairThresholdIsFourteenSecondsAndProgressIsClamped()
        {
            OpeningState state = ReadyForRepair();
            state.StartRepair();
            state.TickRepair(13.9f);
            Assert.That(state.Phase, Is.EqualTo(OpeningPhase.Repair));
            state.TickRepair(0.11f);
            Assert.That(state.Phase, Is.EqualTo(OpeningPhase.Regroup));
            Assert.That(state.RepairProgress, Is.EqualTo(1));
            state.TickRepair(500);
            Assert.That(state.RepairProgress, Is.EqualTo(1));
        }

        [Test]
        public void CompletionRequiresAllCompanions()
        {
            OpeningState state = ReadyForRepair();
            state.StartRepair();
            state.TickRepair(14);
            Assert.That(state.Finish(false), Is.False);
            Assert.That(state.Phase, Is.EqualTo(OpeningPhase.Regroup));
            Assert.That(state.Finish(true), Is.True);
            Assert.That(state.Finish(true), Is.False);
        }

        [Test]
        public void ALargerPileAndLongerRepairAreHonoured()
        {
            OpeningState state = new OpeningState(4, 40f);
            state.MeetZhou();
            state.Reunite();
            state.EnterAlley();
            foreach (string id in new[] { "a", "b", "c" }) Assert.That(state.ClearDebris(id), Is.True);
            Assert.That(state.StartRepair(), Is.False);
            Assert.That(state.ClearDebris("d"), Is.True);
            Assert.That(state.ClearDebris("e"), Is.False);
            Assert.That(state.StartRepair(), Is.True);
            state.TickRepair(39f);
            Assert.That(state.Phase, Is.EqualTo(OpeningPhase.Repair));
            state.TickRepair(1.01f);
            Assert.That(state.Phase, Is.EqualTo(OpeningPhase.Regroup));
        }

        [TestCase(0, 14f)]
        [TestCase(2, 0f)]
        [TestCase(2, float.NaN)]
        public void InvalidOpeningSetupFailsExplicitly(int debris, float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OpeningState(debris, seconds));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidRepairTimeFailsExplicitly(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OpeningState().TickRepair(seconds));
        }

        [Test]
        public void EmptyDebrisIdFailsExplicitly()
        {
            Assert.Throws<ArgumentException>(() => ReadyForDebris().ClearDebris(""));
        }

        private static OpeningState ReadyForDebris()
        {
            OpeningState state = new OpeningState();
            state.MeetZhou();
            state.Reunite();
            state.EnterAlley();
            return state;
        }

        private static OpeningState ReadyForRepair()
        {
            OpeningState state = ReadyForDebris();
            state.ClearDebris("left");
            state.ClearDebris("right");
            return state;
        }
    }

    [TestFixture]
    public sealed class MemoryTests
    {
        [Test]
        public void OneWayRecognitionProtectsOnlyTheSubject()
        {
            MemoryNetwork network = new MemoryNetwork();
            network.Recognize("woman", "zhou");
            Assert.That(network.IsRemembered("zhou"), Is.True);
            Assert.That(network.IsRemembered("woman"), Is.False);
        }

        [Test]
        public void DogCanRemainTheLastEffectiveObserver()
        {
            MemoryNetwork network = new MemoryNetwork();
            network.Recognize("player", "zhou");
            network.Recognize("dog", "zhou");
            network.DecayObserver("player", 1f);
            Assert.That(network.IsRemembered("zhou"), Is.True);
            network.DecayObserver("dog", 1f);
            Assert.That(network.IsRemembered("zhou"), Is.False);
        }

        [Test]
        public void PartialDecayRetainsProtectionIncludingDuringSleep()
        {
            MemoryNetwork network = new MemoryNetwork();
            network.Recognize("woman", "player");
            // Sleep uses the same decay operation; consciousness is not a protection gate.
            network.DecayObserver("woman", 0.7f);
            Assert.That(network.Strength("woman", "player"), Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(network.IsRemembered("player"), Is.True);
            network.DecayObserver("woman", 0.4f);
            Assert.That(network.Strength("woman", "player"), Is.Zero);
            Assert.That(network.IsRemembered("player"), Is.False);
        }

        [Test]
        public void RecognitionReestablishesAnExhaustedLinkWithoutAnyThreshold()
        {
            MemoryNetwork network = new MemoryNetwork();
            network.Recognize("player", "zhou");
            network.DecayObserver("player", 1f);
            Assert.That(network.IsRemembered("zhou"), Is.False);
            network.Recognize("player", "zhou");
            Assert.That(network.IsRemembered("zhou"), Is.True);
            Assert.That(network.Strength("player", "zhou"), Is.EqualTo(1f));
        }

        [Test]
        public void MemoryRemainsUntilAnExplicitWorldDecayOperation()
        {
            MemoryNetwork network = new MemoryNetwork();
            network.Recognize("player", "zhou");
            for (int i = 0; i < 100; i++)
                Assert.That(network.IsRemembered("zhou"), Is.True);
            Assert.That(network.Strength("player", "zhou"), Is.EqualTo(1f));
        }

        [Test]
        public void OutsideObserversAlsoProtectTheSubject()
        {
            MemoryNetwork network = new MemoryNetwork();
            network.Recognize("distantFriend", "zhou");
            network.Recognize("player", "zhou");
            network.DecayObserver("player", 1f);
            Assert.That(network.IsRemembered("zhou"), Is.True);
        }

        [Test]
        public void RecognitionDoesNotEraseOtherRelationships()
        {
            MemoryNetwork network = new MemoryNetwork();
            network.Recognize("player", "woman");
            network.DecayObserver("player", 0.5f);
            network.Recognize("player", "zhou");
            Assert.That(network.Strength("player", "woman"), Is.EqualTo(0.5f));
            Assert.That(network.Strength("player", "zhou"), Is.EqualTo(1f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDecayFailsExplicitly(float amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryNetwork().DecayObserver("player", amount));
        }

        [TestCase("", "zhou")]
        [TestCase("player", "")]
        [TestCase("player", "player")]
        public void InvalidRelationshipsFailExplicitly(string observer, string subject)
        {
            Assert.Throws<ArgumentException>(() => new MemoryNetwork().Recognize(observer, subject));
        }
    }
}
