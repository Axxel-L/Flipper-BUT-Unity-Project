using System;
using UnityEngine;

/// <summary>
/// Prépare les obstacles de la scène et incline la gravité dans le sens du plateau.
/// Les colliders existants sont conservés afin de ne pas modifier les flippers et sorties déjà configurés.
/// </summary>
public sealed class PinballPhysicsSetup : MonoBehaviour
{
    private static readonly string[] CollisionPrefixes =
    {
        "Bumper",
        "Blocker",
        "Slingshot",
        "Border",
        "Front",
        "Back",
        "Left",
        "Right",
        "Sortie"
    };

    [Header("Inclinaison du plateau")]
    [Tooltip("Inclinaison physique du plateau vers le bas, en degrés.")]
    [Range(0f, 15f)]
    [SerializeField] private float slopeAngle = 7f;

    [Tooltip("Direction horizontale vers laquelle la bille doit naturellement descendre.")]
    [SerializeField] private Vector3 downhillDirection = Vector3.back;

    [Min(0.01f)]
    [SerializeField] private float gravityMagnitude = 9.81f;

    [Tooltip("Force qui plaque la bille sur le plateau (0 = aucune, 1 = autant que la gravité).")]
    [Range(0f, 1f)]
    [SerializeField] private float pressForce = 0.5f;

    [Header("Collisions générées")]
    [Tooltip("Ajoute un peu de marge aux colliders générés pour éviter les traversées à haute vitesse.")]
    [Min(0f)]
    [SerializeField] private float colliderPadding = 0.02f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<PinballPhysicsSetup>() != null)
        {
            return;
        }

        GameObject setupObject = new GameObject(nameof(PinballPhysicsSetup));
        setupObject.AddComponent<PinballPhysicsSetup>();
    }

    private void Awake()
    {
        Physics.gravity = CalculateGravity();
        AddMissingColliders();
        DisableKnownBlockingColliders();
        ConfigureExitTriggers();
    }

    private Vector3 CalculateGravity()
    {
        Transform playfield = FindPlayfieldSurface();
        if (playfield != null)
        {
            Vector3 surfaceNormal = playfield.up;
            if (surfaceNormal.y < 0f)
            {
                surfaceNormal = -surfaceNormal;
            }

                        Vector3 towardFlippers = Vector3.left;
            Vector3 downhill = Vector3.ProjectOnPlane(towardFlippers, surfaceNormal);
            if (downhill.sqrMagnitude > 0.0001f)
            {
                // Composante le long du plateau + composante qui plaque la bille dessus
                Vector3 gravity = downhill.normalized * gravityMagnitude
                                - surfaceNormal * (gravityMagnitude * pressForce);
                Debug.Log($"[PinballPhysics] Gravité : {gravity}", playfield);
                return gravity;
            }
        }

        Vector3 horizontalDirection = downhillDirection;
        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude < 0.0001f)
        {
            horizontalDirection = Vector3.back;
        }

        horizontalDirection.Normalize();
        float angle = slopeAngle * Mathf.Deg2Rad;
        return Vector3.down * (gravityMagnitude * Mathf.Cos(angle))
            + horizontalDirection * (gravityMagnitude * Mathf.Sin(angle));
    }

    private static Transform FindPlayfieldSurface()
    {
        Transform[] candidates = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].name.Equals("Background", StringComparison.OrdinalIgnoreCase))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private void AddMissingColliders()
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!IsCollisionObject(candidate.name)
                || IsDisabledCollisionObject(candidate.name)
                || HasColliderInHierarchy(candidate))
            {
                continue;
            }

            AddCollider(candidate);
        }
    }

    private static void DisableKnownBlockingColliders()
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!IsDisabledCollisionObject(candidate.name))
            {
                continue;
            }

            Collider[] colliders = candidate.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

    }

    private static void ConfigureExitTriggers()
    {
        Transform[] exits = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        Transform start = FindStartPosition();

        for (int i = 0; i < exits.Length; i++)
        {
            Transform exit = exits[i];
            if (!exit.name.Equals("Sortie", StringComparison.OrdinalIgnoreCase)
                && !exit.name.Equals("Sortie Droite", StringComparison.OrdinalIgnoreCase)
                && !exit.name.Equals("Sortie Gauche", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Collider[] colliders = exit.GetComponentsInChildren<Collider>(true);
            for (int j = 0; j < colliders.Length; j++)
            {
                colliders[j].isTrigger = true;
            }

            BallOutOfBounds reset = exit.GetComponent<BallOutOfBounds>();
            if (reset == null)
            {
                reset = exit.gameObject.AddComponent<BallOutOfBounds>();
            }

            if (reset.startPosition == null)
            {
                reset.startPosition = start;
            }
        }
    }

    private static Transform FindStartPosition()
    {
        Transform[] candidates = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].name.Equals("BallStartPosition", StringComparison.OrdinalIgnoreCase))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private static bool IsCollisionObject(string objectName)
    {
        for (int i = 0; i < CollisionPrefixes.Length; i++)
        {
            if (objectName.StartsWith(CollisionPrefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDisabledCollisionObject(string objectName)
    {
        return objectName.Equals("Slingshot (2)", StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("Background (3)", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasColliderInHierarchy(Transform target)
    {
        return target.GetComponent<Collider>() != null
            || target.GetComponentInChildren<Collider>() != null;
    }

    private void AddCollider(Transform target)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>();

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = body != null;
            meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                | MeshColliderCookingOptions.EnableMeshCleaning
                | MeshColliderCookingOptions.WeldColocatedVertices;
            return;
        }

        // Un blocker doit utiliser sa géométrie réelle. Un BoxCollider englobant
        // créerait des surfaces invisibles qui peuvent fermer la voie de lancement.
        if (target.name.StartsWith("Blocker", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"Le blocker {target.name} ne possède aucun mesh utilisable pour sa collision.", target);
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        BoxCollider boxCollider = target.gameObject.AddComponent<BoxCollider>();
        Vector3 localSize = target.InverseTransformVector(worldBounds.size);
        boxCollider.size = new Vector3(
            Mathf.Abs(localSize.x) + colliderPadding,
            Mathf.Abs(localSize.y) + colliderPadding,
            Mathf.Abs(localSize.z) + colliderPadding);
        boxCollider.center = target.InverseTransformPoint(worldBounds.center);
    }
}
