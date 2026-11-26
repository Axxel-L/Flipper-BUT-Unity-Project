using UnityEngine;

public class Slingshot : MonoBehaviour
{
    [SerializeField] private string ballTag = "Ball";
    [SerializeField] private float force = 12f;
    [SerializeField] private bool useSurfaceNormal = true;
    [SerializeField, Min(0f)] private float squashAmount = 0.08f;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.14f;

    private static readonly Color ImpactColor = new Color(1f, 0.55f, 0.12f, 1f);

    private MeshRenderer[] visualRenderers;
    private Transform[] visuals;
    private Vector3[] baseScales;
    private Color[] baseColors;
    private MaterialPropertyBlock propertyBlock;
    private float animationTime;
    private bool isAnimating;

    private void Awake()
    {
        CacheVisuals();
    }

    private void Update()
    {
        UpdateImpactAnimation();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(ballTag)) return;

        Rigidbody rb = collision.rigidbody;
        if (rb == null) return;

        TriggerImpactAnimation();

        Vector3 dir;
        if (useSurfaceNormal)
        {
            // Normale de la face touchée : renvoie la balle perpendiculairement
            dir = -collision.GetContact(0).normal;
        }
        else
        {
            dir = (collision.transform.position - transform.position);
        }

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = rb.worldCenterOfMass - transform.position;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = transform.right;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        dir.Normalize();

        rb.linearVelocity = dir * force;
    }

    private void CacheVisuals()
    {
        visualRenderers = GetComponentsInChildren<MeshRenderer>();
        visuals = new Transform[visualRenderers.Length];
        baseScales = new Vector3[visualRenderers.Length];
        baseColors = new Color[visualRenderers.Length];
        propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            MeshRenderer visualRenderer = visualRenderers[i];
            visuals[i] = visualRenderer.transform;
            baseScales[i] = visuals[i].localScale;

            Material material = visualRenderer.sharedMaterial;
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

    private void TriggerImpactAnimation()
    {
        animationTime = 0f;
        isAnimating = true;
    }

    private void UpdateImpactAnimation()
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
            Vector3 baseScale = baseScales[i];
            visuals[i].localScale = new Vector3(
                baseScale.x * (1f + squash),
                baseScale.y * (1f - squash),
                baseScale.z * (1f + squash));

            SetVisualColor(i, Color.Lerp(baseColors[i], ImpactColor, pulse));
        }

        if (progress >= 1f)
        {
            isAnimating = false;
            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i].localScale = baseScales[i];
                SetVisualColor(i, baseColors[i]);
            }
        }
    }

    private void SetVisualColor(int index, Color color)
    {
        MeshRenderer visualRenderer = visualRenderers[index];
        if (visualRenderer == null)
        {
            return;
        }

        visualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        visualRenderer.SetPropertyBlock(propertyBlock);
    }
}