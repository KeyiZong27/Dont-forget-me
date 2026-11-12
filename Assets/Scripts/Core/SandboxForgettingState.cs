using System;

namespace DontForgetMe
{
    // Isolated test relationships; never edits the story's or main companions' memory.
    public sealed class SandboxForgettingState
    {
        private const string Subject = "test_friend";
        private readonly MemoryNetwork memories = new MemoryNetwork();
        public float PlayerMemory => memories.Strength("player", Subject);
        public float WitnessMemory => memories.Strength("witness", Subject);
        public bool IsProtected => memories.IsRemembered(Subject);
        public bool AutoDecay { get; set; }
        public float Progress { get; private set; }
        public bool IsHostile => Progress >= 1f && !IsProtected;
        public const float DecayPerSecond = 0.01f;
        public const float TransitionSeconds = 12f;

        public SandboxForgettingState() { Reset(); }

        public void SetPlayerMemory(float value)
        {
            SetMemory("player", value);
        }

        public void SetWitnessMemory(float value)
        {
            SetMemory("witness", value);
        }

        private void SetMemory(string observer, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            memories.SetStrength(observer, Subject, value);
            if (IsProtected) Progress = 0;
        }

        public void Tick(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            float unprotectedSeconds = seconds;
            if (AutoDecay && PlayerMemory > 0)
            {
                float untilForgotten = PlayerMemory / DecayPerSecond;
                memories.DecayObserver("player", seconds * DecayPerSecond);
                unprotectedSeconds = Math.Max(0, seconds - untilForgotten);
            }
            if (IsProtected) Progress = 0;
            else Progress = Math.Min(1, Progress + unprotectedSeconds / TransitionSeconds);
        }

        public void Reset()
        {
            AutoDecay = false;
            Progress = 0;
            SetPlayerMemory(0.12f);
            SetWitnessMemory(0);
        }
    }
}
