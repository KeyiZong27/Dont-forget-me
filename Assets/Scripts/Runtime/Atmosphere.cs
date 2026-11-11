using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DontForgetMe
{
    // Sky, sun, fog, reflections and the camera's film look. Gameplay only tells it how tense
    // the moment is and whether a menu is open; everything else follows from that.
    public sealed class Atmosphere : MonoBehaviour
    {
        public static Atmosphere Instance { get; private set; }
        public float Tension { get; set; }
        public bool MenuBlur { get; set; } = true;
        public Light Sun { get; private set; }
        public static readonly Quaternion SunRotation = Quaternion.Euler(36f, -32f, 0f);

        private readonly List<ReflectionProbe> probes = new List<ReflectionProbe>();
        private Camera view;
        private DepthOfField focus;
        private ColorAdjustments grade;
        private WhiteBalance balance;
        private Vignette vignette;
        private ChromaticAberration aberration;
        private FilmGrain grain;
        private Transform dust;
        private float tension;
        private float blur = 1f;
        private int fittedWidth, fittedHeight;

        public void Build(Camera camera)
        {
            Instance = this;
            view = camera;
            QualitySettings.vSyncCount = 1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.lodBias = 2f;

            Material sky = Art.Own(new Material(Art.Base("sky")));
            sky.SetFloat("_Exposure", 1.05f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = Art.SkyAmbient(1.4f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = Art.Get<Cubemap>("sky");
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.63f, 0.64f);
            RenderSettings.fogDensity = 0.009f;

            Sun = new GameObject("LateAfternoonSun").AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.81f, 0.60f);
            Sun.intensity = 2.7f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.96f;
            Sun.transform.rotation = SunRotation;
            RenderSettings.sun = Sun;

            camera.allowHDR = true;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 420f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 52f;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            // Temporal anti-aliasing needs a run of displayed frames; offscreen captures use SMAA instead.
            data.antialiasing = Application.isBatchMode
                ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.TemporalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
            data.stopNaN = true;
            data.renderShadows = true;

            Volume volume = new GameObject("Look").AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = Art.Get<VolumeProfile>("look");
            VolumeProfile profile = volume.profile;
            profile.TryGet(out focus);
            profile.TryGet(out grade);
            profile.TryGet(out balance);
            profile.TryGet(out vignette);
            profile.TryGet(out aberration);
            profile.TryGet(out grain);
            BuildDust();
        }

        public void AddProbe(Vector3 center, Vector3 size)
        {
            ReflectionProbe probe = new GameObject("Reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(transform, false);
            probe.transform.position = center;
            probe.size = size;
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 256;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.blendDistance = 2f;
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = 150f;
            probe.cullingMask = ~(1 << 8);
            probes.Add(probe);
        }

        private IEnumerator Start()
        {
            // Wait for the world and its lights to exist before capturing what surfaces reflect.
            yield return null;
            yield return null;
            foreach (ReflectionProbe probe in probes) probe.RenderProbe();
        }

        private void BuildDust()
        {
            Texture2D dot = Kit.Noise("DustMote", 32, (u, v, n) =>
            {
                float d = Mathf.Clamp01(1f - new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f);
                return new Color(1, 1, 1, d * d);
            }, 1f, 0f);
            Material material = Art.Textured("mat/shaft", dot, new Color(1f, 0.93f, 0.8f, 0.5f), "dust");
            ParticleSystem system = new GameObject("Dust").AddComponent<ParticleSystem>();
            dust = system.transform;
            dust.SetParent(transform, false);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
            main.startColor = new Color(1f, 0.95f, 0.85f, 0.55f);
            main.maxParticles = 320;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 34f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 4.5f, 16f);
            shape.position = new Vector3(0, 1.4f, 4f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.35f;
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
        }

        // Dense displays cost more than the picture gains: above roughly 1080p worth of pixels,
        // render fewer and let the upscaler fill the screen. Builds only, so the editor never
        // rewrites the pipeline asset on disk.
        private void FitRenderScale()
        {
            if (Application.isEditor || (Screen.width == fittedWidth && Screen.height == fittedHeight)) return;
            fittedWidth = Screen.width;
            fittedHeight = Screen.height;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
                pipeline.renderScale = Mathf.Clamp(Mathf.Sqrt(2.1e6f / Mathf.Max(1f, (float)Screen.width * Screen.height)), 0.62f, 1f);
        }

        private void LateUpdate()
        {
            if (view == null) return;
            FitRenderScale();
            float dt = Time.unscaledDeltaTime;
            DemoGame game = DemoGame.Instance;
            tension = Mathf.MoveTowards(tension, Mathf.Clamp01(Tension), dt * 0.35f);
            blur = Mathf.MoveTowards(blur, MenuBlur ? 1f : 0f, dt * 1.6f);
            if (dust != null) dust.position = new Vector3(view.transform.position.x, 0, view.transform.position.z);

            if (focus != null)
            {
                float subject = game != null && game.Player != null
                    ? Vector3.Distance(view.transform.position, game.Player.transform.position + Vector3.up * 1.4f) : 5f;
                focus.focusDistance.value = Mathf.Lerp(Mathf.Max(1.2f, subject + 0.6f), 0.35f, blur);
                focus.aperture.value = Mathf.Lerp(20f, 1.2f, blur);
                focus.focalLength.value = Mathf.Lerp(30f, 60f, blur);
            }
            if (grade != null)
            {
                grade.postExposure.value = Mathf.Lerp(0.8f, 0.7f, tension);
                grade.saturation.value = Mathf.Lerp(-8f, -24f, tension);
                grade.contrast.value = Mathf.Lerp(10f, 20f, tension);
            }
            if (balance != null) balance.temperature.value = Mathf.Lerp(6f, -9f, tension);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0.26f, 0.4f, Mathf.Max(tension, blur * 0.6f));
            if (aberration != null) aberration.intensity.value = Mathf.Lerp(0.05f, 0.2f, tension);
            if (grain != null) grain.intensity.value = Mathf.Lerp(0.22f, 0.42f, tension);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
