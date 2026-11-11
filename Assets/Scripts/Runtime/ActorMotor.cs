using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontForgetMe
{
    public sealed class ActorMotor : MonoBehaviour
    {
        public string DisplayName { get; set; }
        public string Identity { get; set; }
        public Vector3 FollowOffset { get; set; }
        public float Speed { get; set; } = 3.2f;
        public bool HasDestination { get; private set; }
        public Vector3 Destination { get; private set; }
        public CharacterAnimator Body { get; private set; }
        public DogAnimator DogBody { get; private set; }
        // When standing still, turn to face this point (a person being spoken to, a job at hand).
        public Vector3? Facing { get; set; }
        public float PlanarSpeed => new Vector2(velocity.x, velocity.z).magnitude;

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
        private float verticalSpeed;
        private bool stopped;
        private bool resting;
        private Vector3 velocity;
        private readonly Queue<Vector3> route = new Queue<Vector3>();

        public void Configure(bool dog, Transform model)
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.radius = dog ? 0.3f : 0.34f;
            controller.height = dog ? 0.7f : 1.8f;
            controller.center = new Vector3(0, controller.height / 2f, 0);
            controller.stepOffset = 0.22f;
            controller.skinWidth = 0.03f;
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
                throw new ArgumentException("An actor route needs at least one point.", nameof(points));
            GoTo(points[0]);
            for (int i = 1; i < points.Length; i++) route.Enqueue(points[i]);
        }

        public void Stop()
        {
            route.Clear();
            FollowTarget = null;
            HasDestination = false;
            stopped = true;
        }

        public void KnockBack(Vector3 displacement)
        {
            Stop();
            velocity = Vector3.zero;
            controller.Move(displacement);
        }

        private void Update()
        {
            DemoGame game = DemoGame.Instance;
            if (controller == null || game == null || game.IsPaused || game.IsFailed)
                return;
            bool finished = game.State.Phase == OpeningPhase.Complete;
            Vector3 direction = Vector3.zero;
            float pace = Speed;
            if (!stopped && !finished)
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
                        if (following) pace = Mathf.Lerp(1.5f, Speed, Mathf.InverseLerp(1.2f, 3.2f, distance));
                        else if (route.Count == 0) pace = Mathf.Min(Speed, Mathf.Max(1.2f, distance * 3.5f));
                    }
                }
            }

            Vector3 wanted = direction * pace;
            velocity = Vector3.MoveTowards(velocity, wanted, (wanted.sqrMagnitude > velocity.sqrMagnitude ? 12f : 18f) * Time.deltaTime);
            Vector3 face = velocity.sqrMagnitude > 0.05f ? velocity
                : Facing.HasValue ? Facing.Value - transform.position : Vector3.zero;
            face.y = 0;
            if (face.sqrMagnitude > 0.0025f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face),
                    1f - Mathf.Exp(-(velocity.sqrMagnitude > 0.05f ? 9f : 5f) * Time.deltaTime));

            verticalSpeed = controller.isGrounded ? -2f : verticalSpeed - 20f * Time.deltaTime;
            Vector3 before = transform.position;
            controller.Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);
            Vector3 moved = transform.position - before;
            moved.y = 0;
            float actual = Time.deltaTime > 0 ? moved.magnitude / Time.deltaTime : 0;
            if (Body != null) Body.SetSpeed(Mathf.Min(actual, velocity.magnitude + 0.2f));
            if (DogBody != null) DogBody.SetSpeed(Mathf.Min(actual, velocity.magnitude + 0.2f));
        }
    }
}
