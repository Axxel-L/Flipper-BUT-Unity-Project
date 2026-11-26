using UnityEngine;

/// <summary>
/// Façade de score conservée pour les scripts existants. Toute la logique
/// (combo, multiplicateur de tempête, meilleurs scores) vit dans PinballGameManager.
/// </summary>
public static class PinballScore
{
    public static int Current => PinballGameManager.Instance != null ? PinballGameManager.Instance.Score : 0;

    public static void Add(int points)
    {
        PinballGameManager manager = PinballGameManager.Instance;
        if (manager != null)
        {
            manager.AddScore(points);
        }
    }

    public static void Add(int points, Vector3 worldPosition)
    {
        PinballGameManager manager = PinballGameManager.Instance;
        if (manager != null)
        {
            manager.AddScore(points, worldPosition);
        }
    }

    public static void Add(int points, Vector3 worldPosition, string label)
    {
        PinballGameManager manager = PinballGameManager.Instance;
        if (manager != null)
        {
            manager.AddScore(points, worldPosition, label);
        }
    }
}
