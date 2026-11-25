using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Physique et animation d'un bumper de flipper.
/// Le bootstrap permet d'utiliser le script directement avec les instances de modèle
/// déjà présentes dans la scène, sans modifier chaque instance manuellement.
/// </summary>
public sealed class BumperPhysics : MonoBehaviour
{
    private const string BumperPrefix = "Bumper";
    private const float ContactCooldown = 0.055f;

    [Header("Réglages du rebond")]
    [Min(0f)]
    [SerializeField] private float minimumSpeed = 7.5f;
    [Min(0f)]
    [SerializeField] private float impactSpeedBoost = 5f;
    [Range(0f, 1.5f)]
    [SerializeField] private float energyRetention = 1.08f;
    [Min(0f)]
    [SerializeField] private float radialImpulse = 2.2f;
    [Min(0f)]
    [SerializeField] private float tangentDeflection = 0.12f;

    [Header("Direction aléatoire")]
    [Tooltip("Écart angulaire maximal par rapport à la normale du bumper, en degrés (0 = rebond droit).")]
    [Range(0f, 89f)]
    [SerializeField] private float randomAngle = 40f;

    [Tooltip("Variation aléatoire de la vitesse de sortie (0.2 = ±20 %).")]
    [Range(0f, 0.8f)]
    [SerializeField] private float randomSpeedVariation = 0.18f;

    [Header("Animation")]
    [Min(0f)]
    [SerializeField] private float squashAmount = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float animationDuration = 0.11f;

    [Header("Effets d'impact")]
    [Tooltip("Active le flash de couleur, la lumière et le son à chaque impact.")]
    [SerializeField] private bool enableImpactEffects = true;

