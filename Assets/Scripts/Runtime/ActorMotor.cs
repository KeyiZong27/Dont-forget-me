using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontForgetMe
{
    public sealed class ActorMotor : MonoBehaviour
    {
        public string DisplayName { get; set; }
        public string Identity { get; set; }
        public Transform FollowTarget { get; set; }
        public Vector3 FollowOffset { get; set; }
        public float Speed { get; set; } = 3.2f;
        public bool HasDestination { get; private set; }
        public Vector3 Destination { get; private set; }

        private CharacterController controller;
        private float verticalSpeed;
        private bool stopped;
        private bool isDog;
        private Transform visual;
        private readonly Queue<Vector3> route = new Queue<Vector3>();

        public void Configure(bool dog, Transform model)
        {
            isDog = dog;
            visual = model;
            controller = gameObject.AddComponent<CharacterController>();
            controller.radius = dog ? 0.3f : 0.34f;
            controller.height = dog ? 0.7f : 1.8f;
            controller.center = new Vector3(0, controller.height / 2f, 0);
            controller.stepOffset = 0.22f;
        }

        public void GoTo(Vector3 point)
        {
            route.Clear();
            FollowTarget = null;
            Destination = point;
            HasDestination = true;
            stopped = false;
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
            controller.Move(displacement);
        }

        private void Update()
        {
            if (controller == null || DemoGame.Instance == null || DemoGame.Instance.IsPaused
                || DemoGame.Instance.IsFailed || DemoGame.Instance.State.Phase == OpeningPhase.Complete) return;
            Vector3 direction = Vector3.zero;
            if (!stopped)
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
                    float stoppingDistance = FollowTarget != null ? 0.8f : 0.2f;
                    if (direction.magnitude <= stoppingDistance)
                    {
                        direction = Vector3.zero;
                        if (FollowTarget == null)
                        {
                            if (route.Count > 0) Destination = route.Dequeue();
                            else HasDestination = false;
                        }
                    }
                    else
                    {
                        direction = direction.normalized;
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(direction), Time.deltaTime * 9f);
                    }
                }
            }

            verticalSpeed = controller.isGrounded ? -2f : verticalSpeed - 20f * Time.deltaTime;
            controller.Move((direction * Speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (visual != null)
            {
                float bob = direction.sqrMagnitude > 0 ? Mathf.Sin(Time.time * (isDog ? 16f : 10f)) * 0.025f : 0;
                visual.localPosition = Vector3.up * bob;
            }
        }
    }
}
