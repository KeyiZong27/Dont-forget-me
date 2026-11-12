using UnityEngine;

namespace DontForgetMe
{
    public sealed class SandboxTarget : MonoBehaviour
    {
        public SandboxHealth Health { get; } = new SandboxHealth();
        public bool IsEnemy { get; private set; }
        public bool CanBeAttacked { get; private set; } = true;
        public string CustomLabel { get; set; }
        public string Label => CustomLabel ?? (IsEnemy ? "测试异常者" : "训练木桩");
        private ActorMotor motor;
        private Vector3 home;
        private Quaternion initialRotation;
        private Transform visual;
        private float stunnedUntil;

        public void Configure(ActorMotor actor, Transform body)
        {
            motor = actor;
            visual = body;
            IsEnemy = actor != null;
            home = transform.position;
            initialRotation = visual.localRotation;
        }

        private void Update()
        {
            SandboxGame game = SandboxGame.Instance;
            if (game == null || !PlayerWorld.CanSimulate || !IsEnemy || Health.IsDown) return;
            float distance = Vector3.Distance(transform.position, game.Player.transform.position);
            if (!game.EnemiesEnabled || Time.time < stunnedUntil)
            {
                motor.Stop();
                return;
            }
            if (distance <= 14f && Vector3.Distance(transform.position, home) < 22f)
            {
                if (distance > 1.25f) motor.GoTo(game.Player.transform.position);
                else
                {
                    motor.Stop();
                    if (!Physics.Linecast(transform.position + Vector3.up,
                        game.Player.transform.position + Vector3.up, ~(1 << 8), QueryTriggerInteraction.Ignore))
                        game.Player.TakeHit();
                }
            }
            else if (Vector3.Distance(transform.position, home) > 0.4f) motor.GoTo(home);
            else motor.Stop();
        }

        public void Hit(float damage)
        {
            if (!CanBeAttacked) return;
            if (!Health.Hit(damage)) return;
            stunnedUntil = Time.time + 0.65f;
            if (motor != null) motor.Stop();
            if (Health.IsDown)
            {
                if (motor != null) motor.enabled = false;
                visual.localRotation = initialRotation * Quaternion.Euler(0, 0, 75);
            }
        }

        public void SetTestHostility(bool hostile)
        {
            IsEnemy = hostile;
            CanBeAttacked = hostile;
            if (!hostile && motor != null) motor.Stop();
        }

        public void Push(Vector3 direction)
        {
            if (motor == null || Health.IsDown) return;
            stunnedUntil = Time.time + 1.8f;
            // CharacterController.Move keeps knockback outside scenery.
            motor.KnockBack(direction * 1.5f);
        }

        public void ResetTarget()
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.position = home;
            if (controller != null) controller.enabled = true;
            Health.Reset();
            visual.localRotation = initialRotation;
            stunnedUntil = Time.time + 1f;
            if (motor != null)
            {
                motor.enabled = true;
                motor.Stop();
            }
        }
    }
}
