using UnityEngine;

public class BallOutOfBounds : MonoBehaviour
{
    [Tooltip("Position de départ de la boule (lanceur du flipper)")]
    public Transform startPosition;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
        {
            return;
        }

        PinballGameManager manager = PinballGameManager.Instance;
        if (manager != null)
        {
            manager.RegisterDrain();
            return;
        }

        Debug.LogWarning(
            $"[Bille][Diagnostic] Reset demandée par le trigger '{other.name}' " +
            $"depuis '{name}' à t={Time.time:F3}.", this);
        ResetBall(other.attachedRigidbody);
    }

    private void ResetBall(Rigidbody ballRb)
    {
        if (ballRb == null) return;
        if (startPosition == null)
        {
            Debug.LogError($"[Bille] Impossible de réinitialiser la bille : startPosition est vide sur '{name}'.", this);
            return;
        }

        // Stoppe tout mouvement
        // Stoppe tout mouvement
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.useGravity = false;

        // Coupe la traînée pendant la téléportation, sinon elle trace une ligne sur tout le plateau
        TrailRenderer trail = ballRb.GetComponent<TrailRenderer>();
        if (trail != null) trail.emitting = false;

        // Replace la bille (Rigidbody ET Transform, à cause de l'interpolation)
        ballRb.position = startPosition.position;
        ballRb.rotation = startPosition.rotation;
        ballRb.transform.SetPositionAndRotation(startPosition.position, startPosition.rotation);
        Physics.SyncTransforms();

        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }
        Debug.LogWarning(
            $"[Bille][Diagnostic] Bille replacée par '{name}' à {startPosition.position}.", this);
    }
}