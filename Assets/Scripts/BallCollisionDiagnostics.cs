using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Journalise les objets qui touchent la bille afin d'identifier un blocage physique.
/// Le composant est ajouté automatiquement à l'objet portant le tag Ball.
/// </summary>
public sealed class BallCollisionDiagnostics : MonoBehaviour
{
    private const float StuckSpeedThreshold = 0.15f;
    private const float StuckDelay = 0.35f;
    private const float StuckLogInterval = 0.75f;

    private Rigidbody body;
    private float lowSpeedSince = -1f;
    private float lastStuckLog = -Mathf.Infinity;
    private readonly HashSet<Collider> activeContacts = new HashSet<Collider>();

    [Tooltip("Active les journaux détaillés (coûteux). À laisser désactivé en jeu.")]
    [SerializeField] private bool verbose = false;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (verbose)
        {
            Debug.Log($"[Bille][Diagnostic] Démarrage : position={transform.position}, " +
                $"vitesse={(body != null ? body.linearVelocity : Vector3.zero)}, " +
                $"colliders={GetComponentsInChildren<Collider>(true).Length}", this);
        }
    }

    private void FixedUpdate()
    {
        if (!verbose || body == null)
        {
            return;
        }

        float speed = body.linearVelocity.magnitude;
        if (speed > StuckSpeedThreshold)
        {
            lowSpeedSince = -1f;
            return;
        }

        if (lowSpeedSince < 0f)
        {
            lowSpeedSince = Time.time;
        }

        if (Time.time - lowSpeedSince < StuckDelay
            || Time.time - lastStuckLog < StuckLogInterval)
        {
            return;
        }

        lastStuckLog = Time.time;
        string contacts = activeContacts.Count == 0
            ? "aucun contact enregistré"
            : string.Join(", ", GetContactNames());
        Debug.LogWarning(
            $"[Bille][Diagnostic] Bille presque immobile depuis {Time.time - lowSpeedSince:F2}s : " +
            $"vitesse={speed:F3}, position={transform.position}, contacts=[{contacts}]", this);
    }

    private void OnCollisionEnter(Collision collision)
    {
        activeContacts.Add(collision.collider);
        if (verbose)
        {
            LogCollision("ENTRÉE", collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        activeContacts.Add(collision.collider);
    }

    private void OnCollisionExit(Collision collision)
    {
        activeContacts.Remove(collision.collider);
        if (verbose)
        {
            Debug.Log(
                $"[Bille][Diagnostic] SORTIE collision avec '{GetHierarchyPath(collision.collider.transform)}' " +
                $"à t={Time.time:F3}, vitesse={body.linearVelocity.magnitude:F3}", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!verbose)
        {
            return;
        }

        Debug.LogWarning(
            $"[Bille][Diagnostic] ENTRÉE trigger '{GetHierarchyPath(other.transform)}' " +
            $"à t={Time.time:F3}, position={transform.position}", other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!verbose)
        {
            return;
        }

        Debug.Log(
            $"[Bille][Diagnostic] SORTIE trigger '{GetHierarchyPath(other.transform)}' " +
            $"à t={Time.time:F3}", other);
    }

    private void LogCollision(string eventName, Collision collision)
    {
        Vector3 normal = collision.contactCount > 0
            ? collision.GetContact(0).normal
            : Vector3.zero;
        Debug.LogWarning(
            $"[Bille][Diagnostic] {eventName} collision avec " +
            $"'{GetHierarchyPath(collision.collider.transform)}' " +
            $"(tag={collision.collider.tag}, layer={LayerMask.LayerToName(collision.collider.gameObject.layer)}) " +
            $"à t={Time.time:F3}, position={transform.position}, " +
            $"vitesse={body.linearVelocity} ({body.linearVelocity.magnitude:F3}), " +
            $"contacts={collision.contactCount}, normale={normal}", collision.collider);
    }

    private IEnumerable<string> GetContactNames()
    {
        foreach (Collider contact in activeContacts)
        {
            if (contact != null)
            {
                yield return GetHierarchyPath(contact.transform);
            }
        }
    }

    private static string GetHierarchyPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }
}
