using System;
using NUnit.Framework;

namespace DontForgetMe.Tests
{
    public sealed class SandboxForgettingTests
    {
        [Test]
        public void StartsNearlyForgottenButProtectedUntilUserStartsDecay()
        {
            SandboxForgettingState state = new SandboxForgettingState();
            Assert.That(state.PlayerMemory, Is.EqualTo(0.12f).Within(0.00001f));
            Assert.That(state.AutoDecay, Is.False);
            state.Tick(100);
            Assert.That(state.IsProtected, Is.True);
            Assert.That(state.Progress, Is.Zero);
        }

        [Test]
        public void DecayAndTransitionUseOnlyTimeAfterLastMemoryReachesZero()
        {
            SandboxForgettingState state = new SandboxForgettingState { AutoDecay = true };
            state.Tick(10);
            Assert.That(state.PlayerMemory, Is.EqualTo(0.02f).Within(0.00001f));
            Assert.That(state.Progress, Is.Zero);
            state.Tick(8);
            Assert.That(state.PlayerMemory, Is.Zero);
            Assert.That(state.Progress, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(state.IsHostile, Is.False);
            state.Tick(6.01f);
            Assert.That(state.IsHostile, Is.True);
            Assert.That(state.Progress, Is.EqualTo(1));
        }

        [Test]
        public void AnotherObserverProtectsEvenWhenPlayerMemoryIsZero()
        {
            SandboxForgettingState state = new SandboxForgettingState();
            state.SetWitnessMemory(1);
            state.SetPlayerMemory(0);
            state.Tick(100);
            Assert.That(state.IsProtected, Is.True);
            Assert.That(state.Progress, Is.Zero);
            state.SetWitnessMemory(0);
            state.Tick(6);
            Assert.That(state.Progress, Is.EqualTo(0.5f));
        }

        [TestCase(0.0001f)]
        [TestCase(float.Epsilon)]
        [TestCase(0.12f)]
        [TestCase(1f)]
        public void AnyPositiveExternalMemoryRestoresProtectionWithoutThreshold(float strength)
        {
            SandboxForgettingState state = new SandboxForgettingState();
            state.SetPlayerMemory(0);
            state.Tick(12);
            Assert.That(state.IsHostile, Is.True);
            state.SetPlayerMemory(strength);
            Assert.That(state.IsProtected, Is.True);
            Assert.That(state.IsHostile, Is.False);
            Assert.That(state.Progress, Is.Zero);
        }

        [Test]
        public void ResetClearsTransformationAndRestoresInitialExperiment()
        {
            SandboxForgettingState state = new SandboxForgettingState { AutoDecay = true };
            state.Tick(100);
            Assert.That(state.IsHostile, Is.True);
            state.SetWitnessMemory(1);
            state.Reset();
            Assert.That(state.PlayerMemory, Is.EqualTo(0.12f).Within(0.00001f));
            Assert.That(state.WitnessMemory, Is.Zero);
            Assert.That(state.Progress, Is.Zero);
            Assert.That(state.AutoDecay, Is.False);
        }

        [Test]
        public void SmallAndLargeTicksGiveTheSameTransition()
        {
            SandboxForgettingState large = new SandboxForgettingState { AutoDecay = true };
            SandboxForgettingState small = new SandboxForgettingState { AutoDecay = true };
            large.Tick(18);
            for (int i = 0; i < 180; i++) small.Tick(0.1f);
            Assert.That(small.Progress, Is.EqualTo(large.Progress).Within(0.0001f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTimeAndMemoryAreRejected(float value)
        {
            SandboxForgettingState state = new SandboxForgettingState();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SetPlayerMemory(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SetWitnessMemory(value));
        }

        [Test]
        public void MemoryAboveOneIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SandboxForgettingState().SetPlayerMemory(1.1f));
        }
    }
}
