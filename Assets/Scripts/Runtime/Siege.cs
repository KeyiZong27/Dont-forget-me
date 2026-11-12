using System.Collections.Generic;
using UnityEngine;

namespace DontForgetMe
{
    // The crowd that follows the group into the alley: who comes in when, who each of them
    // goes for, and what a shove or a slammed gate does to them.
    public sealed class Siege : MonoBehaviour
    {
        private static readonly string[] Looks = { "Stranger", "HostileB", "HostileD", "HostileC" };
        private static readonly float[] Paces = { 1.7f, 2.1f, 1.85f, 3.6f, 2.2f, 1.8f, 2.0f, 3.2f, 1.9f };
        private static readonly float[] WaveAt = { 0f, 0.14f, 0.34f, 0.56f, 0.78f };
        private static readonly int[] WaveSize = { 1, 2, 2, 2, 1 };
        private static readonly Color[] Pallor =
        {
            new Color(0.74f, 0.78f, 0.80f), new Color(0.66f, 0.70f, 0.68f), new Color(0.80f, 0.77f, 0.74f), new Color(0.68f, 0.72f, 0.77f)
        };

        public IReadOnlyList<Hostile> Hostiles => hostiles;
        public bool Begun { get; private set; }
        public bool Shut { get; private set; }

        private readonly List<Hostile> hostiles = new List<Hostile>();
        private readonly List<Transform> prey = new List<Transform>();
        private readonly List<int> caps = new List<int>();
        private readonly List<float> reluctance = new List<float>();
        private readonly List<int> taken = new List<int>();
        private DemoGame game;
        private int wave;
        private float nextAssign;
        private float nextGrowl;
        private float nextGateSound;
        private bool surged;
        private readonly List<float> reinforcements = new List<float>();

        // Bodies the alley will hold in all, standing or not.
        private const int Most = 14;
        public int FallenCount { get; private set; }
        private Transform keeper;

        public void Configure(DemoGame owner)
        {
            game = owner;
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0;
            return value;
        }

        private Hostile Spawn(Vector3 at)
        {
            int n = hostiles.Count;
            ActorMotor motor = OpeningWorld.Actor("unknown" + n, "行为异常的人", at, Looks[n % Looks.Length],
                "m_idle_drunk_01", n % 3 == 1 ? "m_walk_bruised" : "m_walk_drunk", "m_run_injured");
            motor.transform.localScale = Vector3.one * (0.97f + (n * 37 % 7) * 0.012f);
            motor.Weight = 1.15f;
            motor.Restless = true;
            CharacterAnimator body = motor.Body;
            body.MaxStride = 2.3f;
            body.Stoop = 7f + (n * 53 % 9);
            body.HeadTilt = (n % 2 == 0 ? 1f : -1f) * (8f + (n * 29 % 10));
            body.LookTarget = game.Player.transform;

            // Drained of colour rather than made up as monsters: the same people, wrong.
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer renderer in motor.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Pallor[n % Pallor.Length]);
                renderer.SetPropertyBlock(block);
            }

