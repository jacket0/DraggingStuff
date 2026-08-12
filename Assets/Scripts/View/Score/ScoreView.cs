using TMPro;
using UnityEngine;

public class ScoreView : MonoBehaviour
{
    [SerializeField] private ScoreSystem _scoreSystem;
    [SerializeField] private TMP_Text _text;

    private void OnEnable()
    {
        _scoreSystem.ScoreChanged += Refresh;
        Refresh(_scoreSystem.CurrentScore);
    }

    private void OnDisable()
    {
        _scoreSystem.ScoreChanged -= Refresh;
    }

    private void Refresh(long score)
    {
        _text.SetText(score.ToString());
    }
}
