using System.Globalization;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class TotalScoreView : MonoBehaviour
{
    [SerializeField] private TotalScoreService _totalScore;

    private TMP_Text _scoreText;

    private void Awake()
    {
        _scoreText = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        _scoreText.SetText(FormatScore(_totalScore.Value));
    }

    private static string FormatScore(long score)
    {
        return score.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
    }
}
