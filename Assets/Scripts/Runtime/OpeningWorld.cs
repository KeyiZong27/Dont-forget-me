using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DontForgetMe
{
    // Builds the old market street, the repair stall, the back alley and the courtyard.
    // Walkable space and blocking walls match the original grey-box layout exactly; everything
    // else here is dressing on top of it.
    public static class OpeningWorld
    {
        private const float Skin = 0.3f;
        private const float FloorHeight = 3.1f;
        private static Transform root;
        private static Material trim, darkMetal, greenPaint, greyPaint, glass, roofing, curb;

        private static readonly string[] SignColours = { "8C2B22", "1F4E79", "2E6B4A", "D9D2C0", "B8862B", "5B3A6B" };
        private static readonly Color[] CurtainColours =
        {
            new Color(0.78f, 0.72f, 0.60f), new Color(0.62f, 0.44f, 0.42f), new Color(0.45f, 0.55f, 0.60f),
            new Color(0.80f, 0.78f, 0.74f), new Color(0.50f, 0.56f, 0.42f)
        };

        private static Material S(string id)
        {
            return Art.Surface(id);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        private static Transform Group(string name, Vector3 position, float yaw, Transform parent = null)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent != null ? parent : root, false);
            group.localPosition = position;
            group.localRotation = Quaternion.Euler(0, yaw, 0);
            return group;
        }

        private static void Blocker(string name, Vector3 center, Vector3 size)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(root, false);
            item.transform.position = center;
            item.AddComponent<BoxCollider>().size = size;
        }

        private static void NoShadow(GameObject item)
        {
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        public static void SetLayer(GameObject item, int layer)
        {
            foreach (Transform child in item.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }

        public static ActorMotor Actor(string id, string label, Vector3 position, string character,
            string idle = null, string walk = null, string run = null, string sprint = null)
        {
            GameObject actorRoot = new GameObject(id);
            actorRoot.transform.position = position;
            Transform model = Model(actorRoot.transform, character, idle, walk, run, sprint);
            SetLayer(actorRoot, 8);
            ActorMotor actor = actorRoot.AddComponent<ActorMotor>();
            actor.Identity = id;
            actor.DisplayName = label;
            actor.Configure(character == "Dog", model);
            return actor;
        }

        private static Transform Model(Transform parent, string character, string idle, string walk, string run, string sprint)
        {
            GameObject model = Object.Instantiate(Art.Get<GameObject>("char/" + character), parent, false);
            model.name = "Body";
            model.transform.localPosition = new Vector3(0, -0.03f, 0);
            if (character == "Dog") model.AddComponent<DogAnimator>().Configure(model.transform);
            else model.AddComponent<CharacterAnimator>().Configure(model.GetComponent<Animator>(), idle, walk, run, sprint);
            return model.transform;
        }

        public static void Build(DemoGame game)
        {
            Art.Load();
            shelves = null;
            System.Array.Clear(Garments, 0, Garments.Length);
            root = new GameObject("World").transform;
            trim = Art.Tint("surf/concrete_wall_008", new Color(0.86f, 0.85f, 0.82f));
            curb = Art.Tint("surf/concrete_wall_008", new Color(0.74f, 0.74f, 0.72f));
            darkMetal = Art.Metal(new Color(0.16f, 0.17f, 0.18f), 0.45f);
            greenPaint = Art.Tint("surf/green_metal_rust", Color.white);
            greyPaint = Art.Plain(new Color(0.55f, 0.56f, 0.55f), 0.35f);
            glass = Art.Base("glass");
            roofing = Art.Plain(new Color(0.16f, 0.16f, 0.17f), 0.15f);

            Camera camera = new GameObject("ThirdPersonCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<AudioListener>();
            Atmosphere atmosphere = new GameObject("Atmosphere").AddComponent<Atmosphere>();
            atmosphere.Build(camera);
            atmosphere.AddProbe(new Vector3(0, 4f, 4f), new Vector3(21f, 16f, 16f));
            atmosphere.AddProbe(new Vector3(0, 4f, 20f), new Vector3(21f, 16f, 16f));
            atmosphere.AddProbe(new Vector3(0, 4f, 41f), new Vector3(6f, 14f, 26f));
            atmosphere.AddProbe(new Vector3(0, 4f, 63f), new Vector3(17.5f, 14f, 18f));

            BuildGround();
            BuildBlockers();
            BuildStreet();
            BuildWorkshop(game);
            BuildStreetLife(game);
            BuildAlley(game);
            BuildCourtyard();
            BuildBackdrop();
            BuildActors(game, camera);
        }

        private static void BuildBlockers()
        {
            Blocker("StreetWest", new Vector3(-11, 3, 12), new Vector3(1, 6, 33));
            Blocker("StreetEast", new Vector3(11, 3, 12), new Vector3(1, 6, 33));
            Blocker("StreetSouth", new Vector3(0, 3, -4), new Vector3(22, 6, 1));
            Blocker("NorthLeft", new Vector3(-6.8f, 3, 28), new Vector3(7.6f, 6, 1));
            Blocker("NorthRight", new Vector3(6.8f, 3, 28), new Vector3(7.6f, 6, 1));
            Blocker("AlleyWest", new Vector3(-3.3f, 3, 41), new Vector3(0.6f, 6, 26));
            Blocker("AlleyEast", new Vector3(3.3f, 3, 41), new Vector3(0.6f, 6, 26));
            Blocker("CourtyardWest", new Vector3(-9, 3, 63), new Vector3(0.5f, 6, 18));
            Blocker("CourtyardEast", new Vector3(9, 3, 63), new Vector3(0.5f, 6, 18));
            Blocker("CourtyardEnd", new Vector3(0, 3, 72), new Vector3(18, 6, 0.5f));
            Blocker("CourtyardSouthLeft", new Vector3(-6.15f, 3, 54.1f), new Vector3(5.7f, 6, 0.4f));
            Blocker("CourtyardSouthRight", new Vector3(6.15f, 3, 54.1f), new Vector3(5.7f, 6, 0.4f));
        }

        private static void BuildGround()
        {
            Material asphalt = Art.Tint("surf/asphalt_02", new Color(0.74f, 0.74f, 0.76f));
            Material pavers = Art.Tint("surf/square_concrete_pavers", new Color(0.58f, 0.64f, 0.72f));
            Kit.Box("Road", new Vector3(1.9f, -0.25f, 12), new Vector3(12.8f, 0.5f, 32), asphalt, root, true);
            Kit.Box("WestPavement", new Vector3(-7.5f, -0.2f, 12), new Vector3(6f, 0.6f, 32), pavers, root, true);
            Kit.Box("WestCurb", new Vector3(-4.42f, -0.2f, 12), new Vector3(0.16f, 0.6f, 32), curb, root, true);
            Kit.Box("EastPavement", new Vector3(9.4f, -0.2f, 12), new Vector3(2.2f, 0.6f, 32), pavers, root, true);
            Kit.Box("EastCurb", new Vector3(8.22f, -0.2f, 12), new Vector3(0.16f, 0.6f, 32), curb, root, true);
            Kit.Box("AlleyGround", new Vector3(0, -0.25f, 41.1f), new Vector3(6.6f, 0.5f, 26.2f), S("concrete_floor_worn_001"), root, true);
            Kit.Box("CourtyardGround", new Vector3(0, -0.25f, 63.1f), new Vector3(18f, 0.5f, 17.8f), Art.Tint("surf/brick_pavement_02", new Color(1f, 0.98f, 0.95f)), root, true);
            Kit.Box("BeyondRoad", new Vector3(0, -0.27f, 84f), new Vector3(40f, 0.5f, 24f), asphalt, root);
            Kit.Box("Underlay", new Vector3(0, -0.6f, 34f), new Vector3(160f, 0.5f, 180f), roofing, root);

            // Worn paint, drains and standing water break up the large ground surfaces.
            Texture2D wear = Kit.Noise("PaintWear", 128, (u, v, n) =>
                new Color(1, 1, 1, Mathf.Lerp(0.3f, 0.88f, Mathf.Clamp01((n - 0.4f) * 4f))), 24f, 3.7f);
            wear.wrapMode = TextureWrapMode.Repeat;
            Material yellow = Art.Textured("mat/decal", wear, new Color(0.78f, 0.62f, 0.2f), "yellow");
            Material white = Art.Textured("mat/decal", wear, new Color(0.82f, 0.82f, 0.78f), "white");
            Paint(new Vector3(-4.15f, 0.004f, 12), new Vector2(0.14f, 31f), yellow);
            Paint(new Vector3(8.0f, 0.004f, 12), new Vector2(0.14f, 31f), yellow);
            for (int i = 0; i < 3; i++)
                Paint(new Vector3(6.9f, 0.004f, 12.6f + i * 5.2f), new Vector2(2.3f, 0.12f), white);
            Paint(new Vector3(5.72f, 0.004f, 17.8f), new Vector2(0.12f, 10.5f), white);
            for (int i = 0; i < 6; i++)
                Paint(new Vector3(-1.2f + i * 0.9f, 0.004f, 25.2f), new Vector2(0.45f, 2.6f), white);

            Texture2D pool = Kit.Noise("Puddle", 128, (u, v, n) =>
            {
                float edge = 1f - new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                return new Color(1, 1, 1, Mathf.Clamp01((edge - 0.28f + (n - 0.5f) * 0.55f) * 6f));
            }, 4f, 11.3f);
            Material water = Art.Textured("mat/water", pool, new Color(0.035f, 0.04f, 0.045f, 0.86f), "puddle");
            float[,] puddles =
            {
                { 3.4f, 9.5f, 2.6f, 1.5f }, { -2.6f, 19.5f, 1.9f, 2.4f }, { 5.0f, 23.5f, 2.8f, 1.6f }, { -0.9f, 1.2f, 1.6f, 1.1f },
                { -1.6f, 34.5f, 1.6f, 2.6f }, { 1.3f, 41.5f, 1.4f, 2.2f }, { -0.6f, 51.2f, 2.2f, 1.5f }, { 4.5f, 60.5f, 2.6f, 1.8f }
            };
            for (int i = 0; i < puddles.GetLength(0); i++)
                Kit.Quad("Puddle", new Vector3(puddles[i, 0], 0.006f, puddles[i, 1]), Vector3.up, Vector3.forward,
                    new Vector2(puddles[i, 2], puddles[i, 3]), water, root);

            Texture2D blot = Kit.Noise("Stain", 128, (u, v, n) =>
            {
                float edge = 1f - new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                return new Color(1, 1, 1, Mathf.Clamp01((edge - 0.3f + (n - 0.5f) * 0.6f) * 2.6f) * 0.5f);
            }, 5f, 21.9f);
            Material stain = Art.Textured("mat/decal", blot, new Color(0.05f, 0.045f, 0.04f), "stain");
            System.Random random = new System.Random(11);
            for (int i = 0; i < 26; i++)
            {
                bool alley = i >= 16;
                Vector3 at = alley
                    ? new Vector3(Mathf.Lerp(-2.6f, 2.6f, (float)random.NextDouble()), 0.005f, Mathf.Lerp(29f, 53f, (float)random.NextDouble()))
                    : new Vector3(Mathf.Lerp(-4f, 8f, (float)random.NextDouble()), 0.005f, Mathf.Lerp(-3f, 27f, (float)random.NextDouble()));
                float size = Mathf.Lerp(1.2f, 3.6f, (float)random.NextDouble());
                Kit.Quad("Stain", at, Vector3.up, Quaternion.Euler(0, random.Next(360), 0) * Vector3.forward,
                    new Vector2(size, size * Mathf.Lerp(0.6f, 1.2f, (float)random.NextDouble())), stain, root);
            }

            foreach (Vector3 at in new[] { new Vector3(-3.6f, 0, 6.5f), new Vector3(7.6f, 0, 21.5f), new Vector3(0.4f, 0, 37f), new Vector3(-5.2f, 0, 62f) })
                Kit.Prop("water_manhole_cover", at + Vector3.down * 0.055f, at.z * 37f, root);
        }

        private static void Paint(Vector3 center, Vector2 size, Material material)
        {
            Kit.Quad("Paint", center, Vector3.up, Vector3.forward, size, material, root, 1.6f);
        }

        // One building front. Local x runs along the wall, y is up and +z points out into the street.
        // "shops" has one letter per ground-floor bay: S shutter, G glazed shop, O open workshop,
        // D doorway, W plain wall.
        private static Transform Facade(string name, Vector3 origin, float yaw, float length, Material wall, int floors,
            string shops, string[] signs, int seed, float depth = 8f, float ground = 3.5f, bool cages = true,
            Material pier = null, bool awnings = true)
        {
            Transform t = Group(name, origin, yaw);
            System.Random random = new System.Random(seed);
            float height = ground + floors * FloorHeight;
            bool workshop = shops != null && shops.Contains("O");
            bool hollow = workshop || (shops != null && shops.Contains("G"));
            float inner = workshop ? 3.6f : hollow ? 1.9f : Skin;
            Material frame = seed % 3 == 0 ? Art.Plain(new Color(0.80f, 0.80f, 0.78f), 0.5f)
                : seed % 3 == 1 ? greenPaint : Art.Plain(new Color(0.30f, 0.20f, 0.14f), 0.3f);
            if (pier == null) pier = wall;

            Kit.Box("Core", new Vector3(length / 2, height / 2, -(depth + inner) / 2), new Vector3(length, height, depth - inner), wall, t);
            Kit.Box("Roof", new Vector3(length / 2, height + 0.03f, -depth / 2), new Vector3(length, 0.06f, depth), roofing, t);
            Kit.Box("Parapet", new Vector3(length / 2, height + 0.33f, -0.15f), new Vector3(length, 0.6f, 0.3f), wall, t);
            Kit.Box("Coping", new Vector3(length / 2, height + 0.67f, -0.15f), new Vector3(length + 0.08f, 0.08f, 0.42f), trim, t);
            if (hollow && floors > 0)
                Kit.Box("UpperFill", new Vector3(length / 2, (ground + height) / 2, -(inner + Skin) / 2),
                    new Vector3(length, height - ground, inner - Skin), wall, t);

            if (string.IsNullOrEmpty(shops))
                Kit.Box("GroundWall", new Vector3(length / 2, ground / 2, -Skin / 2), new Vector3(length, ground, Skin), pier, t);
            else
            {
                int bays = shops.Length;
                float bay = length / bays;
                float open = ground - 0.8f;
                Kit.Box("Lintel", new Vector3(length / 2, (open + ground) / 2, -Skin / 2), new Vector3(length, ground - open, Skin), wall, t);
                for (int i = 0; i <= bays; i++)
                {
                    bool end = i == 0 || i == bays;
                    float x = i == 0 ? 0.125f : i == bays ? length - 0.125f : i * bay;
                    Kit.Box("Pier", new Vector3(x, open / 2, -Skin / 2), new Vector3(end ? 0.25f : 0.5f, open, Skin), pier, t);
                }
                for (int i = 0; i < bays; i++)
                {
                    float x0 = i * bay + 0.25f, x1 = (i + 1) * bay - 0.25f, cx = (x0 + x1) / 2, w = x1 - x0;
                    char kind = shops[i];
                    if (hollow && kind != 'O' && kind != 'G')
                        Kit.Box("BayFill", new Vector3(cx, open / 2, -(inner + Skin) / 2 - 0.02f), new Vector3(bay, open, inner - Skin), wall, t);
                    if (hollow)
                        Kit.Box("BayCeiling", new Vector3(cx, (open + ground) / 2, -(inner + Skin) / 2), new Vector3(bay, ground - open, inner - Skin), wall, t);
                    string sign = signs != null && i < signs.Length ? signs[i] : null;
                    Bay(t, kind, cx, w, open, inner, random, awnings, pier);
                    if (!string.IsNullOrEmpty(sign))
                    {
                        Color board = Hex(SignColours[random.Next(SignColours.Length)]);
                        Kit.Box("SignBoard", new Vector3(cx, open + 0.4f, 0.03f), new Vector3(w - 0.1f, 0.6f, 0.07f), Art.Plain(board, 0.45f), t);
                        bool pale = board.grayscale > 0.6f;
                        Kit.Sign(sign, new Vector3(cx, open + 0.4f, 0.075f), Vector3.forward, 0.36f,
                            pale ? new Color(0.55f, 0.1f, 0.08f) : new Color(0.95f, 0.9f, 0.78f), pale ? 1f : 1.25f, t);
                    }
                }
            }
            Kit.Box("Cornice", new Vector3(length / 2, ground + 0.02f, -0.05f), new Vector3(length + 0.06f, 0.14f, 0.5f), trim, t);

            int windows = Mathf.Max(1, Mathf.RoundToInt(length / 2.7f));
            float cell = length / windows;
            const float ww = 1.35f, wh = 1.5f, sill = 0.95f;
            for (int f = 0; f < floors; f++)
            {
                float y0 = ground + f * FloorHeight;
                Kit.Box("SillBand", new Vector3(length / 2, y0 + sill / 2, -Skin / 2), new Vector3(length, sill, Skin), wall, t);
                float head = FloorHeight - sill - wh;
                Kit.Box("HeadBand", new Vector3(length / 2, y0 + sill + wh + head / 2, -Skin / 2), new Vector3(length, head, Skin), wall, t);
                for (int j = 0; j <= windows; j++)
                {
                    float xa = j == 0 ? 0 : (j - 0.5f) * cell + ww / 2;
                    float xb = j == windows ? length : (j + 0.5f) * cell - ww / 2;
                    Kit.Box("WindowPier", new Vector3((xa + xb) / 2, y0 + sill + wh / 2, -Skin / 2), new Vector3(xb - xa, wh, Skin), wall, t);
                }
                for (int j = 0; j < windows; j++)
                    Window(t, (j + 0.5f) * cell, y0 + sill, ww, wh, frame, random, cages);
            }

            // Services that every old building wears on its face.
            for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(length / 9f)); i++)
            {
                float x = Mathf.Lerp(0.5f, length - 0.5f, (i + 0.35f + (float)random.NextDouble() * 0.3f) / Mathf.Max(1f, Mathf.Round(length / 9f)));
                x = (Mathf.Round(x / cell) * cell);
                x = Mathf.Clamp(x, 0.2f, length - 0.2f);
                Material pipe = random.Next(2) == 0 ? greyPaint : Art.Plain(new Color(0.78f, 0.77f, 0.72f), 0.4f);
                NoShadow(Kit.Tube("Downpipe", new Vector3(x, height + 0.3f, 0.07f), new Vector3(x, 0.15f, 0.07f), 0.055f, pipe, t));
                for (float y = 1.2f; y < height; y += 2.4f)
                    NoShadow(Kit.Box("PipeClip", new Vector3(x, y, 0.05f), new Vector3(0.16f, 0.04f, 0.1f), darkMetal, t));
            }
            if (floors > 0)
                Kit.Cable("ServiceCable", new Vector3(0.1f, ground + 0.45f, 0.04f), new Vector3(length - 0.1f, ground + 0.5f, 0.04f),
                    0.12f, 0.018f, darkMetal, t, 20);

            if (random.Next(3) > 0)
            {
                float x = Mathf.Lerp(1.5f, Mathf.Max(1.6f, length - 1.5f), (float)random.NextDouble());
                Material steel = Art.Metal(new Color(0.72f, 0.73f, 0.74f), 0.6f);
                Kit.Lathe("WaterTank", new Vector3(x, height + 0.55f, -2.2f),
                    new[] { new Vector2(0, 0), new Vector2(0.62f, 0), new Vector2(0.62f, 1.3f), new Vector2(0.4f, 1.48f), new Vector2(0, 1.5f) }, steel, t, 20);
                for (int leg = 0; leg < 4; leg++)
                    Kit.Box("TankLeg", new Vector3(x + (leg % 2 == 0 ? -0.45f : 0.45f), height + 0.3f, -2.2f + (leg < 2 ? -0.45f : 0.45f)),
                        new Vector3(0.06f, 0.55f, 0.06f), darkMetal, t);
            }
            if (random.Next(2) == 0)
            {
                float x = Mathf.Lerp(1f, Mathf.Max(1.1f, length - 1f), (float)random.NextDouble());
                NoShadow(Kit.Tube("Antenna", new Vector3(x, height, -1f), new Vector3(x, height + 3.2f, -1f), 0.025f, darkMetal, t, 5));
                NoShadow(Kit.Tube("AntennaArm", new Vector3(x - 0.5f, height + 2.7f, -1f), new Vector3(x + 0.5f, height + 2.7f, -1f), 0.012f, darkMetal, t, 4));
            }
            return t;
        }

        private static void Bay(Transform t, char kind, float cx, float w, float open, float inner, System.Random random, bool awnings, Material pier)
        {
            switch (kind)
            {
                case 'S':
                {
                    Kit.PropFit("rollershutter_door#" + (random.Next(3) == 0 ? 1 : 0), new Vector3(cx, 0, -0.27f),
                        new Vector3(w, open, 0.24f), Quaternion.identity, t, new Vector3(0, -1, -1));
                    break;
                }
                case 'G':
                {
                    // A shallow lit room behind real glass, so shop fronts have depth instead of a flat pane.
                    float depth = inner - Skin, mid = -(inner + Skin) / 2;
                    Material room = Art.Tint("surf/plastered_wall_04", new Color(0.66f, 0.66f, 0.62f));
                    Kit.Box("ShopFloor", new Vector3(cx, 0.05f, mid), new Vector3(w + 0.5f, 0.1f, depth), S("long_white_tiles"), t);
                    Kit.Box("ShopLeftWall", new Vector3(cx - w / 2 - 0.19f, open / 2, mid), new Vector3(0.12f, open, depth), room, t);
                    Kit.Box("ShopRightWall", new Vector3(cx + w / 2 + 0.19f, open / 2, mid), new Vector3(0.12f, open, depth), room, t);
                    Kit.Quad("ShopShelves", new Vector3(cx, open / 2, -inner + 0.02f), Vector3.forward, Vector3.up, new Vector2(w + 0.26f, open),
                        Art.Textured("mat/plain", Shelves(), Color.white, "shelves"), t, 2.7f);
                    Kit.Box("Counter", new Vector3(cx + w * (random.Next(2) == 0 ? -0.2f : 0.2f), 0.52f, -inner * 0.52f),
                        new Vector3(w * 0.45f, 0.84f, 0.42f), S("old_wood_floor"), t);
                    bool warm = random.Next(3) > 0;
                    Color tone = warm ? new Color(1f, 0.78f, 0.52f) : new Color(0.85f, 0.93f, 1f);
                    NoShadow(Kit.Box("ShopTube", new Vector3(cx, open - 0.06f, mid), new Vector3(w * 0.5f, 0.03f, 0.05f), Art.Glow(tone, 3.5f), t));
                    Kit.Lamp("ShopLight", new Vector3(cx, open - 0.45f, mid), tone, 1.5f, 4.5f, t);
                    Kit.Quad("ShopGlass", new Vector3(cx, open / 2, -0.2f), Vector3.forward, Vector3.up, new Vector2(w, open),
                        Art.Tint("mat/water", new Color(0.05f, 0.06f, 0.07f, 0.3f), 0.96f), t);
                    Material metal = Art.Metal(new Color(0.55f, 0.56f, 0.57f), 0.6f);
                    NoShadow(Kit.Box("Plinth", new Vector3(cx, 0.19f, -0.16f), new Vector3(w, 0.18f, 0.1f), metal, t));
                    for (int m = 1; m < 3; m++)
                        NoShadow(Kit.Box("Mullion", new Vector3(cx - w / 2 + w * m / 3f, open / 2, -0.15f), new Vector3(0.06f, open, 0.08f), metal, t));
                    NoShadow(Kit.Box("Transom", new Vector3(cx, open - 0.55f, -0.15f), new Vector3(w, 0.06f, 0.08f), metal, t));
                    for (int p = 0; p < 2; p++)
                    {
                        Color paper = p == 0 ? new Color(0.72f, 0.16f, 0.12f) : new Color(0.85f, 0.8f, 0.62f);
                        NoShadow(Kit.Box("Poster", new Vector3(cx - w / 2 + 0.5f + (float)random.NextDouble() * (w - 1f), 1.25f + (float)random.NextDouble() * 0.5f, -0.185f),
                            new Vector3(0.42f, 0.58f, 0.01f), Art.Plain(paper, 0.2f), t));
                    }
                    break;
                }
                case 'O':
                {
                    Kit.Box("ShopFloor", new Vector3(cx, 0.05f, -(inner + Skin) / 2), new Vector3(w + 0.5f, 0.1f, inner - Skin), S("concrete_floor_worn_001"), t);
                    Kit.Box("ShopLeftWall", new Vector3(cx - w / 2 - 0.19f, open / 2, -(inner + Skin) / 2), new Vector3(0.12f, open, inner - Skin), Art.Tint("surf/plastered_wall_04", new Color(0.62f, 0.64f, 0.6f)), t);
                    Kit.Box("ShopRightWall", new Vector3(cx + w / 2 + 0.19f, open / 2, -(inner + Skin) / 2), new Vector3(0.12f, open, inner - Skin), Art.Tint("surf/plastered_wall_04", new Color(0.62f, 0.64f, 0.6f)), t);
                    Kit.Box("ShopBackTiles", new Vector3(cx, 0.75f, -inner + 0.03f), new Vector3(w + 0.26f, 1.3f, 0.05f), S("long_white_tiles"), t);
                    break;
                }
                case 'D':
                {
                    const float door = 1.1f, top = 2.15f;
                    Kit.Box("DoorLeft", new Vector3(cx - (w + door) / 4, open / 2, -Skin / 2), new Vector3((w - door) / 2, open, Skin), pier, t);
                    Kit.Box("DoorRight", new Vector3(cx + (w + door) / 4, open / 2, -Skin / 2), new Vector3((w - door) / 2, open, Skin), pier, t);
                    Kit.Box("DoorHead", new Vector3(cx, (top + open) / 2, -Skin / 2), new Vector3(door, open - top, Skin), pier, t);
                    Kit.Box("Door", new Vector3(cx, top / 2, -0.2f), new Vector3(door, top, 0.06f), greenPaint, t);
                    NoShadow(Kit.Box("DoorHandle", new Vector3(cx + 0.4f, 1.05f, -0.15f), new Vector3(0.04f, 0.18f, 0.05f), darkMetal, t));
                    Kit.Box("DoorStep", new Vector3(cx, 0.13f, 0.1f), new Vector3(door + 0.3f, 0.06f, 0.35f), trim, t);
                    Kit.Box("DoorCanopy", new Vector3(cx, top + 0.32f, 0.3f), new Vector3(door + 0.5f, 0.06f, 0.65f), S("corrugated_iron_02"), t);
                    Kit.Prop("industrial_wall_lamp", new Vector3(cx, top + 0.05f, 0.0f), 0, t, false, 1f, new Vector3(0, 0, -1));
                    Kit.Prop("power_box_01", new Vector3(cx + door / 2 + 0.45f, 1.5f, 0.0f), 0, t, false, 1f, new Vector3(0, 0, -1));
                    break;
                }
                default:
                    Kit.Box("BayWall", new Vector3(cx, open / 2, -Skin / 2), new Vector3(w, open, Skin), pier, t);
                    break;
            }

            if (awnings && (kind == 'S' || kind == 'G') && random.Next(5) < 2)
            {
                Color cloth = new[] { new Color(0.50f, 0.16f, 0.13f), new Color(0.20f, 0.36f, 0.30f), new Color(0.24f, 0.30f, 0.44f), new Color(0.62f, 0.55f, 0.40f) }[random.Next(4)];
                Vector3 slope = new Vector3(0, -0.45f, 1.25f).normalized;
                Kit.Quad("Awning", new Vector3(cx, open - 0.12f, 0.64f), Vector3.Cross(slope, Vector3.right), slope,
                    new Vector2(w + 0.3f, 1.34f), Art.Tint("mat/cloth", cloth, 0.08f), t, 0.4f, true);
                for (int side = -1; side <= 1; side += 2)
                    NoShadow(Kit.Tube("AwningArm", new Vector3(cx + side * (w / 2 + 0.1f), open + 0.1f, 0.02f),
                        new Vector3(cx + side * (w / 2 + 0.1f), open - 0.34f, 1.26f), 0.015f, darkMetal, t, 5));
            }
        }

        private static void Window(Transform t, float cx, float y, float ww, float wh, Material frame, System.Random random, bool cages)
        {
            int roll = random.Next(100);
            Material pane = roll < 8 ? Art.Glow(new Color(1f, 0.78f, 0.5f), 0.45f) : glass;
            Kit.Quad("Glass", new Vector3(cx, y + wh / 2, -0.17f), Vector3.forward, Vector3.up, new Vector2(ww, wh), pane, t);
            if (random.Next(100) < 45)
            {
                float part = Mathf.Lerp(0.3f, 0.55f, (float)random.NextDouble());
                float side = random.Next(2) == 0 ? -1 : 1;
                Kit.Quad("Curtain", new Vector3(cx + side * ww * (1 - part) / 2, y + wh / 2, -0.165f), Vector3.forward, Vector3.up,
                    new Vector2(ww * part, wh), Art.Tint("mat/cloth", CurtainColours[random.Next(CurtainColours.Length)], 0.05f), t, 0.4f);
            }
            NoShadow(Kit.Box("FrameL", new Vector3(cx - ww / 2 + 0.025f, y + wh / 2, -0.13f), new Vector3(0.05f, wh, 0.07f), frame, t));
            NoShadow(Kit.Box("FrameR", new Vector3(cx + ww / 2 - 0.025f, y + wh / 2, -0.13f), new Vector3(0.05f, wh, 0.07f), frame, t));
            NoShadow(Kit.Box("FrameT", new Vector3(cx, y + wh - 0.025f, -0.13f), new Vector3(ww, 0.05f, 0.07f), frame, t));
            NoShadow(Kit.Box("FrameB", new Vector3(cx, y + 0.025f, -0.13f), new Vector3(ww, 0.05f, 0.07f), frame, t));
            NoShadow(Kit.Box("Mullion", new Vector3(cx, y + wh / 2, -0.13f), new Vector3(0.045f, wh, 0.06f), frame, t));
            Kit.Box("Sill", new Vector3(cx, y - 0.03f, 0.02f), new Vector3(ww + 0.2f, 0.06f, 0.44f), trim, t);

            int extra = random.Next(100);
            if (cages && extra < 30)
            {
                const float reach = 0.42f;
                Material bar = Art.Metal(new Color(0.42f, 0.43f, 0.44f), 0.5f);
                NoShadow(Kit.Box("CageBase", new Vector3(cx, y + 0.01f, reach / 2), new Vector3(ww + 0.1f, 0.025f, reach), bar, t));
                NoShadow(Kit.Box("CageTop", new Vector3(cx, y + wh, reach / 2), new Vector3(ww + 0.1f, 0.025f, reach), bar, t));
                NoShadow(Kit.Box("CageRail", new Vector3(cx, y + wh * 0.5f, reach), new Vector3(ww + 0.1f, 0.02f, 0.02f), bar, t));
                int bars = 11;
                for (int b = 0; b < bars; b++)
                    NoShadow(Kit.Box("CageBar", new Vector3(cx - ww / 2 - 0.05f + (ww + 0.1f) * b / (bars - 1), y + wh / 2, reach), new Vector3(0.014f, wh, 0.014f), bar, t));
                for (int side = -1; side <= 1; side += 2)
                    for (int b = 1; b < 3; b++)
                        NoShadow(Kit.Box("CageSide", new Vector3(cx + side * (ww / 2 + 0.05f), y + wh / 2, reach * b / 3f), new Vector3(0.014f, wh, 0.014f), bar, t));
                if (random.Next(2) == 0)
                    Kit.Prop("potted_plant_04", new Vector3(cx + ((float)random.NextDouble() - 0.5f) * 0.8f, y + 0.03f, 0.2f), random.Next(360), t, false, 2.2f);
                if (random.Next(3) == 0) Laundry(t, new Vector3(cx - ww / 2, y + wh - 0.08f, reach - 0.05f), new Vector3(cx + ww / 2, y + wh - 0.08f, reach - 0.05f), random, 3, false);
            }
            else if (extra < 58)
            {
                // Split air conditioner on a bracket under the window, with its drain line.
                string unit = Art.Has("prop/exterior_aircon_unit#0") ? "exterior_aircon_unit#" + random.Next(2) : "exterior_aircon_unit";
                float scale = 0.92f;
                Vector3 size = Kit.PropSize(unit) * scale;
                float ax = cx + (random.Next(2) == 0 ? -0.15f : 0.15f);
                Kit.Prop(unit, new Vector3(ax, y - 0.2f - size.y, 0.06f), 0, t, false, scale, new Vector3(0, -1, -1));
                NoShadow(Kit.Box("Bracket", new Vector3(ax, y - 0.22f - size.y, 0.2f), new Vector3(size.x + 0.1f, 0.03f, 0.42f), darkMetal, t));
                NoShadow(Kit.Tube("Drain", new Vector3(ax + size.x / 2, y - 0.3f, 0.03f), new Vector3(ax + size.x / 2 + 0.05f, y - 2.6f, 0.03f), 0.012f, greyPaint, t, 4));
            }
        }

        // Clothes drying on a line between two points.
        private static void Laundry(Transform t, Vector3 from, Vector3 to, System.Random random, int pieces, bool line = true)
        {
            if (line) Kit.Cable("ClothesLine", from, to, 0.18f, 0.006f, darkMetal, t, 10);
            Vector3 along = (to - from).normalized;
            Vector3 normal = Vector3.Cross(Vector3.up, along).normalized;
            for (int i = 0; i < pieces; i++)
            {
                float at = (i + 0.5f + ((float)random.NextDouble() - 0.5f) * 0.4f) / pieces;
                float sag = line ? 0.18f * 4f * at * (1 - at) : 0;
                float width = Mathf.Lerp(0.38f, 0.7f, (float)random.NextDouble());
                float drop = Mathf.Lerp(0.5f, 0.95f, (float)random.NextDouble());
                Color cloth = Color.Lerp(CurtainColours[random.Next(CurtainColours.Length)], new Color(0.85f, 0.84f, 0.8f), (float)random.NextDouble() * 0.6f);
                Vector3 lean = (normal + along * ((float)random.NextDouble() - 0.5f) * 0.25f).normalized;
                int kind = random.Next(3);
                if (kind == 1) { width *= 0.75f; drop = Mathf.Max(drop, 0.85f); }
                Kit.Quad("Washing", Vector3.Lerp(from, to, at) + Vector3.down * (sag + drop / 2), lean, Vector3.up,
                    new Vector2(kind == 0 ? width * 1.5f : width, drop),
                    Art.Textured("mat/clothcut", Garment(kind), cloth, "garment" + kind + ColorUtility.ToHtmlStringRGB(cloth)), t, 0f, true);
            }
        }

        private static Texture2D shelves;

        // Rows of stocked shelves for the back wall of a shop, seen through its window.
        private static Texture2D Shelves()
        {
            if (shelves != null) return shelves;
            const int size = 256;
            shelves = Art.Own(new Texture2D(size, size, TextureFormat.RGB24, true) { name = "ShopShelves" });
            Color[] pixels = new Color[size * size];
            System.Random random = new System.Random(17);
            Color back = new Color(0.20f, 0.17f, 0.14f), board = new Color(0.34f, 0.25f, 0.17f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = back;
            const int rows = 6, rowHeight = size / rows;
            for (int row = 0; row < rows; row++)
            {
                int baseY = row * rowHeight;
                for (int x = 0; x < size; x++)
                    for (int y = 0; y < 4; y++) pixels[(baseY + y) * size + x] = board;
                int at = 2;
                while (at < size - 6)
                {
                    int wide = random.Next(6, 22), tall = random.Next(rowHeight / 2, rowHeight - 8);
                    Color goods = Color.HSVToRGB((float)random.NextDouble(), 0.25f + (float)random.NextDouble() * 0.4f, 0.4f + (float)random.NextDouble() * 0.45f);
                    for (int x = at; x < Mathf.Min(size, at + wide); x++)
                        for (int y = 4; y < 4 + tall; y++)
                            pixels[(baseY + y) * size + x] = goods * (x == at || y == 3 + tall ? 0.7f : 1f);
                    at += wide + random.Next(1, 5);
                }
            }
            shelves.SetPixels(pixels);
            shelves.wrapMode = TextureWrapMode.Repeat;
            shelves.Apply(true);
            return shelves;
        }

        private static readonly Texture2D[] Garments = new Texture2D[3];

        // Silhouettes for hanging washing: 0 shirt, 1 trousers, 2 towel. Soft vertical bands suggest folds.
        private static Texture2D Garment(int kind)
        {
            if (Garments[kind] != null) return Garments[kind];
            const int size = 96;
            Texture2D texture = Art.Own(new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Garment" + kind });
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size, c = Mathf.Abs(u - 0.5f);
                    bool inside;
                    if (kind == 0)
                        inside = (c < 0.2f && v < 0.96f && !(v > 0.9f && c < 0.07f))
                            || (v > 0.56f && v < 0.96f - (c - 0.2f) * 0.5f && c < 0.48f && v > 0.96f - (c - 0.2f) * 0.5f - 0.3f);
                    else if (kind == 1)
                        inside = c < 0.34f && (v > 0.8f || c > 0.035f + (0.8f - v) * 0.03f) && c < 0.34f - (0.8f - Mathf.Min(v, 0.8f)) * 0.06f;
                    else
                        inside = c < 0.46f && v > 0.02f;
                    float fold = 0.86f + 0.14f * Mathf.Sin(u * 31f + Mathf.Sin(v * 5f) * 1.5f);
                    pixels[y * size + x] = new Color(fold, fold, fold, inside ? 1f : 0f);
                }
            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(true);
            Garments[kind] = texture;
            return texture;
        }

        private static void BuildStreet()
        {
            Material yellow = S("yellow_plaster"), grey = S("plastered_wall_04"), worn = S("worn_plaster_wall");
            Material tiles = S("long_white_tiles"), concrete = S("concrete_wall_008");
            Material cream = Art.Tint("surf/plastered_wall_04", new Color(0.93f, 0.86f, 0.74f));
            Material rose = Art.Tint("surf/yellow_plaster", new Color(0.95f, 0.78f, 0.72f));

            // West side, north to south. The middle building is the repair shop.
            Facade("West3", new Vector3(-10.5f, 0, 28), 90, 9, cream, 3, "GSD", new[] { "老街面馆", "烟酒茶", null }, 31, 9f, 3.5f, true, tiles);
            Facade("WestRepair", new Vector3(-10.5f, 0, 19), 90, 13, yellow, 2, "GOSD", new[] { "家电维修", "修理铺", "五金杂货", null }, 14, 9f, 3.5f, true);
            Facade("West1", new Vector3(-10.5f, 0, 6), 90, 15, grey, 4, "SGSDS", new[] { "便民超市", "惠民药房", "理发", null, "粮油" }, 52, 9f, 3.5f, true, tiles);

            // East side, south to north: lower, so the late sun still reaches the west pavement.
            Facade("East1", new Vector3(10.5f, 0, -9), -90, 17, worn, 2, "SSGDS", new[] { "裁缝铺", "水果", "早点", null, "杂货" }, 23, 9f, 3.4f, true);
            Facade("East2", new Vector3(10.5f, 0, 8), -90, 12, rose, 2, "GSSD", new[] { "文具", "钟表", "电器", null }, 77, 9f, 3.4f, true, tiles);
            Facade("East3", new Vector3(10.5f, 0, 20), -90, 8, concrete, 3, "SG", new[] { "茶馆", "彩票" }, 45, 9f, 3.4f, false);

            // North row either side of the alley mouth.
            Facade("NorthRight", new Vector3(10.5f, 0, 28), 180, 7.5f, cream, 3, "GS", new[] { "小卖部", "修鞋" }, 61, 9f, 3.5f, true);
            Facade("NorthLeft", new Vector3(-3.0f, 0, 28), 180, 7.5f, grey, 3, "SD", new[] { "照相馆", null }, 88, 9f, 3.5f, true, tiles);

            // South end: a site hoarding with an unfinished block behind it.
            Material sheet = S("corrugated_iron_02");
            Kit.Box("Hoarding", new Vector3(0, 1.35f, -3.56f), new Vector3(21f, 2.7f, 0.08f), sheet, root);
            Kit.Box("HoardingBase", new Vector3(0, 0.12f, -3.5f), new Vector3(21f, 0.24f, 0.24f), curb, root);
            for (int i = 0; i <= 8; i++)
                Kit.Box("HoardingPost", new Vector3(-10.4f + i * 2.6f, 1.45f, -3.47f), new Vector3(0.09f, 2.9f, 0.09f), darkMetal, root);
            Kit.Sign("施工围挡  注意安全", new Vector3(-3.4f, 1.75f, -3.5f), Vector3.forward, 0.3f, new Color(0.75f, 0.2f, 0.15f, 0.8f), 1f, root);
            // Kept to a single storey: anything taller would shade the whole street from the low sun.
            Facade("SouthBlock", new Vector3(-10.5f, 0, -9), 0, 21, concrete, 0, null, null, 19, 9f, 4.2f, false);

            // Overhead lines strung from front to front.
            System.Random random = new System.Random(5);
            for (int i = 0; i < 9; i++)
            {
                float z = -2f + i * 3.4f;
                Kit.Cable("StreetLine", new Vector3(-10.42f, 6.2f + (float)random.NextDouble() * 1.5f, z),
                    new Vector3(10.42f, 5.6f + (float)random.NextDouble() * 1.2f, z + (float)random.NextDouble() * 3f - 1.5f),
                    0.5f + (float)random.NextDouble() * 0.6f, 0.014f, darkMetal, root, 18);
            }
            Laundry(root, new Vector3(-10.4f, 7.3f, 1.2f), new Vector3(-10.4f + 0.01f, 7.3f, 4.6f), random, 4);

            foreach (Vector3 at in new[] { new Vector3(-4.85f, 0.1f, 2.2f), new Vector3(-4.85f, 0.1f, 21.5f), new Vector3(8.75f, 0.1f, 9.5f), new Vector3(8.75f, 0.1f, 25f) })
            {
                GameObject lamp = Kit.Prop("street_lamp_01", at, at.x < 0 ? 90 : -90, root, false, 1.25f);
                BoxCollider post = lamp.AddComponent<BoxCollider>();
                post.center = new Vector3(0, 1.5f, 0);
                post.size = new Vector3(0.22f, 3f, 0.22f);
            }
        }

        // Facade-local position on the west repair building: x = 19 - worldZ, z = worldX + 10.5.
        private static void BuildWorkshop(DemoGame game)
        {
            Transform shop = root.Find("WestRepair");
            float open = 2.7f;
            // Inside the open bay (local x 3.5..6.25, z -3.6..-0.3).
            Kit.Prop("steel_frame_shelves_01", new Vector3(4.1f, 0.1f, -3.3f), 0, shop);
            Kit.Prop("steel_frame_shelves_01", new Vector3(5.5f, 0.1f, -3.3f), 0, shop);
            Kit.Prop("Television_01", new Vector3(4.1f, 0.62f, -3.3f), 8, shop);
            Kit.Prop("boombox", new Vector3(5.5f, 1.12f, -3.3f), -6, shop);
            Kit.Prop("metal_toolbox", new Vector3(4.0f, 1.12f, -3.3f), 12, shop);
            Kit.Prop("plastic_crate_01", new Vector3(5.6f, 0.62f, -3.3f), 0, shop);
            Kit.Prop("vintage_electric_kettle", new Vector3(5.2f, 1.62f, -3.3f), 40, shop);
            Kit.Prop("metal_tool_chest", new Vector3(6.0f, 0.1f, -1.6f), -90, shop);
            Kit.Prop("old_tyre", new Vector3(3.75f, 0.4f, -1.3f), 0, shop, false, 1f, null, Quaternion.Euler(80, 20, 0));
            Kit.Prop("old_tyre", new Vector3(3.8f, 0.4f, -0.9f), 0, shop, false, 1f, null, Quaternion.Euler(78, -10, 0));
            Kit.Prop("wall_clock", new Vector3(4.9f, 2.25f, -3.57f), 0, shop, false, 1f, new Vector3(0, 0, -1));
            Kit.Prop("mounted_fluorescent_lights", new Vector3(4.9f, open - 0.02f, -1.9f), 90, shop, false, 1f, Vector3.up);
            Kit.Box("TubeGlow", new Vector3(4.9f, open - 0.07f, -1.9f), new Vector3(0.05f, 0.03f, 0.8f), Art.Glow(new Color(0.9f, 0.95f, 1f), 3f), shop);
            Kit.Lamp("ShopTube", new Vector3(4.9f, open - 0.35f, -1.9f), new Color(0.86f, 0.93f, 1f), 1.5f, 6f, shop, true);
            Kit.Lamp("BenchBulb", new Vector3(4.2f, 2.05f, 2.9f), new Color(1f, 0.72f, 0.42f), 2.4f, 5f, shop);
            NoShadow(Kit.Tube("BulbCord", new Vector3(4.2f, open + 0.1f, 0.2f), new Vector3(4.2f, 2.16f, 2.9f), 0.006f, darkMetal, shop, 4));
            Kit.Lathe("Bulb", new Vector3(4.2f, 2.05f, 2.9f), new[] { new Vector2(0, 0), new Vector2(0.035f, 0.02f), new Vector2(0.035f, 0.06f), new Vector2(0.015f, 0.1f), new Vector2(0, 0.11f) },
                Art.Glow(new Color(1f, 0.74f, 0.42f), 6f), shop, 10).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // The stall on the pavement: bench, the loose chair, and the chipped enamel cup.
            GameObject bench = Kit.Prop("wooden_table_02", new Vector3(-7.4f, 0.1f, 15f), new Vector3(2f, 1.05f, 1.4f), Quaternion.identity, root, true);
            float top = 0.1f + Kit.PropSize("wooden_table_02").y * 1.05f;
            Kit.Prop("bench_vice_01", new Vector3(-8.25f, top, 15.2f), 90, root);
            Kit.Prop("cross_pein_hammer", new Vector3(-7.75f, top + 0.012f, 14.8f), 0, root, false, 1f, null, Quaternion.Euler(90, 35, 0));
            Kit.Prop("adjustable_wrench", new Vector3(-7.55f, top + 0.015f, 15.25f), 0, root, false, 1f, null, Quaternion.Euler(90, -60, 0));
            Kit.Prop("pliers", new Vector3(-6.95f, top + 0.012f, 14.75f), 0, root, false, 1f, null, Quaternion.Euler(90, 110, 0));
            Kit.Prop("screwdrivers_02", new Vector3(-6.75f, top, 15.3f), 25, root);
            Kit.Prop("modified_thermos", new Vector3(-6.55f, top, 15.25f), 200, root);
            Kit.Prop("metal_toolbox", new Vector3(-8.1f, 0.1f, 14.15f), 70, root);
            Kit.Prop("wooden_stool_01", new Vector3(-7.9f, 0.1f, 15.95f), 15, root);
            Kit.Prop("plastic_crate_01", new Vector3(-6.2f, 0.1f, 16.1f), 30, root);

            GameObject chair = Kit.Prop("painted_wooden_chair_01", new Vector3(-5.6f, 0.1f, 14.95f), 205, root, true);
            GameObject cup = BuildCup(new Vector3(-7.2f, top, 15f));
            game.AddInteraction(chair, "扶稳松动的椅子（可选）", () => game.State.Phase == OpeningPhase.Street,
                () => game.Caption("老周：响得烦。等我把这条腿垫好。", 5));
            game.AddInteraction(cup, "看看搪瓷杯（可选）", () => game.State.Phase == OpeningPhase.Street,
                () => game.Caption("杯沿掉了漆，旁边留着一圈旧水印。老周把它往工具旁挪了挪。", 5));
            bench.name = "WorkshopBench";
        }

        private static GameObject BuildCup(Vector3 position)
        {
            Material enamel = Art.Plain(new Color(0.88f, 0.87f, 0.80f), 0.78f);
            Material rim = Art.Plain(new Color(0.10f, 0.16f, 0.30f), 0.7f);
            GameObject cup = Kit.Lathe("EnamelCup", position, new[]
            {
                new Vector2(0, 0), new Vector2(0.040f, 0), new Vector2(0.043f, 0.004f), new Vector2(0.046f, 0.088f),
                new Vector2(0.043f, 0.088f), new Vector2(0.040f, 0.008f), new Vector2(0, 0.008f)
            }, enamel, root, 28);
            Kit.Lathe("Rim", new Vector3(0, 0.086f, 0), new[] { new Vector2(0.0425f, 0), new Vector2(0.0475f, 0), new Vector2(0.0475f, 0.006f), new Vector2(0.0425f, 0.006f), new Vector2(0.0425f, 0) },
                rim, cup.transform, 28);
            Vector3[] handle = new Vector3[9];
            for (int i = 0; i < handle.Length; i++)
            {
                float a = Mathf.Lerp(-80f, 80f, i / 8f) * Mathf.Deg2Rad;
                handle[i] = new Vector3(0.044f + Mathf.Cos(a) * 0.026f, 0.046f + Mathf.Sin(a) * 0.028f, 0);
            }
            Kit.Path("Handle", handle, 0.005f, enamel, cup.transform, 6);
            // A chip in the enamel near the rim, down to the dark steel.
            Kit.Box("Chip", new Vector3(-0.012f, 0.083f, 0.0445f), new Vector3(0.016f, 0.012f, 0.004f), Art.Plain(new Color(0.07f, 0.07f, 0.08f), 0.3f), cup.transform);
            cup.transform.localRotation = Quaternion.Euler(0, 35, 0);
            NoShadow(cup);
            cup.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            return cup;
        }

        private static void BuildStreetLife(DemoGame game)
        {
            Kit.Prop("covered_car", new Vector3(6.9f, 0, 15), 0, root, true);

            // Water bowl for the dog.
            Material steel = Art.Metal(new Color(0.62f, 0.63f, 0.64f), 0.7f);
            GameObject bowl = Kit.Lathe("WaterBowl", new Vector3(2, 0.002f, 6), new[]
            {
                new Vector2(0, 0), new Vector2(0.15f, 0), new Vector2(0.17f, 0.065f), new Vector2(0.155f, 0.065f),
                new Vector2(0.135f, 0.012f), new Vector2(0, 0.012f)
            }, steel, root, 26);
            game.Water = Kit.Lathe("Water", new Vector3(2, 0.05f, 6), new[] { new Vector2(0.148f, 0), new Vector2(0, 0) },
                Art.Tint("mat/glass", new Color(0.05f, 0.09f, 0.11f), 0.97f), root, 26);
            game.Water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            game.Water.SetActive(false);
            game.AddInteraction(bowl, "给狗倒水（可选）", () => game.State.Phase == OpeningPhase.Street && !game.State.GaveWater, game.GiveWater);

            // Pavement clutter, kept clear of the routes people take through the street.
            float[,] items =
            {
                { -9.9f, 26.3f, 20 }, { -9.6f, 9.6f, 0 }, { -9.9f, 4.4f, 180 }, { 10.0f, 2.5f, -90 }, { 9.9f, 18.4f, -90 }
            };
            string[] ids = { "metal_trash_can", "utility_box_01", "utility_box_01", "utility_box_01", "metal_trash_can" };
            for (int i = 0; i < ids.Length; i++)
            {
                Kit.Prop(ids[i], new Vector3(items[i, 0], 0.1f, items[i, 1]), items[i, 2], root, true);
            }
            Kit.Prop("trashbag", new Vector3(-9.7f, 0.1f, 25.3f), 40, root);
            Kit.Prop("trashbag", new Vector3(-9.3f, 0.1f, 25.75f), 190, root, false, 0.85f);
            Kit.Prop("trashbag", new Vector3(9.85f, 0.1f, 17.5f), 100, root);
            Kit.Prop("cardboard_box_01", new Vector3(9.8f, 0.1f, 16.7f), 20, root);
            Kit.Prop("cardboard_box_01", new Vector3(9.75f, 0.1f + Kit.PropSize("cardboard_box_01").y, 16.75f), 65, root, false, 0.85f);
            Kit.Prop("plastic_monobloc_chair_01", new Vector3(-9.2f, 0.1f, 22.6f), 110, root, true);
            Kit.Prop("plastic_monobloc_chair_01", new Vector3(-9.3f, 0.1f, 21.5f), 70, root, true);
            Kit.Prop("wooden_stool_01", new Vector3(-8.6f, 0.1f, 22.1f), 0, root);
            Kit.Prop("small_lpg_tank", new Vector3(-10.0f, 0.1f, 20.4f), 0, root);
            Kit.Prop("plastic_crate_01", new Vector3(9.9f, 0.1f, -1.2f), 10, root);
            Kit.Prop("plastic_crate_01", new Vector3(9.9f, 0.1f + Kit.PropSize("plastic_crate_01").y, -1.2f), 4, root);
            Kit.Prop("plastic_crate_01", new Vector3(9.85f, 0.1f, -0.6f), 80, root);
            Kit.Prop("korean_fire_extinguisher_01", new Vector3(-10.25f, 0.1f, 1.6f), 90, root);
            Kit.Prop("planter_box_01", new Vector3(-10.0f, 0.1f, -1.6f), 90, root, true);
            Kit.Prop("planter_box_01", new Vector3(10.0f, 0.1f, 6.2f), 90, root, true);
            Kit.Prop("potted_plant_04", new Vector3(-10.1f, 0.1f, 11.9f), 0, root, false, 3.2f);
            Kit.Prop("potted_plant_04", new Vector3(10.05f, 0.1f, 12.4f), 120, root, false, 2.8f);
            Kit.Prop("plastic_broom", new Vector3(-10.3f, 0.1f, 8.2f), 90, root);
            Kit.Prop("hand_truck", new Vector3(9.9f, 0.1f, 22.4f), -80, root);
            Kit.Prop("cement_bag", new Vector3(-9.6f, 0.1f, -2.6f), 15, root);
            Kit.Prop("cement_bag", new Vector3(-9.55f, 0.1f + Kit.PropSize("cement_bag").y, -2.55f), 28, root);
            Kit.Prop("wooden_ladder_02", new Vector3(8.9f, 0.1f, -3.1f), 185, root);
            Kit.Prop("plastic_jerrycan", new Vector3(-9.9f, 0.1f, 16.6f), 30, root);
        }

        private static void BuildAlley(DemoGame game)
        {
            Material brick = S("red_brick_03"), rough = S("rough_plaster_brick_02");
            // The alley walls start just behind the street fronts, so the two never share a plane.
            Facade("AlleyWestWall", new Vector3(-3f, 0, 54), 90, 25.7f, brick, 2, "WDWWDWWW", null, 101, 6f, 3.2f, true, null, false);
            Facade("AlleyEastWall", new Vector3(3f, 0, 28.3f), -90, 25.7f, rough, 2, "WWDWWWDW", null, 202, 6f, 3.2f, false, null, false);

            // Gutter along the east wall and the run of pipes above it.
            Kit.Box("Gutter", new Vector3(2.72f, 0.003f, 41f), new Vector3(0.34f, 0.01f, 25.6f), Art.Plain(new Color(0.06f, 0.065f, 0.07f), 0.85f), root);
            Kit.Tube("GasPipe", new Vector3(2.9f, 2.55f, 28.4f), new Vector3(2.9f, 2.55f, 53.6f), 0.035f, Art.Plain(new Color(0.72f, 0.62f, 0.2f), 0.4f), root);
            Kit.Tube("WaterPipe", new Vector3(-2.9f, 0.5f, 30f), new Vector3(-2.9f, 0.5f, 47.5f), 0.045f, greyPaint, root);
            Kit.Tube("Conduit", new Vector3(-2.93f, 2.9f, 28.4f), new Vector3(-2.93f, 2.9f, 47.6f), 0.02f, darkMetal, root, 6);

            System.Random random = new System.Random(8);
            for (int i = 0; i < 9; i++)
            {
                float z = 30f + i * 2.6f + (float)random.NextDouble();
                Kit.Cable("AlleyLine", new Vector3(-2.95f, 4.4f + (float)random.NextDouble() * 3f, z),
                    new Vector3(2.95f, 4.2f + (float)random.NextDouble() * 3f, z + (float)random.NextDouble() * 2f - 1f),
                    0.25f + (float)random.NextDouble() * 0.3f, 0.012f, darkMetal, root, 12);
            }
            Laundry(root, new Vector3(-2.9f, 5.3f, 35.5f), new Vector3(2.9f, 5.1f, 36.1f), random, 6);
            Laundry(root, new Vector3(-2.9f, 6.2f, 42.2f), new Vector3(2.9f, 6.4f, 41.6f), random, 5);
            Laundry(root, new Vector3(-2.9f, 4.9f, 51.4f), new Vector3(2.9f, 5.0f, 51.0f), random, 5);

            // Caged work lights: the alley is in shade even in the afternoon.
            float[] lampZ = { 32.5f, 39.5f, 45.2f, 51.5f };
            for (int i = 0; i < lampZ.Length; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Vector3 at = new Vector3(side * 2.75f, 3.05f, lampZ[i]);
                NoShadow(Kit.Tube("LampArm", new Vector3(side * 2.98f, 3.25f, lampZ[i]), at + Vector3.up * 0.2f, 0.018f, darkMetal, root, 5));
                Kit.Lathe("LampShade", at + Vector3.up * 0.04f, new[] { new Vector2(0.05f, 0.12f), new Vector2(0.16f, 0f), new Vector2(0.17f, 0f), new Vector2(0.06f, 0.13f), new Vector2(0.03f, 0.16f) },
                    greenPaint, root, 16);
                NoShadow(Kit.Lathe("LampBulb", at - Vector3.up * 0.02f, new[] { new Vector2(0, 0), new Vector2(0.04f, 0.03f), new Vector2(0.03f, 0.09f), new Vector2(0, 0.1f) },
                    Art.Glow(new Color(1f, 0.72f, 0.4f), 7f), root, 10));
                Kit.Lamp("AlleyLamp", at - Vector3.up * 0.15f, new Color(1f, 0.70f, 0.40f), i == 2 ? 5.5f : 4f, 7.5f, root, i == 0 || i == 2);
            }

            // Things left against the walls. None of these block the walking line down the middle.
            Kit.Prop("trashbag", new Vector3(-2.55f, 0, 30.2f), 20, root);
            Kit.Prop("trashbag", new Vector3(-2.6f, 0, 30.9f), 140, root, false, 0.9f);
            Kit.Prop("trashbag", new Vector3(2.35f, 0, 33.6f), 70, root, false, 1.1f);
            Kit.Prop("cardboard_box_01", new Vector3(2.5f, 0, 34.4f), 12, root);
            Kit.Prop("cardboard_box_01", new Vector3(2.55f, 0, 35.0f), 80, root, false, 0.8f);
            Kit.Prop("plastic_crate_01", new Vector3(-2.65f, 0, 36.8f), 5, root);
            Kit.Prop("plastic_crate_01", new Vector3(-2.65f, Kit.PropSize("plastic_crate_01").y, 36.8f), 12, root);
            Kit.Prop("plastic_broom", new Vector3(-2.85f, 0, 38.2f), 90, root);
            Kit.Prop("wooden_ladder_02", new Vector3(2.62f, 0, 38.3f), -90, root);
            Kit.Prop("plastic_jerrycan", new Vector3(2.6f, 0, 40.4f), 30, root);
            Kit.Prop("cement_bag", new Vector3(-2.55f, 0, 41.2f), 95, root);
            Kit.Prop("cement_bag", new Vector3(-2.55f, Kit.PropSize("cement_bag").y, 41.3f), 80, root);
            Kit.Prop("old_tyre", new Vector3(2.75f, 0.3f, 42.6f), 0, root, false, 1f, null, Quaternion.Euler(75, 90, 0));
            Kit.Prop("hand_truck", new Vector3(-2.6f, 0, 49.6f), 95, root);
            Kit.Prop("garden_hose_wall_mounted_01", new Vector3(2.98f, 1.1f, 50.3f), -90, root, false, 1f, new Vector3(0, 0, -1));
            Kit.Prop("korean_fire_extinguisher_01", new Vector3(2.8f, 0, 46.9f), -90, root);
            Kit.Prop("security_light", new Vector3(1.9f, 3.0f, 47.82f), 180, root, false, 1f, new Vector3(0, 0, -1));
            Kit.Spot("GateLight", new Vector3(1.6f, 2.95f, 47.4f), new Vector3(-0.45f, -1f, -0.5f), new Color(0.82f, 0.9f, 1f), 9f, 9f, 95f, root, true);

            // The jammed gate: sliding leaf, its floor track, pillars and a beam overhead.
            Material pillar = S("concrete_wall_008");
            Kit.Box("GateLeft", new Vector3(-2.2f, 1.5f, 48), new Vector3(1.6f, 3, 0.35f), pillar, root, true);
            Kit.Box("GateRight", new Vector3(2.2f, 1.5f, 48), new Vector3(1.6f, 3, 0.35f), pillar, root, true);
            Kit.Box("GateBeam", new Vector3(0, 3.3f, 48), new Vector3(6f, 0.6f, 0.45f), pillar, root);
            Kit.Box("GateCanopy", new Vector3(0, 3.66f, 48.1f), new Vector3(6f, 0.05f, 1.5f), S("corrugated_iron_02"), root);
            Kit.Box("GateTrack", new Vector3(-1.4f, 0.012f, 48.3f), new Vector3(5.8f, 0.024f, 0.1f), Art.Metal(new Color(0.3f, 0.27f, 0.25f), 0.35f), root);
            NoShadow(Kit.Box("GateTopGuide", new Vector3(-1.4f, 2.78f, 48.3f), new Vector3(5.8f, 0.06f, 0.12f), darkMetal, root));

            GameObject gate = new GameObject("JamGate");
            gate.transform.SetParent(root, false);
            gate.transform.position = DemoGame.GateClosed;
            gate.AddComponent<BoxCollider>().size = new Vector3(2.8f, 2.7f, 0.18f);
            Material leaf = S("rusty_painted_metal");
            Kit.Box("Leaf", Vector3.zero, new Vector3(2.72f, 2.6f, 0.03f), leaf, gate.transform);
            Material rail = Art.Tint("surf/rusty_metal_02", new Color(0.55f, 0.5f, 0.46f));
            foreach (float y in new[] { -1.28f, -0.45f, 0.45f, 1.28f })
                Kit.Box("Rail", new Vector3(0, y, 0), new Vector3(2.8f, 0.07f, 0.09f), rail, gate.transform);
            foreach (float x in new[] { -1.37f, 0f, 1.37f })
                Kit.Box("Stile", new Vector3(x, 0, 0), new Vector3(0.07f, 2.66f, 0.09f), rail, gate.transform);
            Kit.Box("Bolt", new Vector3(1.1f, -0.1f, -0.07f), new Vector3(0.3f, 0.05f, 0.05f), darkMetal, gate.transform);
            foreach (float x in new[] { -1.1f, 1.1f })
                Kit.Lathe("Roller", new Vector3(x, -1.33f, 0), new[] { new Vector2(0, -0.03f), new Vector2(0.05f, -0.03f), new Vector2(0.05f, 0.03f), new Vector2(0, 0.03f) },
                    darkMetal, gate.transform, 12).transform.localRotation = Quaternion.Euler(90, 0, 0);
            game.Gate = gate;

            for (int i = 0; i < 2; i++)
            {
                int index = i;
                GameObject debris = new GameObject("Debris" + i);
                debris.transform.SetParent(root, false);
                debris.transform.position = new Vector3(i == 0 ? -0.8f : 0.8f, 0.5f, 44);
                debris.AddComponent<BoxCollider>().size = new Vector3(1.2f, 1f, 1.0f);
                Kit.Prop("wooden_crate_02", new Vector3(0, -0.5f, 0), i == 0 ? 8 : -12, debris.transform);
                Kit.Prop("wooden_crate_01", new Vector3(i == 0 ? -0.1f : 0.12f, -0.5f + Kit.PropSize("wooden_crate_02").y, 0.02f), i == 0 ? -25 : 31, debris.transform);
                if (i == 0) Kit.Prop("cardboard_box_01", new Vector3(0.22f, -0.5f + Kit.PropSize("wooden_crate_02").y + Kit.PropSize("wooden_crate_01").y, 0), 50, debris.transform, false, 0.8f);
                else Kit.Prop("plastic_jerrycan", new Vector3(-0.2f, -0.5f + Kit.PropSize("wooden_crate_02").y + Kit.PropSize("wooden_crate_01").y, 0.05f), 15, debris.transform);
                game.AddInteraction(debris, "搬开挡路的杂物", () => game.State.Phase == OpeningPhase.Debris,
                    () => game.MoveDebris(debris, index));
            }
        }

        private static void BuildCourtyard()
        {
            Material worn = S("worn_plaster_wall"), brick = S("red_brick_03");
            Facade("CourtWest", new Vector3(-8.75f, 0, 72), 90, 18, Art.Tint("surf/plastered_wall_04", new Color(0.92f, 0.88f, 0.78f)), 3, "WDWWDW", null, 301, 7f, 3.2f, true, S("long_white_tiles"), false);
            Facade("CourtEast", new Vector3(8.75f, 0, 54), -90, 18, worn, 3, "WWDWWD", null, 302, 7f, 3.2f, true, null, false);
            foreach (float side in new[] { -1f, 1f })
            {
                Kit.Box("CourtNorthWall", new Vector3(side * 5.4f, 1.6f, 71.9f), new Vector3(6.8f, 3.2f, 0.3f), brick, root);
                Kit.Box("CourtNorthCap", new Vector3(side * 5.4f, 3.24f, 71.9f), new Vector3(6.9f, 0.08f, 0.42f), trim, root);
                Kit.Box("GatePost", new Vector3(side * 2.0f, 1.9f, 71.9f), new Vector3(0.5f, 3.8f, 0.5f), brick, root);
                Kit.Box("GatePostCap", new Vector3(side * 2.0f, 3.84f, 71.9f), new Vector3(0.62f, 0.08f, 0.62f), trim, root);
            }
            Kit.Tube("ArchBar", new Vector3(-2f, 3.5f, 71.9f), new Vector3(2f, 3.5f, 71.9f), 0.04f, darkMetal, root);

            System.Random random = new System.Random(4);
            Laundry(root, new Vector3(-8.6f, 4.6f, 59f), new Vector3(8.6f, 4.9f, 60.5f), random, 9);
            Laundry(root, new Vector3(-8.6f, 5.4f, 66.5f), new Vector3(8.6f, 5.1f, 65.4f), random, 8);
            for (int i = 0; i < 4; i++)
                Kit.Cable("CourtLine", new Vector3(-8.7f, 7f + i * 0.5f, 56f + i * 4f), new Vector3(8.7f, 6.6f + i * 0.4f, 57f + i * 4.2f), 0.7f, 0.012f, darkMetal, root, 16);

            Kit.Prop("painted_wooden_bench", new Vector3(-7.9f, 0, 61.5f), 90, root, true);
            Kit.Prop("painted_wooden_bench", new Vector3(7.9f, 0, 66.5f), -90, root, true);
            Kit.Prop("planter_box_01", new Vector3(-8.1f, 0, 64.6f), 90, root, true);
            Kit.Prop("planter_box_01", new Vector3(-8.1f, 0, 58.4f), 90, root, true);
            Kit.Prop("planter_box_01", new Vector3(8.1f, 0, 61.6f), 90, root, true);
            Kit.Prop("potted_plant_04", new Vector3(7.9f, 0, 63.4f), 30, root, false, 3.4f);
            Kit.Prop("potted_plant_04", new Vector3(-7.8f, 0, 67.2f), 200, root, false, 3f);
            Kit.Prop("covered_car", new Vector3(5.4f, 0, 69.2f), 82, root, true);
            Kit.Prop("wooden_stool_01", new Vector3(-6.6f, 0, 62.3f), 20, root);
            Kit.Prop("plastic_monobloc_chair_01", new Vector3(6.9f, 0, 58.2f), -120, root, true);
            Kit.Prop("utility_box_01", new Vector3(-8.2f, 0, 70.2f), 90, root, true);
            Kit.Prop("trashbag", new Vector3(-7.9f, 0, 69.3f), 45, root);
            Kit.Prop("street_lamp_01", new Vector3(-3.2f, 0, 70.9f), 0, root, false, 1.25f);
        }

        private static void BuildBackdrop()
        {
            // Distant blocks are simple boxes carrying a window pattern; haze and focus do the rest.
            Texture2D pattern = Art.Own(new Texture2D(128, 128, TextureFormat.RGB24, true));
            Color[] pixels = new Color[128 * 128];
            System.Random random = new System.Random(3);
            Color wallColour = new Color(0.62f, 0.60f, 0.56f), window = new Color(0.10f, 0.12f, 0.14f);
            bool[] lit = new bool[64];
            for (int i = 0; i < lit.Length; i++) lit[i] = random.Next(12) == 0;
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    int cx = x / 16, cy = y / 16, lx = x % 16, ly = y % 16;
                    bool pane = lx > 3 && lx < 12 && ly > 4 && ly < 12;
                    float dirt = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.16f;
                    pixels[y * 128 + x] = pane ? (lit[cy * 8 + cx] ? new Color(0.75f, 0.6f, 0.38f) : window) : wallColour * (0.9f + dirt);
                }
            pattern.SetPixels(pixels);
            pattern.Apply(true);
            float[,] blocks =
            {
                { -34, 4, 18, 30, 26 }, { -32, 34, 16, 26, 38 }, { -38, 66, 22, 30, 30 }, { 33, 0, 18, 28, 34 }, { 36, 32, 20, 32, 24 },
                { 34, 68, 18, 30, 42 }, { -14, 104, 26, 16, 34 }, { 16, 108, 24, 18, 46 }, { 0, 132, 60, 14, 28 }, { -16, -36, 28, 18, 40 },
                { 18, -40, 26, 18, 30 }, { -52, 100, 24, 24, 52 }, { 52, 104, 24, 24, 36 }, { 0, -64, 70, 14, 24 }
            };
            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                float tone = 0.75f + (float)random.NextDouble() * 0.35f;
                Material block = Art.Textured("mat/plain", pattern, new Color(tone, tone * 0.98f, tone * 0.95f), "block" + i % 4);
                NoShadow(Kit.Box("DistantBlock", new Vector3(blocks[i, 0], blocks[i, 4] / 2f - 0.3f, blocks[i, 1]),
                    new Vector3(blocks[i, 2], blocks[i, 4], blocks[i, 3]), block, root, false, 12.4f));
            }
            // A facade to close the view through the courtyard gateway.
            Facade("BeyondBlock", new Vector3(14f, 0, 90f), 180, 28, S("yellow_plaster"), 4, "SGSDSGSS",
                new[] { "旅社", "五金", "小吃", null, "服装", "通讯", "茶叶", "日杂" }, 404, 8f, 3.5f, true);
            Kit.Prop("street_lamp_01", new Vector3(5f, 0, 78f), 0, root, false, 1.25f);
            Kit.Prop("covered_car", new Vector3(-6.5f, 0, 80.5f), 95, root);
        }

        private static void BuildActors(DemoGame game, Camera camera)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.1f, 2);
            Transform playerBody = Model(player.transform, "Player", "m_idle_neutral_01", "m_walk_neutral", "m_run_neutral", "m_run_fast_01");
            SetLayer(player, 8);
            game.Player = player.AddComponent<ThirdPersonPlayer>();
            game.Player.Configure(playerBody, camera);
            game.View = camera;

            game.Woman = Actor("woman", "同行的她", new Vector3(2, 0.1f, 4), "Woman", "f_idle_neutral_01", "f_walk_neutral", "f_run_neutral", "f_run_fast_01");
            game.Dog = Actor("dog", "狗", new Vector3(0.7f, 0.1f, 4), "Dog");
            game.Zhou = Actor("zhou", "老周", new Vector3(-7.0f, 0.2f, 14.1f), "Zhou", "m_idle_look_around_01", "m_walk_neutral", "m_run_neutral");
            game.ResidentA = Actor("residentA", "街坊", new Vector3(-1, 0.1f, 20), "ResidentA", "f_idle_neutral_01", "f_walk_neutral", "f_run_neutral");
            game.ResidentB = Actor("residentB", "街坊", new Vector3(1.5f, 0.1f, 18), "ResidentB", "m_idle_neutral_01", "m_walk_neutral", "m_run_neutral");
            game.Dog.FollowTarget = game.Woman.transform;
            game.Dog.FollowOffset = new Vector3(-0.9f, 0, -0.4f);

            game.Woman.transform.rotation = Quaternion.Euler(0, 215, 0);
            game.Woman.Body.LookTarget = player.transform;
            game.Dog.transform.rotation = Quaternion.Euler(0, 80, 0);
            game.Dog.DogBody.LookTarget = player.transform;
            game.Zhou.transform.rotation = Quaternion.Euler(0, 0, 0);
            game.Zhou.Body.Stance("m_work_table");
            game.ResidentA.transform.rotation = Quaternion.Euler(0, 100, 0);
            game.ResidentB.transform.rotation = Quaternion.Euler(0, 285, 0);
            game.ResidentA.Body.Stance("f_gestic_talk_neutral_01");
            game.ResidentB.Body.Stance("m_gestic_talk_neutral_01");
            game.ResidentA.Body.LookTarget = game.ResidentB.transform;
            game.ResidentB.Body.LookTarget = game.ResidentA.transform;

            game.AddInteraction(game.Zhou.gameObject, "和修理铺的人说话",
                () => game.State.Phase == OpeningPhase.Street, game.MeetZhou);
            game.AddInteraction(game.Woman.gameObject, "确认她和狗都平安",
                () => game.State.Phase == OpeningPhase.Reunion, game.Reunite);
            game.AddInteraction(game.Zhou.gameObject, "帮老周扶住门，开始修理",
                () => game.State.Phase == OpeningPhase.Debris && game.State.ClearedDebrisCount == 2, game.StartRepair);
            game.AddInteraction(game.Zhou.gameObject, "确认大家都跟上了",
                () => game.State.Phase == OpeningPhase.Regroup, game.Finish);
        }

        private static void BuildStreetDetails(DemoGame game)
        {
            Color fadedPaint = new Color(0.58f, 0.56f, 0.49f);
            Color rust = new Color(0.43f, 0.29f, 0.22f);
            Color mutedGreen = new Color(0.34f, 0.4f, 0.35f);

            Box("WestCurb", new Vector3(-9.45f, 0.06f, 12), new Vector3(1.7f, 0.12f, 32),
                new Color(0.42f, 0.41f, 0.38f), null, false);
            Box("EastCurb", new Vector3(9.45f, 0.06f, 12), new Vector3(1.7f, 0.12f, 32),
                new Color(0.42f, 0.41f, 0.38f), null, false);

            for (int i = 0; i < 5; i++)
            {
                float x = -4.8f + i * 2.4f;
                Box("WornCrosswalk", new Vector3(x, 0.015f, 27.1f), new Vector3(1.2f, 0.025f, 0.45f),
                    fadedPaint, null, false);
            }

            Box("WorkshopSign", new Vector3(-10.22f, 4.3f, 15), new Vector3(0.22f, 0.9f, 3.3f),
                new Color(0.43f, 0.36f, 0.27f), null, false);
            Box("WorkshopAwning", new Vector3(-9.65f, 2.95f, 15), new Vector3(1.2f, 0.18f, 4.6f),
                new Color(0.48f, 0.31f, 0.24f), null, false);
            for (int i = 0; i < 4; i++)
                Box("AwningStripe", new Vector3(-9.62f, 2.84f, 13.15f + i * 1.23f),
                    new Vector3(1.17f, 0.04f, 0.08f), fadedPaint, null, false);

            Box("ToolCrateA", new Vector3(-9.15f, 0.42f, 18.5f), new Vector3(0.85f, 0.8f, 0.85f),
                mutedGreen, null, false);
            Box("ToolCrateB", new Vector3(-8.75f, 0.38f, 19.25f), new Vector3(0.72f, 0.72f, 0.7f),
                Warm * 0.72f, null, false);
            Box("CrateBrace", new Vector3(-9.15f, 0.43f, 18.5f), new Vector3(0.88f, 0.08f, 0.08f),
                rust, null, false);

            Shape(PrimitiveType.Cylinder, "StreetDrain", new Vector3(7.5f, 0.025f, 24),
                new Vector3(0.85f, 0.025f, 0.85f), MetalColor(), null);
            for (int i = 0; i < 3; i++)
                Box("DrainSlot", new Vector3(7.5f, 0.06f, 23.65f + i * 0.34f),
                    new Vector3(0.56f, 0.015f, 0.05f), Stone, null, false);

            Box("LampPost", new Vector3(8.65f, 2.55f, 8), new Vector3(0.14f, 5.1f, 0.14f),
                MetalColor(), null, false);
            Box("LampArm", new Vector3(8.25f, 4.9f, 8), new Vector3(0.85f, 0.1f, 0.12f),
                MetalColor(), null, false);
            GameObject lamp = Shape(PrimitiveType.Sphere, "StreetLampHead", new Vector3(7.87f, 4.83f, 8),
                new Vector3(0.32f, 0.16f, 0.26f), new Color(0.79f, 0.68f, 0.44f), null);
            game.StreetLamp = lamp.AddComponent<Light>();
            game.StreetLamp.type = LightType.Point;
            game.StreetLamp.color = new Color(1f, 0.72f, 0.42f);
            game.StreetLamp.range = 9f;
            game.StreetLamp.intensity = 0.7f;
            game.StreetLamp.enabled = false;

            Box("AlleyCable", new Vector3(0, 4.25f, 39), new Vector3(5.5f, 0.045f, 0.045f),
                new Color(0.12f, 0.13f, 0.13f), null, false);
            Box("AlleyCable", new Vector3(0, 4.05f, 50), new Vector3(5.5f, 0.045f, 0.045f),
                new Color(0.12f, 0.13f, 0.13f), null, false);
            Box("CourtyardWayfinding", new Vector3(8.3f, 1.7f, 65), new Vector3(0.16f, 1.1f, 2.4f),
                mutedGreen, null, false);
            Box("WayfindingArrow", new Vector3(8.18f, 1.7f, 64.5f), new Vector3(0.04f, 0.12f, 0.55f),
                fadedPaint, null, false);
        }

        private static Color MetalColor()
        {
            return new Color(0.34f, 0.37f, 0.38f);
        }
    }
}
