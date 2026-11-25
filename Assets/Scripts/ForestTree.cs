using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ForestTree : MonoBehaviour
{
    [Header("Rebond")]
    [Min(0f)] [SerializeField] private float minimumSpeed = 4.5f;
    [Min(0f)] [SerializeField] private float impactSpeedBoost = 2f;
    [Range(0f, 1.5f)] [SerializeField] private float energyRetention = 1f;
    [Range(0f, 60f)] [SerializeField] private float randomAngle = 14f;
    [Range(0f, 0.5f)] [SerializeField] private float randomSpeedVariation = 0.12f;
    [Min(0.01f)] [SerializeField] private float contactCooldown = 0.08f;

    [Header("Retour visuel")]
    [SerializeField] private Transform visual;
    [Min(0f)] [SerializeField] private float swayAngle = 5f;
    [Min(0.05f)] [SerializeField] private float swayDuration = 0.34f;

    [Header("Son")]
    [Range(0f, 1f)] [SerializeField] private float impactSoundVolume = 0.45f;

    private readonly Dictionary<Rigidbody, float> lastContacts = new Dictionary<Rigidbody, float>();
    private readonly List<float> recentHits = new List<float>();
    private AudioSource impactAudio;
    private Quaternion visualBaseRotation;
    private Vector3 swayAxisLocal;
    private float swayTime;
    private bool isSwaying;

    private void Awake()
    {
        if (visual == null)
        {
            visual = transform.Find("Visuel");
        }

        if (visual != null)
        {
            visualBaseRotation = visual.localRotation;
        }

        ArcadeAudio.GetTreeHit(0);

        impactAudio = gameObject.AddComponent<AudioSource>();
        impactAudio.playOnAwake = false;
        impactAudio.spatialBlend = 0.45f;
        impactAudio.rolloffMode = AudioRolloffMode.Linear;
        impactAudio.minDistance = 8f;
        impactAudio.maxDistance = 60f;
        impactAudio.dopplerLevel = 0f;
        impactAudio.volume = 1f;
    }

    private void Update()
    {
        if (!isSwaying || visual == null)
        {
            return;
        }

        swayTime += Time.deltaTime;
        float progress = Mathf.Clamp01(swayTime / swayDuration);
        float angle = swayAngle * Mathf.Sin(progress * Mathf.PI * 2f) * (1f - progress);
        visual.localRotation = visualBaseRotation * Quaternion.AngleAxis(angle, swayAxisLocal);

        if (progress >= 1f)
        {
            isSwaying = false;
            visual.localRotation = visualBaseRotation;
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
            && Time.time - lastContact < contactCooldown)
        {
            return;
        }

        lastContacts[ball] = Time.time;

        Vector3 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : ball.worldCenterOfMass;

        PinballGameManager manager = PinballGameManager.Instance;
        bool breakStuck = false;

        if (manager != null)
        {
            manager.AddScore(manager.treePoints, contactPoint);
            manager.NotifyImpact(transform.position);

            float now = Time.time;
            recentHits.Add(now);
            recentHits.RemoveAll(time => now - time > 1.5f);

            if (recentHits.Count >= 4)
            {
                recentHits.Clear();
                breakStuck = true;
            }
        }
        else
        {
            PinballScore.Add(25, contactPoint);
        }

        Vector3 normal = ball.worldCenterOfMass - transform.position;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.forward;
            normal.y = 0f;
        }

        normal.Normalize();
        Vector3 incoming = ball.linearVelocity;
        Vector3 tangent = Vector3.Cross(Vector3.up, normal);
        float radialSpeed = Vector3.Dot(incoming, normal);
        float tangentialSpeed = Vector3.Dot(incoming, tangent);
        float reflectedSpeed = Mathf.Max(minimumSpeed, Mathf.Abs(radialSpeed) * energyRetention)
            + impactSpeedBoost;

        float speedVariation = Random.Range(1f - randomSpeedVariation, 1f + randomSpeedVariation);
        float angle = Random.Range(-randomAngle, randomAngle);
        Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * normal;

        ball.linearVelocity = direction * (reflectedSpeed * speedVariation)
            + tangent * tangentialSpeed * 0.9f;

        if (breakStuck && manager != null)
        {
            manager.TryKickStuckBall(ball, true);
        }

        StartSway(direction);
        PlaySound(incoming.magnitude);
    }

    private void StartSway(Vector3 direction)
    {
        if (visual == null)
        {
            return;
        }

        Vector3 axis = Vector3.Cross(Vector3.up, direction);
        if (axis.sqrMagnitude < 0.0001f)
        {
            axis = Vector3.right;
        }

        swayAxisLocal = transform.InverseTransformDirection(axis).normalized;
        swayTime = 0f;
        isSwaying = true;
    }

    private void PlaySound(float impactSpeed)
    {
        if (impactAudio == null)
        {
            return;
        }

        AudioClip clip = ArcadeAudio.GetTreeHit(Random.Range(0, 2));
        if (clip == null)
        {
            return;
        }

        impactAudio.pitch = Random.Range(0.9f, 1.1f);
        float volume = impactSoundVolume * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(impactSpeed / 30f));
        impactAudio.PlayOneShot(clip, volume);
    }
}
