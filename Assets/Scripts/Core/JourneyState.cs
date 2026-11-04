namespace DontForgetMe
{
    public enum JourneyPhase
    {
        Calls,
        Path,
        Shelter,
        Night,
        Morning,
        Complete
    }

    // 大纲第 05—08 场：两通电话、窄路碎玻璃、服务区过夜、清晨分路。
    // 只描述事件先后关系；可选行为（递水）不构成通过条件。
    public sealed class JourneyState
    {
        public JourneyPhase Phase { get; private set; } = JourneyPhase.Calls;
        public bool CalledFamily { get; private set; }
        public bool CalledMother { get; private set; }
        public bool BothCallsDone => CalledFamily && CalledMother;
        public bool DogCalmed { get; private set; }
        public bool GlassRemoved { get; private set; }
        public bool ZhouPersuaded { get; private set; }
        public bool SharedWater { get; private set; }
        public bool DoorFixed { get; private set; }
        public bool AddressCopied { get; private set; }

        public bool CallFamily()
        {
            if (Phase != JourneyPhase.Calls || CalledFamily) return false;
            CalledFamily = true;
            return true;
        }

        public bool CallMother()
        {
            if (Phase != JourneyPhase.Calls || CalledMother) return false;
            CalledMother = true;
            return true;
        }

        public bool Depart()
        {
            return BothCallsDone && Advance(JourneyPhase.Calls, JourneyPhase.Path);
        }

        public bool CalmDog()
        {
            if (Phase != JourneyPhase.Path || DogCalmed) return false;
            DogCalmed = true;
            return true;
        }

        public bool RemoveGlass()
        {
            if (Phase != JourneyPhase.Path || !DogCalmed || GlassRemoved) return false;
            GlassRemoved = true;
            return true;
        }

        public bool EnterShelter()
        {
            return GlassRemoved && Advance(JourneyPhase.Path, JourneyPhase.Shelter);
        }

        public bool ShareWater()
        {
            if (Phase != JourneyPhase.Shelter || SharedWater) return false;
            SharedWater = true;
            return true;
        }

        public bool PersuadeZhou()
        {
            if (Phase != JourneyPhase.Shelter || ZhouPersuaded) return false;
            ZhouPersuaded = true;
            return true;
        }

        public bool Rest()
        {
            return ZhouPersuaded && Advance(JourneyPhase.Shelter, JourneyPhase.Night);
        }

        public bool FixDoor()
        {
            if (Phase != JourneyPhase.Night || DoorFixed) return false;
            DoorFixed = true;
            return true;
        }

        public bool Sleep()
        {
            return DoorFixed && Advance(JourneyPhase.Night, JourneyPhase.Morning);
        }

        public bool CopyAddress()
        {
            if (Phase != JourneyPhase.Morning || AddressCopied) return false;
            AddressCopied = true;
            return true;
        }

        public bool Farewell()
        {
            return AddressCopied && Advance(JourneyPhase.Morning, JourneyPhase.Complete);
        }

        private bool Advance(JourneyPhase expected, JourneyPhase next)
        {
            if (Phase != expected) return false;
            Phase = next;
            return true;
        }
    }
}
