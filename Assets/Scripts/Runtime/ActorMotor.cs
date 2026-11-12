using System.Collections.Generic;
using UnityEngine;

namespace DontForgetMe
{
    // Everyone who takes up room on the ground. People do not collide through physics (that
    // jams doorways); instead each body eases itself out of the others, lighter ones giving
    // way more, so a crowd presses together without anyone passing through anyone.
    public static class Crowd
    {
        public sealed class Body
        {
            public Transform Root;
            public float Radius = 0.3f;
            public float Weight = 1f;
        }

        private static readonly List<Body> bodies = new List<Body>();

        public static IReadOnlyList<Body> Bodies => bodies;

        public static Body Join(Transform root, float radius, float weight)
        {
            Body body = new Body { Root = root, Radius = radius, Weight = weight };
            bodies.Add(body);
            return body;
        }

        public static void Leave(Body body)
        {
            bodies.Remove(body);
        }

        public static void Clear()
        {
            bodies.Clear();
        }

        private static bool Present(Body body)
        {
            return body.Root != null && body.Root.gameObject.activeInHierarchy;
        }

        // How far "self" should shift this frame to stop overlapping its neighbours.
        public static Vector3 Push(Body self, float dt)
        {
            Vector3 total = Vector3.zero;
            Vector3 at = self.Root.position;
            for (int i = 0; i < bodies.Count; i++)
            {
                Body other = bodies[i];
                if (other == self || !Present(other)) continue;
                Vector3 apart = at - other.Root.position;
                if (Mathf.Abs(apart.y) > 1.5f) continue;
                apart.y = 0;
                float room = self.Radius + other.Radius;
                float squared = apart.sqrMagnitude;
                if (squared >= room * room) continue;
                float distance = Mathf.Sqrt(squared);
                Vector3 away = distance > 0.001f ? apart / distance
                    : Quaternion.Euler(0, (bodies.IndexOf(self) * 97) % 360, 0) * Vector3.forward;
                total += away * (room - distance) * (other.Weight / (self.Weight + other.Weight));
            }
            return total * Mathf.Min(1f, dt * 12f);
        }

        // A sideways nudge so a walker goes round whoever is standing in the way, instead of
        // pressing into their back. Bodies near "goal" are left alone: that is who the walker is going to.
        public static Vector3 Around(Body self, Vector3 heading, Vector3 goal)
        {
            Vector3 steer = Vector3.zero;
            Vector3 at = self.Root.position;
            Vector3 beside = new Vector3(heading.z, 0, -heading.x);
            for (int i = 0; i < bodies.Count; i++)
            {
                Body other = bodies[i];
                if (other == self || !Present(other)) continue;
                Vector3 to = other.Root.position - at;
                to.y = 0;
                float ahead = Vector3.Dot(to, heading);
                if (ahead < 0.05f || ahead > 1.5f) continue;
                float offset = Vector3.Dot(to, beside);
                if (Mathf.Abs(offset) > self.Radius + other.Radius + 0.2f) continue;
                Vector3 fromGoal = other.Root.position - goal;
                fromGoal.y = 0;
                if (fromGoal.sqrMagnitude < 0.9f * 0.9f) continue;
                steer += beside * ((offset > 0 ? -1f : 1f) * (1f - ahead / 1.5f));
            }
            return steer;
        }
    }

    public sealed class ActorMotor : MonoBehaviour
    {
        public string DisplayName { get; set; }
        public string Identity { get; set; }
        public Vector3 FollowOffset { get; set; }
        public float Speed { get; set; } = 3.2f;
        // Pace used when "Hurry" is set, or when a follower has fallen well behind.
        public float RunSpeed { get; set; } = 4.7f;
        public bool Hurry { get; set; }
        // Keeps moving after the chapter's closing beat (the crowd outside the gate does not stop for it).
        public bool Restless { get; set; }
        public bool HasDestination { get; private set; }
        public Vector3 Destination { get; private set; }
        public CharacterAnimator Body { get; private set; }
        public DogAnimator DogBody { get; private set; }
        // When standing still, turn to face this point (a person being spoken to, a job at hand).
        public Vector3? Facing { get; set; }
        public float PlanarSpeed => new Vector2(velocity.x, velocity.z).magnitude;
        // True while a shove still has this body on its heels.
        public bool Reeling => Time.time < reelUntil;
        // Ran into a wall or other scenery on the last move.
        public bool HitScenery { get; private set; }
        public float Weight { get => body.Weight; set => body.Weight = value; }

        public Transform FollowTarget
        {
            get { return followTarget; }
            set
            {
                followTarget = value;
                if (value != null) stopped = false;
            }
        }

        private Transform followTarget;
        private CharacterController controller;
        private Crowd.Body body;
        private float verticalSpeed;
        private bool stopped;
        private bool collapsed;
        private bool resting;
        private Vector3 velocity;
        private Vector3 thrown;
        private float reelUntil;
        private readonly Queue<Vector3> route = new Queue<Vector3>();

        public void Configure(bool dog, Transform model)
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.radius = dog ? 0.3f : 0.34f;
            controller.height = dog ? 0.7f : 1.8f;
            controller.center = new Vector3(0, controller.height / 2f, 0);
            controller.stepOffset = 0.22f;
            controller.skinWidth = 0.03f;
            // The default ignores moves under a millimetre, which stalls slow walkers at high frame rates.
            controller.minMoveDistance = 0f;
            body = Crowd.Join(transform, dog ? 0.26f : 0.31f, dog ? 0.3f : 1f);
            if (model != null)
            {
                Body = model.GetComponent<CharacterAnimator>();
                DogBody = model.GetComponent<DogAnimator>();
            }
        }

