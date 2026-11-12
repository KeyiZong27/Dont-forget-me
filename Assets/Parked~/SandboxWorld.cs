using UnityEngine;

namespace DontForgetMe
{
    public static class SandboxWorld
    {
        private static readonly Color Grass = new Color(0.31f, 0.38f, 0.27f);
        private static readonly Color Wood = new Color(0.41f, 0.29f, 0.18f);
        private static readonly Color Metal = new Color(0.36f, 0.4f, 0.42f);

        public static void Build(SandboxGame game)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.003f;
            game.View = new GameObject("SandboxCamera").AddComponent<Camera>();
            game.View.tag = "MainCamera";
            game.View.clearFlags = CameraClearFlags.SolidColor;
            game.View.farClipPlane = 450;
            game.View.gameObject.AddComponent<AudioListener>();
            game.Sun = new GameObject("SandboxSun").AddComponent<Light>();
            game.Sun.type = LightType.Directional;
            game.Sun.shadows = LightShadows.Soft;
            game.Sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            game.CampLight = new GameObject("CampLight").AddComponent<Light>();
            game.CampLight.type = LightType.Point;
            game.CampLight.transform.position = new Vector3(-5, 3, 5);
            game.CampLight.range = 24;
            game.CampLight.intensity = 3;
            game.CampLight.color = new Color(1f, 0.75f, 0.4f);

            OpeningWorld.Box("300mGround", new Vector3(0, -0.5f, 0), new Vector3(300, 1, 300), Grass);
            OpeningWorld.Box("NorthBoundary", new Vector3(0, 2, 150), new Vector3(301, 4, 1), Metal);
            OpeningWorld.Box("SouthBoundary", new Vector3(0, 2, -150), new Vector3(301, 4, 1), Metal);
            OpeningWorld.Box("EastBoundary", new Vector3(150, 2, 0), new Vector3(1, 4, 300), Metal);
            OpeningWorld.Box("WestBoundary", new Vector3(-150, 2, 0), new Vector3(1, 4, 300), Metal);
            OpeningWorld.Box("EastWestTrail", new Vector3(0, 0.008f, 0), new Vector3(296, 0.015f, 5),
                new Color(0.48f, 0.43f, 0.33f), null, false);
            OpeningWorld.Box("NorthSouthTrail", new Vector3(0, 0.009f, 0), new Vector3(5, 0.015f, 296),
                new Color(0.48f, 0.43f, 0.33f), null, false);
            BuildCamp(game);
            BuildCombatArea(game);
            BuildRuins();
            BuildForest();

            GameObject player = new GameObject("SandboxPlayer") { layer = 8 };
            player.transform.position = new Vector3(0, 0.1f, -4);
            Transform body = OpeningWorld.PersonModel(player.transform, PersonLook.XuChuan, Color.gray);
            game.Player = player.AddComponent<ThirdPersonPlayer>();
            game.Player.Configure(body, game.View);
            ActorMotor observer = OpeningWorld.Actor("memory_observer", "记忆观察员",
                new Vector3(5, 0, 0), new Color(0.35f, 0.55f, 0.48f));
            game.Observer = observer.gameObject.AddComponent<SandboxObserver>();
            game.Observer.Configure(observer);
            game.Targets.Add(game.Observer.Target);
            game.AddAction(observer.gameObject, "打开记忆观察面板（M）", game.ToggleObservation);
            game.Companions.Add(OpeningWorld.Actor("woman", "林禾", new Vector3(-3, 0, -3),
                Color.gray, false, PersonLook.LinHe));
            game.Companions.Add(OpeningWorld.Actor("zhou", "老周", new Vector3(-6, 0, -3),
                Color.gray, false, PersonLook.LaoZhou));
            game.Companions.Add(OpeningWorld.Actor("dog", "豆子", new Vector3(3, 0, -3),
                new Color(0.6f, 0.48f, 0.3f), true));
            for (int i = 0; i < game.Companions.Count; i++)
            {
                ActorMotor actor = game.Companions[i];
                game.AddAction(actor.gameObject, "辨认 / 切换" + actor.DisplayName + "跟随",
                    () => game.ToggleCompanion(actor));
            }
            game.SpawnItem(SandboxItem.Axe, new Vector3(2, 0, 3));
            game.SpawnItem(SandboxItem.Club, new Vector3(4, 0, 3));
            game.SpawnBackpack(new SandboxBackpack(), new Vector3(-2, 0, 3));
            game.SpawnBackpack(new SandboxBackpack(4), new Vector3(-4, 0, 3));
            for (int i = 0; i < 8; i++)
                game.SpawnItem(SandboxItem.Supply, new Vector3(-8 + i * 1.5f, 0, 9));
            game.SetNight(false);
        }

