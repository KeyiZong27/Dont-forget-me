using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontForgetMe
{
    // One movable part of the rubbish heaped against the gate.
    public sealed class Piece
    {
        public string Id;
        public GameObject Root;
        // Heavy pieces are the player's to move and are what the story counts.
        public bool Heavy;
        // Where it is set down, against one of the alley walls.
        public Vector3 Rest;
        // Hand holds: half the spacing between them, and their height above the base.
        public Vector2 Grip;
        // How far the base comes off the ground while carried.
        public float Lift;
        // Tipped back on its wheels and rolled, rather than lifted.
        public bool Wheeled;
        // How far from its middle the carrier stands.
        public float Reach = 0.56f;
        // Pieces lying in front of this one, which have to go first.
        public Piece[] After;
        public bool Claimed;
        public bool Moved;
    }

    public sealed class DemoGame : MonoBehaviour
    {
        public static readonly Vector3 GateClosed = new Vector3(0, 1.35f, 48.3f);
        public static readonly Vector3 GateOpen = new Vector3(-2.85f, 1.35f, 48.3f);
        // Where old Zhou kneels to work, and the chain and bent rail he works on.
        public static readonly Vector3 ZhouWork = new Vector3(0.85f, 0, 47.35f);
        private static readonly Vector3 LockPoint = new Vector3(1.25f, 0, 48.3f);
        private static readonly Vector3 AlleyMouth = new Vector3(0, 0, 29f);
        private const float RepairSeconds = 42f;

        // The closing lines stay on screen over the scene for a moment before the end card.
        public const float EndPanelDelay = 7f;

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
        public CharacterAnimator Keeper { get; set; }
        public Transform ShopShutter { get; set; }
        public Weapon ZhouHammer { get; set; }
        public List<Weapon> Weapons { get; } = new List<Weapon>();
        public GameObject Gate { get; set; }
        public GameObject GateChain { get; set; }
        public GameObject Water { get; set; }
        public List<Piece> Pile { get; } = new List<Piece>();
        public List<Light> AlleyLamps { get; } = new List<Light>();
        public List<Renderer> AlleyBulbs { get; } = new List<Renderer>();
        public Siege Siege { get; private set; }
        public bool IsPaused => !started || paused;
        public bool HasStarted => started;
        public bool IsFailed { get; private set; }
        public Interactable Nearest { get; private set; }
        public string CurrentCaption { get; private set; }
        public float StartedAt { get; private set; }
        public float EndedAt { get; private set; } = -1f;
        public bool GateShut { get; private set; }
        // The gate work has stopped because someone has hold of old Zhou.
        public bool RepairStalled { get; private set; }
        // While the fight lasts the world drops away: no street, no footsteps, no gate. What is
        // left is a pulse, breathing, blows landing and people screaming.
        public bool Hushed => (State.Phase == OpeningPhase.Repair || State.Phase == OpeningPhase.Regroup) && !GateShut;
        // What was heard during the fight, for the walkthrough to check: how many of each, the
        // loudest anything other than a cry was played, and the quietest any cry was.
        public int Beats { get; private set; }
        public int Breaths { get; private set; }
        public int Blows { get; private set; }
        public int Cries { get; private set; }
        public float LoudestOther { get; private set; }
        public float QuietestCry { get; private set; } = 1f;

        private sealed class Hands
        {
            public Transform Root;
            public CharacterAnimator Body;
            public Func<Vector3> Forward;
            public Action<Vector3, float> WalkTo;
            public Func<bool> Walking;
            public Action<Vector3> Face;
        }

        private readonly List<Interactable> interactions = new List<Interactable>();
        private readonly Dictionary<ActorMotor, Companion> companions = new Dictionary<ActorMotor, Companion>();
        private bool started;
        private bool paused;
        private float captionUntil;
        private float nextBark;
        private float nextNag;
        private float nextBeat;
        private float nextCry;
        private float pulseLevel;
        private readonly Dictionary<Component, float> breathAt = new Dictionary<Component, float>();
        private readonly Dictionary<Component, float> criedAt = new Dictionary<Component, float>();
        private float playerStruckAt = -100f;
        private AudioSource sound;
        private AudioSource steps;
        private AudioSource drone;
        private AudioSource pulse;
        private AudioSource[] voices;
        private AudioSource[] cries;
        private int voice, cry;
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private AudioClip bark;
        private AudioClip warning;
        private AudioClip step;
        private AudioClip scrape;
        private AudioClip thud;
        private AudioClip clang;
        private AudioClip beat;
        private AudioClip pant;
        private AudioClip rasp;
        private AudioClip shriek;
        private AudioClip shout;
        private AudioClip struckMetal;
        private AudioClip struckWood;
        private AudioClip struckPlastic;
        private Vector3 gateAt = GateClosed;
        private Vector3 gateTarget = GateClosed;
        private float gateSpeed = 2.4f;
        private float gateShake;
        private bool closing;
        private bool noticed;
        private readonly float[] lampFailedAt = { -1f, -1f };
        private float[] lampLevels;
        private Material[] lampGlow;
        private Material lampDead;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
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
            // Only the opening scene hosts this game; other scenes in the project are left alone.
            if (Instance == null && SceneManager.GetActiveScene().name == "Opening")
                new GameObject("OpeningDemo").AddComponent<DemoGame>();
        }

        private void Awake()
        {
            Instance = this;
            Memories = new MemoryNetwork();
            Crowd.Clear();
            Physics.IgnoreLayerCollision(8, 8, true);
            OpeningWorld.Build(this);
            int heavy = 0;
            foreach (Piece piece in Pile)
                if (piece.Heavy) heavy++;
            State = new OpeningState(heavy, RepairSeconds);
            Siege = gameObject.AddComponent<Siege>();
            Siege.Configure(this);
            foreach (ActorMotor actor in new[] { Woman, Zhou, ResidentA, ResidentB })
            {
                Companion companion = actor.gameObject.AddComponent<Companion>();
                companion.Configure(Siege, actor);
                companions[actor] = companion;
            }
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
            if (!started || IsFailed || State.Phase == OpeningPhase.Complete) return;
            paused = !paused;
            Time.timeScale = paused ? 0 : 1;
            SetCursor(!paused);
            if (Atmosphere.Instance != null) Atmosphere.Instance.MenuBlur = paused;
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0;
            return value;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            UpdateMood();
            if (IsPaused || IsFailed) return;
            UpdateGate();
            UpdateLamps();
            if (State.Phase == OpeningPhase.Complete) return;
            UpdateInteraction();
            if (Input.GetKeyDown(KeyCode.E)) Interact();
            switch (State.Phase)
            {
                case OpeningPhase.Reunion:
                    if (!Woman.HasDestination) Woman.Facing = Player.transform.position;
                    if (Time.time > nextBark)
                    {
                        nextBark = Time.time + 4f;
                        sound.PlayOneShot(bark, 0.4f);
                        Dog.DogBody.Bark();
                    }
                    break;
                case OpeningPhase.Alley:
                    if (Player.transform.position.z > 29) EnterAlley();
                    break;
                case OpeningPhase.Debris:
                    if (!Woman.HasDestination && Woman.Facing == null) Woman.Facing = AlleyMouth;
                    break;
                case OpeningPhase.Repair:
                    UpdateRepair();
                    break;
                case OpeningPhase.Regroup:
                    UpdateEscape();
                    break;
            }
            if (Hushed) UpdateFightSound();
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
                case OpeningPhase.Debris: tension = noticed ? 0.7f : 0.5f; break;
                case OpeningPhase.Repair: tension = 1f; break;
                case OpeningPhase.Regroup: tension = GateShut ? 0.5f : 1f; break;
                default: tension = 0.3f; break;
            }
            atmosphere.Tension = tension;
            if (EndedAt >= 0 && Time.time > EndedAt + (IsFailed ? 0.8f : EndPanelDelay)) atmosphere.MenuBlur = true;
            bool quiet = paused || Hushed;
            if (drone != null)
                drone.volume = Mathf.MoveTowards(drone.volume, quiet ? 0 : Mathf.InverseLerp(0.3f, 1f, tension) * 0.24f, Time.unscaledDeltaTime * (Hushed ? 0.5f : 0.12f));
            if (sound != null)
                sound.volume = Mathf.MoveTowards(sound.volume, Hushed ? 0 : 0.16f, Time.unscaledDeltaTime * (Hushed ? 0.4f : 0.1f));
        }

        private void UpdateGate()
        {
            if (Gate == null) return;
            gateAt = Vector3.MoveTowards(gateAt, gateTarget, gateSpeed * Time.deltaTime);
            gateShake = Mathf.MoveTowards(gateShake, 0f, Time.deltaTime * 0.06f);
            // A struck leaf rattles in its guides.
            Gate.transform.position = gateAt + new Vector3(0, 0, Mathf.Sin(Time.time * 75f) * gateShake);
        }

        // The lamps nearest the street give out as the alley fills, so they arrive as shapes against
        // the daylight. Each one stutters twice and stays down; nothing keeps flashing.
        private void UpdateLamps()
        {
            if (AlleyLamps.Count < 2) return;
            if (lampLevels == null)
            {
                lampLevels = new float[AlleyLamps.Count];
                lampGlow = new Material[AlleyLamps.Count];
                for (int i = 0; i < lampLevels.Length; i++)
                {
                    lampLevels[i] = AlleyLamps[i].intensity;
                    lampGlow[i] = AlleyBulbs[i].sharedMaterial;
                }
                lampDead = Art.Plain(new Color(0.2f, 0.17f, 0.13f), 0.5f);
            }
            int crowd = Siege.Hostiles.Count;
            bool siege = State.Phase == OpeningPhase.Repair || State.Phase == OpeningPhase.Regroup || State.Phase == OpeningPhase.Complete;
            if (siege && crowd >= 3 && lampFailedAt[0] < 0) lampFailedAt[0] = Time.time;
            if (siege && crowd >= 5 && lampFailedAt[1] < 0) lampFailedAt[1] = Time.time;
            SetLamp(0, Failing(lampFailedAt[0], 0f));
            SetLamp(1, Failing(lampFailedAt[1], 0.3f));
        }

        private static float Failing(float since, float rest)
        {
            if (since < 0) return 1f;
            float t = Time.time - since;
            if (t < 0.15f) return 0.35f;
            if (t < 0.45f) return 1f;
            if (t < 0.6f) return 0.2f;
            if (t < 0.9f) return 0.8f;
            return Mathf.Lerp(0.8f, rest, (t - 0.9f) / 0.3f);
        }

        private void SetLamp(int index, float level)
        {
            AlleyLamps[index].intensity = lampLevels[index] * level;
            AlleyBulbs[index].sharedMaterial = level > 0.3f ? lampGlow[index] : lampDead;
        }

        public void Interact()
        {
            if (IsPaused || IsFailed || Nearest == null) return;
            Nearest.OnInteract?.Invoke();
        }

        private void UpdateInteraction()
        {
            Nearest = null;
            if (Player.Busy) return;
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

        public Interactable AddInteraction(GameObject owner, string prompt, Func<bool> available, Action action)
        {
            Interactable item = owner.AddComponent<Interactable>();
            item.Prompt = prompt;
            item.IsAvailable = available;
            item.OnInteract = action;
            interactions.Add(item);
            return item;
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
            Dog.Follow(Woman.transform, new Vector3(-0.9f, 0, -0.5f));
            Zhou.Body.Stance("m_gestic_talk_neutral_01", 2.4f);
            Zhou.Body.LookTarget = Player.transform;
            Zhou.Facing = Player.transform.position;
            StartCoroutine(After(2.4f, () =>
            {
                if (State.Phase == OpeningPhase.Reunion) Zhou.GoVia(new Vector3(-3, 0, 13), new Vector3(-1.6f, 0, 26));
            }));
            // Everybody on the street hears it, and nobody goes on with what they were doing.
            foreach (ActorMotor actor in new[] { Woman, Zhou, ResidentA, ResidentB }) actor.Body.Flinch();
            Player.Body.Flinch();
            Woman.Body.Stance("f_idle_nervous_01");
            StartCoroutine(Scatter());
            nextBark = Time.time + 2;
        }

        // The store pulls its shutter down; one customer freezes with her arms over her head
        // before running for the junction, the other goes out into the road to see.
        private IEnumerator Scatter()
        {
            Vector3 south = new Vector3(0, 0, -4);
            ResidentA.Body.Relax();
            ResidentA.Body.Cover = true;
            ResidentA.Facing = south;
            ResidentB.Body.Stance("m_idle_nervous_01");
            ResidentB.Body.LookTarget = null;
            ResidentB.Facing = south;
            if (Keeper != null)
            {
                Keeper.Flinch();
                Keeper.Stance("m_idle_nervous_01");
                Keeper.LookTarget = null;
            }
            yield return new WaitForSeconds(1.3f);
            if (State.Phase == OpeningPhase.Reunion)
            {
                ResidentB.Hurry = true;
                ResidentB.GoVia(new Vector3(-4.6f, 0, 11.4f), new Vector3(2.4f, 0, 21.2f));
            }
            yield return new WaitForSeconds(0.7f);
            if (ShopShutter != null)
            {
                Play(scrape, Loud(ShopShutter.position) * 0.5f, 1f);
                for (float t = 0; t < 1f; t += Time.deltaTime / 2.2f)
                {
                    ShopShutter.localScale = new Vector3(1, Mathf.Lerp(0.1f, 1f, t * t * (3f - 2f * t)), 1);
                    yield return null;
                }
                ShopShutter.localScale = Vector3.one;
                Play(clang, Loud(ShopShutter.position) * 0.35f, 1f);
                if (Keeper != null) Keeper.transform.parent.gameObject.SetActive(false);
            }
            ResidentA.Body.Cover = false;
            ResidentA.Body.Stance("f_idle_nervous_01");
            if (State.Phase == OpeningPhase.Reunion)
            {
                ResidentA.Hurry = true;
                ResidentA.GoVia(new Vector3(-5.0f, 0, 12.2f), new Vector3(-4.0f, 0, 23.6f));
            }
            float patience = Time.time + 8f;
            while (Time.time < patience && State.Phase == OpeningPhase.Reunion && (ResidentA.HasDestination || ResidentB.HasDestination))
                yield return null;
            if (State.Phase != OpeningPhase.Reunion) yield break;
            ResidentA.Hurry = ResidentB.Hurry = false;
            ResidentA.Facing = south;
            ResidentB.GoTo(new Vector3(3.2f, 0, 24));
        }

        private IEnumerator After(float seconds, Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        private static IEnumerator Until(Func<bool> done, float timeout)
        {
            float end = Time.time + timeout;
            while (!done() && Time.time < end) yield return null;
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
            Woman.Follow(Player.transform, new Vector3(0.85f, 0, -1.2f));
            Woman.Body.Stance("f_gestic_talk_neutral_01", 3f);
            StartCoroutine(After(3f, () => Woman.Body.Relax()));
            Dog.DogBody.Excite(1f);
            Player.Face(Woman.transform.position);
            Caption("她：有没有受伤？先离开这儿。\n老周：别挤前面，后头能走。跟着我。", 8);
            ResidentA.Hurry = ResidentB.Hurry = false;
            ResidentA.Body.Cover = false;
            Zhou.GoVia(new Vector3(0, 0, 29.5f), new Vector3(0.9f, 0, 38.5f));
            ResidentA.GoVia(new Vector3(0, 0, 29.5f), new Vector3(-0.9f, 0, 35.5f));
            ResidentB.GoVia(new Vector3(0, 0, 29.5f), new Vector3(0.3f, 0, 33.8f));
        }

        private void EnterAlley()
        {
            if (!State.EnterAlley()) return;
            // She keeps watch on the way they came, with the dog; everyone else goes to the pile.
            Woman.GoVia(new Vector3(0.4f, 0, 29.5f), new Vector3(0.9f, 0, 39.6f));
            Dog.Follow(Woman.transform, new Vector3(-0.7f, 0, 0.6f));
            StartCoroutine(Help(ResidentB, new Vector3(0.6f, 0, 41.6f), "pile-tyres", "pile-bags", "pile-chairs"));
            StartCoroutine(Help(ResidentA, new Vector3(-0.6f, 0, 42.0f), "pile-boxes", "pile-baskets"));
            StartCoroutine(Help(Zhou, new Vector3(0f, 0, 43.0f), "pile-telly"));
            Caption("老周：门让杂物堆死了，链子也锈住了。把中间腾出来，我才够得着。\n街坊：轻的我们来，重的搭把手！", 8);
        }

        private static bool Reachable(Piece piece)
        {
            if (piece.After != null)
                foreach (Piece first in piece.After)
                    if (!first.Moved) return false;
            return true;
        }

        public bool CanMove(Piece piece)
        {
            return State.Phase == OpeningPhase.Debris && !piece.Moved && !piece.Claimed && !Player.Busy && Reachable(piece);
        }

        public void MovePiece(Piece piece)
        {
            if (!CanMove(piece)) return;
            piece.Claimed = true;
            StartCoroutine(HaulByPlayer(piece));
        }

        private IEnumerator HaulByPlayer(Piece piece)
        {
            Player.Occupy(true);
            yield return Haul(new Hands
            {
                Root = Player.transform, Body = Player.Body, Forward = () => Player.Forward,
                WalkTo = Player.WalkTo, Walking = () => Player.Walking, Face = Player.Face
            }, piece);
            Player.Occupy(false);
            if (!piece.Heavy || !State.ClearDebris(piece.Id)) yield break;
            int left = State.DebrisRequired - State.ClearedDebrisCount;
            if (State.ClearedDebrisCount == 1) Caption("她：等一下，手往外一点，别夹住。", 5);
            else if (left == 0)
            {
                Zhou.Body.Relax();
                Zhou.Body.LookTarget = null;
                Zhou.GoTo(ZhouWork);
                Caption("中间腾出来了。老周已经往门边去——过去帮他顶住后面。", 6);
            }
            else if (left == 1) Caption("街坊：就剩卡在轨道上那辆车了！", 4);
            else Caption("老周：再来。把路腾到门根底下。", 4);
            if (State.ClearedDebrisCount == 2) StartCoroutine(Notice());
        }

        // Halfway through the pile someone turns up at the alley mouth and does nothing at all.
        private IEnumerator Notice()
        {
            Siege.Foreshadow();
            yield return new WaitForSeconds(6.5f);
            if (State.Phase != OpeningPhase.Debris) yield break;
            noticed = true;
            Woman.Facing = AlleyMouth;
            Woman.Body.Stance("f_idle_nervous_01");
            if (Siege.Hostiles.Count > 0)
            {
                Woman.Body.LookTarget = Siege.Hostiles[0].transform;
                Dog.DogBody.LookTarget = Siege.Hostiles[0].transform;
            }
            Dog.DogBody.Bark();
            sound.PlayOneShot(bark, 0.4f);
            Caption("她：巷口站着个人。喊他，他不应。\n老周：别回头，先把门弄开。", 7);
        }

        private Hands HandsOf(ActorMotor actor)
        {
            return new Hands
            {
                Root = actor.transform, Body = actor.Body, Forward = () => actor.transform.forward,
                WalkTo = (point, pace) =>
                {
                    actor.Speed = pace;
                    actor.GoTo(point);
                },
                Walking = () => actor.HasDestination, Face = point => actor.Facing = point
            };
        }

        // A neighbour clears the things one person can lift as the way to each opens up, and
        // stands back out of the carrying line in between.
        private IEnumerator Help(ActorMotor actor, Vector3 wait, params string[] ids)
        {
            Companion companion = companions[actor];
            string uneasy = actor == ResidentA ? "f_idle_nervous_01" : "m_idle_nervous_01";
            foreach (string id in ids)
            {
                Piece piece = Pile.Find(item => item.Id == id);
                if (piece == null) continue;
                bool waiting = false;
                while (State.Phase == OpeningPhase.Debris && !piece.Moved && !piece.Claimed && !Reachable(piece))
                {
                    if (!waiting)
                    {
                        waiting = true;
                        actor.GoTo(wait);
                        actor.Body.Stance(uneasy);
                        actor.Body.LookTarget = Player.transform;
                    }
                    else if (!actor.HasDestination && actor.Facing == null) actor.Facing = GateClosed;
                    yield return null;
                }
                if (piece.Claimed || piece.Moved || State.Phase != OpeningPhase.Debris) continue;
                piece.Claimed = true;
                companion.Busy = true;
                actor.Body.Relax();
                actor.Body.LookTarget = null;
                yield return Haul(HandsOf(actor), piece);
                companion.Busy = false;
                actor.Speed = 3.2f;
            }
            if (State.Phase != OpeningPhase.Debris) yield break;
            actor.GoTo(wait);
            actor.Body.Stance(uneasy);
            actor.Body.LookTarget = Player.transform;
            yield return Until(() => !actor.HasDestination, 6f);
            if (State.Phase == OpeningPhase.Debris && !actor.HasDestination) actor.Facing = GateClosed;
        }

        // Carrying one piece off the pile: walk up, bend and take hold, bring it to the wall with
        // the hands on it the whole way, set it down.
        private IEnumerator Haul(Hands who, Piece piece)
        {
            Transform item = piece.Root.transform;
            piece.Root.GetComponent<Collider>().enabled = false;
            float ground = item.position.y;

            // A wheeled piece is taken from behind its handles; anything else from the nearer side.
            float side = piece.Wheeled || Vector3.Dot(Flat(who.Root.position - item.position), item.forward) < 0 ? -1f : 1f;
            who.WalkTo(item.position + item.forward * (side * piece.Reach), 2.6f);
            yield return Until(() => !who.Walking(), 8f);
            who.Face(item.position);
            yield return new WaitForSeconds(0.2f);

            if (!piece.Wheeled) who.Body.Stance("m_crouch_idle", 0.7f);
            Vector3 leftHold = new Vector3(side * piece.Grip.x, piece.Grip.y, 0), rightHold = new Vector3(-side * piece.Grip.x, piece.Grip.y, 0);
            who.Body.Hold(() => item.TransformPoint(leftHold), () => item.TransformPoint(rightHold), piece.Wheeled ? 6f : 14f);
            yield return new WaitForSeconds(piece.Wheeled ? 0.4f : 0.6f);
            Play(scrape, Loud(item.position) * 0.4f, 1f);

            // From here the piece rides in the carrier's hands and turns as they turn.
            Quaternion facing = Quaternion.LookRotation(Flat(who.Forward()));
            Vector3 offset = Quaternion.Inverse(facing) * Flat(item.position - who.Root.position);
            Quaternion relative = Quaternion.Inverse(facing) * item.rotation;
            float raised = 0;
            Action carry = () =>
            {
                Quaternion heading = Quaternion.LookRotation(Flat(who.Forward()));
                Vector3 at = who.Root.position + heading * offset;
                at.y = ground + piece.Lift * raised;
                item.SetPositionAndRotation(at, heading * relative * Quaternion.Euler(piece.Wheeled ? side * 14f * raised : 0, 0, 0));
            };
            Vector3 inward = new Vector3(-Mathf.Sign(piece.Rest.x), 0, 0);
            who.Body.Relax();
            who.WalkTo(piece.Rest + inward * offset.magnitude, piece.Wheeled ? 2.4f : piece.Heavy ? 2.0f : 2.3f);
            float limit = Time.time + 10f;
            while (who.Walking() && Time.time < limit)
            {
                raised = Mathf.MoveTowards(raised, 1f, Time.deltaTime / 0.35f);
                carry();
                yield return null;
            }
            who.Face(piece.Rest);
            limit = Time.time + 1.2f;
            while (Time.time < limit && Vector3.Angle(Flat(who.Forward()), -inward) > 6f)
            {
                carry();
                yield return null;
            }

            if (!piece.Wheeled) who.Body.Stance("m_crouch_idle", 0.55f);
            while (raised > 0)
            {
                raised = Mathf.MoveTowards(raised, 0f, Time.deltaTime / 0.35f);
                carry();
                yield return null;
            }
            Thud(item.position, piece.Heavy ? 0.7f : 0.4f);
            who.Body.Release();
            piece.Root.GetComponent<Collider>().enabled = true;
            piece.Moved = true;
            yield return new WaitForSeconds(0.2f);
        }

        public void StartRepair()
        {
            if (Flat(Zhou.transform.position - ZhouWork).magnitude > 1f)
            {
                Caption("等老周走到门边，先别挤在门口。", 3);
                return;
            }
            if (!State.StartRepair()) return;
            noticed = true;
            companions[Zhou].Assign(Companion.Duty.Work, ZhouWork, LockPoint);
            companions[ResidentA].Assign(Companion.Duty.Cower, new Vector3(-1.05f, 0, 47.3f), AlleyMouth);
            companions[ResidentB].Assign(Companion.Duty.Brawl, new Vector3(-0.6f, 0, 44.9f), AlleyMouth, 2.3f, 2.8f);
            companions[Woman].Assign(Companion.Duty.Guard, new Vector3(0.5f, 0, 45.9f), AlleyMouth, 1.5f, 3.5f);
            Woman.Body.Relax();
            ResidentB.Body.Relax();
            Dog.Follow(Woman.transform, new Vector3(-0.65f, 0, -0.6f));
            Dog.DogBody.Bark();
            if (ZhouHammer != null)
            {
                ZhouHammer.Root.gameObject.SetActive(true);
                Zhou.Body.Carry(ZhouHammer);
            }
            Siege.Begin();
            Caption("巷口那个人动了，后面还跟着别的。老周：给我点时间，别让他们碰到我！\nF 抄起手边的杂物，把靠近的人打退。撑到门打开就走。", 10);
        }

        private void UpdateRepair()
        {
            Siege.Tick(State.RepairProgress);
            Companion worker = companions[Zhou];
            RepairStalled = !worker.Working;
            if (worker.Working)
            {
                State.TickRepair(Time.deltaTime);
            }
            else if (worker.Harassed && Time.time >= nextNag)
            {
                nextNag = Time.time + 5f;
                Scream(Zhou);
                Caption("老周：有人拽我——把他弄开！我腾不出手！", 3.5f);
            }
            WatchTheCrowd();
            if (State.Phase == OpeningPhase.Regroup) OpenGate();
        }

        private void WatchTheCrowd()
        {
            Hostile near = Siege.Nearest(Dog.transform.position, 20f, false);
            Dog.DogBody.LookTarget = near != null ? near.transform : null;
            if (near == null || Time.time < nextBark) return;
            nextBark = Time.time + UnityEngine.Random.Range(1.8f, 3.2f);
            Dog.DogBody.Bark();
            if (!Hushed) sound.PlayOneShot(bark, 0.45f);
        }

        private void OpenGate()
        {
            RepairStalled = false;
            gateTarget = GateOpen;
            gateSpeed = 2.4f;
            if (GateChain != null) GateChain.SetActive(false);
            Siege.Surge(ResidentB.transform);
            foreach (ActorMotor actor in new[] { Zhou, Woman, ResidentA })
            {
                companions[actor].Assign(Companion.Duty.None, actor.transform.position, GateOpen);
                actor.Body.Relax();
                actor.Body.LookTarget = null;
                actor.Hurry = true;
            }
            // The one who could only hide goes first; the one who fought holds the doorway for the rest.
            ResidentA.GoVia(new Vector3(-0.4f, 0, 47.0f), new Vector3(0.1f, 0, 50.5f), new Vector3(-2.2f, 0, 54.8f));
            Woman.GoVia(new Vector3(0.3f, 0, 47.0f), new Vector3(0.2f, 0, 50.5f), new Vector3(1.4f, 0, 55.4f));
            Zhou.GoVia(new Vector3(0.5f, 0, 47.8f), new Vector3(0.3f, 0, 50.5f), new Vector3(-1.6f, 0, 57));
            StartCoroutine(After(0.9f, () =>
            {
                if (State.Phase == OpeningPhase.Regroup) Dog.Follow(Woman.transform, new Vector3(-0.6f, 0, -0.5f));
            }));
            companions[ResidentB].Assign(Companion.Duty.Brawl, new Vector3(-0.7f, 0, 49.6f), AlleyMouth, 1.3f, 1.9f);
            Caption("老周：开了！都进去！\n穿过铁门。人到齐了，门才关得上。", 7);
        }

        private bool Through(float z)
        {
            return Woman.transform.position.z > z && Dog.transform.position.z > z && Zhou.transform.position.z > z
                && ResidentA.transform.position.z > z && ResidentB.transform.position.z > z && Player.transform.position.z > z;
        }

        public bool EveryonePresent()
        {
            return Through(49f);
        }

        private void UpdateEscape()
        {
            Siege.Tick(1f);
            WatchTheCrowd();
            if (GateShut || closing || !Through(49.2f)) return;
            if (!Siege.AnyBeyond(50.6f)) StartCoroutine(CloseGate());
            else if (Time.time >= nextNag)
            {
                nextNag = Time.time + 4f;
                Caption("她：有人跟进来了！把他推回门外，门才关得上！", 3.5f);
            }
        }

        // The neighbour who held the doorway heaves whoever is in it back out, then puts his weight
        // on the end of the leaf and walks it shut; anyone still in the way is driven out by it.
        private IEnumerator CloseGate()
        {
            closing = true;
            Companion holder = companions[ResidentB];
            holder.Busy = true;
            ResidentB.Stop();
            ResidentB.Facing = ResidentB.transform.position + Vector3.back * 3f;
            ResidentB.transform.rotation = Quaternion.LookRotation(Vector3.back);
            ResidentB.Body.Shove();
            yield return new WaitForSeconds(0.17f);
            Siege.ClearGateway(1.5f);
            Impact(null, ResidentB.transform.position + Vector3.back);
            ResidentB.Hurry = true;
            ResidentB.GoTo(new Vector3(gateAt.x + 1.1f, 0, 48.95f));
            yield return Until(() => !ResidentB.HasDestination, 2.5f);
            ResidentB.Hurry = false;
            ResidentB.Facing = ResidentB.transform.position + new Vector3(1f, 0, -0.7f);
            ResidentB.Body.Hold(() => Gate.transform.position + new Vector3(1.3f, -0.05f, 0.08f),
                () => Gate.transform.position + new Vector3(1.3f, -0.4f, 0.08f), 14f);
            yield return new WaitForSeconds(0.35f);
            gateSpeed = 2.0f;
            gateTarget = GateClosed;
            while (Mathf.Abs(gateAt.x - GateClosed.x) > 0.01f)
            {
                ResidentB.Speed = 2.3f;
                ResidentB.GoTo(new Vector3(Mathf.Min(gateAt.x + 1.1f, 1.0f), 0, 48.95f));
                Siege.ClearGateway(1.2f);
                yield return null;
            }
            // The slam is where the world comes back.
            GateShut = true;
            Play(clang, 0.8f, 1f);
            gateShake = 0.02f;
            closing = false;
            ResidentB.Stop();
            yield return new WaitForSeconds(0.4f);
            ResidentB.Body.Release();
            holder.Busy = false;
            holder.Assign(Companion.Duty.None, ResidentB.transform.position, GateClosed);
            ResidentB.Speed = 3.2f;
            ResidentB.Body.LookTarget = null;
            ResidentB.GoTo(new Vector3(2.1f, 0, 54.8f));
            Siege.GateShut();
            Caption("铁门撞上门柱，街坊把门闩插死。外面的手一下一下拍在铁皮上。\n到老周身边，确认大家都在。", 8);
        }

        public void Finish()
        {
            if (!GateShut)
            {
                Caption("门还开着，先让所有人进来。", 3);
                return;
            }
            if (!State.Finish(EveryonePresent()))
            {
                Caption("还有人在过门，等一下他们。", 3);
                return;
            }
            foreach (ActorMotor actor in new[] { Woman, Zhou, ResidentA, ResidentB })
            {
                actor.Hurry = false;
                actor.Stop();
                actor.Facing = Player.transform.position;
                actor.Body.Relax();
                actor.Body.LookTarget = Player.transform;
            }
            Dog.Stop();
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

        public void ShoveLanded(int count)
        {
            if (Time.time - playerStruckAt < 2f) return;
            Caption(count > 1 ? "这一下扫到了不止一个。趁现在站稳。" : "他被打得连退几步，又站住了。", 2.2f);
        }

        public void PlayerStruck()
        {
            playerStruckAt = Time.time;
            Caption("被扑中了。别让他们围住——推开，换个位置。", 2.5f);
        }

        public void CompanionStruck(Transform victim)
        {
            ActorMotor struck = victim.GetComponent<ActorMotor>();
            if (struck != null) Scream(struck);
            if (Time.time < nextNag || State.Phase != OpeningPhase.Repair) return;
            nextNag = Time.time + 4.5f;
            if (victim == ResidentA.transform) Caption("街坊：别过来！别过来——！", 3f);
            else if (victim == Woman.transform) Caption("她：我没事！看住老周那边！", 3f);
            else if (victim == ResidentB.transform) Caption("街坊：我顶着！快点开门！", 3f);
        }

        public void Fail()
        {
            IsFailed = true;
            EndedAt = Time.time;
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
                    case OpeningPhase.Debris: return State.ClearedDebrisCount == State.DebrisRequired
                        ? "到铁门旁，帮老周顶住后面，让他开门。"
                        : "把堵死铁门的重物一件件搬开（" + State.ClearedDebrisCount + " / " + State.DebrisRequired + "）。\n轻的杂物街坊会清。";
                    case OpeningPhase.Repair: return RepairStalled
                        ? "老周被缠住了，门停了——把他身边的人打开（F）。"
                        : Player.Held == null ? "守住老周，撑到门打开。F 从杂物里抄起一样东西。"
                        : "守住老周，撑到门打开。F 用手里的" + Player.Held.Name + "砸退靠近的人。";
                    case OpeningPhase.Regroup: return GateShut ? "到老周身边，确认大家都在。" : "穿过铁门！人到齐，门才关得上。";
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
                    case OpeningPhase.Debris:
                        foreach (Piece piece in Pile)
                            if (piece.Heavy && !piece.Moved && !piece.Claimed) return piece.Root.transform.position - Vector3.up * 0.6f;
                        return Zhou.transform.position;
                    case OpeningPhase.Repair: return Zhou.transform.position;
                    case OpeningPhase.Regroup: return GateShut ? Zhou.transform.position : new Vector3(0, 0, 53.5f);
                    default: return Zhou.transform.position;
                }
            }
        }

        public void Footstep(float volume)
        {
            if (steps == null || Hushed) return;
            steps.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
            steps.PlayOneShot(step, volume);
        }

        // How loud something at "at" should be for the player: full nearby, gone by about 25 m.
        private float Loud(Vector3 at)
        {
            float fade = EndedAt >= 0 ? Mathf.Clamp01(1f - (Time.time - EndedAt - EndPanelDelay) / 4f) * 0.7f + 0.3f : 1f;
            return Mathf.Clamp01(1.1f - Vector3.Distance(at, Player.transform.position) / 24f) * fade;
        }

        // Each sound gets a source of its own for as long as it lasts, so that pitching one does
        // not bend another that is still playing.
        private void Play(AudioClip clip, float volume, float pitch)
        {
            AudioSource source = voices[voice++ % voices.Length];
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
            if (Hushed) LoudestOther = Mathf.Max(LoudestOther, volume);
        }

        // Something heavy set down.
        public void Thud(Vector3 at, float volume)
        {
            Play(thud, Loud(at) * volume, UnityEngine.Random.Range(0.85f, 1.15f));
        }

        // A blow landing on one of them: the body it lands on, and whatever it was struck with.
        public void Impact(Weapon weapon, Vector3 at)
        {
            float loud = Loud(at);
            Blows++;
            Play(thud, loud * 0.5f, UnityEngine.Random.Range(0.9f, 1.2f));
            if (weapon == null || weapon.Made == Weapon.Material.Masonry) return;
            AudioClip clip = weapon.Made == Weapon.Material.Metal ? struckMetal
                : weapon.Made == Weapon.Material.Wood ? struckWood : struckPlastic;
            Play(clip, loud * 0.62f, UnityEngine.Random.Range(0.9f, 1.12f));
        }

        // One of them breathing: hoarse, and loudest just before they throw themselves forward.
        public void Rasp(Vector3 at, float volume)
        {
            Breaths++;
            Play(rasp, Loud(at) * volume * 0.36f, UnityEngine.Random.Range(0.78f, 1.08f));
        }

        // One of ours crying out. Nothing else in the fight is as loud.
        public void Scream(ActorMotor who)
        {
            if (!Hushed || Time.time < nextCry) return;
            if (criedAt.TryGetValue(who, out float last) && Time.time - last < 4.5f) return;
            criedAt[who] = Time.time;
            nextCry = Time.time + 1.3f;
            bool high = who == Woman || who == ResidentA;
            float pitch = who == ResidentA ? 1.04f : who == Woman ? 0.93f : who == Zhou ? 0.86f : 1f;
            AudioSource source = cries[cry++ % cries.Length];
            source.pitch = pitch * UnityEngine.Random.Range(0.97f, 1.04f);
            float volume = Mathf.Max(0.8f, Loud(who.transform.position));
            source.PlayOneShot(high ? shriek : shout, volume);
            Cries++;
            QuietestCry = Mathf.Min(QuietestCry, volume);
        }

        // A pulse that comes up as they close in, never loud; everyone near enough to hear, breathing.
        private void UpdateFightSound()
        {
            Hostile near = Siege.Nearest(Player.transform.position, 9f, true);
            float close = near != null
                ? Mathf.InverseLerp(9f, 1.6f, Flat(near.transform.position - Player.transform.position).magnitude) : 0f;
            pulseLevel = Mathf.MoveTowards(pulseLevel, close, Time.deltaTime * (close > pulseLevel ? 1.2f : 0.35f));
            if (pulseLevel > 0.03f && Time.time >= nextBeat)
            {
                nextBeat = Time.time + Mathf.Lerp(0.92f, 0.43f, pulseLevel);
                pulse.PlayOneShot(beat, Mathf.Lerp(0.07f, 0.2f, pulseLevel));
                Beats++;
            }

            foreach (Hostile hostile in Siege.Hostiles)
            {
                if (hostile.Fallen || Flat(hostile.transform.position - Player.transform.position).magnitude > 6f) continue;
                if (!Breathes(hostile, 1.8f, 3f)) continue;
                Rasp(hostile.transform.position, 0.6f);
            }
            foreach (ActorMotor actor in new[] { Woman, ResidentB, Zhou, ResidentA })
            {
                bool frightened = actor == ResidentA;
                if (!Breathes(actor, frightened ? 0.8f : 1.2f, frightened ? 1.2f : 1.9f)) continue;
                float pitch = actor == Woman ? 1.22f : actor == ResidentA ? 1.36f : actor == Zhou ? 0.84f : 0.95f;
                Breaths++;
                Play(pant, Loud(actor.transform.position) * 0.3f, pitch * UnityEngine.Random.Range(0.95f, 1.06f));
            }
            // The one against the wall screams when any of them gets close to her.
            if (Siege.Nearest(ResidentA.transform.position, 2.3f, false) != null) Scream(ResidentA);
        }

        private bool Breathes(Component who, float shortest, float longest)
        {
            if (!breathAt.TryGetValue(who, out float due)) due = Time.time + UnityEngine.Random.Range(0f, longest);
            bool now = Time.time >= due;
            breathAt[who] = now ? Time.time + UnityEngine.Random.Range(shortest, longest) : due;
            return now;
        }

        // A fist or a shoulder against the shut gate.
        public void GateBlow(Vector3 at)
        {
            gateShake = Mathf.Max(gateShake, 0.012f);
            Play(clang, Loud(at) * 0.4f, UnityEngine.Random.Range(0.8f, 1.1f));
        }

        // Picks, at random from the few nearest, something loose in the rubbish to fight with.
        public Weapon Reserve(Vector3 near, float within, int among)
        {
            List<Weapon> free = Weapons.FindAll(item => !item.Taken && Flat(item.Root.position - near).magnitude <= within);
            if (free.Count == 0) return null;
            free.Sort((a, b) => Flat(a.Root.position - near).sqrMagnitude.CompareTo(Flat(b.Root.position - near).sqrMagnitude));
            Weapon chosen = free[UnityEngine.Random.Range(0, Mathf.Min(among, free.Count))];
            chosen.Taken = true;
            return chosen;
        }

        public void Armed(Weapon weapon)
        {
            Caption("你从杂物堆里抄起" + weapon.Measure + weapon.Name + "。", 2.5f);
        }

        public void HostileFell(int count)
        {
            if (count == 1) Caption("他倒下去，没有再起来。巷口又有人进来。", 4f);
        }

        // All sound is synthesised placeholder audio: air and distant traffic, a low drone for
        // tense moments, short percussive cues, and for the fight a pulse, breathing, blows and
        // cries. None of it is a recording.
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
            pulse = gameObject.AddComponent<AudioSource>();
            voices = new AudioSource[10];
            for (int i = 0; i < voices.Length; i++) voices[i] = gameObject.AddComponent<AudioSource>();
            cries = new[] { gameObject.AddComponent<AudioSource>(), gameObject.AddComponent<AudioSource>() };
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

            float[] boom = new float[(int)(rate * 1.6f)];
            float rumble = 0;
            for (int i = 0; i < boom.Length; i++)
            {
                float t = (float)i / boom.Length;
                float white = (float)random.NextDouble() * 2f - 1f;
                rumble += (white - rumble) * 0.02f;
                boom[i] = (Mathf.Sin(i * 2f * Mathf.PI * Mathf.Lerp(70f, 38f, t) / rate) * 0.7f + rumble * 3f) * Mathf.Exp(-t * 4.5f);
            }
            warning = Clip("DistantWarningPlaceholder", boom, rate);

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

            // A body taking a blow: a dull low knock with a little cloth in it.
            float[] knock = new float[(int)(rate * 0.22f)];
            float muffle = 0;
            for (int i = 0; i < knock.Length; i++)
            {
                float t = (float)i / knock.Length;
                float white = (float)random.NextDouble() * 2f - 1f;
                muffle += (white - muffle) * 0.08f;
                knock[i] = (Mathf.Sin(i * 2f * Mathf.PI * Mathf.Lerp(120f, 52f, t) / rate) * 0.8f + muffle * 1.6f) * Mathf.Exp(-t * 7f);
            }
            thud = Clip("ImpactPlaceholder", knock, rate);

            // Sheet steel struck with the flat of a hand: a few partials that do not agree.
            float[] ring = new float[(int)(rate * 0.55f)];
            for (int i = 0; i < ring.Length; i++)
            {
                float s = (float)i / rate, t = (float)i / ring.Length;
                float white = (float)random.NextDouble() * 2f - 1f;
                ring[i] = (Mathf.Sin(s * 2f * Mathf.PI * 163f) * 0.5f * Mathf.Exp(-t * 5f) + Mathf.Sin(s * 2f * Mathf.PI * 311f) * 0.3f * Mathf.Exp(-t * 7f)
                    + Mathf.Sin(s * 2f * Mathf.PI * 517f) * 0.22f * Mathf.Exp(-t * 9f) + Mathf.Sin(s * 2f * Mathf.PI * 841f) * 0.14f * Mathf.Exp(-t * 12f)
                    + white * 0.5f * Mathf.Exp(-t * 40f)) * 0.8f;
            }
            clang = Clip("GatePlaceholder", ring, rate);

            // Heartbeat: two soft knocks, the second lighter. Kept above the range small speakers lose.
            float[] heart = new float[(int)(rate * 0.42f)];
            for (int i = 0; i < heart.Length; i++)
            {
                float s = (float)i / rate, value = 0;
                for (int k = 0; k < 2; k++)
                {
                    float u = s - k * 0.15f;
                    if (u < 0) continue;
                    float f = Mathf.Lerp(96f, 60f, Mathf.Clamp01(u / 0.1f));
                    value += (k == 0 ? 1f : 0.65f) * Mathf.Exp(-u * 27f)
                        * (Mathf.Sin(2f * Mathf.PI * f * u) + 0.4f * Mathf.Sin(4f * Mathf.PI * f * u));
                }
                heart[i] = value * 0.6f;
            }
            beat = Clip("HeartbeatPlaceholder", heart, rate);

            // One of ours out of breath: a quick pull in, a longer push out.
            float[] gasp = new float[(int)(rate * 0.62f)];
            float soft = 0, softer = 0;
            for (int i = 0; i < gasp.Length; i++)
            {
                float t = (float)i / gasp.Length;
                float white = (float)random.NextDouble() * 2f - 1f;
                soft += (white - soft) * 0.38f;
                softer += (white - softer) * 0.07f;
                float inward = t < 0.36f ? Mathf.Pow(Mathf.Sin(t / 0.36f * Mathf.PI), 2f) : 0f;
                float outward = t > 0.44f ? Mathf.Pow(Mathf.Sin((t - 0.44f) / 0.56f * Mathf.PI), 2f) : 0f;
                gasp[i] = ((soft - softer) * 1.1f + (white - soft) * 0.35f) * inward * 0.8f + (soft - softer) * 1.5f * outward;
            }
            pant = Clip("BreathPlaceholder", Level(gasp, 0.8f), rate);

            // One of them breathing: lower, wet, catching in the throat.
            float[] hoarse = new float[(int)(rate * 0.85f)];
            float chest = 0, deep = 0, throat = 0;
            for (int i = 0; i < hoarse.Length; i++)
            {
                float s = (float)i / rate, t = (float)i / hoarse.Length;
                float white = (float)random.NextDouble() * 2f - 1f;
                chest += (white - chest) * 0.13f;
                deep += (white - deep) * 0.016f;
                throat += 74f / rate;
                float rattle = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * 29f * s)), 3f);
                float saw = (throat % 1f) * 2f - 1f;
                hoarse[i] = ((chest - deep) * (0.45f + 0.9f * rattle) + saw * 0.12f * rattle) * Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.5f);
            }
            rasp = Clip("HostileBreathPlaceholder", Level(hoarse, 0.85f), rate);

            shriek = Clip("ScreamPlaceholder", Voice(rate, random, 1.05f, new[] { 520f, 940f, 900f, 820f }, new[] { 1050f, 1750f, 3100f }, 0.28f, 0.12f), rate);
            shout = Clip("ShoutPlaceholder", Voice(rate, random, 0.6f, new[] { 250f, 345f, 320f, 235f }, new[] { 720f, 1180f, 2600f }, 0.45f, 0.2f), rate);

            struckMetal = Clip("MetalBlowPlaceholder", Blow(rate, random, 0.32f, 150f, 70f, 30f,
                new[] { 1130f, 1870f, 2950f, 4210f }, new[] { 18f, 24f, 32f, 42f }, 0.24f, 0.5f, 200f), rate);
            struckWood = Clip("WoodBlowPlaceholder", Blow(rate, random, 0.2f, 210f, 120f, 38f,
                new[] { 640f }, new[] { 50f }, 0.3f, 0.6f, 90f), rate);
            struckPlastic = Clip("PlasticBlowPlaceholder", Blow(rate, random, 0.18f, 380f, 300f, 45f,
                new[] { 760f }, new[] { 60f }, 0.3f, 0.4f, 120f), rate);
        }

        // Scales a clip so its loudest sample sits at "peak".
        private static float[] Level(float[] samples, float peak)
        {
            float top = 0.0001f;
            foreach (float sample in samples) top = Mathf.Max(top, Mathf.Abs(sample));
            for (int i = 0; i < samples.Length; i++) samples[i] *= peak / top;
            return samples;
        }

        // A cry on an open vowel: a pitch that climbs fast and sags, its overtones shaped by three
        // resonances of the mouth, roughened, with breath in it.
        private static float[] Voice(int rate, System.Random random, float seconds, float[] pitch, float[] formants, float rough, float airy)
        {
            float[] samples = new float[(int)(rate * seconds)];
            float phase = 0, hiss = 0, drift = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float s = (float)i / rate, t = (float)i / samples.Length;
                // The first tenth is the climb; the rest follows the remaining pitch points evenly.
                float f0 = t < 0.1f ? Mathf.Lerp(pitch[0], pitch[1], t / 0.1f)
                    : t < 0.55f ? Mathf.Lerp(pitch[1], pitch[2], (t - 0.1f) / 0.45f) : Mathf.Lerp(pitch[2], pitch[3], (t - 0.55f) / 0.45f);
                float white = (float)random.NextDouble() * 2f - 1f;
                drift += (white - drift) * 0.002f;
                f0 *= 1f + 0.022f * Mathf.Sin(2f * Mathf.PI * 6.5f * s) + drift * 0.25f;
                phase += f0 / rate;
                float value = 0;
                for (int h = 1; h <= 12 && h * f0 < rate * 0.45f; h++)
                {
                    float f = h * f0, gain = 0.06f;
                    for (int k = 0; k < formants.Length; k++)
                    {
                        float off = (f - formants[k]) / (300f + formants[k] * 0.12f);
                        gain += (k == 0 ? 1f : k == 1 ? 0.8f : 0.55f) * Mathf.Exp(-off * off);
                    }
                    value += gain / Mathf.Sqrt(h) * Mathf.Sin(2f * Mathf.PI * h * phase);
                }
                value *= 1f - rough * (0.5f + 0.5f * Mathf.Sin(Mathf.PI * phase));
                hiss += (white - hiss) * 0.5f;
                value += (white - hiss) * airy;
                float envelope = Mathf.Clamp01(s / 0.03f) * Mathf.Clamp01((seconds - s) / 0.18f);
                samples[i] = value * envelope;
            }
            return Level(samples, 0.97f);
        }

        // Something hard landing: a low knock, the ring of whatever it is made of, and a click of contact.
        private static float[] Blow(int rate, System.Random random, float seconds, float from, float to, float damp,
            float[] partials, float[] decays, float ringing, float click, float clickDamp)
        {
            float[] samples = new float[(int)(rate * seconds)];
            for (int i = 0; i < samples.Length; i++)
            {
                float s = (float)i / rate, t = (float)i / samples.Length;
                float value = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(from, to, t) * s) * 0.7f * Mathf.Exp(-s * damp);
                for (int k = 0; k < partials.Length; k++)
                    value += Mathf.Sin(2f * Mathf.PI * partials[k] * s) * ringing * Mathf.Exp(-s * decays[k]);
                value += ((float)random.NextDouble() * 2f - 1f) * click * Mathf.Exp(-s * clickDamp);
                samples[i] = value;
            }
            return Level(samples, 0.9f);
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
            Crowd.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
