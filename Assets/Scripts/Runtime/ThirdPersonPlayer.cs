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
        public Transform Model => model;

        private CharacterController controller;
        private Transform model;
        private Camera view;
        private float yaw;
        private float pitch = 24f;
        private float fallSpeed;
        private float pushCooldown;
        private float hitCooldown;

        public void Configure(Transform body, Camera camera)
        {
            model = body;
            view = camera;
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.34f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.25f;
            yaw = 0;
            UpdateCamera(true);
        }

        private void Update()
        {
            IPlayerWorld game = PlayerWorld.Current;
            if (!PlayerWorld.CanSimulate) return;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxisRaw("Mouse X") * 2.3f;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.7f, 8f, 65f);
            }

            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            Vector3 direction = Quaternion.Euler(0, yaw, 0) * Vector3.ClampMagnitude(new Vector3(x, 0, z), 1f);
            bool running = Input.GetKey(KeyCode.LeftShift) && Stamina > 5f && direction.sqrMagnitude > 0;
            Stamina = Mathf.Clamp(Stamina + (running ? -19f : 14f) * Time.deltaTime, 0, 100);
            EnergyDrainPerSecond = (running ? 0.08f : 0.025f) + game.ThreatEnergyDrainPerSecond;
            Energy = Mathf.Max(0, Energy - Time.deltaTime * EnergyDrainPerSecond);
            fallSpeed = controller.isGrounded ? -2f : fallSpeed - 20f * Time.deltaTime;
            controller.Move((direction * (running ? 5.2f : 3.2f) + Vector3.up * fallSpeed) * Time.deltaTime);
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion rotation = Quaternion.LookRotation(direction);
                model.rotation = Quaternion.Slerp(model.rotation, rotation, Time.deltaTime * 12f);
                Heading = rotation.eulerAngles.y;
            }

            pushCooldown -= Time.deltaTime;
            hitCooldown -= Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.F) && pushCooldown <= 0 && Stamina >= 12f)
            {
                if (game.PushThreat())
                {
                    Stamina -= 12f;
                    pushCooldown = 1.1f;
                }
            }
        }

        private void LateUpdate()
        {
            if (view != null && PlayerWorld.CanSimulate) UpdateCamera(false);
        }

        private void UpdateCamera(bool immediate)
        {
            Vector3 pivot = transform.position + Vector3.up * 1.45f;
            Vector3 offset = Quaternion.Euler(pitch, yaw, 0) * new Vector3(0.55f, 0, -5.3f);
            Vector3 target = pivot + offset;
            int scenery = ~(1 << 8);
            if (Physics.SphereCast(pivot, 0.2f, offset.normalized, out RaycastHit hit,
                offset.magnitude, scenery, QueryTriggerInteraction.Ignore))
                target = pivot + offset.normalized * Mathf.Max(0.45f, hit.distance - 0.15f);
            view.transform.position = immediate ? target :
                Vector3.Lerp(view.transform.position, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            view.transform.LookAt(pivot);
        }

        public void TakeHit()
        {
            if (hitCooldown > 0) return;
            Health = Mathf.Max(0, Health - 12);
            hitCooldown = 1.7f;
            PlayerWorld.Current.Caption("他扑了过来。拉开距离，或者按 F 推开；不需要杀死他。", 3);
            if (Health <= 0) PlayerWorld.Current.Fail();
        }

        public bool SpendStamina(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (Stamina < amount) return false;
            Stamina -= amount;
            return true;
        }

        public void Respawn(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            fallSpeed = 0;
            hitCooldown = 0;
            pushCooldown = 0;
            Health = Stamina = Energy = 100f;
            UpdateCamera(true);
        }

        // 一夜睡眠：恢复精力与体力；轻伤有所缓解，但不保证满血。
        public void Rest()
        {
            Energy = 100f;
            Stamina = 100f;
            Health = Mathf.Max(Health, 80f);
        }
    }
}
