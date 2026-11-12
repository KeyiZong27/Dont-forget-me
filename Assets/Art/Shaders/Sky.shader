// The sky as lit by one sun: single scattering in a plane-parallel atmosphere of air (Rayleigh)
// and haze (Mie), plus a small isotropic term standing in for light scattered more than once.
// Sky.cs evaluates exactly the same expression on the CPU for ambient light and fog, and feeds
// the constants below, so what is seen overhead and what lights the street cannot drift apart.
Shader "DontForgetMe/Sky"
{
    Properties
    {
        _SunDir ("Direction to the sun", Vector) = (0.14, 0.17, -0.97, 0)
        _TauR ("Vertical optical depth of air (RGB)", Vector) = (0.0464, 0.108, 0.265, 0)
        _TauM ("Vertical optical depth of haze", Float) = 0.11
        _Top ("Sunlight above the atmosphere", Float) = 4
        _SunAirmass ("Air mass toward the sun", Float) = 5.6
        _Multiple ("Multiple scattering", Float) = 0.5
        _Disc ("Sun disc radiance (RGB)", Vector) = (40, 28, 12, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _SunDir;
            float4 _TauR;
            float _TauM;
            float _Top;
            float _SunAirmass;
            float _Multiple;
            float4 _Disc;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }
            // Kasten and Young (1989): relative air mass for a given sine of the elevation.
            float Airmass(float s)
            {
                s = saturate(s);
                return 1.0 / (s + 0.50572 * pow(degrees(asin(s)) + 6.07995, -1.6364));
            }
            // (1 - exp(-x)) / x, which stays finite where the view and sun paths are equally long.
            float3 Ratio(float3 x)
            {
                float3 far = step(0.001, abs(x));
                float3 safe = lerp(float3(1, 1, 1), x, far);
                return lerp(1.0 - x * 0.5, (1.0 - exp(-safe)) / safe, far);
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz);
                float mv = Airmass(d.y);
                float ms = _SunAirmass;
                float mu = dot(d, sun);
                float3 tauR = _TauR.xyz;
                float3 tau = tauR + _TauM;
                float rayleigh = 0.0596831 * (1.0 + mu * mu);
                float mie = 0.0336137 / pow(max(1.5776 - 1.52 * mu, 0.0001), 1.5);
                float3 phase = (tauR * rayleigh + 0.9 * _TauM * mie) / tau;
                float3 once = 3.14159265 * _Top * phase * mv * tau * exp(-tau * ms) * Ratio(tau * (mv - ms));
                float3 again = _Multiple * _Top * (tauR / tau) * (1.0 - exp(-tau * mv)) * exp(-tau * ms * 0.35);
                float3 sky = once + again;
                // Below the horizon there is only haze over ground that the scene does not model.
                sky *= lerp(1.0, 0.35, saturate(-d.y * 4.0));
                float disc = smoothstep(0.99988, 0.99996, mu) * step(0.0, d.y);
                return float4(sky + _Disc.xyz * disc, 1);
            }
            ENDHLSL
        }
    }
}
