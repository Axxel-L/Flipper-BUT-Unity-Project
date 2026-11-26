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
        ConfigureSlingshotColliders();
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

        if (target.name.StartsWith("Blocker", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
                {
                    continue;
                }

                Bounds bounds = meshFilter.sharedMesh.bounds;
                CapsuleCollider capsule = meshFilter.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = bounds.center;
                capsule.direction = 0;
                capsule.radius = Mathf.Min(bounds.size.y, bounds.size.z) * 0.5f;
                capsule.height = Mathf.Max(bounds.size.x, capsule.radius * 2f);
                return;
            }

            Debug.LogWarning($"Le blocker {target.name} ne possède aucun mesh utilisable pour sa collision.", target);
            return;
        }

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

    private static void ConfigureSlingshotColliders()
    {
        Slingshot[] slingshots = FindObjectsByType<Slingshot>(FindObjectsInactive.Exclude);
        foreach (Slingshot slingshot in slingshots)
        {
            MeshFilter[] meshFilters = slingshot.GetComponentsInChildren<MeshFilter>(true);
            Bounds localBounds = default;
            bool hasBounds = false;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                        (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                        (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                    Vector3 localPoint = slingshot.transform.InverseTransformPoint(
                        meshFilter.transform.TransformPoint(point));

                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }

            if (!hasBounds)
            {
                Debug.LogWarning($"Le slingshot {slingshot.name} ne possède aucun mesh pour calculer son collider.", slingshot);
                continue;
            }

            Vector3 size = localBounds.size;
            int direction = size.x >= size.y && size.x >= size.z
                ? 0
                : size.y >= size.z ? 1 : 2;
            float axialSize = direction == 0 ? size.x : direction == 1 ? size.y : size.z;
            float radius = direction == 0
                ? Mathf.Min(size.y, size.z) * 0.5f
                : direction == 1
                    ? Mathf.Min(size.x, size.z) * 0.5f
                    : Mathf.Min(size.x, size.y) * 0.5f;

            CapsuleCollider capsule = slingshot.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = slingshot.gameObject.AddComponent<CapsuleCollider>();
            }

            capsule.center = localBounds.center;
            capsule.direction = direction;
            capsule.radius = Mathf.Max(0.01f, radius);
            capsule.height = Mathf.Max(axialSize, capsule.radius * 2f);

            foreach (MeshCollider meshCollider in slingshot.GetComponentsInChildren<MeshCollider>(true))
            {
                meshCollider.enabled = false;
            }
        }
    }
}
