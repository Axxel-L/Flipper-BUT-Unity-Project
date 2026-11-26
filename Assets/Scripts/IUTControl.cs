using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class IUTBumper : MonoBehaviour
{
    [Header("Propulsion")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private string ballTag = "Ball";

    [Header("Optionnel")]
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private float hitSoundVolume = 0.9f;
    [SerializeField] private float hitSoundCooldown = 0.5f;
    [SerializeField] private bool debugLogs = false;

    private float lastPushTime = -10f;
    private float lastSoundTime = -10f;
    private readonly List<float> recentHits = new List<float>();

    private void Awake()
    {
        if (hitSound == null)
        {
            hitSound = gameObject.AddComponent<AudioSource>();
            hitSound.playOnAwake = false;
            hitSound.spatialBlend = 0.6f;
            hitSound.rolloffMode = AudioRolloffMode.Linear;
            hitSound.minDistance = 10f;
            hitSound.maxDistance = 80f;
            hitSound.dopplerLevel = 0f;
            hitSound.volume = 1f;
        }

        ArcadeAudio.GetIut67();
        ArcadeAudio.GetBumperHit(0);
    }

    private void OnCollisionEnter(Collision collision) => Push(collision, true);
    private void OnCollisionStay(Collision collision) => Push(collision, false);

    private void Push(Collision collision, bool addScore)
    {
        if (!collision.collider.CompareTag(ballTag)) return;

        Rigidbody ballRb = collision.rigidbody;
        if (ballRb == null) return;

        if (Time.time - lastPushTime < 0.2f) return;

        PinballGameManager manager = PinballGameManager.Instance;
        bool breakStuck = false;

        if (manager != null)
        {
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

        ContactPoint contact = collision.GetContact(0);

        Vector3 outward = contact.normal;
        if (Vector3.Dot(outward, ballRb.position - contact.point) < 0f)
            outward = -outward;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f)
        {
            outward = ballRb.position - transform.position;
            outward.y = 0f;
        }
        outward.Normalize();

        Vector3 incoming = collision.relativeVelocity;
        incoming.y = 0f;
        if (Vector3.Dot(incoming, outward) > 0f) incoming = -incoming;

        Vector3 reflected = Vector3.Reflect(incoming, outward);
        reflected.y = 0f;
        if (reflected.sqrMagnitude < 0.0001f || Vector3.Dot(reflected.normalized, outward) < 0.1f)
            reflected = outward;
        reflected.Normalize();

        Vector3 newVelocity = reflected * Mathf.Max(speed, incoming.magnitude);

        if (debugLogs)
            Debug.Log($"[IUT] outward={outward} incoming={incoming} -> newVelocity={newVelocity} (vitesse avant={ballRb.linearVelocity})");

        ballRb.linearVelocity = newVelocity;
        lastPushTime = Time.time;

        if (!breakStuck)
        {
            StartCoroutine(ReapplyNextStep(ballRb, newVelocity));
        }

        if (breakStuck && manager != null)
        {
            manager.TryKickStuckBall(ballRb, true);
        }

        if (addScore)
        {
            if (PinballGameManager.Instance != null)
            {
                PinballGameManager.Instance.AddScore(PinballGameManager.Instance.iutPoints, contact.point);
            }
            else
            {
                PinballScore.Add(75, contact.point);
            }
        }
        PlayHitSound();
    }

    private void PlayHitSound()
    {
        if (hitSound == null || hitSound.isPlaying)
        {
            return;
        }

        if (Time.time - lastSoundTime < hitSoundCooldown)
        {
            return;
        }

        lastSoundTime = Time.time;

        if (ArcadeAudio.FunSoundsEnabled)
        {
            AudioClip clip = ArcadeAudio.GetIut67();
            if (clip != null)
            {
                hitSound.pitch = 1f;
                hitSound.PlayOneShot(clip, hitSoundVolume);
                return;
            }
        }

        // Mode normal : ancien son de la plaque s'il existe, sinon impact synthétique.
        if (hitSound.clip != null)
        {
            hitSound.Play();
            return;
        }

        AudioClip normal = ArcadeAudio.GetBumperHit(0);
        if (normal != null)
        {
            hitSound.pitch = 1f;
            hitSound.PlayOneShot(normal, hitSoundVolume);
        }
    }

    private IEnumerator ReapplyNextStep(Rigidbody rb, Vector3 velocity)
    {
        yield return new WaitForFixedUpdate();
        if (rb == null) yield break;

        if (debugLogs)
            Debug.Log($"[IUT] vitesse de la bille au pas suivant = {rb.linearVelocity} (attendue {velocity})");

        rb.linearVelocity = velocity;
    }
}