    [Tooltip("Couleur du flash appliqué au bumper à l'impact.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.55f, 0.12f, 1f);

    [Tooltip("Intensité du flash de couleur (1 = couleur vive, valeurs > 1 = surbrillance).")]
    [Range(0f, 4f)]
    [SerializeField] private float flashIntensity = 1.8f;

    [Tooltip("Intensité maximale de la petite lumière allumée à l'impact.")]
    [Range(0f, 20f)]
    [SerializeField] private float lightFlashIntensity = 5f;

    [Tooltip("Volume du son de bumper.")]
    [Range(0f, 1f)]
    [SerializeField] private float impactSoundVolume = 0.85f;

    [Tooltip("Plage de hauteur du son à chaque impact (x = minimum, y = maximum).")]
    [SerializeField] private Vector2 soundPitchRange = new Vector2(0.92f, 1.12f);

    [Tooltip("Durée du flash et de la lumière d'impact, en secondes.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float flashDuration = 0.22f;

    private readonly Dictionary<Rigidbody, float> lastContacts = new Dictionary<Rigidbody, float>();
    private Transform[] visuals;
    private Vector3[] visualScales;
    private MeshRenderer[] visualRenderers;
    private Color[] baseColors;
    private MaterialPropertyBlock propertyBlock;
    private AudioSource impactAudio;
    private Light flashLight;
    private float animationTime;
    private bool isAnimating;
    private float flashTime;
    private bool isFlashing;

    private readonly List<float> recentHits = new List<float>();
    private static readonly List<float> globalHits = new List<float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnSceneBumpers()
    {
        BumperPhysics[] existing = FindObjectsByType<BumperPhysics>(FindObjectsInactive.Exclude);
        HashSet<GameObject> configured = new HashSet<GameObject>();
        foreach (BumperPhysics bumper in existing)
        {
            configured.Add(bumper.gameObject);
        }

        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!candidate.name.StartsWith(BumperPrefix) || configured.Contains(candidate.gameObject))
            {
                continue;
            }

            // Un bumper peut avoir un enfant de mesh qui porte aussi le nom "Bumper" :
            // on évite d'empiler deux composants (double animation, double son).
            if (candidate.GetComponentInParent<BumperPhysics>() != null)
            {
                continue;
            }

            candidate.gameObject.AddComponent<BumperPhysics>();
        }
    }

    private void Awake()
    {
        EnsureCollisionShape();
        CacheVisuals();
        CreateImpactLight();
        CreateImpactAudio();
    }

    private void Update()
    {
        UpdateSquash();
        UpdateFlash();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody ball = collision.rigidbody;
        if (ball == null || !ball.CompareTag("Ball"))
        {
            return;
        }

        if (lastContacts.TryGetValue(ball, out float lastContact)
            && Time.time - lastContact < ContactCooldown)
        {
            return;
        }

        lastContacts[ball] = Time.time;
        Vector3 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : ball.worldCenterOfMass;

        if (PinballGameManager.Instance != null)
        {
            PinballGameManager.Instance.RegisterBumperHit(contactPoint);
            PinballGameManager.Instance.NotifyImpact(transform.position);
        }
        else
        {
            PinballScore.Add(100, contactPoint);
        }

        Vector3 normal = ball.worldCenterOfMass - transform.position;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = GetPlanarContactNormal(collision);
        }

        normal.Normalize();
        Vector3 incoming = ball.linearVelocity;
        Vector3 tangent = Vector3.Cross(Vector3.up, normal);
        float incomingRadialSpeed = Vector3.Dot(incoming, normal);
        float tangentialSpeed = Vector3.Dot(incoming, tangent);
        float reflectedRadialSpeed = Mathf.Max(minimumSpeed, Mathf.Abs(incomingRadialSpeed) * energyRetention)
            + impactSpeedBoost;

        float speedVariation = Random.Range(1f - randomSpeedVariation, 1f + randomSpeedVariation);
        float angle = Random.Range(-randomAngle, randomAngle);
        Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * normal;

        ball.linearVelocity = direction * (reflectedRadialSpeed * speedVariation)
            + tangent * tangentialSpeed * (1f - tangentDeflection);
        ball.AddForce(direction * radialImpulse, ForceMode.Impulse);

        if (ShouldBreakStuck())
        {
            PinballGameManager manager = PinballGameManager.Instance;
            if (manager != null)
            {
                manager.TryKickStuckBall(ball);
            }
            else
            {
                Vector3 kickDirection = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                float kick = Mathf.Max(46f, ball.linearVelocity.magnitude);
                ball.linearVelocity = kickDirection * kick;
                ball.WakeUp();
            }
        }

        animationTime = 0f;
        isAnimating = true;

        PlayImpactEffects(incoming.magnitude);
    }

    /// <summary>
    /// Détecte une bille coincée : plusieurs impacts rapprochés sur ce bumper
    /// (ou trop d'impacts sur l'ensemble des bumpers) déclenchent une propulsion.
    /// </summary>
    private bool ShouldBreakStuck()
    {
        float now = Time.time;

        recentHits.Add(now);
        recentHits.RemoveAll(time => now - time > 1.6f);

        globalHits.Add(now);
        globalHits.RemoveAll(time => now - time > 1.4f);

        bool stuck = recentHits.Count >= 3 || globalHits.Count >= 6;
        if (!stuck)
        {
            return false;
        }

        recentHits.Clear();
        globalHits.Clear();
        return true;
    }

    private void UpdateSquash()
    {
        if (!isAnimating)
        {
            return;
        }

        animationTime += Time.deltaTime;
        float progress = Mathf.Clamp01(animationTime / animationDuration);
        float pulse = Mathf.Sin(progress * Mathf.PI);
        float squash = squashAmount * pulse;

        for (int i = 0; i < visuals.Length; i++)
        {
            Vector3 baseScale = visualScales[i];
            visuals[i].localScale = new Vector3(
                baseScale.x * (1f + squash),
                baseScale.y * (1f - squash),
                baseScale.z * (1f + squash));
        }

        if (progress >= 1f)
        {
            isAnimating = false;
            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i].localScale = visualScales[i];
            }
        }
    }

    private void UpdateFlash()
    {
        if (!isFlashing)
        {
            return;
        }

        flashTime += Time.deltaTime;
        float progress = Mathf.Clamp01(flashTime / flashDuration);
        float amount = Mathf.Pow(1f - progress, 1.6f);
        ApplyFlash(amount);

        if (progress >= 1f)
        {
            isFlashing = false;
            ApplyFlash(0f);
        }
    }

    private void PlayImpactEffects(float impactSpeed)
    {
        if (!enableImpactEffects)
        {
            return;
        }

        flashTime = 0f;
        isFlashing = true;
        ApplyFlash(1f);

        if (impactAudio != null)
        {
            AudioClip clip = ArcadeAudio.GetBumperHit(Random.Range(0, 3));
            if (clip != null)
            {
                float pitchMin = Mathf.Min(soundPitchRange.x, soundPitchRange.y);
                float pitchMax = Mathf.Max(soundPitchRange.x, soundPitchRange.y);
                impactAudio.pitch = Random.Range(pitchMin, pitchMax);

                float volume = impactSoundVolume
                    * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(impactSpeed / 35f));
                impactAudio.PlayOneShot(clip, volume);
            }
        }
    }

    private void ApplyFlash(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (flashLight != null)
        {
            flashLight.intensity = lightFlashIntensity * amount;
        }

        if (visualRenderers == null || visualRenderers.Length == 0 || propertyBlock == null)
        {
            return;
        }

        Color target = flashColor * (1f + flashIntensity);
        target.a = 1f;

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            MeshRenderer renderer = visualRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color tint = Color.Lerp(baseColors[i], target, amount);
            propertyBlock.SetColor("_BaseColor", tint);
            propertyBlock.SetColor("_Color", tint);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void CreateImpactLight()
    {
        if (!enableImpactEffects)
        {
            return;
        }

        GameObject lightObject = new GameObject("ImpactLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up * 0.5f;

        flashLight = lightObject.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = flashColor;
        flashLight.range = 8f;
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;
        flashLight.renderMode = LightRenderMode.Auto;
    }

    private void CreateImpactAudio()
    {
        if (!enableImpactEffects)
        {
            return;
        }

        // Pré-génère les sons pour éviter un micro-freeze au premier impact.
        ArcadeAudio.GetBumperHit(0);

        impactAudio = gameObject.AddComponent<AudioSource>();
        impactAudio.playOnAwake = false;
        impactAudio.spatialBlend = 0.55f;
        impactAudio.rolloffMode = AudioRolloffMode.Linear;
        impactAudio.minDistance = 12f;
        impactAudio.maxDistance = 90f;
        impactAudio.dopplerLevel = 0f;
        impactAudio.volume = 1f;
    }

    private Vector3 GetPlanarContactNormal(Collision collision)
    {
        if (collision.contactCount == 0)
        {
            return Vector3.forward;
        }

        Vector3 normal = collision.GetContact(0).normal;
        normal.y = 0f;
        return normal.sqrMagnitude > 0.0001f ? normal : Vector3.forward;
    }

    private void EnsureCollisionShape()
    {
        if (GetComponent<Collider>() != null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"Le bumper {name} ne possède aucun rendu pour calculer sa collision.", this);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        SphereCollider collider = gameObject.AddComponent<SphereCollider>();
        Vector3 lossyScale = transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z), 0.001f);
        collider.center = transform.InverseTransformPoint(bounds.center);
        collider.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f / largestScale;
    }

    private void CacheVisuals()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        visuals = new Transform[renderers.Length];
        visualScales = new Vector3[renderers.Length];
        visualRenderers = renderers;
        baseColors = new Color[renderers.Length];
        propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            visuals[i] = renderers[i].transform;
            visualScales[i] = visuals[i].localScale;

            Material material = renderers[i].sharedMaterial;
            if (material != null && material.HasProperty("_BaseColor"))
            {
                baseColors[i] = material.GetColor("_BaseColor");
            }
            else if (material != null && material.HasProperty("_Color"))
            {
                baseColors[i] = material.GetColor("_Color");
            }
            else
            {
                baseColors[i] = Color.white;
            }
        }
    }
}
