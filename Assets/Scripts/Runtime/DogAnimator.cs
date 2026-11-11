using System.Collections.Generic;
using UnityEngine;

namespace DontForgetMe
{
    // The dog model has a skeleton but no motion capture, so its gait, tail, ears and
    // drinking are generated here by rotating bones around the body's own axes.
    public sealed class DogAnimator : MonoBehaviour
    {
        public bool Drinking { get; set; }
        public Transform LookTarget { get; set; }

        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        private Transform model;
        private Vector3 modelRest;
        private float speed;
        private float gait;
        private float stride;
        private float drink;
        private float alert;
        private float barkUntil;
        private float wagEnergy = 0.5f;
        private float seed;

        public void Configure(Transform dogModel)
        {
            model = dogModel;
            modelRest = model.localPosition;
            seed = Random.value * 20f;
            foreach (Transform bone in model.GetComponentsInChildren<Transform>())
            {
                string key = bone.name.Replace("beagle ", "").Replace(" ", "");
                bones[key] = bone;
                rest[bone] = bone.localRotation;
            }
        }

        public void SetSpeed(float metresPerSecond)
        {
            speed = metresPerSecond;
        }

        public void Bark()
        {
            barkUntil = Time.time + 0.35f;
            alert = 1f;
        }

        public void Excite(float amount)
        {
            wagEnergy = Mathf.Clamp01(amount);
        }

        private void Turn(string bone, float degrees, Vector3 axis)
        {
            if (Mathf.Abs(degrees) < 0.01f || !bones.TryGetValue(bone, out Transform target)) return;
            target.rotation = Quaternion.AngleAxis(degrees, axis) * target.rotation;
        }

        private void LateUpdate()
        {
            if (model == null || Time.timeScale <= 0) return;
            foreach (KeyValuePair<Transform, Quaternion> pair in rest) pair.Key.localRotation = pair.Value;

            float dt = Time.deltaTime;
            float t = Time.time + seed;
            Vector3 right = model.right, up = model.up, forward = model.forward;
            float moving = Mathf.InverseLerp(0.15f, 1.2f, speed);
            stride = Mathf.Lerp(stride, moving, 1f - Mathf.Exp(-8f * dt));
            gait += dt * Mathf.Lerp(7f, 15f, Mathf.InverseLerp(1f, 4.5f, speed)) * Mathf.Max(moving, 0.2f);
            drink = Mathf.MoveTowards(drink, Drinking && moving < 0.1f ? 1f : 0f, dt * 2.2f);
            alert = Mathf.MoveTowards(alert, 0f, dt * 0.5f);

            // Trot: diagonal legs move together.
            float reach = Mathf.Lerp(16f, 30f, Mathf.InverseLerp(1f, 4.5f, speed)) * stride;
            Leg("L", true, Mathf.Sin(gait), reach, right);
            Leg("R", true, Mathf.Sin(gait + Mathf.PI), reach, right);
            Leg("L", false, Mathf.Sin(gait + Mathf.PI), reach, right);
            Leg("R", false, Mathf.Sin(gait), reach, right);

            float breath = Mathf.Sin(t * 2.4f);
            float bob = Mathf.Abs(Mathf.Sin(gait)) * 0.018f * stride;
            model.localPosition = modelRest + Vector3.up * (bob + breath * 0.002f) - Vector3.up * (drink * 0.015f);
            Turn("Spine1", Mathf.Sin(gait) * 3.5f * stride, up);
            Turn("Spine2", -Mathf.Sin(gait) * 3.5f * stride + breath * 0.6f, up);

            // Head: drinking lowers the whole neck; otherwise it follows what the dog is watching.
            float lap = drink * Mathf.Sin(t * 9f) * 3f;
            float bark = Time.time < barkUntil ? Mathf.Sin((barkUntil - Time.time) / 0.35f * Mathf.PI) : 0f;
            float pitch = drink * 20f + lap - bark * 14f - alert * 5f + Mathf.Sin(gait * 2f) * 2.5f * stride;
            float yaw = 0f;
            if (LookTarget != null && drink < 0.1f)
            {
                Vector3 to = LookTarget.position - model.position;
                to.y = 0;
                if (to.sqrMagnitude > 0.2f && to.sqrMagnitude < 200f)
                    yaw = Mathf.Clamp(Vector3.SignedAngle(forward, to, Vector3.up), -55f, 55f) / 3f;
            }
            else yaw = Mathf.Sin(t * 0.37f) * 5f * (1f - drink);
            // The forelegs hang off the first neck bone in this rig, so undo its turn on the shoulders.
            Turn("Neck", pitch * 1.5f, right);
            Turn("LClavicle", -pitch * 1.5f, right);
            Turn("RClavicle", -pitch * 1.5f, right);
            foreach (string bone in new[] { "Neck1", "Neck2" })
            {
                Turn(bone, pitch * 0.8f, right);
                Turn(bone, yaw * 1.5f, up);
            }
            Turn("Head", drink * 12f - bark * 10f, right);
            Turn("XtraMouth", bark * 16f + drink * Mathf.Max(0, Mathf.Sin(t * 9f)) * 6f, right);

            float twitch = Mathf.PerlinNoise(t * 0.6f, 3.1f) > 0.72f ? Mathf.Sin(t * 22f) * 6f : 0f;
            Turn("EarL", twitch - alert * 10f + stride * Mathf.Sin(gait * 2f) * 5f, forward);
            Turn("EarR", -twitch + alert * 10f - stride * Mathf.Sin(gait * 2f) * 5f, forward);

            float energy = Mathf.Lerp(wagEnergy, 1f, stride * 0.5f) * (1f - drink * 0.5f);
            float wag = Mathf.Sin(t * Mathf.Lerp(5f, 13f, energy)) * Mathf.Lerp(6f, 22f, energy);
            Turn("Tail", -alert * 14f - 6f * stride, right);
            Turn("Tail", wag * 0.5f, up);
            Turn("Tail1", wag * 0.8f, up);
            Turn("Tail2", wag, up);
            Turn("Tail3", wag, up);
        }

        private void Leg(string side, bool front, float swing, float reach, Vector3 right)
        {
            // The paw lifts while the leg swings forward and stays straight while it pushes back.
            bool leads = (side == "L") == front;
            float lift = Mathf.Max(0f, Mathf.Cos(gait + (leads ? 0f : Mathf.PI))) * (reach > 0.01f ? 1f : 0f);
            if (front)
            {
                Turn(side + "UpperArm", -swing * reach, right);
                Turn(side + "Forearm", lift * reach * 1.1f * -1f, right);
                Turn(side + "Hand", lift * reach * 0.9f, right);
            }
            else
            {
                Turn(side + "Thigh", -swing * reach, right);
                Turn(side + "Calf", lift * reach * 0.9f, right);
                Turn(side + "Foot", -lift * reach * 0.8f, right);
            }
        }
    }
}
