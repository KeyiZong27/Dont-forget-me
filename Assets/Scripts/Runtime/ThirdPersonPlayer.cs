using System;
using UnityEngine;

namespace DontForgetMe
{
    public sealed class ThirdPersonPlayer : MonoBehaviour
    {
        public float Stamina { get; private set; } = 100f;
        public float Health { get; private set; } = 100f;
        public float Energy { get; private set; } = 100f;
        public float Heading { get; private set; }
        public float LastHitTime { get; private set; } = -10f;
        public bool Sprinting { get; private set; }
        public float PlanarSpeed => new Vector2(velocity.x, velocity.z).magnitude;
        public CharacterAnimator Body { get; private set; }
        public Vector3 Forward => model.forward;
        // Lets a scripted walkthrough steer instead of the keyboard: world-space direction and sprint flag.
        public Func<Vector3> ScriptedMove { get; set; }
        public bool ScriptedSprint { get; set; }
        public float Yaw { get => yaw; set => yaw = value; }
        public float Pitch { get => pitch; set => pitch = value; }
        // Hands full (carrying something off the pile): the controls wait until it is set down.
        public bool Busy { get; private set; }
        public bool Walking => walkTarget.HasValue;
        public int ShovesLanded { get; private set; }

        private CharacterController controller;
        private Crowd.Body body;
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
        private float shoveAt = -1f;
        private float faceHeldUntil;
        private Vector3 velocity;
        private Vector3 thrown;
        private Vector3 pivotSmooth;
        private Vector3? walkTarget;
        private float walkPace;

        public void Configure(Transform bodyModel, Camera camera)
        {
            model = bodyModel;
            view = camera;
            Body = bodyModel.GetComponent<CharacterAnimator>();
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.34f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;
            body = Crowd.Join(transform, 0.31f, 1.6f);
            yaw = 0;
            pivotSmooth = transform.position;
            UpdateCamera(true);
        }

        public void Occupy(bool busy)
        {
            Busy = busy;
            if (!busy) walkTarget = null;
        }

        // A short walk made on the player's behalf while Busy (stepping up to a crate, carrying it off).
        public void WalkTo(Vector3 point, float pace)
        {
            walkTarget = point;
            walkPace = pace;
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

            Vector3 direction = Vector3.zero;
            bool wantsSprint = false;
            if (finished) { }
            else if (Busy)
            {
                if (walkTarget.HasValue)
                {
                    Vector3 to = walkTarget.Value - transform.position;
                    to.y = 0;
                    if (to.magnitude < 0.12f) walkTarget = null;
                    else direction = to.normalized * Mathf.Min(1f, to.magnitude * 4f + 0.3f);
                }
            }
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
            Vector3 wanted = direction * (Busy ? walkPace : running ? 5.2f : 3.2f);
            velocity = Vector3.MoveTowards(velocity, wanted, (wanted.sqrMagnitude >= velocity.sqrMagnitude ? 20f : 26f) * Time.deltaTime);
            thrown = Vector3.Lerp(thrown, Vector3.zero, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (thrown.sqrMagnitude < 0.01f) thrown = Vector3.zero;
            fallSpeed = controller.isGrounded ? -2f : fallSpeed - 20f * Time.deltaTime;
            Vector3 before = transform.position;
            controller.Move((velocity + thrown + Vector3.up * fallSpeed) * Time.deltaTime + Crowd.Push(body, Time.deltaTime));
            Vector3 moved = transform.position - before;
            moved.y = 0;
            float actual = Time.deltaTime > 0 ? moved.magnitude / Time.deltaTime : 0;
            bool backwards = thrown.sqrMagnitude > 0.6f && Vector3.Dot(moved, model.forward) < 0;
            if (Body != null) Body.SetSpeed(Mathf.Min(actual, velocity.magnitude + thrown.magnitude + 0.2f), backwards);
            if (velocity.sqrMagnitude > 0.04f && Time.time > faceHeldUntil)
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

            // The shove lands when the arms reach full stretch, not when the key goes down.
            if (shoveAt > 0 && Time.time >= shoveAt)
            {
                shoveAt = -1f;
                int struck = game.Siege != null ? game.Siege.ShoveFrom(transform.position, model.forward, 1.7f, 70f, 1f, transform) : 0;
                if (struck > 0)
                {
                    ShovesLanded++;
                    shake = 0.4f;
                    game.ShoveLanded(struck);
                }
            }
        }

        // Steps in and drives both hands forward. Only people actually within reach in front are moved.
        public bool TryPush()
        {
            DemoGame game = DemoGame.Instance;
            if (pushCooldown > 0 || Stamina < 15f || Busy || shoveAt > 0) return false;
            Vector3? contact = null;
            Hostile near = game.Siege != null ? game.Siege.Nearest(transform.position, 2.1f, false) : null;
            if (near != null)
            {
                Vector3 to = near.transform.position - transform.position;
                to.y = 0;
                Face(near.transform.position);
                if (to.magnitude < 1.75f) contact = near.transform.position + Vector3.up * 1.3f - to.normalized * 0.14f;
            }
            Body.Shove(contact);
            faceHeldUntil = Time.time + 0.5f;
            thrown += model.forward * 2.6f;
            shoveAt = Time.time + 0.17f;
            Stamina -= 15f;
            pushCooldown = 0.85f;
            game.Footstep(0.25f);
            return true;
        }

        public void Face(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.01f)
            {
                model.rotation = Quaternion.LookRotation(to);
                Heading = model.rotation.eulerAngles.y;
            }
        }

        private void LateUpdate()
        {
            if (view != null) UpdateCamera(false);
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

        // Struck by someone at "from": knocked back a step, and it hurts.
        public void TakeHit(Vector3 from, float damage)
        {
            if (hitCooldown > 0) return;
            Health = Mathf.Max(0, Health - damage);
            hitCooldown = 1.4f;
            LastHitTime = Time.time;
            shake = 0.6f;
            Vector3 away = transform.position - from;
            away.y = 0;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -model.forward;
            thrown += away * 3.2f;
            Body.Recoil(away, 0.55f);
            if (Health <= 0) DemoGame.Instance.Fail();
        }
    }
}
