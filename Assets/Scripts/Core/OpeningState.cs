using System;
using System.Collections.Generic;

namespace DontForgetMe
{
    public enum OpeningPhase
    {
        Street,
        Reunion,
        Alley,
        Debris,
        Repair,
        Regroup,
        Complete
    }

    public sealed class OpeningState
    {
        private readonly HashSet<string> clearedDebris = new HashSet<string>();

        public OpeningPhase Phase { get; private set; } = OpeningPhase.Street;
        public bool GaveWater { get; private set; }
        public float RepairProgress { get; private set; }
        public int ClearedDebrisCount => clearedDebris.Count;

        public bool GiveWater()
        {
            if (GaveWater || Phase != OpeningPhase.Street) return false;
            GaveWater = true;
            return true;
        }

        public bool MeetZhou()
        {
            return Advance(OpeningPhase.Street, OpeningPhase.Reunion);
        }

        public bool Reunite()
        {
            return Advance(OpeningPhase.Reunion, OpeningPhase.Alley);
        }

        public bool EnterAlley()
        {
            return Advance(OpeningPhase.Alley, OpeningPhase.Debris);
        }

        public bool ClearDebris(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Debris needs a stable identifier.", nameof(id));
            if (Phase != OpeningPhase.Debris || clearedDebris.Count >= 2) return false;
            return clearedDebris.Add(id);
        }

        public bool StartRepair()
        {
            if (clearedDebris.Count != 2) return false;
            return Advance(OpeningPhase.Debris, OpeningPhase.Repair);
        }

        public void TickRepair(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (Phase != OpeningPhase.Repair) return;
            RepairProgress = Math.Min(1f, RepairProgress + seconds / 14f);
            if (RepairProgress >= 1f) Phase = OpeningPhase.Regroup;
        }

        public bool Finish(bool everyonePresent)
        {
            return everyonePresent && Advance(OpeningPhase.Regroup, OpeningPhase.Complete);
        }

        private bool Advance(OpeningPhase expected, OpeningPhase next)
        {
            if (Phase != expected) return false;
            Phase = next;
            return true;
        }
    }
}
