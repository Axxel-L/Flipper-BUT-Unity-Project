using UnityEngine;
using System.Collections;

public class SlingshotMover : MonoBehaviour
{
    [Header("Axe (X = rouge dans la scène)")]
    [Tooltip("Direction locale de la translation (sera normalisée). Pour l'axe rouge (X) : (1,0,0)")]
    public Vector3 direction = Vector3.right;

    [Tooltip("Utiliser l'espace local (true) ou l'espace monde (false)")]
    public bool useLocalSpace = true;

    [Header("Phase 1 : recul (avant le tir)")]
    [Tooltip("Distance du léger recul, en sens inverse de 'direction'")]
    public float pullbackDistance = 0.03f;

    [Tooltip("Temps pour reculer")]
    public float pullbackDuration = 0.06f;

    [Header("Phase 2 : coup (projection de la bille)")]
    [Tooltip("Distance parcourue vers l'avant, dans le sens de 'direction'")]
    public float punchDistance = 0.25f;

    [Tooltip("Temps pour aller jusqu'au bout du coup (très court = coup sec et rapide)")]
    public float punchDuration = 0.03f;

    [Header("Phase 3 : retour à la position initiale")]
    [Tooltip("Temps pour revenir à la position de repos")]
    public float returnDuration = 0.15f;

    [Header("Input")]
    [Tooltip("Touche qui déclenche le mouvement")]
    public KeyCode triggerKey = KeyCode.Space;

    private Vector3 startPosition;
    private Vector3 dirNormalized;
    private Coroutine currentRoutine;

    void Start()
    {
        // Mémorise la position de repos au démarrage
        startPosition = transform.localPosition;
        dirNormalized = direction.normalized;
    }

    void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            TriggerMove();
        }
    }

    public void TriggerMove()
    {
        // Si le slingshot est déjà en train de bouger, on ignore le nouvel appel
        // pour ne pas casser l'animation en cours (évite les à-coups).
        if (currentRoutine != null) return;

        currentRoutine = StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        Vector3 localDir = useLocalSpace ? dirNormalized : transform.InverseTransformDirection(dirNormalized);

        Vector3 pulledBackPosition = startPosition - localDir * pullbackDistance;
        Vector3 punchedPosition = startPosition + localDir * punchDistance;

        // Phase 1 : léger recul
        yield return MoveOverTime(transform.localPosition, pulledBackPosition, pullbackDuration);

        // Phase 2 : coup rapide vers l'avant (projette la bille)
        yield return MoveOverTime(transform.localPosition, punchedPosition, punchDuration);

        // Phase 3 : retour à la position de repos
        yield return MoveOverTime(transform.localPosition, startPosition, returnDuration);

        currentRoutine = null;
    }

    private IEnumerator MoveOverTime(Vector3 from, Vector3 to, float duration)
    {
        // Sécurité : si duration = 0, on saute directement à la position finale
        if (duration <= 0f)
        {
            transform.localPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }
        transform.localPosition = to;
    }
}