        private static void BuildCamp(SandboxGame game)
        {
            OpeningWorld.Box("CampCanopy", new Vector3(-6, 3.4f, 5), new Vector3(9, 0.2f, 8),
                new Color(0.48f, 0.4f, 0.28f));
            foreach (float x in new[] { -10f, -2f })
                foreach (float z in new[] { 1.5f, 8.5f })
                    OpeningWorld.Box("CanopyPost", new Vector3(x, 1.6f, z), new Vector3(0.16f, 3.2f, 0.16f), Wood);
            GameObject bed = OpeningWorld.Box("RestBed", new Vector3(-8, 0.25f, 6),
                new Vector3(1.3f, 0.5f, 2.4f), new Color(0.46f, 0.44f, 0.32f));
            game.AddAction(bed, "休息至清晨（恢复状态，记忆少量衰减）", game.Rest);
            GameObject switcher = OpeningWorld.Box("DayNightSwitch", new Vector3(-10, 0.7f, -1),
                new Vector3(0.8f, 1.4f, 0.8f), new Color(0.75f, 0.64f, 0.31f));
            game.AddAction(switcher, "切换昼夜", () => game.SetNight(!game.IsNight));
            GameObject reset = OpeningWorld.Box("CombatResetConsole", new Vector3(10, 0.7f, -2),
                new Vector3(1, 1.4f, 1), new Color(0.25f, 0.5f, 0.6f));
            game.AddAction(reset, "重置战斗目标", game.ResetTargets);
            GameObject enemySwitch = OpeningWorld.Box("EnemySwitch", new Vector3(13, 0.7f, -2),
                new Vector3(1, 1.4f, 1), new Color(0.6f, 0.27f, 0.22f));
            game.AddAction(enemySwitch, "开关敌人攻击（初始关闭）", game.ToggleEnemies);
        }

        private static void BuildCombatArea(SandboxGame game)
        {
            OpeningWorld.Box("TrainingGround", new Vector3(26, 0.013f, 10), new Vector3(32, 0.02f, 24),
                new Color(0.48f, 0.45f, 0.37f), null, false);
            for (int i = 0; i < 3; i++)
            {
                GameObject root = new GameObject("TrainingDummy") { layer = 8 };
                root.transform.position = new Vector3(17 + i * 4, 0, 5);
                Transform body = new GameObject("DummyVisual").transform;
                body.SetParent(root.transform, false);
                OpeningWorld.Box("Post", new Vector3(0, 0.9f, 0), new Vector3(0.25f, 1.8f, 0.25f), Wood, body);
                OpeningWorld.Box("Target", new Vector3(0, 1.3f, 0), new Vector3(0.8f, 0.8f, 0.5f),
                    new Color(0.63f, 0.41f, 0.26f), body);
                SandboxTarget target = root.AddComponent<SandboxTarget>();
                target.Configure(null, body);
                game.Targets.Add(target);
            }
            for (int i = 0; i < 2; i++)
            {
                ActorMotor actor = OpeningWorld.Actor("sandbox_enemy_" + i, "测试异常者",
                    new Vector3(26 + i * 6, 0, 15), new Color(0.55f, 0.2f, 0.17f));
                actor.Speed = 1.8f;
                SandboxTarget target = actor.gameObject.AddComponent<SandboxTarget>();
                target.Configure(actor, actor.transform.GetChild(0));
                game.Targets.Add(target);
            }
        }

