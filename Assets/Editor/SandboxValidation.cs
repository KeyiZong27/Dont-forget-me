using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontForgetMe.Editor
{
    [InitializeOnLoad]
    public static class SandboxValidation
    {
        private const string RunningKey = "DontForgetMe.SandboxValidation.Running";
        private const string StageKey = "DontForgetMe.SandboxValidation.Stage";
        private static double deadline;
        private static double nextStep;

        static SandboxValidation()
        {
            if (SessionState.GetBool(RunningKey, false)) StartWatching();
        }

        [MenuItem("Dont Forget Me/Validate Sandbox (Play Mode)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before running sandbox validation.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetInt(StageKey, 0);
            SessionState.SetBool(RunningKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
            StartWatching();
            EditorApplication.isPlaying = true;
        }

        private static void StartWatching()
        {
            deadline = EditorApplication.timeSinceStartup + 90;
            nextStep = EditorApplication.timeSinceStartup + 1.5;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                int stage = SessionState.GetInt(StageKey, 0);
                if (stage == 6)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    SessionState.SetBool(RunningKey, false);
                    EditorApplication.update -= Tick;
                    Debug.Log("SANDBOX VALIDATION PASSED: real Play-mode forgetting/appearance/hostility/protection, inventory, combat, obstruction, death/recovery and scene isolation.");
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                    return;
                }
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Sandbox Play-mode validation timed out at stage " + stage);
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextStep) return;
                SandboxGame game = SandboxGame.Instance;
                switch (stage)
                {
                    case 0:
                        if (game == null) return;
                        ValidateForgetting(game);
                        ValidateEquipmentAndWorld(game);
                        ValidateFirstAttack(game);
                        game.Observer.State.AutoDecay = true;
                        game.ToggleObservation();
                        Advance(20);
                        break;
                    case 20:
                        Check(game.Observer.State.PlayerMemory < 0.12f && game.Observer.State.PlayerMemory > 0,
                            "Real Update did not slowly decay memory while the observation panel was open.");
                        game.Observer.SetPlayerMemory(0);
                        Advance(21);
                        break;
                    case 21:
                        Check(game.Observer.State.Progress > 0 && game.Observer.State.Progress < 1,
                            "Real Update did not animate the forgotten actor's transformation.");
                        Check(game.Observer.transform.GetChild(0).localRotation != Quaternion.identity,
                            "Real LateUpdate did not change the actor's posture.");
                        game.Observer.SetWitnessMemory(1);
                        Check(game.Observer.State.Progress == 0 && !game.Observer.Target.IsEnemy,
                            "Witness did not cancel an in-progress transformation.");
                        game.Observer.ResetObservation();
                        game.ToggleObservation();
                        Advance(1);
                        break;
                    case 1:
                        ValidateObstruction(game);
                        Advance(11);
                        break;
                    case 11:
                        game.Player.Model.rotation = Quaternion.Euler(0, 180, 0);
                        game.SendMessage("Attack", SendMessageOptions.RequireReceiver);
                        Check(game.Targets[0].Health.Value == 65, "Weapon hit a target behind the player.");
                        Advance(12);
                        break;
                    case 12:
                        MovePlayer(game, game.Targets[0].transform.position + new Vector3(0, 0.1f, -2.5f));
                        game.SendMessage("Attack", SendMessageOptions.RequireReceiver);
                        Check(game.Targets[0].Health.Value == 65, "Weapon hit a target outside 2.4m.");
                        Advance(2);
                        break;
                    case 2:
                        ValidateRecoveryAndMemory(game);
                        MovePlayer(game, game.Targets.First(item => item.IsEnemy).transform.position + new Vector3(0, 0.1f, -1.1f));
                        Advance(10);
                        break;
                    case 10:
                        if (!game.IsFailed) return;
                        Check(game.Player.Health == 0 && Time.timeScale == 0, "Enemy damage did not cause actual death.");
                        SandboxBackpack bag = game.Equipment.Backpack;
                        game.ReturnToCamp();
                        Check(!game.IsFailed && !game.EnemiesEnabled && Time.timeScale == 1, "Recovery did not resume simulation.");
                        Check(game.Equipment.Backpack == bag && bag.Contents.Count == 4 && game.Equipment.Weapon == SandboxItem.Axe,
                            "Recovery lost equipped inventory.");
                        game.LoadScene("Opening");
                        Advance(3);
                        break;
                    case 3:
                        if (DemoGame.Instance == null) return;
                        Check(SandboxGame.Instance == null, "Sandbox singleton leaked into Opening.");
                        Check(ReferenceEquals(PlayerWorld.Current, DemoGame.Instance), "Opening does not own player control.");
                        Check(DemoGame.Instance.State.Phase == OpeningPhase.Street, "Original opening phase changed.");
                        Check(DemoGame.Instance.Journey.Phase == JourneyPhase.Calls, "Original journey initial state changed.");
                        DemoGame.Instance.Begin();
                        DemoGame.Instance.Restart();
                        Advance(4);
                        break;
                    case 4:
                        if (DemoGame.Instance == null) return;
                        Check(DemoGame.Instance.IsPaused, "Restart did not create a fresh opening.");
                        SceneManager.LoadScene("Sandbox");
                        Advance(5);
                        break;
                    case 5:
                        if (game == null) return;
                        Check(DemoGame.Instance == null && ReferenceEquals(PlayerWorld.Current, game), "Story state leaked into sandbox.");
                        Check(game.Equipment.Backpack == null && !game.Equipment.Weapon.HasValue, "Fresh sandbox retained old equipment.");
                        Check(game.Targets.Count == 6 && game.Companions.Count == 3, "Scene reentry duplicated actors.");
                        Check(ActivePickups().Length == 12, "Scene reentry duplicated or lost pickups.");
                        SessionState.SetInt(StageKey, 6);
                        EditorApplication.isPlaying = false;
                        break;
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(RunningKey, false);
                EditorApplication.update -= Tick;
                Debug.LogException(exception);
                EditorApplication.isPlaying = false;
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void Advance(int stage)
        {
            SessionState.SetInt(StageKey, stage);
            nextStep = EditorApplication.timeSinceStartup + 1.2;
        }

        private static SandboxPickup[] ActivePickups()
        {
            return UnityEngine.Object.FindObjectsByType<SandboxPickup>()
                .Where(item => item.gameObject.activeInHierarchy).ToArray();
        }

        private static void Pick(SandboxPickup pickup)
        {
            pickup.GetComponent<Interactable>().OnInteract();
        }

        private static void MovePlayer(SandboxGame game, Vector3 position)
        {
            game.Player.Respawn(position);
            game.Player.Model.rotation = Quaternion.identity;
            Physics.SyncTransforms();
        }

        private static void ValidateEquipmentAndWorld(SandboxGame game)
        {
            Check(DemoGame.Instance == null && ReferenceEquals(PlayerWorld.Current, game), "Sandbox started story systems.");
            Check(!game.EnemiesEnabled && game.Targets.Count == 6 && game.Companions.Count == 3, "Unexpected test population.");
            Check(GameObject.Find("300mGround").transform.localScale == new Vector3(300, 1, 300), "World is not 300 by 300 metres.");
            Check(ActivePickups().Length == 12, "Initial pickups missing.");
            SandboxPickup axe = ActivePickups().Single(item => item.Item == SandboxItem.Axe);
            MovePlayer(game, new Vector3(2, 0.1f, 1.8f));
            game.SendMessage("UpdateInteraction", SendMessageOptions.RequireReceiver);
            Check(game.Nearest == axe.GetComponent<Interactable>(), "Nearby axe is not selected for interaction.");
            GameObject wall = OpeningWorld.Box("ValidationPickupWall", new Vector3(2, 1, 2.4f),
                new Vector3(1, 2, 0.2f), Color.gray);
            Physics.SyncTransforms();
            game.SendMessage("UpdateInteraction", SendMessageOptions.RequireReceiver);
            Check(game.Nearest != axe.GetComponent<Interactable>(), "Interaction can select a pickup through a wall.");
            wall.GetComponent<Collider>().enabled = false;
            UnityEngine.Object.Destroy(wall);
            MovePlayer(game, new Vector3(2, 0.1f, -1));
            game.SendMessage("UpdateInteraction", SendMessageOptions.RequireReceiver);
            Check(game.Nearest != axe.GetComponent<Interactable>(), "Interaction can select a distant pickup.");
            Pick(axe);
            Pick(ActivePickups().First(item => item.Backpack != null && item.Backpack.Capacity == 6));
            SandboxBackpack bag = game.Equipment.Backpack;
            Check(game.Equipment.Weapon == SandboxItem.Axe && bag != null, "Pickup callbacks did not equip items.");
            Check(game.Player.Model.Find("斧头") != null && game.Player.Model.Find("BackpackVisual") != null,
                "Equipment visuals were not attached to the player.");
            game.StoreWeapon();
            Check(!game.Equipment.Weapon.HasValue && bag.Contents.Single() == SandboxItem.Axe, "Weapon was not stored.");
            game.DropBackpack();
            Check(game.Equipment.Backpack == null, "Backpack was not dropped.");
            Pick(ActivePickups().Single(item => item.Backpack == bag));
            Check(game.Equipment.Backpack == bag && bag.Contents.Single() == SandboxItem.Axe, "Dropped backpack lost its contents.");
            game.UseSlot(0);
            Check(game.Equipment.Weapon == SandboxItem.Axe && bag.Contents.Count == 0, "Drawing a weapon lost or duplicated it.");
            game.DropWeapon();
            Pick(ActivePickups().Single(item => item.Item == SandboxItem.Axe));
            Check(game.Equipment.Weapon == SandboxItem.Axe, "Dropped weapon cannot be picked up again.");
            foreach (SandboxPickup supply in ActivePickups().Where(item => item.Item == SandboxItem.Supply).Take(6)) Pick(supply);
            SandboxPickup overflow = ActivePickups().First(item => item.Item == SandboxItem.Supply);
            Pick(overflow);
            Check(bag.Contents.Count == 6 && overflow.gameObject.activeSelf, "Full backpack lost or consumed the overflow item.");
            game.UseSlot(0);
            Check(bag.Contents.Count == 5, "Using a supply did not remove exactly one item.");
            game.DropSlot(0);
            Check(bag.Contents.Count == 4, "Dropping a slot did not remove exactly one item.");
            game.DropBackpack();
            Pick(ActivePickups().Single(item => item.Backpack == bag));
            Check(bag.Contents.Count == 4 && game.Equipment.Backpack == bag, "Backpack contents changed on repeated drop.");
            MovePlayer(game, new Vector3(0, 0.1f, 148));
            game.Player.GetComponent<CharacterController>().Move(new Vector3(0, 0, 5));
            Check(game.Player.transform.position.z < 150, "Player can walk through map boundary.");
        }

        private static void ValidateFirstAttack(SandboxGame game)
        {
            SandboxTarget target = game.Targets[0];
            MovePlayer(game, target.transform.position + new Vector3(0, 0.1f, -2.3f));
            float stamina = game.Player.Stamina;
            game.SendMessage("Attack", SendMessageOptions.RequireReceiver);
            Check(target.Health.Value == 65, "Axe did not deal exactly 35 damage at 2.3m.");
            Check(Mathf.Approximately(game.Player.Stamina, stamina - 18), "Axe attack did not spend 18 stamina.");
            game.SendMessage("Attack", SendMessageOptions.RequireReceiver);
            Check(target.Health.Value == 65, "Cooldown allowed an immediate duplicate hit.");
        }

        private static void ValidateForgetting(SandboxGame game)
        {
            SandboxObserver observer = game.Observer;
            Check(observer.State.IsProtected && !observer.Target.CanBeAttacked, "Test friend starts hostile.");
            observer.Target.Hit(35);
            Check(observer.Target.Health.Value == 100, "Friendly test actor can be damaged.");
            observer.SetPlayerMemory(0);
            observer.State.Tick(6);
            observer.ApplyState();
            Check(observer.State.Progress == 0.5f && !observer.Target.IsEnemy, "Mid-transition became hostile early.");
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            Renderer renderer = observer.GetComponentInChildren<Renderer>();
            renderer.GetPropertyBlock(block);
            Check(block.GetColor("_Color") != renderer.sharedMaterial.color, "Transition did not change renderer color.");
            observer.State.Tick(6);
            observer.ApplyState();
            Check(observer.Target.IsEnemy && observer.Target.CanBeAttacked, "Forgotten actor cannot become an attackable enemy.");
            observer.Target.Hit(35);
            Check(observer.Target.Health.Value == 65, "Transformed actor cannot take weapon damage.");
            observer.SetWitnessMemory(1);
            Check(!observer.Target.IsEnemy && observer.State.Progress == 0, "External witness failed to restore protection.");
            observer.ResetObservation();
            Check(observer.Target.Health.Value == 100 && !observer.Target.CanBeAttacked, "Experiment reset failed.");
            game.ToggleObservation();
            Check(!game.IsPaused && Cursor.lockState == CursorLockMode.None, "Live observation panel pauses the experiment.");
            game.ToggleObservation();
        }

        private static void ValidateObstruction(SandboxGame game)
        {
            SandboxTarget target = game.Targets[0];
            MovePlayer(game, target.transform.position + new Vector3(0, 0.1f, -2.3f));
            GameObject wall = OpeningWorld.Box("ValidationAttackWall", target.transform.position + new Vector3(0, 1.5f, -1.1f),
                new Vector3(3, 3, 0.3f), Color.gray);
            Physics.SyncTransforms();
            game.SendMessage("Attack", SendMessageOptions.RequireReceiver);
            Check(target.Health.Value == 65, "Weapon hit through a wall.");
            wall.GetComponent<Collider>().enabled = false;
            UnityEngine.Object.Destroy(wall);
        }

        private static void ValidateRecoveryAndMemory(SandboxGame game)
        {
            SandboxTarget target = game.Targets[0];
            game.Player.SpendStamina(game.Player.Stamina);
            game.SendMessage("Attack", SendMessageOptions.RequireReceiver);
            Check(target.Health.Value == 65, "Weapon attacked without enough stamina.");
            game.ResetTargets();
            Check(game.Targets.All(item => item.Health.Value == 100), "Combat reset failed.");
            game.SetNight(true);
            Check(game.CampLight.enabled && game.IsNight, "Night lighting did not activate.");
            game.Rest();
            Check(!game.IsNight && game.Player.Energy == 100 && game.Player.Stamina == 100, "Rest did not recover state.");
            Check(Mathf.Approximately(game.Memories.Strength("player", "dog"), 0.92f), "Sleep did not decay directed memory by .08.");
            ActorMotor dog = game.Companions.Single(actor => actor.Identity == "dog");
            game.ToggleCompanion(dog);
            Check(dog.FollowTarget == game.Player.transform && game.Memories.Strength("dog", "player") == 1, "Recognition/follow failed.");
            game.ToggleCompanion(dog);
            Check(dog.FollowTarget == null, "Companion cannot wait.");
            SandboxTarget enemy = game.Targets.First(item => item.IsEnemy);
            MovePlayer(game, enemy.transform.position + new Vector3(0, 0.1f, -2));
            Vector3 enemyPosition = enemy.transform.position;
            Check(game.PushThreat(), "Nearby enemy cannot be pushed.");
            Check(Vector3.Distance(enemy.transform.position, enemyPosition) > 1, "Push did not physically move the enemy.");
            game.ToggleEnemies();
            Check(game.EnemiesEnabled, "Enemies cannot be enabled.");
            game.Player.TakeHit();
            Check(game.Player.Health == 88, "Incoming hit did not deal 12 damage.");
            game.Player.TakeHit();
            Check(game.Player.Health == 88, "Incoming hit cooldown failed.");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("SANDBOX VALIDATION FAILED: " + message);
        }
    }
}
