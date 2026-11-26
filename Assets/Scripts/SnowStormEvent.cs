using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tempête de neige périodique : particules, post-processing et vent.
/// Le multiplicateur de score est appliqué par <see cref="PinballGameManager"/>.
/// </summary>
public sealed class SnowStormEvent : MonoBehaviour
{
    [Header("Rythme")]
    [Min(5f)] public float minimumDelay = 30f;
    [Min(5f)] public float maximumDelay = 48f;
    [Min(2f)] public float duration = 10f;

    [Header("Neige")]
    [Min(10f)] public float emissionRate = 260f;
    [Min(1f)] public float particleLifetime = 7f;

    [Header("Vent")]
    [Range(0f, 1f)] public float windVolume = 0.26f;

    private PinballGameManager manager;
    private ParticleSystem snow;
    private ParticleSystem.EmissionModule emission;
    private Volume volume;
    private VolumeProfile profile;
    private AudioSource wind;
    private bool storming;
    private float stormWeight;
    private float windWeight;

    public void Bind(PinballGameManager gameManager)
    {
        manager = gameManager;
    }

    private void Start()
    {
        if (manager == null)
        {
            manager = PinballGameManager.Instance;
        }

        EnsureSnow();
        EnsureVolume();
        EnsureWind();
        StartCoroutine(WeatherLoop());
    }

    private void EnsureSnow()
    {
        GameObject existing = GameObject.Find("Snowfall");
        if (existing != null)
        {
            snow = existing.GetComponent<ParticleSystem>();
        }

        if (snow == null)
        {
            GameObject host = new GameObject("Snowfall");
            host.transform.position = new Vector3(-4f, 17f, 0f);
            snow = host.AddComponent<ParticleSystem>();
            ConfigureSnow(snow);
        }

        emission = snow.emission;
        emission.enabled = false;
        snow.gameObject.SetActive(false);
    }

    private void ConfigureSnow(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.24f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 1f, 1f, 0.92f);
        main.maxParticles = 2600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(58f, 1f, 42f);

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
        velocity.y = new ParticleSystem.MinMaxCurve(-4.4f, -7.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.45f;
        noise.damping = true;

        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.material = CreateSnowMaterial();
    }

    private Material CreateSnowMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        Material material = new Material(shader);
        material.mainTexture = CreateSnowTexture();
        material.color = Color.white;
        return material;
    }

    private Texture2D CreateSnowTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * (3f - 2f * alpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private void EnsureVolume()
    {
        GameObject existing = GameObject.Find("Storm Volume");
        if (existing != null)
        {
            volume = existing.GetComponent<Volume>();
        }

        if (volume == null)
        {
            GameObject host = new GameObject("Storm Volume");
            volume = host.AddComponent<Volume>();
        }

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "StormVolumeProfile";

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.32f);
            color.contrast.Override(14f);
            color.saturation.Override(-38f);
            color.colorFilter.Override(new Color(0.72f, 0.84f, 1f, 1f));

            WhiteBalance whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(-34f);
            whiteBalance.tint.Override(6f);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.86f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.72f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.58f);

            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.28f);
            grain.response.Override(0.75f);
        }

        volume.isGlobal = true;
        volume.priority = 20f;
        volume.weight = 0f;
        volume.sharedProfile = profile;
    }

    private void EnsureWind()
    {
        AudioClip clip = ArcadeAudio.GetStormWind();
        if (clip == null)
        {
            return;
        }

        wind = gameObject.GetComponent<AudioSource>();
        if (wind == null)
        {
            wind = gameObject.AddComponent<AudioSource>();
        }

        wind.clip = clip;
        wind.loop = true;
        wind.playOnAwake = false;
        wind.spatialBlend = 0f;
        wind.volume = 0f;
        wind.dopplerLevel = 0f;
    }

    private IEnumerator WeatherLoop()
    {
        yield return new WaitForSeconds(12f);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minimumDelay, maximumDelay));
            yield return RunStorm();
        }
    }

    /// <summary>Déclenche immédiatement une tempête (utile pour tester ou pour un bonus).</summary>
    public void TriggerStormNow()
    {
        StopAllCoroutines();
        StartCoroutine(RunStorm());
        StartCoroutine(WeatherLoop());
    }

    private IEnumerator RunStorm()
    {
        storming = true;
        if (manager != null)
        {
            manager.SetStorm(true);
        }

        snow.gameObject.SetActive(true);
        emission.enabled = true;
        emission.rateOverTime = 0f;

        if (wind != null)
        {
            wind.volume = 0f;
            wind.Play();
        }

        float time = 0f;
        const float rampUp = 1.2f;
        while (time < rampUp)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / rampUp);
            emission.rateOverTime = emissionRate * t;
            stormWeight = Mathf.SmoothStep(0f, 1f, t);
            windWeight = windVolume * t;
            ApplyWeights();
            yield return null;
        }

        emission.rateOverTime = emissionRate;
        stormWeight = 1f;
        windWeight = windVolume;
        ApplyWeights();

        yield return new WaitForSeconds(duration);

        time = 0f;
        const float rampDown = 1.6f;
        while (time < rampDown)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / rampDown);
            emission.rateOverTime = emissionRate * (1f - t);
            stormWeight = 1f - Mathf.SmoothStep(0f, 1f, t);
            windWeight = windVolume * (1f - t);
            ApplyWeights();
            yield return null;
        }

        emission.enabled = false;
        emission.rateOverTime = 0f;
        stormWeight = 0f;
        windWeight = 0f;
        ApplyWeights();
        storming = false;

        if (manager != null)
        {
            manager.SetStorm(false);
        }

        if (wind != null)
        {
            wind.Stop();
        }

        yield return new WaitForSeconds(5f);
        if (!storming)
        {
            snow.gameObject.SetActive(false);
        }
    }

    private void ApplyWeights()
    {
        if (volume != null)
        {
            volume.weight = stormWeight;
        }

        if (wind != null)
        {
            wind.volume = windWeight;
        }
    }
}