        private static void BuildRuins()
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(45 + i % 2 * 24, 0, 45 + i / 2 * 24);
                OpeningWorld.Box("RuinsFloor", p + Vector3.up * 0.015f, new Vector3(15, 0.02f, 16),
                    OpeningWorld.Concrete, null, false);
                OpeningWorld.Box("RuinsBack", p + new Vector3(0, 2, 8), new Vector3(16, 4, 0.4f), OpeningWorld.Concrete);
                OpeningWorld.Box("RuinsSide", p + new Vector3(-8, 1.5f, 0), new Vector3(0.4f, 3, 16), OpeningWorld.Concrete);
                OpeningWorld.Box("DoorLeft", p + new Vector3(-5, 1.5f, -8), new Vector3(6, 3, 0.4f), OpeningWorld.Concrete);
                OpeningWorld.Box("DoorRight", p + new Vector3(5, 1.5f, -8), new Vector3(6, 3, 0.4f), OpeningWorld.Concrete);
                OpeningWorld.Box("Crate", p + new Vector3(-4, 0.5f, 4), Vector3.one, Wood);
            }
            for (int i = 0; i < 5; i++)
                OpeningWorld.Box("StepTest", new Vector3(13 + i * 1.3f, 0.1f + i * 0.08f, -10),
                    new Vector3(1.1f, 0.2f + i * 0.16f, 3), OpeningWorld.Concrete);
        }

        private static void BuildForest()
        {
            System.Random random = new System.Random(2718);
            for (int i = 0; i < 120; i++)
            {
                float x = -135 + (float)random.NextDouble() * 105;
                float z = -130 + (float)random.NextDouble() * 260;
                if (Mathf.Abs(z) < 7) continue;
                float height = 4 + (float)random.NextDouble() * 3;
                OpeningWorld.Shape(PrimitiveType.Cylinder, "TreeTrunk", new Vector3(x, height / 2, z),
                    new Vector3(0.55f, height / 2, 0.55f), Wood, null, true);
                OpeningWorld.Shape(PrimitiveType.Sphere, "TreeCrown", new Vector3(x, height, z),
                    new Vector3(4, 4, 4), new Color(0.2f, 0.3f, 0.18f), null);
            }
            for (int i = 0; i < 35; i++)
            {
                Vector3 p = new Vector3(-130 + (float)random.NextDouble() * 260, 0.5f,
                    -130 + (float)random.NextDouble() * 85);
                if (Mathf.Abs(p.x) < 10) continue;
                OpeningWorld.Shape(PrimitiveType.Sphere, "Rock", p, new Vector3(3, 1.8f, 2),
                    new Color(0.4f, 0.41f, 0.39f), null, true);
            }
        }

        public static Transform ItemVisual(SandboxItem item, Transform parent)
        {
            Transform root = new GameObject(SandboxItems.Name(item)).transform;
            root.SetParent(parent, false);
            if (item == SandboxItem.Supply)
            {
                OpeningWorld.Box("SupplyBox", new Vector3(0, 0.2f, 0), new Vector3(0.35f, 0.4f, 0.3f),
                    new Color(0.3f, 0.6f, 0.65f), root, false);
            }
            else
            {
                OpeningWorld.Box("Handle", new Vector3(0, 0.3f, 0), new Vector3(0.08f, 0.85f, 0.08f), Wood, root, false);
                if (item == SandboxItem.Axe)
                    OpeningWorld.Box("AxeHead", new Vector3(0.12f, 0.66f, 0), new Vector3(0.4f, 0.22f, 0.12f),
                        Metal, root, false);
                else
                    OpeningWorld.Box("ClubHead", new Vector3(0, 0.6f, 0), new Vector3(0.16f, 0.38f, 0.16f),
                        Wood, root, false);
            }
            return root;
        }

        public static Transform BackpackVisual(Transform parent)
        {
            Transform root = new GameObject("BackpackVisual").transform;
            root.SetParent(parent, false);
            OpeningWorld.Box("Bag", Vector3.up * 0.3f, new Vector3(0.55f, 0.6f, 0.25f),
                new Color(0.38f, 0.4f, 0.25f), root, false);
            OpeningWorld.Box("BagPocket", new Vector3(0, 0.2f, -0.16f), new Vector3(0.4f, 0.25f, 0.12f),
                new Color(0.29f, 0.31f, 0.19f), root, false);
            for (int side = -1; side <= 1; side += 2)
                OpeningWorld.Box("Strap", new Vector3(side * 0.2f, 0.35f, 0.14f),
                    new Vector3(0.06f, 0.5f, 0.05f), Wood, root, false);
            return root;
        }
    }
}
