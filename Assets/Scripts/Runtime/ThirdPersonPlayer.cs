using System;
using UnityEngine;

namespace DontForgetMe
{
    public sealed class ThirdPersonPlayer : MonoBehaviour
    {
        public float Stamina { get; private set; } = 100f;
        public float Health { get; private set; } = 100f;
        public float Energy { get; private set; } = 100f;
        public float EnergyDrainPerSecond { get; private set; }
        public float Heading { get; private set; }
        public float LastHitTime { get; private set; } = -10f;
        public bool Sprinting { get; private set; }
        public float PlanarSpeed => new Vector2(velocity.x, velocity.z).magnitude;
        public CharacterAnimator Body { get; private set; }
        // Lets a scripted walkthrough steer instead of the keyboard: world-space direction and sprint flag.
        public Func<Vector3> ScriptedMove { get; set; }
        public bool ScriptedSprint { get; set; }
        public float Yaw { get => yaw; set => yaw = value; }
        public float Pitch { get => pitch; set => pitch = value; }

        private CharacterController controller;
        private Transform model;
        private Camera view;
        private float yaw;
        private float pitch = 14f;
        private float fallSpeed;
        private float pushCooldown;
        private float hitCooldown;
        private float shake;
        private float distance = 4.3f;
        private float stepTravel;
        private Vector3 velocity;
        private Vector3 pivotSmooth;

        public void Configure(Transform body, Camera camera)
        {
            model = body;
            view = camera;
            Body = body.GetComponent<CharacterAnimator>();
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.34f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.03f;
            yaw = 0;
            pivotSmooth = transform.position;
            UpdateCamera(true);
        }

        private void Update()
        {
            DemoGame game = DemoGame.Instance;
            if (game == null || game.IsPaused || game.IsFailed) return;
            bool finished = game.State.Phase == OpeningPhase.Complete;

            if (Cursor.lockState == CursorLockMode.Locked && !finished)
            {
                yaw += Input.GetAxisRaw("Mouse X") * 2.3f;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.7f, -22f, 65f);
            }

            Vector3 direction;
            bool wantsSprint;
            if (finished) { direction = Vector3.zero; wantsSprint = false; }
            else if (ScriptedMove != null)
            {
                direction = Vector3.ClampMagnitude(ScriptedMove(), 1f);
                direction.y = 0;
                wantsSprint = ScriptedSprint;
            }
            else
            {
                float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
                float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
                direction = Quaternion.Euler(0, yaw, 0) * Vector3.ClampMagnitude(new Vector3(x, 0, z), 1f);
                wantsSprint = Input.GetKey(KeyCode.LeftShift);
            }
            bool running = wantsSprint && Stamina > 5f && direction.sqrMagnitude > 0;
            Sprinting = running;
            Stamina = Mathf.Clamp(Stamina + (running ? -19f : 14f) * Time.deltaTime, 0, 100);
            Energy = Mathf.Max(0, Energy - Time.deltaTime * (running ? 0.08f : 0.025f));
            Vector3 wanted = direction * (running ? 5.2f : 3.2f);
            velocity = Vector3.MoveTowards(velocity, wanted, (wanted.sqrMagnitude >= velocity.sqrMagnitude ? 20f : 26f) * Time.deltaTime);
            fallSpeed = controller.isGrounded ? -2f : fallSpeed - 20f * Time.deltaTime;
            Vector3 before = transform.position;
            controller.Move((velocity + Vector3.up * fallSpeed) * Time.deltaTime);
            Vector3 moved = transform.position - before;
            moved.y = 0;
            float actual = Time.deltaTime > 0 ? moved.magnitude / Time.deltaTime : 0;
            if (Body != null) Body.SetSpeed(Mathf.Min(actual, velocity.magnitude + 0.2f));
            if (velocity.sqrMagnitude > 0.04f)
            {
                Quaternion rotation = Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.z));
                model.rotation = Quaternion.Slerp(model.rotation, rotation, 1f - Mathf.Exp(-12f * Time.deltaTime));
                Heading = rotation.eulerAngles.y;
            }

            stepTravel += actual * Time.deltaTime;
            float strideLength = running ? 1.25f : 0.95f;
            if (stepTravel > strideLength)
            {
                stepTravel = 0;
                game.Footstep(running ? 0.16f : 0.1f);
            }

            pushCooldown -= Time.deltaTime;
            hitCooldown -= Time.deltaTime;
            if (!finished && Input.GetKeyDown(KeyCode.F)) TryPush();
        }

        public bool TryPush()
        {
            DemoGame game = DemoGame.Instance;
            if (pushCooldown > 0 || Stamina < 12f || !game.PushThreat()) return false;
            Stamina -= 12f;
            pushCooldown = 1.1f;
            shake = 0.35f;
            return true;
        }

        public void Face(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.01f) model.rotation = Quaternion.LookRotation(to);
        }

        private void LateUpdate()
        {
            if (view != null && PlayerWorld.CanSimulate) UpdateCamera(false);
        }

        private void UpdateCamera(bool immediate)
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 anchor = transform.position + Vector3.up * 1.5f;
            pivotSmooth = immediate ? anchor : Vector3.Lerp(pivotSmooth, anchor, 1f - Mathf.Exp(-16f * dt));
            Quaternion aim = Quaternion.Euler(pitch, yaw, 0);
            Vector3 pivot = pivotSmooth + aim * new Vector3(0.5f, 0, 0);
            Vector3 back = aim * Vector3.back;
            float wanted = 4.3f;
            int scenery = ~(1 << 8);
            // Keep the shoulder offset itself from pushing the camera pivot through a wall.
            if (Physics.Linecast(pivotSmooth, pivot, out RaycastHit side, scenery, QueryTriggerInteraction.Ignore))
                pivot = pivotSmooth + (pivot - pivotSmooth).normalized * Mathf.Max(0f, side.distance - 0.15f);
            if (Physics.SphereCast(pivot, 0.22f, back, out RaycastHit hit, wanted, scenery, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.45f, hit.distance - 0.1f);
            // Pull in at once so walls never clip, ease back out so the view does not pump.
            distance = immediate || wanted < distance ? wanted : Mathf.Lerp(distance, wanted, 1f - Mathf.Exp(-3.5f * dt));
            Vector3 position = pivot + back * distance;
            if (shake > 0.001f)
            {
                shake = Mathf.MoveTowards(shake, 0, dt * 1.6f);
                position += aim * new Vector3(Mathf.PerlinNoise(Time.unscaledTime * 31f, 0) - 0.5f,
                    Mathf.PerlinNoise(0, Time.unscaledTime * 29f) - 0.5f, 0) * shake * 0.5f;
            }
            view.transform.SetPositionAndRotation(position, aim);
            float lens = Sprinting ? 59f : 52f;
            view.fieldOfView = immediate ? lens : Mathf.Lerp(view.fieldOfView, lens, 1f - Mathf.Exp(-4f * dt));
        }

        public void TakeHit()
        {
            if (hitCooldown > 0) return;
            Health = Mathf.Max(0, Health - 12);
            hitCooldown = 1.7f;
            LastHitTime = Time.time;
            shake = 0.6f;
            DemoGame.Instance.Caption("他扑了过来。拉开距离，或者按 F 推开；不需要杀死他。", 3);
            if (Health <= 0) DemoGame.Instance.Fail();
        }
    }
}
