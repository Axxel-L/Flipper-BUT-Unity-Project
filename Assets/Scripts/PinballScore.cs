using TMPro;
using UnityEngine;

public static class PinballScore
{
    private const string ScoreTextName = "Score Actuel";
    private static TextMeshProUGUI scoreText;
    private static int currentScore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        currentScore = 0;
        scoreText = FindScoreText();
        UpdateDisplay();
    }

    public static void Add(int points)
    {
        if (points <= 0) return;

        if (scoreText == null)
        {
            scoreText = FindScoreText();
        }

        currentScore = currentScore > int.MaxValue - points
            ? int.MaxValue
            : currentScore + points;
        UpdateDisplay();
    }

    private static TextMeshProUGUI FindScoreText()
    {
        GameObject scoreObject = GameObject.Find(ScoreTextName);
        return scoreObject != null
            ? scoreObject.GetComponent<TextMeshProUGUI>()
            : null;
    }

    private static void UpdateDisplay()
    {
        if (scoreText != null)
            scoreText.text = currentScore.ToString("D7");
    }
}