        public void GoTo(Vector3 point)
        {
            route.Clear();
            FollowTarget = null;
            Destination = point;
            HasDestination = true;
            stopped = false;
            Facing = null;
        }

        public void GoVia(params Vector3[] points)
        {
            if (points == null || points.Length == 0)
                throw new System.ArgumentException("An actor route needs at least one point.", nameof(points));
            GoTo(points[0]);
            for (int i = 1; i < points.Length; i++) route.Enqueue(points[i]);
        }

        public void Follow(Transform target, Vector3 offset)
        {
            route.Clear();
            HasDestination = false;
            FollowTarget = target;
            FollowOffset = offset;
            stopped = false;
            Facing = null;
        }

        public void Stop()
        {
            route.Clear();
            FollowTarget = null;
            HasDestination = false;
            stopped = true;
        }

        // Goes down and stays down: no longer steers, and no longer takes up room among the others.
        public void Collapse()
        {
            Stop();
            collapsed = true;
            Crowd.Leave(body);
        }

        // Thrown by a shove or a blow: the body travels with that velocity, bleeding it off,
        // and cannot steer itself until "seconds" have passed.
        public void Throw(Vector3 push, float seconds)
        {
            push.y = 0;
            thrown = push;
            velocity = Vector3.zero;
            reelUntil = Mathf.Max(reelUntil, Time.time + seconds);
        }

        private void OnDestroy()
        {
            if (body != null) Crowd.Leave(body);
        }

        private void Update()
        {
            DemoGame game = DemoGame.Instance;
            if (controller == null || game == null || game.IsPaused || game.IsFailed)
                return;
            bool finished = game.State.Phase == OpeningPhase.Complete && !Restless;
            bool reeling = Reeling;
            Vector3 direction = Vector3.zero;
            float pace = Hurry ? RunSpeed : Speed;
            if (!stopped && !finished && !reeling)
            {
                Vector3 target = transform.position;
                bool hasTarget = false;
                if (FollowTarget != null)
                {
                    target = FollowTarget.position + FollowTarget.rotation * FollowOffset;
                    hasTarget = true;
                }
                else if (HasDestination)
                {
                    target = Destination;
                    hasTarget = true;
                }

                if (hasTarget)
                {
                    direction = target - transform.position;
                    direction.y = 0;
                    float distance = direction.magnitude;
                    bool following = FollowTarget != null;
                    float stoppingDistance = following ? 0.8f : 0.2f;
                    // A follower who has settled waits for a real gap before setting off again.
                    if (following && resting && distance < 1.5f) distance = 0;
                    if (distance <= stoppingDistance)
                    {
                        direction = Vector3.zero;
                        resting = true;
                        if (!following)
                        {
                            if (route.Count > 0) Destination = route.Dequeue();
                            else HasDestination = false;
                        }
                    }
                    else
                    {
                        resting = false;
                        direction /= distance;
                        if (following)
                            pace = distance > 4.5f ? Mathf.Max(Speed, RunSpeed)
                                : Mathf.Lerp(1.5f, Speed, Mathf.InverseLerp(1.2f, 3.2f, distance));
                        else if (route.Count == 0) pace = Mathf.Min(pace, Mathf.Max(1.2f, distance * 3.5f));
                        Vector3 around = Crowd.Around(body, direction, target);
                        if (around.sqrMagnitude > 0.0001f) direction = (direction + around * 0.9f).normalized;
                    }
                }
            }

            Vector3 wanted = direction * pace;
            velocity = Vector3.MoveTowards(velocity, wanted, (wanted.sqrMagnitude > velocity.sqrMagnitude ? 12f : 18f) * Time.deltaTime);
            thrown = Vector3.Lerp(thrown, Vector3.zero, 1f - Mathf.Exp(-4.5f * Time.deltaTime));
            if (thrown.sqrMagnitude < 0.01f) thrown = Vector3.zero;

            // Someone knocked backwards keeps facing whoever did it; they do not turn to run.
            Vector3 face = !reeling && velocity.sqrMagnitude > 0.05f ? velocity
                : Facing.HasValue ? Facing.Value - transform.position : Vector3.zero;
            face.y = 0;
            if (face.sqrMagnitude > 0.0025f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face),
                    1f - Mathf.Exp(-(velocity.sqrMagnitude > 0.05f ? 9f : 5f) * Time.deltaTime));

            verticalSpeed = controller.isGrounded ? -2f : verticalSpeed - 20f * Time.deltaTime;
            Vector3 before = transform.position;
            CollisionFlags flags = controller.Move((velocity + thrown + Vector3.up * verticalSpeed) * Time.deltaTime
                + (collapsed ? Vector3.zero : Crowd.Push(body, Time.deltaTime)));
            HitScenery = (flags & CollisionFlags.Sides) != 0;
            Vector3 moved = transform.position - before;
            moved.y = 0;
            float actual = Time.deltaTime > 0 ? moved.magnitude / Time.deltaTime : 0;
            float shown = Mathf.Min(actual, (velocity + thrown).magnitude + 0.2f);
            bool backwards = shown > 0.25f && Vector3.Dot(moved, transform.forward) < 0;
            if (Body != null) Body.SetSpeed(collapsed ? 0f : shown, backwards);
            if (DogBody != null) DogBody.SetSpeed(shown);
        }
    }
}
