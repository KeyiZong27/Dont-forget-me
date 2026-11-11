using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DontForgetMe
{
    // Drives a humanoid from motion-capture clips without an Animator Controller asset:
    // a standing clip ("stance") blended with walk, run and sprint cycles by ground speed.
    public sealed class CharacterAnimator : MonoBehaviour
    {
        private sealed class State
        {
            public AnimationClipPlayable Playable;
            public float Weight;
            public float Reference;
        }

        public Transform LookTarget { get; set; }
        // How far a walk cycle may be sped up to keep pace before the feet are allowed to slide.
        public float MaxStride { get; set; } = 1.6f;

        private readonly List<State> states = new List<State>();
        private readonly Dictionary<string, int> lookup = new Dictionary<string, int>();
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private Animator animator;
        private Transform head;
        private Transform neck;
        private Quaternion look = Quaternion.identity;
        private int idle, walk, run, sprint, stance;
        private float stanceUntil;
        private float speed;

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
            states[idle].Playable.SetTime(Random.value * states[idle].Playable.GetAnimationClip().length);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Apply();
            graph.Play();
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

        public void SetSpeed(float metresPerSecond)
        {
            speed = metresPerSecond;
        }

        // Replaces the standing pose (working, talking, crouching...). Zero seconds keeps it until changed.
        public void Stance(string clipId, float seconds = 0f)
        {
            int next = Ensure(clipId);
            if (next != stance && states[next].Weight < 0.01f)
            {
                AnimationClip clip = states[next].Playable.GetAnimationClip();
                states[next].Playable.SetTime(clip.length > 8f ? Random.value * (clip.length - 6f) : 0);
            }
            stance = next;
            stanceUntil = seconds > 0 ? Time.time + seconds : float.MaxValue;
        }

        public void Relax()
        {
            stance = idle;
        }

        private void Update()
        {
            if (!graph.IsValid()) return;
            if (stance != idle && Time.time > stanceUntil) stance = idle;
            float walkRef = Reference(walk, 1.4f), runRef = Reference(run, 3f), sprintRef = Reference(sprint, 5.4f);
            float moving = Mathf.InverseLerp(0.1f, Mathf.Max(0.4f, walkRef * 0.5f), speed);
            float running = run == walk ? 0 : Mathf.InverseLerp(walkRef * 1.2f, runRef * 0.92f, speed);
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
            states[walk].Playable.SetSpeed(Mathf.Clamp(speed / walkRef, 0.6f, MaxStride));
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

        // Turn the head toward whoever matters right now, layered on top of the clip.
        private void LateUpdate()
        {
            if (head == null || Time.timeScale <= 0) return;
            Quaternion wanted = Quaternion.identity;
            if (LookTarget != null)
            {
                Vector3 to = LookTarget.position + Vector3.up * 1.5f - head.position;
                Vector3 forward = transform.forward;
                float yaw = Mathf.Clamp(Vector3.SignedAngle(forward, new Vector3(to.x, 0, to.z), Vector3.up), -65f, 65f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -25f, 25f);
                if (to.sqrMagnitude < 144f)
                    wanted = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, transform.right);
            }
            look = Quaternion.Slerp(look, wanted, 1f - Mathf.Exp(-4f * Time.deltaTime));
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, look, 0.35f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, look, 0.5f) * head.rotation;
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
