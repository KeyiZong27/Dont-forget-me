using UnityEngine;

namespace DontForgetMe
{
    public sealed class SandboxObserver : MonoBehaviour
    {
        public SandboxForgettingState State { get; } = new SandboxForgettingState();
        public SandboxTarget Target { get; private set; }
        public string Stage => Target.Health.IsDown ? "已击倒（可重置）"
            : State.IsHostile ? "已异化 · 可攻击"
            : !State.IsProtected ? "转变中"
            : State.PlayerMemory <= 0 ? "被另一观察者保护"
            : State.PlayerMemory <= 0.15f ? "你快忘记他了（仍受保护）" : "友方 · 被记得";
        private Transform visual;
        private Quaternion restRotation;
        private Renderer[] renderers;
        private Color[] baseColors;
        private MaterialPropertyBlock properties;
        private bool wasHostile;

        public void Configure(ActorMotor actor)
        {
            visual = actor.transform.GetChild(0);
            restRotation = visual.localRotation;
            Target = gameObject.AddComponent<SandboxTarget>();
            Target.Configure(actor, visual);
            Target.CustomLabel = "记忆观察员";
            Target.SetTestHostility(false);
            renderers = visual.GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].sharedMaterial.color;
            properties = new MaterialPropertyBlock();
            ApplyState();
        }

        private void Update()
        {
            if (!PlayerWorld.CanSimulate) return;
            if (!Target.Health.IsDown) State.Tick(Time.deltaTime);
            ApplyState();
        }

        private void LateUpdate()
        {
            if (!PlayerWorld.CanSimulate || Target.Health.IsDown) return;
            float progress = State.Progress;
            float tremble = Mathf.Sin(Time.time * 17f) * progress * 5;
            visual.localRotation = restRotation * Quaternion.Euler(progress * 22, tremble, progress * -13);
        }

        public void ApplyState()
        {
            bool hostile = State.IsHostile;
            Target.SetTestHostility(hostile);
            if (hostile != wasHostile)
            {
                SandboxGame.Instance.Caption(hostile
                    ? "记忆观察员已完成转变。可用武器攻击；是否追击由敌人总开关控制。"
                    : "测试记忆恢复了保护，转变已撤回。", 5);
                wasHostile = hostile;
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                Color original = baseColors[i];
                Color pale = Color.Lerp(original, new Color(original.grayscale, original.grayscale, original.grayscale), 0.8f);
                Color changed = Color.Lerp(pale, new Color(0.37f, 0.12f, 0.16f), State.Progress);
                float amount = !State.IsProtected ? 1f : Mathf.InverseLerp(0.2f, 0, State.PlayerMemory) * 0.25f;
                properties.SetColor("_Color", Color.Lerp(original, changed, amount));
                renderers[i].SetPropertyBlock(properties);
            }
            if (!Target.Health.IsDown && State.Progress == 0) visual.localRotation = restRotation;
        }

        public void SetPlayerMemory(float value)
        {
            State.SetPlayerMemory(value);
            ApplyState();
        }

        public void SetWitnessMemory(float value)
        {
            State.SetWitnessMemory(value);
            ApplyState();
        }

        public void ResetObservation()
        {
            State.Reset();
            Target.ResetTarget();
            ApplyState();
        }
    }
}
