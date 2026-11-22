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

    [Header("Animation")]
    [Min(0f)]
    [SerializeField] private float squashAmount = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float animationDuration = 0.11f;

    private readonly Dictionary<Rigidbody, float> lastContacts = new Dictionary<Rigidbody, float>();
    private Transform[] visuals;
    private Vector3[] visualScales;
    private float animationTime;
    private bool isAnimating;

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
            if (candidate.name.StartsWith(BumperPrefix) && !configured.Contains(candidate.gameObject))
            {
                candidate.gameObject.AddComponent<BumperPhysics>();
            }
        }
    }

    private void Awake()
    {
        EnsureCollisionShape();
        CacheVisuals();
    }

    private void Update()
    {
        if (!isAnimating)
        {
            return;
        }

        animationTime += Time.deltaTime;
        float progress = Mathf.Clamp01(animationTime / animationDuration);
        float pulse = Mathf.Sin(progress * Mathf.PI);
        float scale = 1f + squashAmount * pulse;

        for (int i = 0; i < visuals.Length; i++)
        {
            visuals[i].localScale = visualScales[i] * scale;
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
        PinballScore.Add(100);

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

        ball.linearVelocity = normal * reflectedRadialSpeed
            + tangent * tangentialSpeed * (1f - tangentDeflection);
        ball.AddForce(normal * radialImpulse, ForceMode.Impulse);

        animationTime = 0f;
        isAnimating = true;
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

        for (int i = 0; i < renderers.Length; i++)
        {
            visuals[i] = renderers[i].transform;
            visualScales[i] = visuals[i].localScale;
        }
    }
}
