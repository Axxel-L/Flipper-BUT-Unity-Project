using UnityEngine;

public class Slingshot : MonoBehaviour
{
    [SerializeField] private string ballTag = "Ball";
    [SerializeField] private float force = 12f;
    [SerializeField] private bool useSurfaceNormal = true;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(ballTag)) return;

        Rigidbody rb = collision.rigidbody;
        if (rb == null) return;

        PinballScore.Add(50);

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
}