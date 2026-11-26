using UnityEngine;

/// <summary>
/// Génère les petits sons d'arcade du flipper directement en mémoire.
/// Aucun asset audio à importer : le son est synthétisé au premier impact puis réutilisé.
/// </summary>
public static class ArcadeAudio
{
    private const int SampleRate = 44100;

    private static AudioClip[] bumperHits;

    /// <summary>Renvoie une des variantes de son de bumper (création paresseuse).</summary>
    public static AudioClip GetBumperHit(int variant)
    {
        if (bumperHits == null || bumperHits.Length == 0 || bumperHits[0] == null)
        {
            BuildBumperHits();
        }

        if (bumperHits.Length == 0)
        {
            return null;
        }

        return bumperHits[Mathf.Abs(variant) % bumperHits.Length];
    }

    private static AudioClip[] treeHits;

    public static AudioClip GetTreeHit(int variant)
    {
        if (treeHits == null || treeHits.Length == 0 || treeHits[0] == null)
        {
            BuildTreeHits();
        }

        if (treeHits.Length == 0)
        {
            return null;
        }

        return treeHits[Mathf.Abs(variant) % treeHits.Length];
    }

    private static AudioClip stormWind;

    /// <summary>Boucle de vent pour les tempêtes de neige.</summary>
    public static AudioClip GetStormWind()
    {
        if (stormWind == null)
        {
            stormWind = CreateStormWind();
        }

        return stormWind;
    }

    private static void BuildTreeHits()
    {
        treeHits = new AudioClip[]
        {
            CreateTreeHit(0),
            CreateTreeHit(1)
        };
    }

    private static void BuildBumperHits()
    {
        bumperHits = new AudioClip[]
        {
            CreateBumperHit(0, 300f, 640f, 0.20f),
            CreateBumperHit(1, 340f, 720f, 0.18f),
            CreateBumperHit(2, 260f, 560f, 0.22f)
        };
    }

    private static AudioClip CreateStormWind()
    {
        int length = SampleRate * 3;
        float[] data = new float[length];
        var rng = new System.Random(4242);

        float low = 0f;
        float low2 = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            low += (noise - low) * 0.018f;
            low2 += (low - low2) * 0.06f;

            float gust = 0.55f
                + 0.30f * Mathf.Sin(2f * Mathf.PI * t / 3f)
                + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 1.1f + 1.7f);

            data[i] = low2 * gust * 2.4f;
        }

        Normalize(data, 0.5f);

        int fade = SampleRate / 20;
        for (int i = 0; i < fade; i++)
        {
            float gain = i / (float)fade;
            data[i] *= gain;
            data[length - 1 - i] *= gain;
        }

        AudioClip clip = AudioClip.Create("StormWind", length, 1, SampleRate, false);
        clip.SetData(data, 0);
        clip.hideFlags = HideFlags.DontSave;
        return clip;
    }

    private static AudioClip CreateTreeHit(int seed)
    {
        int length = Mathf.CeilToInt(SampleRate * 0.16f);
        float[] data = new float[length];

        var rng = new System.Random(seed * 3571 + 11);
        float phase = 0f;
        float noiseLowPass = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float attack = Mathf.Clamp01(t / 0.0015f);
            float envelope = Mathf.Exp(-t * 24f);

            phase += 2f * Mathf.PI * (165f + 35f * seed) / SampleRate;
            float knock = Mathf.Sin(phase) * envelope * 0.55f;
            knock += 0.25f * Mathf.Sin(phase * 2.03f) * Mathf.Exp(-t * 30f);

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            noiseLowPass += (noise - noiseLowPass) * 0.22f;
            float rustle = noiseLowPass * Mathf.Exp(-t * 18f) * 0.3f;

            float sample = (knock + rustle) * attack;
            sample = sample / (1f + Mathf.Abs(sample) * 0.7f);
            data[i] = sample;
        }

        Normalize(data, 0.62f);
        ApplyFade(data, 0.0015f);

        AudioClip clip = AudioClip.Create($"TreeHit_{seed}", length, 1, SampleRate, false);
        clip.SetData(data, 0);
        clip.hideFlags = HideFlags.DontSave;
        return clip;
    }

    private static AudioClip CreateBumperHit(int seed, float lowTone, float highTone, float duration)
    {
        int length = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[length];

        var rng = new System.Random(seed * 7919 + 17);

        float phase = 0f;
        float ringPhase = 0f;
        float lowPhase = 0f;
        float noiseLowPass = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            // Attaque très courte pour garder un "clac" net, puis décroissance.
            float attack = Mathf.Clamp01(t / 0.002f);
            float bodyEnvelope = Mathf.Exp(-t * 16f);

            // Corps tonal qui descend : le "boing" du bumper.
            float frequency = Mathf.Lerp(highTone, lowTone, Mathf.Clamp01(t / 0.09f));
            phase += 2f * Mathf.PI * frequency / SampleRate;
            float body = Mathf.Sin(phase) * bodyEnvelope;
            body += 0.35f * Mathf.Sin(phase * 2.01f) * Mathf.Exp(-t * 24f);

            // Grosse frappe grave.
            lowPhase += 2f * Mathf.PI * (85f - 25f * Mathf.Clamp01(t / 0.06f)) / SampleRate;
            float thump = Mathf.Sin(lowPhase) * Mathf.Exp(-t * 34f) * 0.5f;

            // Petite résonance métallique.
            ringPhase += 2f * Mathf.PI * (1250f + 120f * seed) / SampleRate;
            float ring = Mathf.Sin(ringPhase) * Mathf.Exp(-t * 26f) * 0.12f;

            // Transitoire de bruit filtré.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            noiseLowPass += (noise - noiseLowPass) * 0.35f;
            float click = noiseLowPass * Mathf.Exp(-t * 120f) * 0.9f;

            float sample = (body * 0.55f + thump + ring + click) * attack;

            // Saturation douce pour éviter les pics agressifs.
            sample = sample / (1f + Mathf.Abs(sample) * 0.8f);
            data[i] = sample;
        }

        Normalize(data, 0.85f);
        ApplyFade(data, 0.0015f);

        AudioClip clip = AudioClip.Create($"BumperHit_{seed}", length, 1, SampleRate, false);
        clip.SetData(data, 0);
        clip.hideFlags = HideFlags.DontSave;
        return clip;
    }

    private static void Normalize(float[] data, float targetPeak)
    {
        float max = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            max = Mathf.Max(max, Mathf.Abs(data[i]));
        }

        if (max < 0.0001f)
        {
            return;
        }

        float gain = targetPeak / max;
        for (int i = 0; i < data.Length; i++)
        {
            data[i] *= gain;
        }
    }

    private static void ApplyFade(float[] data, float seconds)
    {
        int fade = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
        for (int i = 0; i < fade && i < data.Length; i++)
        {
            float gain = i / (float)fade;
            data[i] *= gain;
            data[data.Length - 1 - i] *= gain;
        }
    }
}
