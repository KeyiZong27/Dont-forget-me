using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontForgetMe
{
    public sealed class DemoGame : MonoBehaviour, IPlayerWorld
    {
        public static readonly Vector3 GateClosed = new Vector3(0, 1.35f, 48.3f);
        public static readonly Vector3 GateOpen = new Vector3(-2.85f, 1.35f, 48.3f);

        // The closing lines stay on screen over the scene for a moment before the end card.
        public const float EndPanelDelay = 7f;

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
        public bool HasStarted => started;
        public bool IsFailed { get; private set; }
        public bool IsComplete => Journey.Phase == JourneyPhase.Complete;
        public bool PathSceneStarted => pathSceneStarted;
        public bool DoorSlammed => doorSlammed;
        public bool FarewellStarted => farewellStarted;
        public Interactable Nearest { get; private set; }
        public string CurrentCaption { get; private set; }
        public float StartedAt { get; private set; }
        public float EndedAt { get; private set; } = -1f;
        public bool ThreatActive => threat != null && threat.enabled && !gateClosed;

        private readonly List<Interactable> interactions = new List<Interactable>();
        private bool started;
        private bool paused;
        private float captionUntil;
        private float threatStunUntil;
        private float threatMovesAt;
        private float nextBark;
        private ActorMotor threat;
        private AudioSource sound;
        private AudioSource steps;
        private AudioSource drone;
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private AudioClip bark;
        private AudioClip warning;
        private AudioClip step;
        private AudioClip scrape;
        private bool gateClosed;
        private Vector3 gateTarget = GateClosed;

        private static void Bootstrap(Scene scene, LoadSceneMode mode)
        {
            // Restarting reloads the scene, which destroys the game object; build it again each time.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Spawn();
        }

        private static void Spawn()
        {
            if (Instance == null)
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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dfmAutopilot") >= 0
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DFM_SHOTS")))
                gameObject.AddComponent<DemoAutopilot>();
        }

        public void Begin()
        {
            if (started) return;
            started = true;
            StartedAt = Time.time;
            SetCursor(true);
            if (Atmosphere.Instance != null) Atmosphere.Instance.MenuBlur = false;
            Caption("她：碗放稳一点，它怕那个响。我们等会儿从修理铺旁边过去。", 7);
        }

        public void TogglePause()
        {
            if (!started || IsFailed || IsComplete) return;
            paused = !paused;
            Time.timeScale = paused ? 0 : 1;
            SetCursor(!paused);
            if (Atmosphere.Instance != null) Atmosphere.Instance.MenuBlur = paused;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            UpdateMood();
            if (IsPaused || IsFailed || State.Phase == OpeningPhase.Complete) return;
            UpdateInteraction();
            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (Gate != null)
                Gate.transform.position = Vector3.MoveTowards(Gate.transform.position, gateTarget, 2.4f * Time.deltaTime);
            if (State.Phase == OpeningPhase.Alley && Player.transform.position.z > 29)
                EnterAlley();
            if (State.Phase == OpeningPhase.Reunion)
            {
                if (!Woman.HasDestination) Woman.Facing = Player.transform.position;
                if (Time.time > nextBark)
                {
                    nextBark = Time.time + 4f;
                    sound.PlayOneShot(bark, 0.4f);
                    Dog.DogBody.Bark();
                }
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

        private void UpdateMood()
        {
            Atmosphere atmosphere = Atmosphere.Instance;
            if (atmosphere == null) return;
            float tension;
            switch (State.Phase)
            {
                case OpeningPhase.Street: tension = 0f; break;
                case OpeningPhase.Reunion: tension = 0.4f; break;
                case OpeningPhase.Alley: tension = 0.42f; break;
                case OpeningPhase.Debris: tension = 0.5f; break;
                case OpeningPhase.Repair: tension = 0.95f; break;
                case OpeningPhase.Regroup: tension = gateClosed ? 0.35f : 0.85f; break;
                default: tension = 0.12f; break;
            }
            atmosphere.Tension = tension;
            if (EndedAt >= 0 && Time.time > EndedAt + (IsFailed ? 0.8f : EndPanelDelay)) atmosphere.MenuBlur = true;
            if (drone != null)
                drone.volume = Mathf.MoveTowards(drone.volume, paused ? 0 : Mathf.InverseLerp(0.3f, 1f, tension) * 0.2f, Time.unscaledDeltaTime * 0.12f);
        }

        public void Interact()
        {
            if (IsPaused || IsFailed || Nearest == null) return;
            Nearest.OnInteract?.Invoke();
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
            Vector3 bowl = Water.transform.position;
            bowl.y = 0;
            Vector3 away = Dog.transform.position - bowl;
            away.y = 0;
            Dog.GoTo(bowl + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back) * 0.38f);
            Dog.Facing = bowl;
            StartCoroutine(DrinkWhenThere());
            Player.Face(bowl);
            Player.Body.Stance("m_crouch_idle", 1.8f);
            Memories.Recognize("player", "dog");
            Memories.Recognize("dog", "player");
            Caption("狗先闻了闻碗，才低下头。她：这样就好，别催它。", 6);
        }

        private IEnumerator DrinkWhenThere()
        {
            Vector3 bowl = Water.transform.position;
            while (Dog.HasDestination && State.Phase == OpeningPhase.Street) yield return null;
            if (State.Phase != OpeningPhase.Street) yield break;
            Dog.Facing = bowl;
            yield return new WaitForSeconds(1.2f);
            Dog.DogBody.Drinking = true;
            Dog.DogBody.Excite(0.9f);
            float until = Time.time + 9f;
            while (Time.time < until && State.Phase == OpeningPhase.Street) yield return null;
            Dog.DogBody.Drinking = false;
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
            sound.PlayOneShot(warning, 0.6f);
            Dog.DogBody.Drinking = false;
            Dog.DogBody.Bark();
            Woman.GoTo(new Vector3(5, 0, 22));
            Dog.FollowTarget = Woman.transform;
            Dog.FollowOffset = new Vector3(-0.9f, 0, -0.5f);
            Zhou.Body.Stance("m_gestic_talk_neutral_01", 2.4f);
            Zhou.Body.LookTarget = Player.transform;
            Zhou.Facing = Player.transform.position;
            StartCoroutine(After(2.4f, () =>
            {
                if (State.Phase == OpeningPhase.Reunion) Zhou.GoVia(new Vector3(-3, 0, 13), new Vector3(-1.6f, 0, 26));
            }));
            ResidentA.Body.Stance("f_idle_nervous_01");
            ResidentB.Body.Stance("m_idle_nervous_01");
            ResidentA.Body.LookTarget = null;
            ResidentB.Body.LookTarget = null;
            Woman.Body.Stance("f_idle_nervous_01");
            ResidentA.GoTo(new Vector3(-4, 0, 24));
            ResidentB.GoTo(new Vector3(3.2f, 0, 24));
            nextBark = Time.time + 2;
        }

        private IEnumerator After(float seconds, Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
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
            Woman.Body.Stance("f_gestic_talk_neutral_01", 3f);
            StartCoroutine(After(3f, () => Woman.Body.Relax()));
            Dog.DogBody.Excite(1f);
            Player.Face(Woman.transform.position);
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
            Interactable item = debris.GetComponent<Interactable>();
            item.enabled = false;
            Player.Face(debris.transform.position);
            Player.Body.Stance("m_crouch_idle", 1.1f);
            sound.PlayOneShot(scrape, 0.35f);
            StartCoroutine(Slide(debris.transform, new Vector3(index == 0 ? -2.35f : 2.35f, 0.5f, 44), index == 0 ? -14f : 17f));
            Caption(State.ClearedDebrisCount == 2
                ? "路腾出来了。靠近老周，按 E 帮他开始修门。"
                : "她扶稳了另一侧。还有一件杂物挡着门。", 5);
        }

        private static IEnumerator Slide(Transform item, Vector3 destination, float twist)
        {
            Vector3 from = item.position;
            Quaternion start = item.rotation, end = start * Quaternion.Euler(0, twist, 0);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.75f)
            {
                float eased = 1f - (1f - t) * (1f - t);
                item.SetPositionAndRotation(Vector3.Lerp(from, destination, eased), Quaternion.Slerp(start, end, eased));
                yield return null;
            }
            item.SetPositionAndRotation(destination, end);
        }

        public void StartRepair()
        {
            if (Vector3.Distance(Zhou.transform.position, new Vector3(-0.8f, 0, 46)) > 1)
            {
                Caption("等老周走到轨道旁，先不要挤在门口。", 3);
                return;
            }
            if (!State.StartRepair()) return;
            Caption("后巷传来急促脚步。老周：给我一点时间！\n撑到门修好就能走，不需要杀死对方。F 可以推开靠近的人。", 10);
            threat = OpeningWorld.Actor("unknown", "行为异常的人", new Vector3(0, 0.1f, 31), "Stranger",
                "m_idle_drunk_01", "m_walk_drunk");
            threat.transform.rotation = Quaternion.identity;
            threat.Speed = 1.8f;
            threat.Body.MaxStride = 2.3f;
            threat.Body.LookTarget = Player.transform;
            Zhou.Facing = new Vector3(-0.8f, 0, 48.3f);
            Zhou.Body.Stance("m_work_mid");
            Zhou.Body.LookTarget = null;
            Woman.GoTo(new Vector3(1.5f, 0, 43));
            Dog.GoTo(new Vector3(1.4f, 0, 46));
            Dog.DogBody.LookTarget = threat.transform;
            Dog.DogBody.Bark();
            sound.PlayOneShot(bark, 0.4f);
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
                gateTarget = GateClosed;
                sound.PlayOneShot(scrape, 0.5f);
                threat.Stop();
                threat.enabled = false;
                threat.Body.SetSpeed(0);
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
            Player.Face(threat.transform.position);
            threat.KnockBack(away.normalized * 1.7f);
            threatStunUntil = Time.time + 2.2f;
            Footstep(0.3f);
            Caption("他踉跄着退开。趁现在拉开距离。", 2);
            return true;
        }

        private void OpenGate()
        {
            gateTarget = GateOpen;
            sound.PlayOneShot(scrape, 0.55f);
            Zhou.Body.Relax();
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
            gateTarget = GateClosed;
            Gate.transform.position = GateClosed;
            if (threat != null) threat.gameObject.SetActive(false);
            Woman.Stop();
            Dog.Stop();
            Zhou.Stop();
            ResidentA.Stop();
            ResidentB.Stop();
            foreach (ActorMotor actor in new[] { Woman, Zhou, ResidentA, ResidentB })
            {
                actor.Facing = Player.transform.position;
                actor.Body.Relax();
                actor.Body.LookTarget = Player.transform;
            }
            Dog.DogBody.LookTarget = Player.transform;
            EndedAt = Time.time;
            Caption("街坊：刚才修理铺门口，是不是还有个人？\n另一人：他没跟进来吧……我记不清。\n老周：先到前面开阔的地方。别堵在这儿。", 30);
            SetCursor(false);
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
            EndedAt = Time.time;
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
                    case OpeningPhase.Repair: return GateClosed;
                    default: return Zhou.transform.position;
                }
            }
        }

        public void Footstep(float volume)
        {
            if (steps == null) return;
            steps.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
            steps.PlayOneShot(step, volume);
        }

        // All sound is synthesised placeholder audio: air and distant traffic, a low drone for
        // tense moments, and short percussive cues. None of it is a recording.
        private void ConfigureSound()
        {
            const int rate = 22050;
            System.Random random = new System.Random(12);
            sound = gameObject.AddComponent<AudioSource>();
            sound.loop = true;
            sound.volume = 0.16f;
            float[] air = new float[rate * 6];
            float low = 0, lower = 0;
            for (int i = 0; i < air.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * 0.035f;
                lower += (white - lower) * 0.004f;
                float t = (float)i / air.Length;
                float swell = 0.65f + 0.35f * Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Sin(t * Mathf.PI * 6f);
                air[i] = (low * 0.7f + lower * 2.2f) * swell + Mathf.Sin(i * 2f * Mathf.PI * 55f / rate) * 0.04f;
            }
            sound.clip = Clip("StreetAir", Crossfade(air, rate / 2), rate);
            sound.Play();

            drone = gameObject.AddComponent<AudioSource>();
            drone.loop = true;
            drone.volume = 0;
            float[] hum = new float[rate * 4];
            for (int i = 0; i < hum.Length; i++)
            {
                float s = (float)i / rate;
                hum[i] = (Mathf.Sin(s * 2f * Mathf.PI * 49f) * 0.5f + Mathf.Sin(s * 2f * Mathf.PI * 73.75f) * 0.3f
                    + Mathf.Sin(s * 2f * Mathf.PI * 98.25f) * 0.12f) * (0.75f + 0.25f * Mathf.Sin(s * 2f * Mathf.PI * 0.25f));
            }
            drone.clip = Clip("TensionDrone", hum, rate);
            drone.Play();

            steps = gameObject.AddComponent<AudioSource>();
            float[] tap = new float[rate / 12];
            float band = 0;
            for (int i = 0; i < tap.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                band += (white - band) * 0.22f;
                float t = (float)i / tap.Length;
                tap[i] = (band * 0.8f + Mathf.Sin(t * 60f) * 0.35f) * Mathf.Exp(-t * 9f);
            }
            step = Clip("FootstepPlaceholder", tap, rate);

            float[] yap = new float[(int)(rate * 0.2f)];
            float phase = 0;
            for (int i = 0; i < yap.Length; i++)
            {
                float t = (float)i / yap.Length;
                phase += Mathf.Lerp(520f, 300f, t) / rate;
                float saw = (phase % 1f) * 2f - 1f;
                yap[i] = (saw * 0.45f + ((float)random.NextDouble() - 0.5f) * 0.3f) * Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 2f);
            }
            bark = Clip("DogCallPlaceholder", yap, rate);

            float[] thud = new float[(int)(rate * 1.6f)];
            float rumble = 0;
            for (int i = 0; i < thud.Length; i++)
            {
                float t = (float)i / thud.Length;
                float white = (float)random.NextDouble() * 2f - 1f;
                rumble += (white - rumble) * 0.02f;
                thud[i] = (Mathf.Sin(i * 2f * Mathf.PI * Mathf.Lerp(70f, 38f, t) / rate) * 0.7f + rumble * 3f) * Mathf.Exp(-t * 4.5f);
            }
            warning = Clip("DistantWarningPlaceholder", thud, rate);

            float[] drag = new float[(int)(rate * 0.8f)];
            float grit = 0;
            for (int i = 0; i < drag.Length; i++)
            {
                float t = (float)i / drag.Length;
                float white = (float)random.NextDouble() * 2f - 1f;
                grit += (white - grit) * 0.3f;
                drag[i] = grit * 0.5f * Mathf.Sin(t * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(t * 90f));
            }
            scrape = Clip("ScrapePlaceholder", drag, rate);
        }

        // Blends the tail into the head and drops the tail, so the loop point is inaudible.
        private static float[] Crossfade(float[] samples, int overlap)
        {
            float[] looped = new float[samples.Length - overlap];
            Array.Copy(samples, looped, looped.Length);
            for (int i = 0; i < overlap; i++)
            {
                float t = (float)i / overlap;
                looped[i] = samples[i] * t + samples[looped.Length + i] * (1f - t);
            }
            return looped;
        }

        private AudioClip Clip(string clipName, float[] samples, int rate)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            clips.Add(clip);
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
            foreach (AudioClip clip in clips)
                if (clip != null) Destroy(clip);
            Art.Release();
            if (Instance == this) Instance = null;
        }
    }
}
