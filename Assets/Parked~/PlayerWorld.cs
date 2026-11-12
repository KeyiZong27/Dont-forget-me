namespace DontForgetMe
{
    public interface IPlayerWorld
    {
        bool IsPaused { get; }
        bool IsFailed { get; }
        bool IsComplete { get; }
        float ThreatEnergyDrainPerSecond { get; }
        bool PushThreat();
        void Caption(string text, float duration);
        void Fail();
    }

    public static class PlayerWorld
    {
        public static IPlayerWorld Current { get; set; }
        public static bool CanSimulate => Current != null && !Current.IsPaused
            && !Current.IsFailed && !Current.IsComplete;
    }
}
