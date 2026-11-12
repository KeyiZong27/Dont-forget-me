using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontForgetMe
{
    public sealed class SandboxGame : MonoBehaviour, IPlayerWorld
    {
        public static SandboxGame Instance { get; private set; }
        public ThirdPersonPlayer Player { get; set; }
        public Camera View { get; set; }
        public Light Sun { get; set; }
        public Light CampLight { get; set; }
        public SandboxEquipment Equipment { get; } = new SandboxEquipment();
        public MemoryNetwork Memories { get; } = new MemoryNetwork();
        public List<ActorMotor> Companions { get; } = new List<ActorMotor>();
        public List<SandboxTarget> Targets { get; } = new List<SandboxTarget>();
        public SandboxObserver Observer { get; set; }
        public bool ObservationOpen { get; private set; }
        public bool IsPaused => paused || InventoryOpen;
        public bool InventoryOpen { get; private set; }
        public bool IsFailed { get; private set; }
        public bool IsComplete => false;
        public bool EnemiesEnabled { get; private set; }
        public bool IsNight { get; private set; }
        public Interactable Nearest { get; private set; }
        public string VisibleCaption => Time.unscaledTime <= captionUntil ? caption : "";
        public float ThreatEnergyDrainPerSecond
        {
            get
            {
                if (!EnemiesEnabled) return 0;
                float drain = 0;
                foreach (SandboxTarget target in Targets)
                    if (target.IsEnemy && !target.Health.IsDown)
                        drain = Mathf.Max(drain, 0.12f * Mathf.InverseLerp(8, 1.35f,
                            Vector3.Distance(target.transform.position, Player.transform.position)));
                return drain;
            }
        }

        private readonly List<Interactable> interactions = new List<Interactable>();
        private bool paused;
        private string caption;
        private float captionUntil;
        private float nextAttack;
        private float swingUntil;
        private Transform heldVisual;
        private Transform bagVisual;
        private static readonly string[] Participants = { "player", "woman", "zhou", "dog" };

        private void Awake()
        {
            Instance = this;
            PlayerWorld.Current = this;
            Physics.IgnoreLayerCollision(8, 8, true);
            SandboxWorld.Build(this);
            foreach (string observer in Participants)
                foreach (string subject in Participants)
                    if (observer != subject) Memories.Recognize(observer, subject);
            gameObject.AddComponent<SandboxHud>();
            UpdatePause();
            Caption("独立测试世界：前方拾取装备，东侧训练区，东北废墟，西侧树林。不会推进剧情。", 9);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ObservationOpen) ObservationOpen = false;
                else if (InventoryOpen) InventoryOpen = false;
                else paused = !paused;
                UpdatePause();
            }
            if (Input.GetKeyDown(KeyCode.I) && !IsFailed && !paused)
            {
                ObservationOpen = false;
                InventoryOpen = !InventoryOpen;
                UpdatePause();
            }
            if (!PlayerWorld.CanSimulate) return;
            if (Input.GetKeyDown(KeyCode.M)) ToggleObservation();
            UpdateInteraction();
            if (Input.GetKeyDown(KeyCode.E) && Nearest != null) Nearest.OnInteract?.Invoke();
            if (Input.GetKeyDown(KeyCode.G)) DropWeapon();
            if (Input.GetKeyDown(KeyCode.B)) DropBackpack();
            if (Input.GetMouseButtonDown(0) && !ObservationOpen) Attack();
            if (Player.transform.position.y < -10) ReturnToCamp();
        }

        private void LateUpdate()
        {
            if (heldVisual == null || !PlayerWorld.CanSimulate) return;
            float t = Mathf.Clamp01((swingUntil - Time.time) / 0.35f);
            heldVisual.localRotation = Quaternion.Euler(-20 - Mathf.Sin(t * Mathf.PI) * 110, 0, -12);
        }

        public void AddAction(GameObject owner, string prompt, Action action)
        {
            Interactable interaction = owner.AddComponent<Interactable>();
            interaction.Prompt = prompt;
            interaction.OnInteract = action;
            interaction.IsAvailable = () => true;
            interactions.Add(interaction);
        }

        private void UpdateInteraction()
        {
            Nearest = null;
            float best = float.MaxValue;
            interactions.RemoveAll(item => item == null);
            foreach (Interactable item in interactions)
            {
                if (!item.Available || !item.gameObject.activeInHierarchy) continue;
                Vector3 delta = item.transform.position - Player.transform.position;
                delta.y = 0;
                if (delta.magnitude > item.Range || delta.magnitude >= best) continue;
                Vector3 end = item.transform.position + Vector3.up * 0.7f;
                if (Physics.Linecast(Player.transform.position + Vector3.up, end, out RaycastHit hit,
                    ~(1 << 8), QueryTriggerInteraction.Ignore)
                    && hit.transform != item.transform && !hit.transform.IsChildOf(item.transform)) continue;
                best = delta.magnitude;
                Nearest = item;
            }
        }

        public void SpawnItem(SandboxItem item, Vector3 position)
        {
            SandboxPickup pickup = NewPickup(position);
            pickup.Item = item;
            Transform visual = SandboxWorld.ItemVisual(item, pickup.transform);
            if (SandboxItems.IsWeapon(item))
            {
                visual.localPosition = new Vector3(0, 0.1f, 0.15f);
                visual.localRotation = Quaternion.Euler(85, 0, 0);
            }
            AddAction(pickup.gameObject, "拾取" + pickup.Label, () => PickUp(pickup));
        }

        public void SpawnBackpack(SandboxBackpack backpack, Vector3 position)
        {
            if (backpack == null) throw new ArgumentNullException(nameof(backpack));
            SandboxPickup pickup = NewPickup(position);
            pickup.Backpack = backpack;
            SandboxWorld.BackpackVisual(pickup.transform);
            AddAction(pickup.gameObject, "穿戴" + pickup.Label, () => PickUp(pickup));
        }

        private static SandboxPickup NewPickup(Vector3 position)
        {
            GameObject root = new GameObject("GroundPickup");
            root.transform.position = position + Vector3.up * 0.04f;
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.center = Vector3.up * 0.35f;
            trigger.size = new Vector3(0.9f, 0.8f, 0.9f);
            trigger.isTrigger = true;
            return root.AddComponent<SandboxPickup>();
        }

        private void PickUp(SandboxPickup pickup)
        {
            if (pickup == null || !pickup.gameObject.activeInHierarchy)
            {
                Caption("该物品已经不在地面上。", 2);
                return;
            }
            if (pickup.Backpack != null)
            {
                if (!Equipment.EquipBackpack(pickup.Backpack))
                {
                    Caption("已经穿着背包；先按 B 丢下当前背包，包内物品会随包留在地上。", 4);
                    return;
                }
            }
            else
            {
                SandboxItem item = pickup.Item.Value;
                if (SandboxItems.IsWeapon(item))
                {
                    if (!Equipment.EquipWeapon(item))
                    {
                        Caption("手中已有武器：按 G 丢下，或按 I 收入背包，再拾取新武器。", 4);
                        return;
                    }
                }
                else if (Equipment.Backpack == null || !Equipment.Backpack.TryAdd(item))
                {
                    Caption(Equipment.Backpack == null ? "需要先穿戴背包才能携带补给。" : "背包已满，未拾取。", 3);
                    return;
                }
            }
            Caption("已拾取" + pickup.Label + "。", 3);
            interactions.Remove(pickup.GetComponent<Interactable>());
            if (Nearest != null && Nearest.gameObject == pickup.gameObject) Nearest = null;
            pickup.gameObject.SetActive(false);
            Destroy(pickup.gameObject);
            RefreshEquipment();
        }

        private Vector3 DropPosition()
        {
            Vector3 start = Player.transform.position + Vector3.up;
            Vector3 offset = Player.Model.forward * 1.3f;
            if (Physics.SphereCast(start, 0.35f, offset.normalized, out RaycastHit wall,
                offset.magnitude, ~(1 << 8), QueryTriggerInteraction.Ignore))
                offset = offset.normalized * Mathf.Max(0, wall.distance - 0.4f);
            Vector3 point = start + offset;
            if (!Physics.Raycast(point, Vector3.down, out RaycastHit ground, 6, ~(1 << 8), QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No ground found for dropped equipment.");
            return ground.point;
        }

        public void DropWeapon()
        {
            if (!Equipment.Weapon.HasValue) { Caption("手中没有武器。", 2); return; }
            Vector3 point = DropPosition();
            SandboxItem item = Equipment.DropWeapon().Value;
            SpawnItem(item, point);
            RefreshEquipment();
            Caption("已丢下" + SandboxItems.Name(item) + "，可以重新拾取。", 3);
        }

        public void DropBackpack()
        {
            if (Equipment.Backpack == null) { Caption("当前没有穿戴背包。", 2); return; }
            Vector3 point = DropPosition();
            SandboxBackpack backpack = Equipment.DropBackpack();
            SpawnBackpack(backpack, point);
            RefreshEquipment();
            Caption("已丢下背包；" + backpack.Contents.Count + " 件物品仍在包里。", 4);
        }

        public void StoreWeapon()
        {
            Caption(Equipment.StoreWeapon() ? "武器已收入背包。" : "无法收纳：需要手持武器、穿戴背包且包未满。", 3);
            RefreshEquipment();
        }

        public void UseSlot(int index)
        {
            SandboxBackpack bag = Equipment.Backpack;
            if (bag == null) { Caption("没有穿戴背包。", 2); return; }
            if (SandboxItems.IsWeapon(bag.Contents[index]))
            {
                Caption(Equipment.DrawWeapon(index) ? "武器已拿到手中。" : "请先丢下或收起手中武器。", 3);
                RefreshEquipment();
            }
            else
            {
                bag.RemoveAt(index);
                Player.Rest();
                Caption("使用一份补给：恢复精力与体力，生命至少恢复到 80；不改变记忆。", 4);
            }
        }

        public void DropSlot(int index)
        {
            if (Equipment.Backpack == null) { Caption("没有穿戴背包。", 2); return; }
            Vector3 point = DropPosition();
            SpawnItem(Equipment.Backpack.RemoveAt(index), point);
            Caption("物品已放到地上。", 2);
        }

        private void RefreshEquipment()
        {
            if (heldVisual != null) Destroy(heldVisual.gameObject);
            if (bagVisual != null) Destroy(bagVisual.gameObject);
            heldVisual = null;
            bagVisual = null;
            if (Equipment.Weapon.HasValue)
            {
                heldVisual = SandboxWorld.ItemVisual(Equipment.Weapon.Value, Player.Model);
                heldVisual.localPosition = new Vector3(0.43f, 0.78f, 0.08f);
                heldVisual.localRotation = Quaternion.Euler(-20, 0, -12);
            }
            if (Equipment.Backpack != null)
            {
                bagVisual = SandboxWorld.BackpackVisual(Player.Model);
                bagVisual.localPosition = new Vector3(0, 0.7f, -0.37f);
            }
        }

        private SandboxTarget FindTarget(float range, bool enemyOnly)
        {
            SandboxTarget nearest = null;
            float best = range;
            foreach (SandboxTarget target in Targets)
            {
                if (!target.CanBeAttacked || target.Health.IsDown || (enemyOnly && !target.IsEnemy)) continue;
                Vector3 delta = target.transform.position - Player.transform.position;
                delta.y = 0;
                float distance = delta.magnitude;
                if (distance > best || (distance > 0.01f && Vector3.Dot(Player.Model.forward, delta / distance) < 0.35f))
                    continue;
                if (Physics.Linecast(Player.transform.position + Vector3.up,
                    target.transform.position + Vector3.up, out RaycastHit hit, ~(1 << 8), QueryTriggerInteraction.Ignore)
                    && hit.transform != target.transform && !hit.transform.IsChildOf(target.transform)) continue;
                nearest = target;
                best = distance;
            }
            return nearest;
        }

        private void Attack()
        {
            if (Time.time < nextAttack) return;
            if (!Equipment.Weapon.HasValue) { Caption("先拾取斧头或木棍；F 可推开异常者。", 3); return; }
            SandboxItem weapon = Equipment.Weapon.Value;
            if (!Player.SpendStamina(SandboxItems.StaminaCost(weapon))) { Caption("体力不足，暂时无法挥击。", 2); return; }
            nextAttack = Time.time + SandboxItems.Cooldown(weapon);
            swingUntil = Time.time + 0.35f;
            SandboxTarget target = FindTarget(2.4f, false);
            if (target == null) { Caption("挥击未命中：朝向目标并靠近至 2.4 米内，不能隔墙攻击。", 2); return; }
            target.Hit(SandboxItems.Damage(weapon));
            Caption(target.Health.IsDown ? target.Label + "已击倒；可在菜单或控制台重置。"
                : target.Label + "受到 " + SandboxItems.Damage(weapon) + " 点伤害，剩余 " + target.Health.Value + "。", 3);
        }

        public bool PushThreat()
        {
            SandboxTarget target = FindTarget(3, true);
            if (target == null) { Caption("面前 3 米内没有可推开的异常者。", 2); return false; }
            Vector3 direction = target.transform.position - Player.transform.position;
            direction.y = 0;
            target.Push(direction.sqrMagnitude > 0.01f ? direction.normalized : Player.Model.forward);
            Caption("推开了异常者，争取到短暂脱身时间。", 2);
            return true;
        }

        public void ToggleCompanion(ActorMotor actor)
        {
            Memories.Recognize("player", actor.Identity);
            Memories.Recognize(actor.Identity, "player");
            if (actor.FollowTarget != null) actor.Stop();
            else
            {
                actor.FollowTarget = Player.transform;
                actor.FollowOffset = new Vector3((Companions.IndexOf(actor) - 1) * 1.5f, 0, -2.5f);
            }
            Caption(actor.DisplayName + (actor.FollowTarget == null ? "在此等候。" : "开始跟随。")
                + "双方辨认后，定向记忆已恢复。", 4);
        }

        public void Rest()
        {
            Player.Rest();
            foreach (string observer in Participants) Memories.DecayObserver(observer, 0.08f);
            SetNight(false);
            Caption("休息到清晨：状态恢复，每条已有记忆减少 0.08；睡眠不会冻结记忆，也不强制清空关系。", 5);
        }

        public void SetNight(bool night)
        {
            IsNight = night;
            Sun.intensity = night ? 0.18f : 1.1f;
            Sun.color = night ? new Color(0.55f, 0.65f, 0.88f) : new Color(1, 0.91f, 0.78f);
            RenderSettings.ambientLight = night ? new Color(0.19f, 0.22f, 0.29f) : new Color(0.57f, 0.6f, 0.56f);
            RenderSettings.fogColor = night ? new Color(0.12f, 0.16f, 0.22f) : new Color(0.62f, 0.69f, 0.7f);
            View.backgroundColor = RenderSettings.fogColor;
            CampLight.enabled = night;
            Caption(night ? "已切换为夜间。" : "已切换为白天。", 2);
        }

        public void ToggleEnemies()
        {
            EnemiesEnabled = !EnemiesEnabled;
            if (!EnemiesEnabled)
                foreach (SandboxTarget target in Targets)
                    if (target.IsEnemy) target.GetComponent<ActorMotor>().Stop();
            Caption(EnemiesEnabled ? "敌人攻击已开启：14 米内追击，过远会回到训练区。" : "敌人攻击已关闭。", 4);
        }

        public void ResetTargets()
        {
            foreach (SandboxTarget target in Targets)
                if (target != Observer.Target) target.ResetTarget();
            Observer.ResetObservation();
            Caption("木桩和异常者已复位，生命恢复至 100；你的装备与背包未改变。", 4);
        }

        public void ReturnToCamp()
        {
            EnemiesEnabled = false;
            ResetTargets();
            Player.Respawn(new Vector3(0, 0.1f, -4));
            for (int i = 0; i < Companions.Count; i++)
            {
                ActorMotor actor = Companions[i];
                CharacterController controller = actor.GetComponent<CharacterController>();
                controller.enabled = false;
                actor.transform.position = new Vector3(-3 + i * 3, 0.1f, -7);
                controller.enabled = true;
                actor.Stop();
            }
            IsFailed = paused = InventoryOpen = false;
            ObservationOpen = false;
            UpdatePause();
            Caption("已回到营地并恢复状态；敌人关闭，同伴召回；手中装备与包内物品保留。", 5);
        }

        public void Caption(string text, float duration)
        {
            caption = text;
            captionUntil = Time.unscaledTime + duration;
        }

        public void Fail()
        {
            IsFailed = true;
            InventoryOpen = false;
            ObservationOpen = false;
            UpdatePause();
        }

        public void Resume()
        {
            paused = InventoryOpen = false;
            UpdatePause();
        }

        public void ToggleObservation()
        {
            ObservationOpen = !ObservationOpen;
            UpdatePause();
        }

        private void UpdatePause()
        {
            bool blocked = IsPaused || IsFailed;
            Time.timeScale = blocked ? 0 : 1;
            Cursor.lockState = blocked || ObservationOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = blocked || ObservationOpen;
        }

        public void LoadScene(string name)
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(name);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            OpeningWorld.ReleaseMaterials();
            if (ReferenceEquals(PlayerWorld.Current, this)) PlayerWorld.Current = null;
            if (Instance == this) Instance = null;
        }
    }
}
