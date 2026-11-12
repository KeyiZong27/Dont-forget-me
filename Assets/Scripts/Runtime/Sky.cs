using UnityEngine;
using UnityEngine.Rendering;

namespace DontForgetMe
{
    // The one natural light in the scene. Everything daylight does here is derived from where
    // the sun stands: how much of it survives the air on the way down (the colour and strength
    // of the directional light), what the air scatters back (the sky that is drawn), and the sum
    // of that sky over a hemisphere (the ambient light in every shadow). The model is single
    // scattering in a plane-parallel atmosphere of air and haze; Sky.shader draws the same
    // expression per pixel. Lamps are separate, visible fixtures and are not part of this.
    public static class Sky
    {
        // Vertical optical depth of clean air at roughly 680, 550 and 440 nm, and of city haze.
        private static readonly Vector3 TauR = new Vector3(0.0464f, 0.108f, 0.265f);
        private const float TauM = 0.11f;
        // Sunlight above the atmosphere, in the units the renderer's lights use.
        private const float Top = 4f;
        // Share of light scattered more than once, treated as arriving evenly from the whole sky.
        private const float Multiple = 0.5f;
        // How much of the sky a street between buildings actually sees, and what the ground returns.
        private const float Open = 0.9f;
        private const float GroundAlbedo = 0.16f;

        // Unit vector pointing at the sun.
        public static Vector3 Sun { get; private set; } = Vector3.up;
        public static float Elevation { get; private set; }
        // Direct sunlight reaching the ground, linear RGB.
        public static Vector3 Sunlight { get; private set; }

        private static float sunAirmass;

        // "bearing" is measured from due south (-z) toward the east (+x).
        public static void Place(float elevationDegrees, float bearingDegrees)
        {
            Elevation = elevationDegrees;
            float e = elevationDegrees * Mathf.Deg2Rad, b = bearingDegrees * Mathf.Deg2Rad;
            Sun = new Vector3(Mathf.Sin(b) * Mathf.Cos(e), Mathf.Sin(e), -Mathf.Cos(b) * Mathf.Cos(e));
            sunAirmass = Airmass(Sun.y);
            Sunlight = Top * Exp(-(TauR + Vector3.one * TauM) * sunAirmass);
        }

        // Kasten and Young (1989): relative air mass for a given sine of the elevation.
        private static float Airmass(float sine)
        {
            sine = Mathf.Clamp01(sine);
            return 1f / (sine + 0.50572f * Mathf.Pow(Mathf.Asin(sine) * Mathf.Rad2Deg + 6.07995f, -1.6364f));
        }

        private static Vector3 Exp(Vector3 v)
        {
            return new Vector3(Mathf.Exp(v.x), Mathf.Exp(v.y), Mathf.Exp(v.z));
        }

        private static float Ratio(float x)
        {
            return Mathf.Abs(x) < 0.001f ? 1f - x * 0.5f : (1f - Mathf.Exp(-x)) / x;
        }

        // Radiance of the sky in a direction, without the sun's disc. Mirrors Sky.shader.
        public static Vector3 Radiance(Vector3 direction)
        {
            direction.Normalize();
            float mv = Airmass(direction.y), ms = sunAirmass, mu = Vector3.Dot(direction, Sun);
            float rayleigh = 0.0596831f * (1f + mu * mu);
            float mie = 0.0336137f / Mathf.Pow(Mathf.Max(1.5776f - 1.52f * mu, 0.0001f), 1.5f);
            float below = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(-direction.y * 4f));
            Vector3 sky = Vector3.zero;
            for (int c = 0; c < 3; c++)
            {
                float tau = TauR[c] + TauM;
                float phase = (TauR[c] * rayleigh + 0.9f * TauM * mie) / tau;
                float once = Mathf.PI * Top * phase * mv * tau * Mathf.Exp(-tau * ms) * Ratio(tau * (mv - ms));
                float again = Multiple * Top * (TauR[c] / tau) * (1f - Mathf.Exp(-tau * mv)) * Mathf.Exp(-tau * ms * 0.35f);
                sky[c] = (once + again) * below;
            }
            return sky;
        }

        private static Color Linear(Vector3 v)
        {
            return new Color(v.x, v.y, v.z, 1f);
        }

        public static void Apply(Material material)
        {
            material.SetVector("_SunDir", Sun);
            material.SetVector("_TauR", TauR);
            material.SetFloat("_TauM", TauM);
            material.SetFloat("_Top", Top);
            material.SetFloat("_SunAirmass", sunAirmass);
            material.SetFloat("_Multiple", Multiple);
            // The disc is drawn larger than the real one and far dimmer, enough to read as the sun and bloom.
            material.SetVector("_Disc", Sunlight * 45f);
        }

        public static void Apply(Light sun)
        {
            float strength = Mathf.Max(Sunlight.x, Mathf.Max(Sunlight.y, Sunlight.z));
            sun.transform.rotation = Quaternion.LookRotation(-Sun);
            sun.color = Linear(Sunlight / strength).gamma;
            sun.intensity = strength;
        }

        // Skylight for surfaces the sun does not reach: the sky above summed over the hemisphere
        // each surface faces, and below the horizon what the ground sends back up.
        public static SphericalHarmonicsL2 Ambient()
        {
            const int count = 1024;
            Vector3[] directions = new Vector3[count];
            Vector3[] colours = new Vector3[count];
            Vector3 down = Vector3.zero;
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < count; i++)
            {
                float y = 1f - (i + 0.5f) / count * 2f, r = Mathf.Sqrt(1f - y * y), a = i * golden;
                directions[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                if (y <= 0) continue;
                colours[i] = Radiance(directions[i]) * Open;
                down += colours[i] * y;
            }
            // What a horizontal surface receives, divided by pi: the radiance a white ground would have.
            Vector3 ground = (down * (4f / count) + Sunlight * Sun.y * 0.5f) * GroundAlbedo;
            SphericalHarmonicsL2 probe = new SphericalHarmonicsL2();
            for (int i = 0; i < count; i++)
            {
                if (directions[i].y <= 0) colours[i] = ground;
                probe.AddDirectionalLight(directions[i], Linear(colours[i]), 1f);
            }

            // Scale against a direct sum, so the result does not depend on the engine's internal
            // convention for lights added to a probe.
            Vector3[] check = { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
            Color[] evaluated = new Color[check.Length];
            probe.Evaluate(check, evaluated);
            float expected = 0, actual = 0;
            for (int n = 0; n < check.Length; n++)
            {
                for (int i = 0; i < count; i++)
                {
                    float cosine = Mathf.Max(0, Vector3.Dot(check[n], directions[i]));
                    expected += (colours[i].x + colours[i].y + colours[i].z) * cosine * (4f / count);
                }
                actual += evaluated[n].r + evaluated[n].g + evaluated[n].b;
            }
            return actual > 1e-5f ? probe * (expected / actual) : probe;
        }

        // Distance takes on the colour of the air near the horizon. Fog has one colour for every
        // direction, so this is the typical horizon round the compass (a geometric mean), which
        // the glare round the sun does not dominate.
        public static Color Haze()
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                Vector3 sky = Radiance(new Vector3(Mathf.Cos(a), 0.06f, Mathf.Sin(a)));
                sum += new Vector3(Mathf.Log(sky.x), Mathf.Log(sky.y), Mathf.Log(sky.z));
            }
            return Linear(Exp(sum / 16f)).gamma;
        }
    }
}
