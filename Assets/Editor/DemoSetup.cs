using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DontForgetMe.Editor
{
    // Turns the raw files under Assets/Art into the render pipeline assets, materials,
    // prefabs and catalog the runtime loads. Safe to run repeatedly.
    public static class DemoSetup
    {
        private const string Generated = "Assets/Art/Generated";
        private const string PipelinePath = "Assets/Settings/DontForgetMe_URP.asset";
        private const string RendererPath = "Assets/Settings/DontForgetMe_Renderer.asset";
        private const string CatalogPath = "Assets/Resources/DFM/ArtCatalog.asset";
        private const string VolumePath = "Assets/Art/Generated/Look.asset";

        // Real-world size of one texture repeat, in metres (from the Poly Haven asset pages).
        private static readonly Dictionary<string, float> SurfaceSize = new Dictionary<string, float>
        {
            { "asphalt_02", 3f }, { "blue_metal_plate", 2.5f }, { "brick_pavement_02", 2f },
            { "concrete_floor_worn_001", 3f }, { "concrete_wall_008", 2.71f }, { "corrugated_iron_02", 2.7f },
            { "denim_fabric", 0.4f }, { "dirty_concrete", 3f }, { "green_metal_rust", 1f },
            { "long_white_tiles", 1.27f }, { "old_wood_floor", 3f }, { "painted_metal_shutter", 2f },
            { "plastered_wall_04", 3.2f }, { "red_brick_03", 1f }, { "rough_plaster_brick_02", 1.5f },
            { "rusty_metal_02", 1f }, { "rusty_painted_metal", 2.2f }, { "square_concrete_pavers", 1.8f },
            { "worn_plaster_wall", 1.8f }, { "yellow_plaster", 2f }
        };

        private static StringBuilder report;

        [MenuItem("Dont Forget Me/Rebuild Art Library")]
        public static void Run()
        {
            report = new StringBuilder();
            AssetDatabase.Refresh();
            foreach (string folder in new[] { "Assets/Settings", "Assets/Resources", "Assets/Resources/DFM", Generated,
                Generated + "/Materials", Generated + "/Props", Generated + "/Characters" })
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));

            ConfigurePipeline();
            ArtCatalog catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.entries.Clear();
            BuildSurfaces(catalog);
            BuildBaseMaterials(catalog);
            BuildProps(catalog);
            BuildCharacters(catalog);
            BuildClips(catalog);
            BuildSky(catalog);
            BuildLook(catalog);
            // Bundled fonts: system fonts differ per machine and cannot be relied on for Chinese text.
            Add(catalog, "font/sans", AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansSC-Regular.otf"));
            Add(catalog, "font/serif", AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSerifSC-Medium.otf"));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/dfm_art_report.txt", report.ToString());
            Debug.Log("Art library rebuilt: " + catalog.entries.Count + " entries.");
        }

        private static void ConfigurePipeline()
        {
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                MethodInfo create = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (create == null)
                    throw new InvalidOperationException("This URP version has no CreateRendererAsset; update DemoSetup.");
                renderer = (UniversalRendererData)create.Invoke(null,
                    new object[] { RendererPath, RendererType.UniversalRenderer, false, "Renderer" });
            }

            SerializedObject data = new SerializedObject(renderer);
            Set(data, "m_RenderingMode", 2); // Forward+
            Set(data, "m_DepthPrimingMode", 0);
            Set(data, "m_ShadowTransparentReceive", true);
            data.ApplyModifiedPropertiesWithoutUndo();
            ConfigureAmbientOcclusion(renderer);

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            SerializedObject asset = new SerializedObject(pipeline);
            Set(asset, "m_RequireDepthTexture", true);
            Set(asset, "m_RequireOpaqueTexture", false);
            Set(asset, "m_SupportsHDR", true);
            Set(asset, "m_HDRColorBufferPrecision", 1);
            Set(asset, "m_MSAA", 1);
            Set(asset, "m_RenderScale", 1f);
            Set(asset, "m_UpscalingFilter", 3); // FSR, used only when a build lowers the render scale
            Set(asset, "m_MainLightShadowsSupported", true);
            Set(asset, "m_MainLightShadowmapResolution", 4096);
            Set(asset, "m_AdditionalLightsRenderingMode", 1);
            Set(asset, "m_AdditionalLightsPerObjectLimit", 8);
            Set(asset, "m_AdditionalLightShadowsSupported", true);
            Set(asset, "m_AdditionalLightsShadowmapResolution", 2048);
            // Every local light uses the "high" tier, so this is the size of each shadow map: 16 fit
            // in the atlas. A point light needs 6 and a spot 1. If the lights in view need more than
            // that, URP shrinks all of them, differently from frame to frame as the camera turns,
            // and shadows on the walls visibly jump. Keep shadow-casting local lights within 16 maps.
            Set(asset, "m_AdditionalLightsShadowResolutionTierHigh", 512);
            Set(asset, "m_ReflectionProbeBlending", true);
            Set(asset, "m_ReflectionProbeBoxProjection", true);
            Set(asset, "m_ShadowDistance", 70f);
            Set(asset, "m_ShadowCascadeCount", 4);
            Set(asset, "m_Cascade4Split", new Vector3(0.09f, 0.24f, 0.52f));
            Set(asset, "m_CascadeBorder", 0.15f);
            Set(asset, "m_ShadowDepthBias", 0.7f);
            Set(asset, "m_ShadowNormalBias", 0.7f);
            Set(asset, "m_SoftShadowsSupported", true);
            Set(asset, "m_SoftShadowQuality", 3);
            Set(asset, "m_SupportsLightCookies", true);
            Set(asset, "m_SupportsLightLayers", true);
            Set(asset, "m_ColorGradingMode", 1);
            Set(asset, "m_ColorGradingLutSize", 32);
            asset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);

            Type globals = typeof(UniversalRenderPipelineAsset).Assembly
                .GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
            MethodInfo ensure = globals?.GetMethod("Ensure", BindingFlags.NonPublic | BindingFlags.Static);
            if (ensure != null) ensure.Invoke(null, new object[] { true });
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureAmbientOcclusion(UniversalRendererData renderer)
        {
            ScreenSpaceAmbientOcclusion occlusion = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (occlusion == null)
            {
                occlusion = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                occlusion.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(occlusion, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(occlusion, out string _, out long localId);
                SerializedObject data = new SerializedObject(renderer);
                SerializedProperty features = data.FindProperty("m_RendererFeatures");
                SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = occlusion;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            SerializedObject settings = new SerializedObject(occlusion);
            Set(settings, "m_Settings.AOMethod", 0);
            Set(settings, "m_Settings.Downsample", false);
            Set(settings, "m_Settings.AfterOpaque", false);
            Set(settings, "m_Settings.Source", 1);
            Set(settings, "m_Settings.NormalSamples", 1);
            Set(settings, "m_Settings.Intensity", 1.6f);
            Set(settings, "m_Settings.DirectLightingStrength", 0.3f);
            Set(settings, "m_Settings.Radius", 0.45f);
            Set(settings, "m_Settings.Samples", 0);
            Set(settings, "m_Settings.BlurQuality", 0);
            Set(settings, "m_Settings.Falloff", 80f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(occlusion);
            EditorUtility.SetDirty(renderer);
        }

        private static void Set(SerializedObject target, string path, object value)
        {
            SerializedProperty property = target.FindProperty(path);
            if (property == null)
                throw new InvalidOperationException(target.targetObject.GetType().Name + " has no serialized field " + path);
            switch (value)
            {
                case bool flag: property.boolValue = flag; break;
                case int number: property.intValue = number; break;
                case float number: property.floatValue = number; break;
                case Vector3 vector: property.vector3Value = vector; break;
                default: throw new ArgumentException("Unsupported value for " + path);
            }
        }

        private static Shader Lit => RequireShader("Universal Render Pipeline/Lit");

        private static Shader RequireShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + name);
            return shader;
        }

        private static Material SaveMaterial(Material material, string name)
        {
            string path = Generated + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            existing.shader = material.shader;
            existing.CopyPropertiesFromMaterial(material);
            existing.shaderKeywords = material.shaderKeywords;
            existing.renderQueue = material.renderQueue;
            existing.globalIlluminationFlags = material.globalIlluminationFlags;
            foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" })
                existing.SetShaderPassEnabled(pass, material.GetShaderPassEnabled(pass));
            existing.SetOverrideTag("RenderType", material.GetTag("RenderType", false, ""));
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(material);
            return existing;
        }

        private static Material LitMaterial(Texture2D color, Texture2D normal, Texture2D mask, float smoothness)
        {
            Material material = new Material(Lit);
            material.SetColor("_BaseColor", Color.white);
            if (color != null) material.SetTexture("_BaseMap", color);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (mask != null)
            {
                material.SetTexture("_MetallicGlossMap", mask);
                material.SetTexture("_OcclusionMap", mask);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_Metallic", 1f);
            }
            else
            {
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
            }
            return material;
        }

        private static void MakeTransparent(Material material, bool additive)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            // Reflections must fade with the alpha mask, or the whole quad shows as a pale rectangle.
            material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
        }

        private static void MakeCutout(Material material, float cutoff, bool doubleSided)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", cutoff);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            if (doubleSided) material.SetFloat("_Cull", 0f);
        }

        private static T Find<T>(string folder, string marker) where T : UnityEngine.Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue;
                if (Path.GetFileName(path).ToLowerInvariant().Contains(marker)) return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }

        private static void Add(ArtCatalog catalog, string id, UnityEngine.Object asset, Vector3 size = default, Vector3 center = default)
        {
            if (asset == null) throw new InvalidOperationException("Missing asset for catalog entry " + id);
            catalog.entries.Add(new ArtCatalog.Entry { id = id, asset = asset, size = size, center = center });
        }

        private static void BuildSurfaces(ArtCatalog catalog)
        {
            foreach (string folder in AssetDatabase.GetSubFolders("Assets/Art/Surfaces"))
            {
                string id = Path.GetFileName(folder);
                Texture2D color = Find<Texture2D>(folder, "_diff_");
                if (color == null) throw new InvalidOperationException("Surface has no colour map: " + id);
                Material material = LitMaterial(color, Find<Texture2D>(folder, "_nor_gl_"), Find<Texture2D>(folder, "_mask_"), 0.2f);
                material = SaveMaterial(material, "surf_" + id);
                float size = SurfaceSize.TryGetValue(id, out float metres) ? metres : 2f;
                Add(catalog, "surf/" + id, material, new Vector3(size, size, size));
            }
        }

        private static void BuildBaseMaterials(ArtCatalog catalog)
        {
            Material plain = new Material(Lit);
            plain.SetFloat("_Smoothness", 0.25f);
            Add(catalog, "mat/plain", SaveMaterial(plain, "base_plain"));

            Material metal = new Material(Lit);
            metal.SetFloat("_Metallic", 1f);
            metal.SetFloat("_Smoothness", 0.55f);
            Add(catalog, "mat/metal", SaveMaterial(metal, "base_metal"));

            Material glass = new Material(Lit);
            glass.SetColor("_BaseColor", new Color(0.02f, 0.025f, 0.03f));
            glass.SetFloat("_Smoothness", 0.94f);
            Add(catalog, "mat/glass", SaveMaterial(glass, "base_glass"));

            Material emissive = new Material(Lit);
            emissive.SetColor("_BaseColor", Color.black);
            emissive.SetColor("_EmissionColor", Color.white);
            emissive.EnableKeyword("_EMISSION");
            emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            emissive.SetFloat("_Smoothness", 0.4f);
            Add(catalog, "mat/emissive", SaveMaterial(emissive, "base_emissive"));

            Material cloth = new Material(Lit);
            Texture2D weave = Find<Texture2D>("Assets/Art/Surfaces/denim_fabric", "_nor_gl_");
            if (weave != null)
            {
                cloth.SetTexture("_BumpMap", weave);
                cloth.EnableKeyword("_NORMALMAP");
            }
            cloth.SetFloat("_Smoothness", 0.08f);
            cloth.SetFloat("_Cull", 0f);
            Add(catalog, "mat/cloth", SaveMaterial(cloth, "base_cloth"), new Vector3(0.4f, 0.4f, 0.4f));

            Material garment = new Material(Lit);
            garment.SetFloat("_Smoothness", 0.06f);
            MakeCutout(garment, 0.5f, true);
            Add(catalog, "mat/clothcut", SaveMaterial(garment, "base_clothcut"));

            Material decal = new Material(Lit);
            decal.SetFloat("_Smoothness", 0.3f);
            MakeTransparent(decal, false);
            Add(catalog, "mat/decal", SaveMaterial(decal, "base_decal"));

            Material water = new Material(Lit);
            water.SetColor("_BaseColor", new Color(0.03f, 0.035f, 0.04f, 0.82f));
            water.SetFloat("_Smoothness", 0.97f);
            MakeTransparent(water, false);
            Add(catalog, "mat/water", SaveMaterial(water, "base_water"));

            Material shaft = new Material(RequireShader("Universal Render Pipeline/Particles/Unlit"));
            MakeTransparent(shaft, true);
            shaft.SetFloat("_Cull", 0f);
            shaft.SetColor("_BaseColor", Color.white);
            Add(catalog, "mat/shaft", SaveMaterial(shaft, "base_shaft"));

            Material text = new Material(RequireShader("DontForgetMe/WorldText"));
            Add(catalog, "mat/text", SaveMaterial(text, "base_text"));
        }

        private static string Normalize(string name)
        {
            return new string(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        private static void BuildProps(ArtCatalog catalog)
        {
            foreach (string folder in AssetDatabase.GetSubFolders("Assets/Art/Props"))
            {
                string id = Path.GetFileName(folder);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + id + ".fbx");
                if (model == null) throw new InvalidOperationException("Prop model failed to import: " + id);

                Dictionary<string, Material> sets = new Dictionary<string, Material>();
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                    int cut = file.IndexOf("_diff_", StringComparison.Ordinal);
                    if (cut < 0) continue;
                    string set = file.Substring(0, cut);
                    Material material = LitMaterial(Find<Texture2D>(folder, set.ToLowerInvariant() + "_diff_"),
                        Find<Texture2D>(folder, set.ToLowerInvariant() + "_nor_gl_"),
                        Find<Texture2D>(folder, set.ToLowerInvariant() + "_mask_"), 0.3f);
                    if (set.Contains("glass"))
                    {
                        material.SetFloat("_Smoothness", 0.95f);
                        material.SetColor("_EmissionColor", Color.black);
                    }
                    sets[set] = SaveMaterial(material, "prop_" + set);
                    Add(catalog, "propmat/" + set, sets[set]);
                }
                if (sets.Count == 0) throw new InvalidOperationException("Prop has no textures: " + id);

                GameObject instance = UnityEngine.Object.Instantiate(model);
                instance.name = id;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        string wanted = materials[i] != null ? Normalize(materials[i].name) : "";
                        string best = sets.Keys.OrderBy(key => key.Length).First();
                        foreach (string key in sets.Keys)
                            if (Normalize(key) == wanted) best = key;
                        if (Normalize(best) != wanted)
                            foreach (string key in sets.Keys.OrderByDescending(key => key.Length))
                                if (wanted.Contains(Normalize(key)) || Normalize(key).Contains(wanted) && wanted.Length > 3)
                                {
                                    best = key;
                                    break;
                                }
                        materials[i] = sets[best];
                    }
                    renderer.sharedMaterials = materials;
                }
                Bounds bounds = Measure(instance);
                Renderer[] parts = instance.GetComponentsInChildren<Renderer>();
                Bounds[] partBounds = parts.Select(part => part.bounds).ToArray();
                string[] partNames = parts.Select(part => part.name).ToArray();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, Generated + "/Props/" + id + ".prefab");
                UnityEngine.Object.DestroyImmediate(instance);
                Add(catalog, "prop/" + id, prefab, bounds.size, bounds.center);
                // Some downloads hold several variants side by side; "id#n" addresses a single one.
                if (parts.Length > 1)
                    for (int i = 0; i < parts.Length; i++)
                    {
                        Add(catalog, "prop/" + id + "#" + i, prefab, partBounds[i].size, partBounds[i].center);
                        report.AppendLine(string.Format("  part {0}#{1} {2} size {3:F2} {4:F2} {5:F2} center {6:F2} {7:F2} {8:F2}", id, i,
                            partNames[i], partBounds[i].size.x, partBounds[i].size.y, partBounds[i].size.z,
                            partBounds[i].center.x, partBounds[i].center.y, partBounds[i].center.z));
                    }
                report.AppendLine(string.Format("prop {0} size {1:F2} {2:F2} {3:F2} center {4:F2} {5:F2} {6:F2} sets {7}",
                    id, bounds.size.x, bounds.size.y, bounds.size.z, bounds.center.x, bounds.center.y, bounds.center.z,
                    string.Join(",", sets.Keys)));
            }
        }

        private static Bounds Measure(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void BuildCharacters(ArtCatalog catalog)
        {
            foreach (string folder in AssetDatabase.GetSubFolders("Assets/Art/Characters"))
            {
                string id = Path.GetFileName(folder);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + id + ".fbx");
                if (model == null) throw new InvalidOperationException("Character model failed to import: " + id);
                bool dog = id == "Dog";

                Material body = SaveMaterial(LitMaterial(Find<Texture2D>(folder, dog ? "_color" : "_body_color"),
                    Find<Texture2D>(folder, dog ? "_bump" : "_body_normal"), null, dog ? 0.22f : 0.16f), "char_" + id + "_body");
                Material head = dog ? body : SaveMaterial(LitMaterial(Find<Texture2D>(folder, "_head_color"),
                    Find<Texture2D>(folder, "_head_normal"), null, 0.34f), "char_" + id + "_head");
                Material hair = null;
                Texture2D opacity = dog ? null : Find<Texture2D>(folder, "_opacity_color");
                if (opacity != null)
                {
                    hair = LitMaterial(opacity, null, null, 0.22f);
                    MakeCutout(hair, 0.4f, true);
                    hair = SaveMaterial(hair, "char_" + id + "_hair");
                }

                GameObject instance = UnityEngine.Object.Instantiate(model);
                instance.name = id;
                // Rocketbox ships every level of detail in one file; keep only the most detailed mesh.
                foreach (Transform child in instance.transform.Cast<Transform>().ToArray())
                {
                    string name = child.name.ToLowerInvariant();
                    if (name.Contains("poly") && !name.Contains("hipoly")) UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        string name = materials[i] != null ? materials[i].name.ToLowerInvariant() : "";
                        if (name.Contains("opacity"))
                        {
                            if (hair == null) throw new InvalidOperationException(id + " needs an opacity texture.");
                            materials[i] = hair;
                        }
                        else materials[i] = name.Contains("head") ? head : body;
                    }
                    renderer.sharedMaterials = materials;
                    renderer.updateWhenOffscreen = false;
                    renderer.quality = SkinQuality.Bone4;
                }
                Animator animator = instance.GetComponent<Animator>();
                if (!dog)
                {
                    if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                        throw new InvalidOperationException("Humanoid rig was not created for " + id + ". Check the import log.");
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }
                else if (animator != null) UnityEngine.Object.DestroyImmediate(animator);

                Bounds bounds = Measure(instance);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, Generated + "/Characters/" + id + ".prefab");
                Add(catalog, "char/" + id, prefab, bounds.size, bounds.center);
                report.AppendLine(string.Format("char {0} size {1:F2} {2:F2} {3:F2} center {4:F2} {5:F2} {6:F2} renderers {7}",
                    id, bounds.size.x, bounds.size.y, bounds.size.z, bounds.center.x, bounds.center.y, bounds.center.z,
                    string.Join(",", instance.GetComponentsInChildren<Renderer>().Select(r => r.name + "[" +
                        string.Join("|", r.sharedMaterials.Select(m => m.name)) + "]"))));
                if (dog)
                    foreach (Transform bone in instance.GetComponentsInChildren<Transform>())
                        report.AppendLine(string.Format("  dogbone {0} parent {1} pos {2:F3} {3:F3} {4:F3}", bone.name,
                            bone.parent != null ? bone.parent.name : "-", bone.position.x, bone.position.y, bone.position.z));
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void BuildClips(ArtCatalog catalog)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Animations" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(item => !item.name.StartsWith("__preview__"));
                if (clip == null) throw new InvalidOperationException("No animation clip in " + path);
                if (!clip.isHumanMotion) throw new InvalidOperationException("Clip is not humanoid: " + path);
                string id = Path.GetFileNameWithoutExtension(path);
                Add(catalog, "clip/" + id, clip, new Vector3(clip.length, clip.averageSpeed.magnitude, clip.isLooping ? 1 : 0));
                report.AppendLine(string.Format("clip {0} length {1:F2} avgSpeed {2:F2} {3:F2} {4:F2} loop {5}", id, clip.length,
                    clip.averageSpeed.x, clip.averageSpeed.y, clip.averageSpeed.z, clip.isLooping));
            }
        }

        private static void BuildSky(ArtCatalog catalog)
        {
            // The sky is computed from the sun's position at run time (Sky.cs); there is no sky image.
            Add(catalog, "mat/sky", SaveMaterial(new Material(RequireShader("DontForgetMe/Sky")), "base_sky"));
        }

        private static void BuildLook(ArtCatalog catalog)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath) != null) AssetDatabase.DeleteAsset(VolumePath);
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);

            Tonemapping tone = Component<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            Bloom bloom = Component<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.32f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);

            ColorAdjustments color = Component<ColorAdjustments>(profile);
            color.postExposure.Override(0.15f);
            color.contrast.Override(12f);
            color.saturation.Override(-14f);
            color.colorFilter.Override(new Color(1f, 0.985f, 0.96f));

            WhiteBalance balance = Component<WhiteBalance>(profile);
            balance.temperature.Override(6f);
            balance.tint.Override(2f);

            SplitToning split = Component<SplitToning>(profile);
            split.shadows.Override(new Color(0.42f, 0.5f, 0.56f));
            split.highlights.Override(new Color(0.58f, 0.52f, 0.44f));
            split.balance.Override(-10f);

            Vignette vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.42f);

            FilmGrain grain = Component<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.75f);

            ChromaticAberration aberration = Component<ChromaticAberration>(profile);
            aberration.intensity.Override(0.05f);

            DepthOfField focus = Component<DepthOfField>(profile);
            focus.mode.Override(DepthOfFieldMode.Bokeh);
            focus.focusDistance.Override(6f);
            focus.focalLength.Override(42f);
            focus.aperture.Override(4.5f);
            focus.bladeCount.Override(6);

            MotionBlur blur = Component<MotionBlur>(profile);
            blur.quality.Override(MotionBlurQuality.Medium);
            blur.intensity.Override(0.18f);
            blur.clamp.Override(0.04f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Add(catalog, "look", profile);
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