            Hostile hostile = motor.gameObject.AddComponent<Hostile>();
            hostile.Pace = Paces[n % Paces.Length];
            hostile.Windup = hostile.Pace > 3f ? 0.5f : 0.62f + (n * 17 % 5) * 0.04f;
            hostile.Configure(this, motor);
            hostile.Toughen(85f + (n * 43 % 7) * 8f);
            hostiles.Add(hostile);
            return hostile;
        }

        // They come round the corners of the alley mouth from the street, never out of thin air in view.
        private Hostile Enter()
        {
            int n = hostiles.Count;
            float side = n % 2 == 0 ? 1f : -1f;
            Hostile hostile = Spawn(new Vector3(side * (5.4f + (n * 41 % 5) * 0.45f), 0.1f, 25.6f + (n * 23 % 4) * 0.3f));
            hostile.Arrive(new Vector3(side * 1.7f, 0, 27.1f), new Vector3(side * (0.4f + (n * 31 % 4) * 0.4f), 0, 30.6f));
            return hostile;
        }

        // Before anything has gone wrong: one figure walks to the alley mouth and just stands there.
        public void Foreshadow()
        {
            if (hostiles.Count > 0) return;
            Hostile first = Spawn(new Vector3(6.1f, 0.1f, 26.2f));
            first.Arrive(new Vector3(1.6f, 0, 27.2f), new Vector3(0.3f, 0, 29.3f));
        }

        public void Begin()
        {
            if (Begun) return;
            Begun = true;
            Foreshadow();
            prey.Clear();
            caps.Clear();
            reluctance.Clear();
            taken.Clear();
            // Each of us can be held to a few; whoever is left over goes past the line for the
            // man working on the gate.
            Add(game.Player.transform, 3, 0f);
            Add(game.ResidentB.transform, 2, 0.4f);
            Add(game.Woman.transform, 1, 1.0f);
            Add(game.Zhou.transform, 3, 1.2f);
            Add(game.ResidentA.transform, 1, 2.2f);
        }

        private void Add(Transform who, int cap, float unwilling)
        {
            prey.Add(who);
            caps.Add(cap);
            reluctance.Add(unwilling);
            taken.Add(0);
        }

        // Called every frame while the gate is being worked on or run through.
        public void Tick(float progress)
        {
            if (!Begun || Shut) return;
            while (wave < WaveAt.Length && progress >= WaveAt[wave])
            {
                for (int i = 0; i < WaveSize[wave]; i++) Enter();
                wave++;
                if (wave >= 2) game.Scream(game.ResidentA);
                if (wave == 2) game.Caption("她：不止一个！巷口又进来了！", 4);
                else if (wave == 4) game.Caption("街坊：越来越多了……门还要多久？！", 4);
            }
            if (Time.time >= nextAssign)
            {
                nextAssign = Time.time + 0.4f;
                Assign();
            }
            // For each one put down, another comes in from the street.
            for (int i = reinforcements.Count - 1; i >= 0; i--)
            {
                if (Time.time < reinforcements[i]) continue;
                reinforcements.RemoveAt(i);
                if (hostiles.Count < Most) Enter();
            }
        }

        public void Fell(Hostile hostile)
        {
            FallenCount++;
            if (!Shut && hostiles.Count + reinforcements.Count < Most) reinforcements.Add(Time.time + 3.5f);
            game.HostileFell(FallenCount);
        }

        private void Assign()
        {
            for (int i = 0; i < taken.Count; i++) taken[i] = 0;
            foreach (Hostile hostile in hostiles)
            {
                if (hostile.Fallen) continue;
                Transform grudge = hostile.Grudge;
                int held = grudge != null && Wanted(grudge) ? prey.IndexOf(grudge) : -1;
                if (held < 0) continue;
                hostile.Target = grudge;
                taken[held]++;
            }
            foreach (Hostile hostile in hostiles)
            {
                if (hostile.State == Hostile.Mode.Pound || hostile.Fallen) continue;
                if (hostile.Grudge != null && Wanted(hostile.Grudge) && prey.Contains(hostile.Grudge)) continue;
                int best = prey.IndexOf(keeper != null ? keeper : game.Player.transform);
                float bestScore = float.MaxValue;
                for (int i = 0; i < prey.Count; i++)
                {
                    if (!Wanted(prey[i])) continue;
                    float score = Flat(prey[i].position - hostile.transform.position).magnitude + reluctance[i]
                        + (keeper == null && taken[i] >= caps[i] ? 6f : 0f) - (hostile.Target == prey[i] ? 0.8f : 0f);
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = i;
                }
                hostile.Target = prey[best];
                taken[best]++;
            }
        }

        // Once the gate is open they go for whoever is still on their side of it, or standing in
        // the gateway. Nobody who has got clear is chased across the yard: a hostile loose in
        // there could never be put back out through the press behind it, and the gate could
        // never be shut.
        private bool Wanted(Transform who)
        {
            if (keeper == null) return true;
            float z = who.position.z;
            return z < 49.2f || who == keeper || (who == game.Player.transform && z < 51.5f);
        }

        // The gate is open: whatever was holding them back is gone. "doorkeeper" is whoever
        // stands in the gateway while the others get through.
        public void Surge(Transform doorkeeper)
        {
            keeper = doorkeeper;
            if (surged) return;
            surged = true;
            foreach (Hostile hostile in hostiles)
            {
                hostile.Pace = Mathf.Min(3.5f, hostile.Pace * 1.3f);
                hostile.Motor.Speed = hostile.Pace;
            }
        }

        public Hostile Nearest(Vector3 point, float within, bool dangerousOnly)
        {
            Hostile best = null;
            float bestDistance = within;
            foreach (Hostile hostile in hostiles)
            {
                if (hostile.Fallen || (dangerousOnly && !hostile.Dangerous)) continue;
                float distance = Flat(hostile.transform.position - point).magnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = hostile;
            }
            return best;
        }

        // The one a defender should deal with first: whoever is nearest the post, with those
        // already going for the man at the gate counted as closer than they are.
        public Hostile ThreatNear(Vector3 post, float radius)
        {
            Hostile best = null;
            float bestScore = float.MaxValue;
            foreach (Hostile hostile in hostiles)
            {
                if (!hostile.Dangerous) continue;
                float distance = Flat(hostile.transform.position - post).magnitude;
                if (distance > radius) continue;
                float score = distance - (hostile.Target == game.Zhou.transform ? 1.2f : 0f);
                if (score >= bestScore) continue;
                bestScore = score;
                best = hostile;
            }
            return best;
        }

        public bool AnyBeyond(float z)
        {
            foreach (Hostile hostile in hostiles)
                if (!hostile.Fallen && hostile.transform.position.z > z) return true;
            return false;
        }

        // A blow with something picked up. Whoever is nearest in front takes the whole of it; anyone
        // else inside the arc is caught by less. Returns how many were hit.
        public int Strike(Vector3 origin, Vector3 forward, Weapon weapon, float strength, Transform by)
        {
            forward = Flat(forward).normalized;
            Hostile first = null;
            float nearest = float.MaxValue;
            List<Hostile> caught = new List<Hostile>();
            foreach (Hostile hostile in hostiles)
            {
                if (hostile.Fallen) continue;
                Vector3 to = Flat(hostile.transform.position - origin);
                float distance = to.magnitude;
                if (distance > weapon.Reach || (distance > 0.4f && Vector3.Angle(forward, to) > 60f)) continue;
                caught.Add(hostile);
                if (distance >= nearest) continue;
                nearest = distance;
                first = hostile;
            }
            foreach (Hostile hostile in caught)
            {
                Vector3 to = Flat(hostile.transform.position - origin);
                Vector3 direction = to.sqrMagnitude > 0.0025f ? (to.normalized + forward * 0.6f).normalized : forward;
                float share = hostile == first ? 1f : 0.4f;
                hostile.Shoved(direction, weapon.Power * Mathf.Lerp(0.6f, 1f, share), by);
                hostile.Hurt(weapon.Damage * strength * share);
            }
            if (first != null) game.Impact(weapon, first.transform.position);
            return caught.Count;
        }

        // Everyone in front of "origin" within arm's reach is driven back. Returns how many.
        public int ShoveFrom(Vector3 origin, Vector3 forward, float reach, float halfAngle, float power, Transform by)
        {
            forward = Flat(forward).normalized;
            int struck = 0;
            foreach (Hostile hostile in hostiles)
            {
                if (hostile.Fallen) continue;
                Vector3 to = Flat(hostile.transform.position - origin);
                float distance = to.magnitude;
                if (distance > reach || (distance > 0.4f && Vector3.Angle(forward, to) > halfAngle)) continue;
                Vector3 direction = distance > 0.05f ? (to / distance + forward * 0.6f).normalized : forward;
                hostile.Shoved(direction, power * Mathf.Lerp(1f, 0.8f, distance / reach), by);
                hostile.Hurt(3f);
                struck++;
            }
            if (struck > 0) game.Impact(null, origin + forward * 0.8f);
            return struck;
        }

        // Someone sent reeling takes the people behind them along.
        public void Topple(Hostile source, Vector3 direction, float power)
        {
            if (power < 0.5f) return;
            foreach (Hostile other in hostiles)
            {
                if (other == source || other.State == Hostile.Mode.Reel || other.State == Hostile.Mode.Down || other.Fallen) continue;
                Vector3 to = Flat(other.transform.position - source.transform.position);
                if (to.magnitude > 0.74f || Vector3.Dot(to, direction) < -0.1f) continue;
                other.Shoved(direction, power * 0.55f, null);
            }
        }

        public void Struck(Transform victim, Hostile by)
        {
            if (victim == game.Player.transform)
            {
                game.Player.TakeHit(by.transform.position, 9f);
                game.PlayerStruck();
            }
            else
            {
                Companion companion = victim.GetComponent<Companion>();
                if (companion != null) companion.Struck(by);
                game.CompanionStruck(victim);
            }
        }

        // As the leaf slides shut, whoever is in the gateway is driven back out of it, and
        // anyone lying across the rail is pushed clear.
        public void ClearGateway(float power)
        {
            foreach (Hostile hostile in hostiles)
            {
                float z = hostile.transform.position.z;
                if (hostile.Fallen ? z > 47.6f && z < 50.6f : z > 47.2f && hostile.State != Hostile.Mode.Reel)
                    hostile.Shoved(Vector3.back, power, null);
            }
        }

        // The gate is shut. The nearest take the front and beat on it; the rest press in behind.
        public void GateShut()
        {
            Shut = true;
            List<Hostile> order = hostiles.FindAll(hostile => !hostile.Fallen);
            order.Sort((a, b) => b.transform.position.z.CompareTo(a.transform.position.z));
            for (int i = 0; i < order.Count; i++)
            {
                bool front = i < 4;
                float x = front ? -1.05f + i * 0.7f : -1.2f + (i - 4) * 0.6f;
                order[i].PoundAt(new Vector3(x, 0, front ? 47.72f : 46.95f), new Vector3(x, 1.45f, 48.22f), front);
            }
        }

        // The breath they heave in before throwing themselves forward.
        public void Growl(Vector3 at)
        {
            if (Time.time < nextGrowl) return;
            nextGrowl = Time.time + 0.5f;
            game.Rasp(at, 0.9f);
        }

        public void GateStruck(Vector3 mark)
        {
            if (Time.time < nextGateSound) return;
            nextGateSound = Time.time + 0.16f;
            game.GateBlow(mark);
        }
    }
}
