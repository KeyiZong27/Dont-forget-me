using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DontForgetMe
{
    // Drives a humanoid from motion-capture clips without an Animator Controller asset:
    // a standing clip ("stance") blended with walk, run and sprint cycles by ground speed.
    // On top of the clips sits a small physical layer: the spine can lean and each hand can be
    // placed on a point in the world, which is what shoving, grabbing, carrying, shielding the
    // head, beating on a door and swinging something picked up are built from. The library has
    // no clips for any of those.
    public sealed class CharacterAnimator : MonoBehaviour
    {
        private sealed class State
        {
            public AnimationClipPlayable Playable;
            public float Weight;
            public float Reference;
        }

        private sealed class Arm
        {
            public Transform Upper, Lower, Hand;
            public float Side;
            public float Weight;
            public Vector3 Target;
            public Vector3 Hint;
        }

        private enum Act { None, Shove, Swing, Recoil, Lunge, Flinch }

        public Transform LookTarget { get; set; }
        // How far a walk cycle may be sped up to keep pace before the feet are allowed to slide.
        public float MaxStride { get; set; } = 1.6f;
        // 0..1: arms come up toward the look target, the way someone closes in to grab.
        public float Menace { get; set; }
        // Hunched with both arms over the head.
        public bool Cover { get; set; }
        // A permanent stoop in degrees and a sideways tilt of the head.
        public float Stoop { get; set; }
        public float HeadTilt { get; set; }
        public bool Acting => act != Act.None;
        public Weapon Carried { get; private set; }

        private readonly List<State> states = new List<State>();
        private readonly Dictionary<string, int> lookup = new Dictionary<string, int>();
        private readonly Arm left = new Arm { Side = -1f }, right = new Arm { Side = 1f };
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private Animator animator;
        private Transform head, neck, spine, chest;
        private Quaternion look = Quaternion.identity;
        private int idle, walk, run, sprint, stance;
        private float stanceUntil;
        private float speed;
        private bool reverse;
        private float seed;

        private Act act;
        private float actAt, actLength;
        private Vector3 actPoint, actDirection;
        private bool actHasPoint;
        private Transform actVictim;
        private Func<Vector3> holdLeft, holdRight, pound;
        private float holdLean;
        private Action poundImpact;
        private readonly bool[] poundStruck = new bool[2];
        private float pitch, roll;
        private Vector3 point = Vector3.forward;

        public void Configure(Animator target, string idleClip, string walkClip, string runClip, string sprintClip = null)
        {
            animator = target;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Character " + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 0);
            AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(mixer);
            idle = stance = Ensure(idleClip);
            walk = Ensure(walkClip);
            run = string.IsNullOrEmpty(runClip) ? walk : Ensure(runClip);
            sprint = string.IsNullOrEmpty(sprintClip) ? run : Ensure(sprintClip);
            states[idle].Weight = 1;
            // Start each character somewhere different in the idle, so a group never moves in unison.
            states[idle].Playable.SetTime(UnityEngine.Random.value * states[idle].Playable.GetAnimationClip().length);
            seed = UnityEngine.Random.value * 50f;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            left.Upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            left.Lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            left.Hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            right.Upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            right.Lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            right.Hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Apply();
            graph.Play();
        }

        public Transform Bone(HumanBodyBones bone)
        {
            return animator != null ? animator.GetBoneTransform(bone) : null;
        }

        private int Ensure(string clipId)
        {
            if (lookup.TryGetValue(clipId, out int existing)) return existing;
            ArtCatalog.Entry entry = Art.Entry("clip/" + clipId);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, (AnimationClip)entry.asset);
            playable.SetApplyFootIK(false);
            int index = states.Count;
            mixer.SetInputCount(index + 1);
            graph.Connect(playable, 0, mixer, index);
            states.Add(new State { Playable = playable, Reference = entry.size.y });
            lookup[clipId] = index;
            return index;
        }

        // "backwards" plays the walk in reverse: someone driven back on their heels.
        public void SetSpeed(float metresPerSecond, bool backwards = false)
        {
            speed = metresPerSecond;
            reverse = backwards;
        }

        // Replaces the standing pose (working, talking, crouching...). Zero seconds keeps it until changed.
        public void Stance(string clipId, float seconds = 0f)
        {
            int next = Ensure(clipId);
            if (next != stance && states[next].Weight < 0.01f)
            {
                AnimationClip clip = states[next].Playable.GetAnimationClip();
                states[next].Playable.SetTime(clip.length > 8f ? UnityEngine.Random.value * (clip.length - 6f) : 0);
            }
            stance = next;
            stanceUntil = seconds > 0 ? Time.time + seconds : float.MaxValue;
        }

        public void Relax()
        {
            stance = idle;
        }

        // Both hands driven out from the chest. With a contact point the palms land on it.
        public void Shove(Vector3? contact = null)
        {
            Begin(Act.Shove, 0.58f);
            actHasPoint = contact.HasValue;
            if (contact.HasValue) actPoint = contact.Value;
        }

        // Takes something in the right hand. From then on it follows that hand, and Swing strikes with it.
        public void Carry(Weapon weapon)
        {
            Carried = weapon;
            weapon.Taken = true;
            weapon.Root.SetParent(null, true);
            // Counted with the people from here on: out of reflections, and hidden along with them.
            OpeningWorld.SetLayer(weapon.Root.gameObject, 8);
            point = weapon.Root.forward;
        }

        // Draws the thing carried back over the shoulder and brings it down on the contact point.
        public void Swing(Vector3? contact = null)
        {
            Begin(Act.Swing, 0.68f);
            actHasPoint = contact.HasValue;
            if (contact.HasValue) actPoint = contact.Value;
        }

        // Stops everything where it is: a body that will not move again.
        public void Freeze()
        {
            if (graph.IsValid()) graph.Stop();
            animator.enabled = false;
            enabled = false;
        }

        // Thrown off balance in a direction: the trunk goes with it and the arms fly out.
        public void Recoil(Vector3 direction, float seconds)
        {
            Begin(Act.Recoil, Mathf.Max(0.2f, seconds));
            direction.y = 0;
            actDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : -transform.forward;
        }

        // Rears back with the arms coming up for "windup" seconds, then throws them at the victim.
        public void Lunge(Transform victim, float windup)
        {
            Begin(Act.Lunge, windup);
            actVictim = victim;
        }

        public void Flinch()
        {
            if (act == Act.None) Begin(Act.Flinch, 0.7f);
        }

        private void Begin(Act next, float length)
        {
            act = next;
            actAt = Time.time;
            actLength = length;
        }

        // Keeps the hands on moving points (the sides of a crate, the edge of a gate) until released.
        public void Hold(Func<Vector3> leftPoint, Func<Vector3> rightPoint, float lean = 0f)
        {
            holdLeft = leftPoint;
            holdRight = rightPoint;
            holdLean = lean;
        }

        public void Release()
        {
            holdLeft = holdRight = null;
            holdLean = 0;
        }

        // Fists coming down in turn on a point; "impact" fires on each blow.
        public void Pound(Func<Vector3> point, Action impact)
        {
            pound = point;
            poundImpact = impact;
        }

        public void StopPounding()
        {
            pound = null;
            poundImpact = null;
        }

        private void Update()
        {
            if (!graph.IsValid()) return;
            if (stance != idle && Time.time > stanceUntil) stance = idle;
            float walkRef = Reference(walk, 1.4f), runRef = Reference(run, 3f), sprintRef = Reference(sprint, 5.4f);
            float moving = Mathf.InverseLerp(0.1f, Mathf.Max(0.4f, walkRef * 0.5f), speed);
            float running = run == walk || reverse ? 0 : Mathf.InverseLerp(walkRef * 1.2f, runRef * 0.92f, speed);
            float sprinting = sprint == run ? 0 : Mathf.InverseLerp(runRef * 1.15f, sprintRef * 0.9f, speed);
            float step = Time.deltaTime / 0.2f;
            for (int i = 0; i < states.Count; i++)
            {
                float target = 0;
                if (i == stance) target += 1 - moving;
                if (i == walk) target += moving * (1 - running);
                if (i == run && run != walk) target += moving * running * (1 - sprinting);
                if (i == sprint && sprint != run) target += moving * running * sprinting;
                states[i].Weight = Mathf.MoveTowards(states[i].Weight, target, step);
            }
            float stride = Mathf.Clamp(speed / walkRef, 0.6f, MaxStride);
            AnimationClipPlayable cycle = states[walk].Playable;
            if (reverse)
            {
                double length = cycle.GetAnimationClip().length;
                double time = cycle.GetTime() - Time.deltaTime * stride;
                while (time < 0) time += length;
                cycle.SetTime(time);
                cycle.SetSpeed(0);
            }
            else cycle.SetSpeed(stride);
            if (run != walk) states[run].Playable.SetSpeed(Mathf.Clamp(speed / runRef, 0.8f, 1.3f));
            if (sprint != run) states[sprint].Playable.SetSpeed(Mathf.Clamp(speed / sprintRef, 0.8f, 1.25f));
            Apply();
        }

        private float Reference(int index, float fallback)
        {
            return states[index].Reference > 0.2f ? states[index].Reference : fallback;
        }

        private void Apply()
        {
            float total = 0;
            foreach (State state in states) total += state.Weight;
            if (total < 0.0001f) return;
            for (int i = 0; i < states.Count; i++) mixer.SetInputWeight(i, states[i].Weight / total);
        }

        private static float Ease(float from, float to, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        private void LateUpdate()
        {
            if (head == null || Time.timeScale <= 0) return;
            float dt = Time.deltaTime;
            if (left.Upper != null && right.Upper != null && left.Hand != null && right.Hand != null) Physical(dt);
            Look(dt);
        }

        // Works out where the trunk and hands should be this frame, then bends the bones to match.
        private void Physical(float dt)
        {
            Vector3 side = transform.right, forward = transform.forward, up = Vector3.up;
            Vector3 chestAt = (left.Upper.position + right.Upper.position) * 0.5f;
            float now = Time.time;
            float wantPitch = Stoop, wantRoll = 0, leftWeight = 0, rightWeight = 0;
            Vector3 leftAt = left.Target, rightAt = right.Target;
            left.Hint = left.Upper.position - up * 0.6f - side * 0.45f - forward * 0.25f;
            right.Hint = right.Upper.position - up * 0.6f + side * 0.45f - forward * 0.25f;

            if (Menace > 0.01f && LookTarget != null)
            {
                Vector3 aim = LookTarget.position + up * 1.3f - chestAt;
                aim = aim.sqrMagnitude > 0.01f ? aim.normalized : forward;
                float a = Mathf.PerlinNoise(now * 0.7f + seed, 1.3f), b = Mathf.PerlinNoise(now * 0.6f + seed, 7.9f);
                leftAt = left.Upper.position + (aim + up * (a - 0.5f) * 0.5f).normalized * 0.56f;
                rightAt = right.Upper.position + (aim + up * (b - 0.5f) * 0.5f).normalized * 0.56f;
                leftWeight = Menace * Mathf.Lerp(0.55f, 1f, a);
                rightWeight = Menace * Mathf.Lerp(0.55f, 1f, b);
                wantPitch += 7f * Menace;
            }

            if (Cover)
            {
                float tremble = (Mathf.PerlinNoise(now * 9f + seed, 0.5f) - 0.5f) * 0.03f;
                leftAt = head.position + up * (0.09f + tremble) - side * 0.12f + forward * 0.07f;
                rightAt = head.position + up * (0.09f - tremble) + side * 0.12f + forward * 0.07f;
                leftWeight = rightWeight = 1f;
                left.Hint = left.Upper.position - side * 0.5f + forward * 0.45f;
                right.Hint = right.Upper.position + side * 0.5f + forward * 0.45f;
                wantPitch += 14f + tremble * 60f;
            }

            // Something carried: light things hang from the hand, pointing ahead and down; anything
            // that needs both hands is held across the body.
            Vector3 wantPoint = point;
            if (Carried != null)
            {
                if (Carried.TwoHanded)
                {
                    rightAt = chestAt + forward * 0.3f - up * 0.3f + side * 0.14f;
                    leftAt = chestAt + forward * 0.34f - up * 0.22f - side * 0.12f;
                    leftWeight = Mathf.Max(leftWeight, 0.9f);
                    rightWeight = Mathf.Max(rightWeight, 0.9f);
                    wantPoint = (up * 0.5f + forward * 0.55f - side * 0.45f).normalized;
                }
                else
                {
                    Vector3 forearm = (right.Hand.position - right.Lower.position).normalized;
                    wantPoint = (forearm * 0.6f + forward * 0.65f - up * 0.15f).normalized;
                }
            }

            if (holdLeft != null) { leftAt = holdLeft(); leftWeight = 1f; }
            if (holdRight != null) { rightAt = holdRight(); rightWeight = 1f; }
            wantPitch += holdLean;

            if (pound != null)
            {
                Vector3 mark = pound();
                for (int i = 0; i < 2; i++)
                {
                    Arm arm = i == 0 ? left : right;
                    float phase = Mathf.Repeat(now * 1.05f + seed + i * 0.5f, 1f);
                    // Draw back slowly, come down fast, rest on the surface a moment.
                    float raised = phase < 0.55f ? Ease(0f, 0.55f, phase) : 1f - Ease(0.55f, 0.68f, phase);
                    Vector3 back = arm.Upper.position + up * 0.3f + side * arm.Side * 0.12f + forward * 0.08f;
                    Vector3 hit = mark + side * arm.Side * 0.17f + up * (i == 0 ? 0.06f : -0.05f);
                    Vector3 at = Vector3.Lerp(hit, back, raised);
                    if (i == 0) { leftAt = at; leftWeight = 1f; } else { rightAt = at; rightWeight = 1f; }
                    bool struck = phase >= 0.68f;
                    if (struck && !poundStruck[i]) poundImpact?.Invoke();
                    poundStruck[i] = struck;
                    if (phase > 0.55f && phase < 0.8f) wantPitch += 9f;
                }
                wantPitch += 6f;
            }

            float t = now - actAt;
            switch (act)
            {
                case Act.Shove:
                {
                    float wind = Ease(0f, 0.11f, t), thrust = Ease(0.11f, 0.2f, t), recover = Ease(0.3f, 0.58f, t);
                    for (int i = 0; i < 2; i++)
                    {
                        Arm arm = i == 0 ? left : right;
                        Vector3 drawn = chestAt + side * arm.Side * 0.15f - up * 0.16f + forward * 0.2f;
                        Vector3 driven = actHasPoint ? actPoint + side * arm.Side * 0.14f
                            : chestAt + side * arm.Side * 0.15f - up * 0.06f + forward * 0.7f;
                        Vector3 at = Vector3.Lerp(drawn, driven, thrust);
                        if (i == 0) { leftAt = at; leftWeight = Mathf.Max(leftWeight * recover, wind * (1f - recover)); }
                        else { rightAt = at; rightWeight = Mathf.Max(rightWeight * recover, wind * (1f - recover)); }
                    }
                    wantPitch += Mathf.Lerp(-7f, 24f, thrust) * wind * (1f - recover);
                    if (t >= actLength) act = Act.None;
                    break;
                }
                case Act.Swing:
                {
                    float wind = Ease(0f, 0.2f, t), strike = Ease(0.2f, 0.3f, t), recover = Ease(0.42f, 0.68f, t);
                    float length = Carried != null ? Carried.Length : 0.3f;
                    bool both = Carried != null && Carried.TwoHanded;
                    Vector3 shoulder = both ? chestAt : right.Upper.position;
                    Vector3 mark = actHasPoint ? actPoint : chestAt + forward * 0.95f;
                    Vector3 aim = mark - shoulder;
                    aim = aim.sqrMagnitude > 0.01f ? aim.normalized : forward;
                    // Back over the shoulder, then down and through: the far end lands on the mark.
                    Vector3 chop = (aim * 0.85f - up * 0.5f).normalized;
                    Vector3 raised = shoulder + up * (both ? 0.5f : 0.36f) + side * (both ? 0.05f : 0.16f) - forward * 0.12f;
                    Vector3 landed = shoulder + Vector3.ClampMagnitude(mark - chop * length * 0.85f - shoulder, 0.56f);
                    Vector3 hand = Vector3.Lerp(raised, landed, strike);
                    Vector3 back = (up * 0.75f - forward * 0.6f + side * 0.2f).normalized;
                    Vector3 swung = Vector3.Slerp(back, chop, strike);
                    float weight = wind * (1f - recover);
                    rightAt = Vector3.Lerp(rightAt, hand, weight);
                    rightWeight = Mathf.Max(rightWeight * (1f - weight), weight);
                    if (both)
                    {
                        leftAt = Vector3.Lerp(leftAt, hand + swung * 0.2f - side * 0.06f, weight);
                        leftWeight = Mathf.Max(leftWeight * (1f - weight), weight);
                    }
                    wantPoint = Vector3.Slerp(wantPoint, swung, weight);
                    wantPitch += Mathf.Lerp(-10f * wind, 24f, strike) * (1f - recover);
                    wantRoll += 8f * strike * (1f - recover);
                    if (t >= actLength) act = Act.None;
                    break;
                }
                case Act.Recoil:
                {
                    float left01 = Mathf.Clamp01(1f - t / actLength), force = left01 * left01;
                    float wobble = Mathf.Sin(t * 13f + seed) * 5f * left01;
                    wantPitch += Vector3.Dot(actDirection, forward) * 30f * force + wobble;
                    wantRoll -= Vector3.Dot(actDirection, side) * 16f * force;
                    Vector3 flung = up * 0.1f - forward * 0.05f - actDirection * 0.1f;
                    leftAt = left.Upper.position - side * 0.5f + flung + up * Mathf.Sin(t * 9f) * 0.08f;
                    rightAt = right.Upper.position + side * 0.5f + flung - up * Mathf.Sin(t * 9f) * 0.08f;
                    leftWeight = Mathf.Max(leftWeight * (1f - force), 0.85f * force);
                    rightWeight = Mathf.Max(rightWeight * (1f - force), 0.85f * force);
                    if (t >= actLength) act = Act.None;
                    break;
                }
                case Act.Lunge:
                {
                    Vector3 mark = actVictim != null ? actVictim.position + up * 1.32f : chestAt + forward * 0.8f;
                    Vector3 aim = mark - chestAt;
                    aim = aim.sqrMagnitude > 0.01f ? aim.normalized : forward;
                    float rise = Ease(0f, actLength * 0.8f, t), strike = Ease(actLength, actLength + 0.14f, t);
                    float recover = Ease(actLength + 0.3f, actLength + 0.62f, t);
                    for (int i = 0; i < 2; i++)
                    {
                        Arm arm = i == 0 ? left : right;
                        Vector3 raised = arm.Upper.position + (aim * 0.6f + up * 0.5f + side * arm.Side * 0.2f).normalized * 0.5f;
                        Vector3 thrown = mark + side * arm.Side * 0.13f;
                        Vector3 at = Vector3.Lerp(raised, thrown, strike);
                        float weight = rise * (1f - recover);
                        if (i == 0) { leftAt = at; leftWeight = Mathf.Max(leftWeight * (1f - weight), weight); }
                        else { rightAt = at; rightWeight = Mathf.Max(rightWeight * (1f - weight), weight); }
                    }
                    wantPitch += (Mathf.Lerp(-10f * rise, 26f, strike)) * (1f - recover);
                    if (t >= actLength + 0.62f) act = Act.None;
                    break;
                }
                case Act.Flinch:
                {
                    float force = Mathf.Sin(Mathf.Clamp01(t / actLength) * Mathf.PI);
                    wantPitch += 13f * force;
                    Vector3 guard = chestAt + up * 0.08f + forward * 0.24f;
                    if (leftWeight < 0.5f) { leftAt = guard - side * 0.13f; leftWeight = 0.75f * force; }
                    if (rightWeight < 0.5f) { rightAt = guard + side * 0.13f; rightWeight = 0.75f * force; }
                    if (t >= actLength) act = Act.None;
                    break;
                }
            }

            float follow = 1f - Mathf.Exp(-26f * dt);
            pitch = Mathf.Lerp(pitch, wantPitch, follow);
            roll = Mathf.Lerp(roll, wantRoll, follow);
            if (spine != null && (Mathf.Abs(pitch) > 0.05f || Mathf.Abs(roll) > 0.05f))
            {
                Quaternion bend = Quaternion.AngleAxis(pitch * 0.5f, side) * Quaternion.AngleAxis(roll * 0.5f, forward);
                spine.rotation = bend * spine.rotation;
                if (chest != null) chest.rotation = bend * chest.rotation;
            }
            Place(left, leftAt, leftWeight, dt);
            Place(right, rightAt, rightWeight, dt);

            if (Carried != null)
            {
                // The swing itself is quick; everything else eases, so the thing never snaps about.
                point = Vector3.Slerp(point, wantPoint, 1f - Mathf.Exp(-(act == Act.Swing ? 40f : 9f) * dt));
                Vector3 palm = right.Hand.position + (right.Hand.position - right.Lower.position).normalized * 0.07f;
                Vector3 over = Mathf.Abs(Vector3.Dot(point, up)) > 0.95f ? forward : up;
                Carried.Root.SetPositionAndRotation(palm, Quaternion.LookRotation(point, over));
            }
        }

        private void Place(Arm arm, Vector3 target, float wanted, float dt)
        {
            // A hand that was hanging free starts from the new target rather than sweeping in from an old one.
            arm.Target = arm.Weight < 0.01f ? target : Vector3.Lerp(arm.Target, target, 1f - Mathf.Exp(-32f * dt));
            arm.Weight = Mathf.MoveTowards(arm.Weight, Mathf.Clamp01(wanted), dt * 9f);
            if (arm.Weight < 0.005f) return;

            Quaternion upperBefore = arm.Upper.localRotation, lowerBefore = arm.Lower.localRotation;
            Vector3 a = arm.Upper.position, b = arm.Lower.position, c = arm.Hand.position;
            float upperLength = Vector3.Distance(a, b), lowerLength = Vector3.Distance(b, c);
            Vector3 to = arm.Target - a;
            float reach = Mathf.Clamp(to.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.02f, (upperLength + lowerLength) * 0.995f);

            Vector3 axis = Vector3.Cross(b - a, c - b);
            if (axis.sqrMagnitude < 1e-7f) axis = Vector3.Cross(b - a, arm.Hint - a);
            if (axis.sqrMagnitude < 1e-7f) axis = transform.up;
            float current = Vector3.Angle(a - b, c - b);
            float needed = Mathf.Acos(Mathf.Clamp((upperLength * upperLength + lowerLength * lowerLength - reach * reach)
                / (2f * upperLength * lowerLength), -1f, 1f)) * Mathf.Rad2Deg;
            arm.Lower.rotation = Quaternion.AngleAxis(current - needed, axis.normalized) * arm.Lower.rotation;

            arm.Upper.rotation = Quaternion.FromToRotation(arm.Hand.position - a, to) * arm.Upper.rotation;

            Vector3 line = to.normalized;
            Vector3 elbow = Vector3.ProjectOnPlane(arm.Lower.position - a, line);
            Vector3 wantedElbow = Vector3.ProjectOnPlane(arm.Hint - a, line);
            if (elbow.sqrMagnitude > 1e-6f && wantedElbow.sqrMagnitude > 1e-6f)
                arm.Upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbow, wantedElbow, line), line) * arm.Upper.rotation;

            if (arm.Weight < 0.995f)
            {
                arm.Upper.localRotation = Quaternion.Slerp(upperBefore, arm.Upper.localRotation, arm.Weight);
                arm.Lower.localRotation = Quaternion.Slerp(lowerBefore, arm.Lower.localRotation, arm.Weight);
            }
        }

        // Turn the head toward whoever matters right now, layered on top of the clip.
        private void Look(float dt)
        {
            Quaternion wanted = Quaternion.identity;
            if (LookTarget != null)
            {
                Vector3 to = LookTarget.position + Vector3.up * 1.5f - head.position;
                Vector3 forward = transform.forward;
                float yaw = Mathf.Clamp(Vector3.SignedAngle(forward, new Vector3(to.x, 0, to.z), Vector3.up), -65f, 65f);
                float tilt = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -25f, 25f);
                if (to.sqrMagnitude < 144f)
                    wanted = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(tilt, transform.right);
            }
            look = Quaternion.Slerp(look, wanted, 1f - Mathf.Exp(-4f * dt));
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, look, 0.35f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, look, 0.5f) * head.rotation;
            if (Mathf.Abs(HeadTilt) > 0.05f)
                head.rotation = Quaternion.AngleAxis(HeadTilt, transform.forward) * head.rotation;
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
