using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace DontForgetMe
{
    // Runtime access to the generated art catalog, plus the materials, textures and meshes
    // the procedural world creates on top of it.
    public static class Art
    {
        private static ArtCatalog catalog;
        private static readonly Dictionary<string, ArtCatalog.Entry> Entries = new Dictionary<string, ArtCatalog.Entry>();
        private static readonly Dictionary<string, Material> Variants = new Dictionary<string, Material>();
        private static readonly Dictionary<Material, float> Tiles = new Dictionary<Material, float>();
        private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();

        public static void Load()
        {
            if (catalog != null) return;
            catalog = Resources.Load<ArtCatalog>("DFM/ArtCatalog");
            if (catalog == null)
                throw new InvalidOperationException(
                    "Art catalog is missing. In the Unity editor run: Dont Forget Me > Rebuild Art Library.");
            Entries.Clear();
            foreach (ArtCatalog.Entry entry in catalog.entries)
            {
                Entries[entry.id] = entry;
                if (entry.asset is Material material && entry.size.x > 0) Tiles[material] = entry.size.x;
            }
        }

        public static bool Has(string id)
        {
            Load();
            return Entries.ContainsKey(id);
        }

        public static ArtCatalog.Entry Entry(string id)
        {
            Load();
            if (!Entries.TryGetValue(id, out ArtCatalog.Entry entry))
                throw new KeyNotFoundException("Art catalog has no entry '" + id + "'. Rebuild the art library.");
            return entry;
        }

        public static T Get<T>(string id) where T : UnityEngine.Object
        {
            T asset = Entry(id).asset as T;
            if (asset == null) throw new InvalidCastException("Art entry '" + id + "' is not a " + typeof(T).Name + ".");
            return asset;
        }

        public static Material Surface(string id)
        {
            return Get<Material>("surf/" + id);
        }

        public static Material Base(string id)
        {
            return Get<Material>("mat/" + id);
        }

        public static float TileOf(Material material)
        {
            return material != null && Tiles.TryGetValue(material, out float tile) ? tile : 2f;
        }

        public static T Own<T>(T created) where T : UnityEngine.Object
        {
            Owned.Add(created);
            return created;
        }

        // A tinted copy of a catalog material. Copies keep the source keywords, so the shader
        // variants they need are the ones already included for the source asset.
        public static Material Tint(string id, Color color, float smoothness = -1f, float tile = -1f)
        {
            string key = id + "|" + ColorUtility.ToHtmlStringRGBA(color) + "|" + smoothness.ToString("F2") + "|" + tile.ToString("F2");
            if (Variants.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material source = Get<Material>(id);
            Material copy = Own(new Material(source));
            copy.SetColor("_BaseColor", color);
            if (smoothness >= 0) copy.SetFloat("_Smoothness", smoothness);
            Tiles[copy] = tile > 0 ? tile : TileOf(source);
            Variants[key] = copy;
            return copy;
        }

        public static Material Plain(Color color, float smoothness = 0.25f)
        {
            return Tint("mat/plain", color, smoothness);
        }

        public static Material Metal(Color color, float smoothness = 0.55f)
        {
            return Tint("mat/metal", color, smoothness);
        }

        public static Material Glow(Color color, float intensity)
        {
            string key = "glow|" + ColorUtility.ToHtmlStringRGBA(color) + "|" + intensity.ToString("F2");
            if (Variants.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material copy = Own(new Material(Base("emissive")));
            copy.SetColor("_BaseColor", color * 0.4f);
            copy.SetColor("_EmissionColor", color.linear * intensity);
            Variants[key] = copy;
            return copy;
        }

        public static Material Textured(string id, Texture texture, Color color, string key)
        {
            key = "tex|" + id + "|" + key;
            if (Variants.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material copy = Own(new Material(Get<Material>(id)));
            copy.SetTexture("_BaseMap", texture);
            copy.SetColor("_BaseColor", color);
            Variants[key] = copy;
            return copy;
        }

        public static Font SignFont => Get<Font>("font/sans");

        // The sky's diffuse contribution, rebuilt from coarse samples of the same HDR image the
        // skybox uses, so ambient light matches what is visible overhead in editor and builds alike.
        public static SphericalHarmonicsL2 SkyAmbient(float intensity)
        {
            string[] lines = Get<TextAsset>("skysamples").text.Split('\n');
            List<Vector3> directions = new List<Vector3>();
            List<Vector3> colours = new List<Vector3>();
            SphericalHarmonicsL2 probe = new SphericalHarmonicsL2();
            foreach (string line in lines)
            {
                string[] parts = line.Trim().Split(' ');
                if (parts.Length != 6) continue;
                float[] v = new float[6];
                for (int i = 0; i < 6; i++) v[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
                Vector3 direction = new Vector3(v[0], v[1], v[2]);
                directions.Add(direction);
                colours.Add(new Vector3(v[3], v[4], v[5]));
                probe.AddDirectionalLight(direction, new Color(v[3], v[4], v[5]), 1f);
            }
            if (directions.Count == 0) throw new InvalidOperationException("Sky sample table is empty.");

            // Normalise against a direct evaluation, so the result does not depend on the
            // engine's internal scaling convention for lights added to a probe.
            Vector3[] check = { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            Color[] evaluated = new Color[check.Length];
            probe.Evaluate(check, evaluated);
            float expected = 0, actual = 0;
            for (int n = 0; n < check.Length; n++)
            {
                for (int i = 0; i < directions.Count; i++)
                {
                    float cosine = Mathf.Max(0, Vector3.Dot(check[n], directions[i]));
                    expected += (colours[i].x + colours[i].y + colours[i].z) * cosine;
                }
                actual += evaluated[n].r + evaluated[n].g + evaluated[n].b;
            }
            return probe * (actual > 1e-5f ? expected / actual * intensity : intensity);
        }

        public static void Release()
        {
            foreach (UnityEngine.Object item in Owned)
                if (item != null) UnityEngine.Object.Destroy(item);
            Owned.Clear();
            Variants.Clear();
        }
    }

    // Small geometry builders. Everything is laid out in the space of the given parent, and
    // texture coordinates follow those coordinates so neighbouring pieces tile without seams.
    public static class Kit
    {
        private static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        public static GameObject Box(string name, Vector3 center, Vector3 size, Material material,
            Transform parent = null, bool collide = false, float tile = 0f)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = center;
            if (tile <= 0) tile = Art.TileOf(material);
            Vector3 h = size * 0.5f;
            Vector3[] vertices = new Vector3[24];
            Vector3[] normals = new Vector3[24];
            Vector2[] uv = new Vector2[24];
            int[] triangles = new int[36];
            for (int face = 0; face < 6; face++)
            {
                Vector3 n = FaceNormals[face];
                Vector3 u = face < 2 ? Vector3.forward : Vector3.right;
                Vector3 v = face == 2 || face == 3 ? Vector3.forward : Vector3.up;
                if (Vector3.Dot(Vector3.Cross(u, v), n) < 0) u = -u;
                for (int corner = 0; corner < 4; corner++)
                {
                    float su = corner == 0 || corner == 3 ? -1 : 1;
                    float sv = corner < 2 ? -1 : 1;
                    Vector3 p = Vector3.Scale(n + u * su + v * sv, h);
                    int index = face * 4 + corner;
                    vertices[index] = p;
                    normals[index] = n;
                    Vector3 w = center + p;
                    uv[index] = face < 2 ? new Vector2(w.z, w.y) / tile
                        : face < 4 ? new Vector2(w.x, w.z) / tile : new Vector2(w.x, w.y) / tile;
                }
                int t = face * 6, b = face * 4;
                // Unity treats clockwise triangles as front-facing.
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
                triangles[t + 3] = b; triangles[t + 4] = b + 2; triangles[t + 5] = b + 3;
            }
            Finish(item, name, vertices, normals, uv, triangles, material);
            if (collide) item.AddComponent<BoxCollider>().size = size;
            return item;
        }

        // A flat rectangle facing "normal". Used for glass, paint, puddles and cloth.
        public static GameObject Quad(string name, Vector3 center, Vector3 normal, Vector3 up, Vector2 size,
            Material material, Transform parent = null, float tile = 0f, bool shadows = false)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = center;
            normal.Normalize();
            Vector3 right = Vector3.Cross(normal, up).normalized;
            up = Vector3.Cross(right, normal);
            Vector3 x = right * size.x * 0.5f, y = up * size.y * 0.5f;
            Vector3[] vertices = { -x - y, x - y, x + y, -x + y };
            Vector3[] normals = { normal, normal, normal, normal };
            Vector2[] uv = tile > 0
                ? new[] { Vector2.zero, new Vector2(size.x / tile, 0), new Vector2(size.x / tile, size.y / tile), new Vector2(0, size.y / tile) }
                : new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            Finish(item, name, vertices, normals, uv, new[] { 0, 2, 1, 0, 3, 2 }, material);
            if (!shadows) item.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return item;
        }

        // A round bar between two points: pipes, poles, rails.
        public static GameObject Tube(string name, Vector3 from, Vector3 to, float radius, Material material,
            Transform parent = null, int sides = 10)
        {
            return Path(name, new[] { from, to }, radius, material, parent, sides);
        }

        // A hanging line between two fixings.
        public static GameObject Cable(string name, Vector3 from, Vector3 to, float sag, float radius, Material material,
            Transform parent = null, int segments = 14)
        {
            Vector3[] points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                points[i] = Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));
            }
            GameObject item = Path(name, points, radius, material, parent, 5);
            item.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return item;
        }

        public static GameObject Path(string name, Vector3[] points, float radius, Material material, Transform parent, int sides)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            int rings = points.Length;
            Vector3[] vertices = new Vector3[rings * (sides + 1)];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            List<int> triangles = new List<int>();
            float travelled = 0;
            for (int r = 0; r < rings; r++)
            {
                Vector3 axis = (points[Mathf.Min(r + 1, rings - 1)] - points[Mathf.Max(r - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 other = Vector3.Cross(axis, side);
                if (r > 0) travelled += Vector3.Distance(points[r], points[r - 1]);
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / sides;
                    Vector3 n = side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                    int index = r * (sides + 1) + s;
                    vertices[index] = points[r] + n * radius;
                    normals[index] = n;
                    uv[index] = new Vector2((float)s / sides, travelled);
                    if (r == rings - 1 || s == sides) continue;
                    int next = index + sides + 1;
                    triangles.AddRange(new[] { index, index + 1, next, index + 1, next + 1, next });
                }
            }
            Finish(item, name, vertices, normals, uv, triangles.ToArray(), material);
            return item;
        }

        // A shape turned around the vertical axis from a profile of (radius, height) points.
        public static GameObject Lathe(string name, Vector3 position, Vector2[] profile, Material material,
            Transform parent = null, int sides = 28)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            int rings = profile.Length;
            Vector3[] vertices = new Vector3[rings * (sides + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            List<int> triangles = new List<int>();
            for (int r = 0; r < rings; r++)
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / sides;
                    int index = r * (sides + 1) + s;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * profile[r].x, profile[r].y, Mathf.Sin(angle) * profile[r].x);
                    uv[index] = new Vector2((float)s / sides, (float)r / (rings - 1));
                    if (r == rings - 1 || s == sides) continue;
                    int next = index + sides + 1;
                    triangles.AddRange(new[] { index, next, index + 1, index + 1, next, next + 1 });
                }
            Mesh mesh = Finish(item, name, vertices, null, uv, triangles.ToArray(), material);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return item;
        }

        private static Mesh Finish(GameObject item, string name, Vector3[] vertices, Vector3[] normals, Vector2[] uv,
            int[] triangles, Material material)
        {
            Mesh mesh = Art.Own(new Mesh { name = name });
            mesh.vertices = vertices;
            if (normals != null) mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            if (normals != null) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            return mesh;
        }

        // Places an imported model. "anchor" picks the point of its bounding box that lands on
        // "position": (0,-1,0) is the middle of the underside, (0,0,-1) the middle of the back.
        public static GameObject Prop(string id, Vector3 position, float yaw = 0f, Transform parent = null,
            bool collide = false, float scale = 1f, Vector3? anchor = null, Quaternion? rotation = null)
        {
            return Prop(id, position, Vector3.one * scale, rotation ?? Quaternion.Euler(0, yaw, 0), parent, collide, anchor);
        }

        public static GameObject Prop(string id, Vector3 position, Vector3 scale, Quaternion rotation, Transform parent,
            bool collide, Vector3? anchor = null)
        {
            ArtCatalog.Entry entry = Art.Entry("prop/" + id);
            // The model keeps whatever rotation and scale its file gave it; placement happens on a holder.
            GameObject item = new GameObject(id);
            item.transform.SetParent(parent, false);
            GameObject model = UnityEngine.Object.Instantiate((GameObject)entry.asset, item.transform, false);
            model.name = "Model";
            int variant = id.IndexOf('#');
            if (variant >= 0)
            {
                int keep = int.Parse(id.Substring(variant + 1), CultureInfo.InvariantCulture);
                Renderer[] parts = model.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < parts.Length; i++) parts[i].enabled = i == keep;
            }
            Vector3 a = anchor ?? Vector3.down;
            Vector3 pivot = Vector3.Scale(entry.center + Vector3.Scale(a, entry.size * 0.5f), scale);
            item.transform.localRotation = rotation;
            item.transform.localScale = scale;
            item.transform.localPosition = position - rotation * pivot;
            if (collide)
            {
                BoxCollider box = item.AddComponent<BoxCollider>();
                box.center = entry.center;
                box.size = entry.size;
            }
            return item;
        }

        public static Vector3 PropSize(string id)
        {
            return Art.Entry("prop/" + id).size;
        }

        // Scales a model so its bounding box fills the given dimensions exactly.
        public static GameObject PropFit(string id, Vector3 position, Vector3 dimensions, Quaternion rotation,
            Transform parent, Vector3 anchor)
        {
            Vector3 size = PropSize(id);
            Vector3 scale = new Vector3(dimensions.x / Mathf.Max(size.x, 0.001f), dimensions.y / Mathf.Max(size.y, 0.001f),
                dimensions.z / Mathf.Max(size.z, 0.001f));
            return Prop(id, position, scale, rotation, parent, false, anchor);
        }

        public static Light Lamp(string name, Vector3 position, Color color, float intensity, float range,
            Transform parent = null, bool shadows = false)
        {
            Light light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.4f;
            return light;
        }

        public static Light Spot(string name, Vector3 position, Vector3 direction, Color color, float intensity,
            float range, float angle, Transform parent = null, bool shadows = false)
        {
            Light light = Lamp(name, position, color, intensity, range, parent, shadows);
            light.type = LightType.Spot;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.55f;
            light.transform.localRotation = Quaternion.LookRotation(direction);
            return light;
        }

        // Painted or lit lettering on a wall. "facing" is the direction the text looks toward.
        public static TextMesh Sign(string text, Vector3 position, Vector3 facing, float height, Color color,
            float glow, Transform parent = null)
        {
            GameObject item = new GameObject("Sign " + text);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
            TextMesh mesh = item.AddComponent<TextMesh>();
            Font font = Art.SignFont;
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 96;
            mesh.characterSize = height * 10f / 96f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            Material material = Art.Own(new Material(Art.Base("text")));
            material.SetTexture("_MainTex", font.material.mainTexture);
            material.SetColor("_Color", color);
            material.SetFloat("_Glow", glow);
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            item.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return mesh;
        }

        // Deterministic noise textures for paint wear, stains and puddle edges.
        public static Texture2D Noise(string name, int size, Func<float, float, float, Color> shade, float scale, float seed)
        {
            Texture2D texture = Art.Own(new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name });
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / (size - 1), v = (float)y / (size - 1);
                    float n = Mathf.PerlinNoise(seed + u * scale, seed + v * scale) * 0.6f
                        + Mathf.PerlinNoise(seed * 2 + u * scale * 3.1f, seed * 2 + v * scale * 3.1f) * 0.28f
                        + Mathf.PerlinNoise(seed * 3 + u * scale * 9.3f, seed * 3 + v * scale * 9.3f) * 0.12f;
                    pixels[y * size + x] = shade(u, v, n);
                }
            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(true);
            return texture;
        }
    }
}
