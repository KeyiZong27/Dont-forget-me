using System.Collections.Generic;
using UnityEngine;

namespace DontForgetMe
{
    public static class OpeningWorld
    {
        public static readonly Color Concrete = new Color(0.37f, 0.37f, 0.34f);
        public static readonly Color Warm = new Color(0.68f, 0.47f, 0.24f);
        private static readonly Color Stone = new Color(0.28f, 0.30f, 0.31f);
        private static readonly List<Material> Materials = new List<Material>();

        public static void ReleaseMaterials()
        {
            foreach (Material material in Materials)
                if (material != null) Object.Destroy(material);
            Materials.Clear();
        }

        public static GameObject Box(string name, Vector3 position, Vector3 scale, Color color,
            Transform parent = null, bool collision = true)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            Paint(item, color);
            if (!collision) item.GetComponent<Collider>().enabled = false;
            return item;
        }

        public static GameObject Shape(PrimitiveType type, string name, Vector3 position,
            Vector3 scale, Color color, Transform parent, bool collision = false)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            Paint(item, color);
            item.GetComponent<Collider>().enabled = collision;
            return item;
        }

        private static void Paint(GameObject item, Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("Built-in Standard shader is missing.");
            Material material = new Material(shader) { color = color };
            Materials.Add(material);
            item.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static Transform PersonModel(Transform root, Color coat)
        {
            Transform model = new GameObject("Body").transform;
            model.SetParent(root, false);
            Shape(PrimitiveType.Capsule, "Coat", new Vector3(0, 1, 0),
                new Vector3(0.60f, 0.55f, 0.48f), coat, model);
            Shape(PrimitiveType.Sphere, "Head", new Vector3(0, 1.68f, 0),
                Vector3.one * 0.35f, new Color(0.71f, 0.58f, 0.47f), model);
            Box("Backpack", new Vector3(0, 1.1f, -0.3f), new Vector3(0.43f, 0.52f, 0.22f),
                coat * 0.65f, model, false);
            Box("LeftLeg", new Vector3(-0.15f, 0.3f, 0), new Vector3(0.2f, 0.6f, 0.22f), Stone, model, false);
            Box("RightLeg", new Vector3(0.15f, 0.3f, 0), new Vector3(0.2f, 0.6f, 0.22f), Stone, model, false);
            return model;
        }

        public static ActorMotor Actor(string id, string label, Vector3 position, Color color, bool dog = false)
        {
            GameObject root = new GameObject(id);
            root.transform.position = position;
            root.layer = 8;
            Transform model;
            if (dog)
            {
                model = new GameObject("DogBody").transform;
                model.SetParent(root.transform, false);
                Box("Body", new Vector3(0, 0.42f, 0), new Vector3(0.42f, 0.42f, 0.8f), color, model, false);
                Box("Head", new Vector3(0, 0.65f, 0.4f), new Vector3(0.35f, 0.32f, 0.38f), color, model, false);
                Box("Muzzle", new Vector3(0, 0.57f, 0.65f), new Vector3(0.23f, 0.16f, 0.2f), Stone, model, false);
                for (int i = 0; i < 4; i++)
                    Box("Leg", new Vector3(i % 2 == 0 ? -0.14f : 0.14f, 0.15f, i < 2 ? -0.25f : 0.25f),
                        new Vector3(0.12f, 0.3f, 0.13f), color, model, false);
                Box("Tail", new Vector3(0, 0.62f, -0.48f), new Vector3(0.1f, 0.1f, 0.34f), color, model, false)
                    .transform.localRotation = Quaternion.Euler(-30, 0, 0);
            }
            else model = PersonModel(root.transform, color);
            ActorMotor actor = root.AddComponent<ActorMotor>();
            actor.Identity = id;
            actor.DisplayName = label;
            actor.Configure(dog, model);
            return actor;
        }

        public static void Build(DemoGame game)
        {
            RenderSettings.ambientLight = new Color(0.54f, 0.53f, 0.48f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.55f, 0.50f);
            RenderSettings.fogDensity = 0.012f;
            Camera camera = new GameObject("ThirdPersonCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.backgroundColor = RenderSettings.fogColor;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.farClipPlane = 130;
            camera.gameObject.AddComponent<AudioListener>();
            Light sun = new GameObject("LateAfternoon").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.87f, 0.71f);
            sun.transform.rotation = Quaternion.Euler(42, -35, 0);
            sun.shadows = LightShadows.Soft;

            Box("StreetGround", new Vector3(0, -0.2f, 12), new Vector3(22, 0.4f, 32), Stone);
            Box("AlleyGround", new Vector3(0, -0.2f, 41), new Vector3(6, 0.4f, 26), Concrete);
            Box("CourtyardGround", new Vector3(0, -0.2f, 63), new Vector3(18, 0.4f, 18), Concrete);
            Box("StreetWest", new Vector3(-11, 3, 12), new Vector3(1, 6, 33), Concrete);
            Box("StreetEast", new Vector3(11, 3, 12), new Vector3(1, 6, 33), Concrete);
            Box("StreetSouth", new Vector3(0, 1, -4), new Vector3(22, 2, 1), Concrete);
            Box("NorthLeft", new Vector3(-6.8f, 2, 28), new Vector3(7.6f, 4, 1), Concrete);
            Box("NorthRight", new Vector3(6.8f, 2, 28), new Vector3(7.6f, 4, 1), Concrete);
            Box("AlleyWest", new Vector3(-3.3f, 2.5f, 41), new Vector3(0.6f, 5, 26), Stone);
            Box("AlleyEast", new Vector3(3.3f, 2.5f, 41), new Vector3(0.6f, 5, 26), Stone);
            Box("CourtyardWest", new Vector3(-9, 2, 63), new Vector3(0.5f, 4, 18), Stone);
            Box("CourtyardEast", new Vector3(9, 2, 63), new Vector3(0.5f, 4, 18), Stone);
            Box("CourtyardEnd", new Vector3(0, 2, 72), new Vector3(18, 4, 0.5f), Stone);

            for (int i = 0; i < 5; i++)
            {
                float z = i * 6f;
                Box("RoadMark", new Vector3(0, 0.012f, z), new Vector3(0.13f, 0.02f, 2),
                    new Color(0.7f, 0.65f, 0.47f), null, false);
                Box("ShopShutter", new Vector3(10.42f, 1.25f, z), new Vector3(0.12f, 2.5f, 3.8f),
                    new Color(0.26f + i * 0.025f, 0.29f, 0.30f));
                Box("Window", new Vector3(-10.42f, 3.8f, z), new Vector3(0.12f, 1.2f, 2),
                    new Color(0.21f, 0.24f, 0.24f), null, false);
            }

            Box("WorkshopTable", new Vector3(-7.4f, 0.8f, 15), new Vector3(2.4f, 0.16f, 1.1f), Warm);
            Box("TableLeg", new Vector3(-8.2f, 0.4f, 15), new Vector3(0.15f, 0.8f, 0.7f), Stone);
            Box("TableLeg", new Vector3(-6.6f, 0.4f, 15), new Vector3(0.15f, 0.8f, 0.7f), Stone);
            GameObject chair = Box("LooseChair", new Vector3(-5.4f, 0.45f, 14),
                new Vector3(0.7f, 0.12f, 0.7f), Warm);
            Box("ChairBack", new Vector3(-5.4f, 0.85f, 13.7f), new Vector3(0.7f, 0.7f, 0.1f), Warm);
            for (int i = 0; i < 4; i++)
                Box("ChairLeg", new Vector3(-5.4f + (i % 2 == 0 ? -0.25f : 0.25f), 0.2f,
                    14 + (i < 2 ? -0.25f : 0.25f)), new Vector3(0.09f, 0.4f, 0.09f), Stone);

            GameObject cup = Shape(PrimitiveType.Cylinder, "EnamelCup", new Vector3(-7.2f, 0.99f, 15),
                new Vector3(0.22f, 0.12f, 0.22f), new Color(0.74f, 0.76f, 0.67f), null);
            GameObject bowl = Shape(PrimitiveType.Cylinder, "WaterBowl", new Vector3(2, 0.09f, 6),
                new Vector3(0.65f, 0.09f, 0.65f), new Color(0.45f, 0.50f, 0.51f), null);
            game.Water = Shape(PrimitiveType.Cylinder, "Water", new Vector3(2, 0.19f, 6),
                new Vector3(0.53f, 0.015f, 0.53f), new Color(0.26f, 0.47f, 0.53f), null);
            game.Water.SetActive(false);
            game.AddInteraction(bowl, "给狗倒水（可选）", () => game.State.Phase == OpeningPhase.Street && !game.State.GaveWater,
                game.GiveWater);
            game.AddInteraction(chair, "扶稳松动的椅子（可选）", () => game.State.Phase == OpeningPhase.Street,
                () => game.Caption("老周：响得烦。等我把这条腿垫好。", 5));
            game.AddInteraction(cup, "看看搪瓷杯（可选）", () => game.State.Phase == OpeningPhase.Street,
                () => game.Caption("杯沿掉了漆，旁边留着一圈旧水印。老周把它往工具旁挪了挪。", 5));

            Box("ParkedCar", new Vector3(6.9f, 0.65f, 15), new Vector3(2, 1.3f, 4.1f), new Color(0.34f, 0.4f, 0.4f));
            Box("CarRoof", new Vector3(6.9f, 1.45f, 15), new Vector3(1.7f, 0.6f, 2.2f), Stone);

            game.Gate = Box("JamGate", new Vector3(0, 1.35f, 48), new Vector3(2.8f, 2.7f, 0.18f), Stone);
            Box("GateLeft", new Vector3(-2.2f, 1.5f, 48), new Vector3(1.6f, 3, 0.35f), Concrete);
            Box("GateRight", new Vector3(2.2f, 1.5f, 48), new Vector3(1.6f, 3, 0.35f), Concrete);
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                GameObject debris = Box("Debris" + i, new Vector3(i == 0 ? -0.8f : 0.8f, 0.5f, 44),
                    new Vector3(1.25f, 1, 1.1f), Warm * 0.8f);
                game.AddInteraction(debris, "搬开挡路的杂物", () => game.State.Phase == OpeningPhase.Debris,
                    () => game.MoveDebris(debris, index));
            }

            GameObject player = new GameObject("Player");
            player.layer = 8;
            player.transform.position = new Vector3(0, 0.1f, 2);
            Transform playerBody = PersonModel(player.transform, new Color(0.23f, 0.32f, 0.38f));
            game.Player = player.AddComponent<ThirdPersonPlayer>();
            game.Player.Configure(playerBody, camera);
            game.View = camera;
            game.Woman = Actor("woman", "同行的她", new Vector3(2, 0.1f, 4), new Color(0.48f, 0.32f, 0.27f));
            game.Dog = Actor("dog", "狗", new Vector3(0.7f, 0.1f, 4), new Color(0.58f, 0.48f, 0.32f), true);
            game.Zhou = Actor("zhou", "老周", new Vector3(-6, 0.1f, 13), new Color(0.42f, 0.43f, 0.31f));
            game.ResidentA = Actor("residentA", "街坊", new Vector3(-1, 0.1f, 20), new Color(0.43f, 0.40f, 0.4f));
            game.ResidentB = Actor("residentB", "街坊", new Vector3(1.5f, 0.1f, 18), new Color(0.32f, 0.4f, 0.39f));
            game.Dog.FollowTarget = game.Woman.transform;
            game.Dog.FollowOffset = new Vector3(-0.9f, 0, -0.4f);
            game.AddInteraction(game.Zhou.gameObject, "和修理铺的人说话",
                () => game.State.Phase == OpeningPhase.Street, game.MeetZhou);
            game.AddInteraction(game.Woman.gameObject, "确认她和狗都平安",
                () => game.State.Phase == OpeningPhase.Reunion, game.Reunite);
            game.AddInteraction(game.Zhou.gameObject, "帮老周扶住门，开始修理",
                () => game.State.Phase == OpeningPhase.Debris && game.State.ClearedDebrisCount == 2, game.StartRepair);
            game.AddInteraction(game.Zhou.gameObject, "确认大家都跟上了",
                () => game.State.Phase == OpeningPhase.Regroup, game.Finish);
        }
    }
}
