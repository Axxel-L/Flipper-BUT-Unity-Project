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

        dir.y = 0f; // garde le mouvement à plat (retire cette ligne si tu veux de la hauteur)
        dir.Normalize();

        rb.linearVelocity = dir * force;
    }
}