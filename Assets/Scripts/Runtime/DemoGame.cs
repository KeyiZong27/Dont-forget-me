using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontForgetMe
{
    public sealed class DemoGame : MonoBehaviour
    {
        public static DemoGame Instance { get; private set; }
        public OpeningState State { get; private set; }
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
        public bool IsPaused => !started || paused;
        public bool IsFailed { get; private set; }
        public Interactable Nearest { get; private set; }
        public string CurrentCaption { get; private set; }

        private readonly List<Interactable> interactions = new List<Interactable>();
        private bool started;
        private bool paused;
        private float captionUntil;
        private float threatStunUntil;
        private float nextBark;
        private ActorMotor threat;
        private AudioSource sound;
        private AudioClip ambient;
        private AudioClip bark;
        private AudioClip warning;
        private bool gateClosed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null)
                new GameObject("OpeningDemo").AddComponent<DemoGame>();
        }

        private void Awake()
        {
            Instance = this;
            State = new OpeningState();
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
            if (!started || IsFailed || State.Phase == OpeningPhase.Complete) return;
            paused = !paused;
            Time.timeScale = paused ? 0 : 1;
            SetCursor(!paused);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (IsPaused || IsFailed || State.Phase == OpeningPhase.Complete) return;
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
            Caption("后巷传来急促脚步。老周：给我一点时间！\n撑到门修好就能走，不需要杀死对方。F 可以推开靠近的人。", 10);
            threat = OpeningWorld.Actor("unknown", "行为异常的人", new Vector3(0, 0.1f, 31),
                new Color(0.30f, 0.28f, 0.28f));
            threat.Speed = 1.8f;
            Woman.GoTo(new Vector3(1.5f, 0, 43));
            Dog.GoTo(new Vector3(1.4f, 0, 46));
        }

        private void UpdateThreat()
        {
            if (threat == null || gateClosed) return;
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
                switch (State.Phase)
                {
                    case OpeningPhase.Street: return "经过街边修理铺，和修椅子的人说说话。\n也可以先到水碗旁照顾狗。";
                    case OpeningPhase.Reunion: return "到路口找到她和狗，靠近后按 E 确认平安。";
                    case OpeningPhase.Alley: return "跟随老周，进入街道尽头的后巷。";
                    case OpeningPhase.Debris: return State.ClearedDebrisCount == 2
                        ? "到铁门旁与老周合作，开始修门。"
                        : "搬开铁门前的两件杂物。";
                    case OpeningPhase.Repair: return "撑到铁门修好。不要求击杀；F 推开靠近的威胁。";
                    case OpeningPhase.Regroup: return "穿过铁门，到老周身边确认大家都在。";
                    default: return "这一段路，我们一起走出来了。";
                }
            }
        }

        public Vector3 ObjectivePosition
        {
            get
            {
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
            OpeningWorld.ReleaseMaterials();
            if (Instance == this) Instance = null;
        }
    }
}
