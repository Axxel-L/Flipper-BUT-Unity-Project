using UnityEngine;

public class BallOutOfBounds : MonoBehaviour
{
    [Tooltip("Position de départ de la boule (lanceur du flipper)")]
    public Transform startPosition;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            ResetBall(other.attachedRigidbody);
        }
    }

    private void ResetBall(Rigidbody ballRb)
    {
        if (ballRb == null) return;

        // Stoppe tout mouvement
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        // Replace la boule au point de départ
        ballRb.transform.position = startPosition.position;
        ballRb.transform.rotation = startPosition.rotation;
    }
}