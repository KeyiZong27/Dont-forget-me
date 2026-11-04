using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontForgetMe
{
    public sealed class DemoGame : MonoBehaviour, IPlayerWorld
    {
        public static DemoGame Instance { get; private set; }
        public OpeningState State { get; private set; }
        public JourneyState Journey { get; private set; }
        public MemoryNetwork Memories { get; private set; }
        public ThirdPersonPlayer Player { get; set; }
        public Camera View { get; set; }
        public ActorMotor Woman { get; set; }
        public ActorMotor Dog { get; set; }
        public ActorMotor Zhou { get; set; }
        public ActorMotor ResidentA { get; set; }
        public ActorMotor ResidentB { get; set; }
        public GameObject Gate { get; set; }
        public GameObject Water { get; set; }
        public GameObject NorthWall { get; set; }
        public GameObject NorthGate { get; set; }
        public GameObject ShelterDoor { get; set; }
        public GameObject ShelterExit { get; set; }
        public Light Sun { get; set; }
        public Light StreetLamp { get; set; }
        public Light ShelterLamp { get; set; }
        public bool IsPaused => !started || paused;
        public bool IsFailed { get; private set; }
        public bool IsComplete => Journey.Phase == JourneyPhase.Complete;
        public bool PathSceneStarted => pathSceneStarted;
        public bool DoorSlammed => doorSlammed;
        public bool FarewellStarted => farewellStarted;
        public Interactable Nearest { get; private set; }
        public string CurrentCaption { get; private set; }
        public bool IsThreatActive => threat != null && threat.enabled && threat.gameObject.activeInHierarchy && !gateClosed;
        public bool IsThreatWarning => IsThreatActive && Time.time < threatMovesAt;
        public float ThreatApproachSeconds => Mathf.Max(0, threatMovesAt - Time.time);
        public float ThreatDistance => IsThreatActive
            ? Vector3.Distance(threat.transform.position, Player.transform.position)
            : float.PositiveInfinity;
        public float ThreatEnergyDrainPerSecond => IsThreatActive
            ? 0.12f * Mathf.InverseLerp(8f, 1.35f, ThreatDistance)
            : 0f;
        public bool CanPushThreat => IsThreatActive && ThreatDistance <= 3f && Player.Stamina >= 12f;

        private readonly List<Interactable> interactions = new List<Interactable>();
        private bool started;
        private bool paused;
        private float captionUntil;
        private float threatStunUntil;
        private float threatMovesAt;
        private float nextBark;
        private ActorMotor threat;
        private AudioSource sound;
        private AudioClip ambient;
        private AudioClip bark;
        private AudioClip warning;
        private AudioClip slam;
        private bool gateClosed;
        private bool pathSceneStarted;
        private bool doorSlammed;
        private float doorSlamAt;
        private bool farewellStarted;
        private int farewellStage;
        private float farewellStageAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBootstrap()
        {
            SceneManager.sceneLoaded -= Bootstrap;
            SceneManager.sceneLoaded += Bootstrap;
        }

        private static void Bootstrap(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Sandbox")
            {
                if (SandboxGame.Instance == null)
                    new GameObject("SandboxWorld").AddComponent<SandboxGame>();
            }
            else if (scene.name == "Opening" && Instance == null)
                new GameObject("OpeningDemo").AddComponent<DemoGame>();
        }

        private void Awake()
        {
            Instance = this;
            PlayerWorld.Current = this;
            State = new OpeningState();
            Journey = new JourneyState();
            Memories = new MemoryNetwork();
            Physics.IgnoreLayerCollision(8, 8, true);
            OpeningWorld.Build(this);
            foreach (string observer in new[] { "player", "woman", "dog" })
                foreach (string subject in new[] { "player", "woman", "dog" })
                    if (observer != subject) Memories.Recognize(observer, subject);
            gameObject.AddComponent<DemoHud>();
            ConfigureSound();
            SetCursor(false);
        }

        public void Begin()
        {
            started = true;
            SetCursor(true);
            Caption("她：碗放稳一点，它怕那个响。我们等会儿从修理铺旁边过去。", 7);
        }

        public void TogglePause()
        {
            if (!started || IsFailed || IsComplete) return;
            paused = !paused;
            Time.timeScale = paused ? 0 : 1;
            SetCursor(!paused);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (IsPaused || IsFailed || IsComplete) return;
            UpdateInteraction();
            if (Input.GetKeyDown(KeyCode.E) && Nearest != null)
                Nearest.OnInteract?.Invoke();
            if (State.Phase == OpeningPhase.Alley && Player.transform.position.z > 29)
                EnterAlley();
            if (State.Phase == OpeningPhase.Reunion && Time.time > nextBark)
            {
                nextBark = Time.time + 4f;
                sound.PlayOneShot(bark, 0.45f);
            }
            if (State.Phase == OpeningPhase.Repair)
            {
                State.TickRepair(Time.deltaTime);
                UpdateThreat();
                if (State.Phase == OpeningPhase.Regroup) OpenGate();
            }
            else if (State.Phase == OpeningPhase.Regroup)
            {
                UpdateThreat();
            }
            if (State.Phase == OpeningPhase.Complete) UpdateJourney();
        }

        private void UpdateInteraction()
        {
            Nearest = null;
            float best = float.MaxValue;
            foreach (Interactable item in interactions)
            {
                if (item == null || !item.Available) continue;
                Vector3 target = item.transform.position;
                Vector3 horizontal = target - Player.transform.position;
                horizontal.y = 0;
                float distance = horizontal.magnitude;
                if (distance > item.Range || distance >= best) continue;
                Vector3 start = Player.transform.position + Vector3.up;
                Vector3 end = new Vector3(target.x, Mathf.Max(0.7f, target.y), target.z);
                if (Physics.Linecast(start, end, out RaycastHit hit, ~(1 << 8), QueryTriggerInteraction.Ignore)
                    && hit.transform != item.transform && !hit.transform.IsChildOf(item.transform))
                    continue;
                Nearest = item;
                best = distance;
            }
        }

        public void AddInteraction(GameObject owner, string prompt, Func<bool> available, Action action)
        {
            Interactable item = owner.AddComponent<Interactable>();
            item.Prompt = prompt;
            item.IsAvailable = available;
            item.OnInteract = action;
            interactions.Add(item);
        }

        public void GiveWater()
        {
            if (!State.GiveWater()) return;
            Water.SetActive(true);
            Dog.GoTo(new Vector3(2.6f, 0, 6));
            Memories.Recognize("player", "dog");
            Memories.Recognize("dog", "player");
            Caption("狗先闻了闻碗，才低下头。她：这样就好，别催它。", 6);
        }

        public void MeetZhou()
        {
            if (!State.MeetZhou()) return;
            foreach (string id in new[] { "player", "woman", "dog" })
            {
                Memories.Recognize(id, "zhou");
                Memories.Recognize("zhou", id);
            }
            Caption("老周：叫我老周就行。这椅子响得烦……等等，前面怎么了？\n她：我们在路口！先别往人群里走！", 10);
            sound.PlayOneShot(warning, 0.55f);
            Woman.GoTo(new Vector3(5, 0, 22));
            Dog.FollowTarget = Woman.transform;
            Dog.FollowOffset = new Vector3(-0.9f, 0, -0.5f);
            Zhou.GoVia(new Vector3(-3, 0, 13), new Vector3(-1.6f, 0, 26));
            ResidentA.GoTo(new Vector3(-4, 0, 24));
            ResidentB.GoTo(new Vector3(3.2f, 0, 24));
            nextBark = Time.time + 2;
        }

        public void Reunite()
        {
            if (Vector3.Distance(Dog.transform.position, Woman.transform.position) > 3.5f)
            {
                Caption("她：等等，它还在过来。", 3);
                return;
            }
            if (!State.Reunite()) return;
            Memories.Recognize("player", "woman");
            Memories.Recognize("woman", "player");
            Woman.FollowTarget = Player.transform;
            Woman.FollowOffset = new Vector3(0.85f, 0, -1.2f);
            Caption("她：有没有受伤？先离开这儿。\n老周：别挤前面，后头能走。跟着我。", 8);
            Zhou.GoVia(new Vector3(0, 0, 29.5f), new Vector3(0, 0, 39));
            ResidentA.GoVia(new Vector3(0, 0, 29.5f), new Vector3(-0.8f, 0, 35));
            ResidentB.GoVia(new Vector3(0, 0, 29.5f), new Vector3(0.8f, 0, 33));
        }

        private void EnterAlley()
        {
            if (!State.EnterAlley()) return;
            Woman.GoVia(new Vector3(0, 0, 29.5f), new Vector3(1.7f, 0, 39));
            Dog.FollowTarget = Woman.transform;
            Dog.FollowOffset = new Vector3(-0.6f, 0, -0.8f);
            Zhou.GoTo(new Vector3(-0.8f, 0, 46));
            ResidentA.GoTo(new Vector3(0.6f, 0, 46.5f));
            ResidentB.GoTo(new Vector3(-0.7f, 0, 42));
            Caption("老周：门卡住了。把下面的东西挪开，我看看轨道。\n她：手往外一点，别夹住。", 8);
        }

        public void MoveDebris(GameObject debris, int index)
        {
            if (!State.ClearDebris("crate-" + index)) return;
            debris.transform.position = new Vector3(index == 0 ? -2.35f : 2.35f, 0.5f, 44);
            Interactable item = debris.GetComponent<Interactable>();
            item.enabled = false;
            Caption(State.ClearedDebrisCount == 2
                ? "路腾出来了。靠近老周，按 E 帮他开始修门。"
                : "她扶稳了另一侧。还有一件杂物挡着门。", 5);
        }

        public void StartRepair()
        {
            if (Vector3.Distance(Zhou.transform.position, new Vector3(-0.8f, 0, 46)) > 1)
            {
                Caption("等老周走到轨道旁，先不要挤在门口。", 3);
                return;
            }
            if (!State.StartRepair()) return;
            threatMovesAt = Time.time + 2.5f;
            Caption("入口处出现了异常者，先转身确认位置。它会在几秒后靠近；按 F 推开（需 12 体力），或按住左 Shift 跑开。生命归零会失败。老周需要一点时间。", 10);
            threat = OpeningWorld.Actor("unknown", "行为异常的人", new Vector3(0, 0.1f, 31),
                new Color(0.72f, 0.18f, 0.12f));
            threat.Speed = UnityEngine.Random.Range(1.65f, 1.95f);
            GameObject marker = OpeningWorld.Shape(PrimitiveType.Sphere, "ThreatSignal",
                new Vector3(0, 2.75f, 0), Vector3.one * 0.36f, new Color(1f, 0.16f, 0.04f),
                threat.transform);
            Light signal = marker.AddComponent<Light>();
            signal.color = new Color(1f, 0.16f, 0.04f);
            signal.range = 5f;
            signal.intensity = 1.5f;
            Woman.GoTo(new Vector3(1.5f, 0, 43));
            Dog.GoTo(new Vector3(1.4f, 0, 46));
        }

        private void UpdateThreat()
        {
            if (threat == null || gateClosed) return;
            if (Time.time < threatMovesAt)
            {
                threat.Stop();
                return;
            }
            if (Time.time < threatStunUntil)
            {
                threat.Stop();
                return;
            }
            threat.GoTo(Player.transform.position);
            float distance = Vector3.Distance(threat.transform.position, Player.transform.position);
            if (distance < 1.35f) Player.TakeHit();
            if (State.Phase == OpeningPhase.Regroup && EveryonePresent() && threat.transform.position.z < 47.7f)
            {
                gateClosed = true;
                Gate.transform.position = new Vector3(0, 1.35f, 48);
                threat.Stop();
                threat.enabled = false;
                Caption("铁门在身后合上。先看看大家有没有跟上。", 5);
            }
        }

        public bool PushThreat()
        {
            if (threat == null || !threat.enabled
                || Vector3.Distance(threat.transform.position, Player.transform.position) > 3f) return false;
            Vector3 away = threat.transform.position - Player.transform.position;
            away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            threat.KnockBack(away.normalized * 1.7f);
            threatStunUntil = Time.time + 2.2f;
            Caption("他踉跄着退开。趁现在拉开距离。", 2);
            return true;
        }

        private void OpenGate()
        {
            Gate.transform.position = new Vector3(0, 4.5f, 48);
            SendThroughGate(Zhou, new Vector3(-1.6f, 0, 57));
            SendThroughGate(Woman, new Vector3(1.4f, 0, 55));
            SendThroughGate(Dog, new Vector3(0.5f, 0, 56));
            SendThroughGate(ResidentA, new Vector3(-2.2f, 0, 54));
            SendThroughGate(ResidentB, new Vector3(2.1f, 0, 54));
            Caption("老周：开了！都过去！\n跟着他们穿过铁门，到另一侧确认每个人都在。", 7);
        }

        private static void SendThroughGate(ActorMotor actor, Vector3 destination)
        {
            actor.GoVia(new Vector3(0, 0, 46.5f), new Vector3(0, 0, 50.5f), destination);
        }

        public bool EveryonePresent()
        {
            return Woman.transform.position.z > 50 && Dog.transform.position.z > 50
                && Zhou.transform.position.z > 50 && ResidentA.transform.position.z > 50
                && ResidentB.transform.position.z > 50 && Player.transform.position.z > 50;
        }

        public void Finish()
        {
            if (State.Phase == OpeningPhase.Regroup && EveryonePresent() && !gateClosed)
            {
                Caption("她：他也跟进来了！绕到他后面，把他推回门外，再拉开距离。", 5);
                return;
            }
            if (!State.Finish(EveryonePresent()))
            {
                Caption("还有人在过门，等一下他们。", 3);
                return;
            }
            Gate.transform.position = new Vector3(0, 1.35f, 48);
            if (threat != null) threat.gameObject.SetActive(false);
            Woman.Stop();
            Dog.Stop();
            Zhou.Stop();
            ResidentA.Stop();
            ResidentB.Stop();
            RoadWorld.Extend(this);
            Caption("街坊：刚才修理铺门口，是不是还有个人？——没人答得上来。他们道了谢，往亲属那边去了。\n留下的年轻人叫小谭，公司的学徒。老周掏出手机，看了看信号。", 12);
        }

        // —— 第二章：两通电话、窄路、服务区过夜与分路（大纲 05—08）。 ——

        private void UpdateJourney()
        {
            if (Journey.Phase == JourneyPhase.Calls && Journey.BothCallsDone
                && Player.transform.position.z > 73.5f && Journey.Depart())
                Depart();
            if (Journey.Phase == JourneyPhase.Path && !pathSceneStarted
                && Player.transform.position.z > 79f)
                StartPathScene();
            if (Journey.Phase == JourneyPhase.Path && Journey.GlassRemoved
                && Player.transform.position.z > 99f && Journey.EnterShelter())
                EnterShelter();
            if (Journey.Phase == JourneyPhase.Night && !doorSlammed && Time.time >= doorSlamAt)
                SlamDoor();
            if (farewellStarted && Journey.Phase == JourneyPhase.Morning)
                UpdateFarewell();
        }

        public void CallFamily()
        {
            if (!Journey.CallFamily()) return;
            Memories.Recognize("zhou", "wife");
            Memories.Recognize("wife", "zhou");
            Memories.Recognize("zhou", "xiaoman");
            Memories.Recognize("xiaoman", "zhou");
            Caption("赵秀兰：我们先去柳河，那边说还有车能接。\n老周：到中转站等，别再换地方。真改了，就留信。\n小满：你带充电线，别只带那个杯子。——你把备用线递给老周。", 12);
            UnlockNorthGate();
        }

        public void CallMother()
        {
            if (!Journey.CallMother()) return;
            Memories.Recognize("woman", "mother");
            Memories.Recognize("mother", "woman");
            Caption("林岚：我还在北街临时诊疗点。别往家跑，家里没人。\n林禾：你别一个人转走，我去找你。\n林岚：来之前先问路，别为了快往封住的地方钻。", 12);
            UnlockNorthGate();
        }

        private void UnlockNorthGate()
        {
            if (!Journey.BothCallsDone || NorthGate == null) return;
            NorthGate.transform.position = new Vector3(0, 4.5f, 72);
        }

        private void Depart()
        {
            ResidentB.gameObject.SetActive(false);
            Zhou.GoVia(new Vector3(0, 0, 74), new Vector3(-0.5f, 0, 83));
            Caption("老周走在前面。窄路两侧是河堤的矮墙，雨点开始落下来。\n老周的中转站和北街诊疗点，在服务区之前是同一个方向。", 7);
        }

        private void StartPathScene()
        {
            pathSceneStarted = true;
            Dog.FollowTarget = null;
            Dog.GoTo(new Vector3(0.4f, 0, 84));
            Zhou.GoTo(new Vector3(-0.9f, 0, 85.2f));
            sound.PlayOneShot(bark, 0.4f);
            Caption("豆子停下来，抬起一只前爪。\n老周：先别踩，这儿有碎玻璃。\n林禾：走慢一点吧。", 8);
        }

        public void CalmDog()
        {
            if (!pathSceneStarted || !Journey.CalmDog()) return;
            Memories.Recognize("player", "dog");
            Memories.Recognize("dog", "player");
            Caption("你蹲下来，让豆子先闻了闻手。它的耳朵慢慢放了下来。\n林禾把灯凑近，照着它的爪子。没有倒计时，不急。", 8);
        }

        public void RemoveGlass()
        {
            if (!Journey.RemoveGlass()) return;
            Dog.Speed = 2.3f;
            Dog.FollowTarget = Woman.transform;
            Dog.FollowOffset = new Vector3(-0.7f, 0, -0.7f);
            Zhou.GoVia(new Vector3(0, 0, 96.5f), new Vector3(-4.6f, 0, 104.8f));
            Caption("碎玻璃挑出来了。老周：不赶这几步。\n豆子把爪子收回去，走得慢了一点。跟着老周进前面的服务区附属房。", 8);
        }

        private void EnterShelter()
        {
            ResidentA.FollowTarget = null;
            ResidentA.GoVia(new Vector3(0, 0, 99), new Vector3(4.7f, 0, 103));
            Caption("雨下大了，打在铁皮棚顶上。\n老周把包放到桌边，又站到墙上的路线图前，算剩下的步程。\n小谭在东墙发现一张告示。", 9);
        }

        public void PersuadeZhou()
        {
            if (!Journey.PersuadeZhou()) return;
            Zhou.GoTo(new Vector3(1.8f, 0, 104.9f));
            Caption("老周：我再往前走一点。\n林禾：雨已经这么大了。\n许川：你答应她别硬走。——老周看了他一会儿，把包放下：天亮走。", 12);
        }

        public void ShareWater()
        {
            if (!Journey.ShareWater()) return;
            Caption("饮水机没电。你把自己的水递过去。\n老周只喝了一小口就停下：够不够分？——你点了点头。", 8);
        }

        public void RestForNight()
        {
            if (!Journey.Rest()) return;
            SetNight(true);
            doorSlamAt = Time.time + 5f;
            Woman.FollowTarget = null;
            Woman.GoTo(new Vector3(-4.5f, 0, 100.6f));
            Zhou.GoTo(new Vector3(-4.5f, 0, 103.4f));
            ResidentA.GoTo(new Vector3(-4.5f, 0, 99.2f));
            Dog.FollowTarget = null;
            Dog.GoTo(new Vector3(-3.2f, 0, 102));
            Caption("大家分头躺下，轮换着看门。你靠着墙坐下来，雨声变得很近。", 6);
        }

        private void SlamDoor()
        {
            doorSlammed = true;
            ShelterDoor.transform.position = new Vector3(0, 1.25f, 98);
            sound.PlayOneShot(slam, 0.9f);
            sound.PlayOneShot(bark, 0.5f);
            Dog.GoTo(new Vector3(-1.8f, 0, 103.5f));
            Zhou.GoTo(new Vector3(-0.8f, 0, 99));
            Caption("门被风带上，撞得一响。豆子一下站了起来。\n老周已经摸到门边：我来弄。你搭把手。", 8);
        }

        public void FixDoor()
        {
            if (!doorSlammed || !Journey.FixDoor()) return;
            sound.PlayOneShot(slam, 0.15f);
            Zhou.GoTo(new Vector3(-4.5f, 0, 103.4f));
            Dog.GoTo(new Vector3(-3.2f, 0, 102));
            Caption("你扶住门，老周往闭门器里塞了块垫片。再推，门只剩很轻的一声。\n豆子看了一会儿，转了个圈又卧下。回铺位睡到天亮。", 9);
        }

        public void SleepUntilMorning()
        {
            if (!Journey.Sleep()) return;
            SetNight(false);
            Player.Rest();
            // 睡眠恢复精力，但不暂停遗忘（规则 2.3）；少量衰减不会断开任何联系。
            foreach (string observer in new[] { "player", "woman", "zhou", "dog", "xiaotan" })
                Memories.DecayObserver(observer, 0.08f);
            ShelterExit.transform.position = new Vector3(0, 4.5f, 112);
            Zhou.GoTo(new Vector3(-4.8f, 0, 105));
            Woman.GoTo(new Vector3(1.4f, 0, 107.1f));
            Dog.FollowTarget = Woman.transform;
            Dog.FollowOffset = new Vector3(-0.7f, 0, -0.7f);
            ResidentA.GoTo(new Vector3(0.6f, 0, 108));
            Caption("你睡着了，天亮前雨停了。精力恢复了；睡眠不暂停遗忘，但你们仍然记得彼此。\n老周又站回路线图前。北面的门开了。", 9);
        }

        public void CopyAddress()
        {
            if (!Journey.CopyAddress()) return;
            Caption("林禾把柳河中转站的地址抄进纸质路线册，又存了一张手机照片。\n许川：到了留句话。\n老周：你们接上人，能走就来。路断了别硬来。", 11);
        }

        public void BeginFarewell()
        {
            if (farewellStarted || Journey.Phase != JourneyPhase.Morning || !Journey.AddressCopied) return;
            farewellStarted = true;
            farewellStage = 0;
            farewellStageAt = Time.time + 4f;
            Zhou.GoVia(new Vector3(0, 0, 110.5f), new Vector3(0, 0, 114.5f));
            Dog.FollowTarget = Zhou.transform;
            Dog.FollowOffset = new Vector3(0.8f, 0, -0.8f);
            Caption("林禾：你也一样。\n老周背起包，往北边的步行道走。豆子跟了上去。", 5);
        }

        private void UpdateFarewell()
        {
            if (Time.time < farewellStageAt) return;
            if (farewellStage == 0)
            {
                farewellStage = 1;
                farewellStageAt = Time.time + 4f;
                Zhou.Stop();
                Dog.FollowTarget = null;
                Dog.GoTo(Zhou.transform.position + new Vector3(0.7f, 0, -0.5f));
                Caption("豆子跟出几步，又回头看你们。\n老周蹲下来摸了摸它，指向你们：跟他们。等脚好了再跑。", 5);
            }
            else if (farewellStage == 1)
            {
                farewellStage = 2;
                farewellStageAt = Time.time + 4.5f;
                Dog.FollowTarget = Woman.transform;
                Dog.FollowOffset = new Vector3(-0.8f, 0, -0.6f);
                Zhou.GoVia(new Vector3(2, 0, 117), new Vector3(7.5f, 0, 121));
                Caption("他没有再回头。\n分开以后，他仍带着和你们的联系；没有人因为离队被扣空记忆。", 7);
            }
            else if (Journey.Farewell())
            {
                Zhou.Stop();
                SetCursor(false);
            }
        }

        private void SetNight(bool night)
        {
            if (Sun != null)
            {
                Sun.intensity = night ? 0.25f : 1.15f;
                Sun.color = night ? new Color(0.62f, 0.68f, 0.84f) : new Color(1f, 0.9f, 0.76f);
            }
            if (StreetLamp != null) StreetLamp.enabled = night;
            if (ShelterLamp != null) ShelterLamp.enabled = night;
            RenderSettings.ambientLight = night
                ? new Color(0.19f, 0.21f, 0.27f) : new Color(0.56f, 0.55f, 0.5f);
            RenderSettings.fogColor = night
                ? new Color(0.14f, 0.16f, 0.2f) : new Color(0.6f, 0.6f, 0.55f);
            if (View != null) View.backgroundColor = RenderSettings.fogColor;
        }

        public void Caption(string text, float duration)
        {
            CurrentCaption = text;
            captionUntil = Time.time + duration;
        }

        public string VisibleCaption => Time.time <= captionUntil ? CurrentCaption : "";

        public void Fail()
        {
            IsFailed = true;
            if (threat != null) threat.Stop();
            SetCursor(false);
        }

        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene("Opening");
        }

        public void Quit()
        {
            Application.Quit();
        }

        public string Objective
        {
            get
            {
                if (State.Phase == OpeningPhase.Complete) return JourneyObjective;
                switch (State.Phase)
                {
                    case OpeningPhase.Street: return "经过街边修理铺，和修椅子的人说说话。\n也可以先到水碗旁照顾狗。";
                    case OpeningPhase.Reunion: return "到路口找到她和狗，靠近后按 E 确认平安。";
                    case OpeningPhase.Alley: return "跟随老周，进入街道尽头的后巷。";
                    case OpeningPhase.Debris: return State.ClearedDebrisCount == 2
                        ? "到铁门旁与老周合作，开始修门。"
                        : "搬开铁门前的两件杂物。";
                    case OpeningPhase.Repair: return "撑到铁门修好。不要求击杀；F 推开靠近的威胁。";
                    default: return "穿过铁门，到老周身边确认大家都在。";
                }
            }
        }

        private string JourneyObjective
        {
            get
            {
                switch (Journey.Phase)
                {
                    case JourneyPhase.Calls:
                        if (Journey.BothCallsDone) return "北面的闸板升起来了。穿过缺口，沿窄路往服务区走。";
                        if (Journey.CalledFamily) return "再陪她给母亲打一通电话。";
                        if (Journey.CalledMother) return "再陪老周给家里打一通电话。";
                        return "空地暂时安静。分别陪老周和她打完两通电话。";
                    case JourneyPhase.Path:
                        if (!pathSceneStarted) return "跟着老周，沿窄路往服务区方向走。";
                        if (!Journey.DogCalmed) return "豆子抬起了前爪。蹲下来，先让它闻闻你的手。";
                        if (!Journey.GlassRemoved) return "把它爪垫里的碎玻璃挑出来。没有倒计时。";
                        return "跟着老周，进入前面的服务区附属房。";
                    case JourneyPhase.Shelter:
                        return Journey.ZhouPersuaded
                            ? "到西侧铺位旁，招呼大家休息。"
                            : "老周想连夜赶路。劝他留下来。";
                    case JourneyPhase.Night:
                        if (!doorSlammed) return "夜里轮换看门。先休息。";
                        return Journey.DoorFixed
                            ? "回铺位睡到天亮。"
                            : "门被风撞响了。和老周一起调整闭门器。";
                    case JourneyPhase.Morning:
                        return Journey.AddressCopied ? "和老周道别。" : "到桌边，把柳河中转站的地址抄进路线册。";
                    default:
                        return "分路不是散伙。到了柳河，留句话。";
                }
            }
        }

        public Vector3 ObjectivePosition
        {
            get
            {
                if (State.Phase == OpeningPhase.Complete) return JourneyObjectivePosition;
                switch (State.Phase)
                {
                    case OpeningPhase.Street: return Zhou.transform.position;
                    case OpeningPhase.Reunion: return Woman.transform.position;
                    case OpeningPhase.Alley: return new Vector3(0, 0, 31);
                    case OpeningPhase.Debris: return State.ClearedDebrisCount == 2
                        ? Zhou.transform.position : new Vector3(0, 0, 44);
                    case OpeningPhase.Repair: return Gate.transform.position;
                    default: return Zhou.transform.position;
                }
            }
        }

        private Vector3 JourneyObjectivePosition
        {
            get
            {
                switch (Journey.Phase)
                {
                    case JourneyPhase.Calls:
                        if (Journey.BothCallsDone) return new Vector3(0, 0, 72);
                        return Journey.CalledFamily ? Woman.transform.position : Zhou.transform.position;
                    case JourneyPhase.Path:
                        if (!pathSceneStarted) return new Vector3(0, 0, 84);
                        return Journey.GlassRemoved ? new Vector3(0, 0, 98) : Dog.transform.position;
                    case JourneyPhase.Shelter:
                        return Journey.ZhouPersuaded
                            ? new Vector3(-4.5f, 0, 104.2f) : Zhou.transform.position;
                    case JourneyPhase.Night:
                        return doorSlammed && !Journey.DoorFixed
                            ? ShelterDoor.transform.position : new Vector3(-4.5f, 0, 104.2f);
                    case JourneyPhase.Morning:
                        return Journey.AddressCopied
                            ? Zhou.transform.position : new Vector3(1.8f, 0, 106.4f);
                    default:
                        return Zhou.transform.position;
                }
            }
        }

        private void ConfigureSound()
        {
            sound = gameObject.AddComponent<AudioSource>();
            sound.loop = true;
            sound.volume = 0.08f;
            ambient = AudioClip.Create("StreetHum", 22050 * 2, 1, 22050, false);
            float[] data = new float[22050 * 2];
            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Sin(i * 2f * Mathf.PI * 80f / 22050f) * 0.22f
                    + Mathf.Sin(i * 2f * Mathf.PI * 160f / 22050f) * 0.08f;
            ambient.SetData(data, 0);
            bark = Tone("DogCallPlaceholder", 310, 0.22f);
            warning = Tone("DistantWarningPlaceholder", 540, 1f);
            slam = Tone("DoorSlamPlaceholder", 78, 0.32f);
            sound.clip = ambient;
            sound.Play();
        }

        private static AudioClip Tone(string name, float frequency, float seconds)
        {
            int count = Mathf.RoundToInt(seconds * 22050);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                samples[i] = Mathf.Sin(i * 2 * Mathf.PI * frequency / 22050) * Mathf.Sin(t * Mathf.PI) * 0.5f;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, 22050, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void SetCursor(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1;
            SetCursor(false);
            if (ambient != null) Destroy(ambient);
            if (bark != null) Destroy(bark);
            if (warning != null) Destroy(warning);
            if (slam != null) Destroy(slam);
            OpeningWorld.ReleaseMaterials();
            if (ReferenceEquals(PlayerWorld.Current, this)) PlayerWorld.Current = null;
            if (Instance == this) Instance = null;
        }
    }
}
