using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelCompletionView : MonoBehaviour
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private Button _restart;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private GameObject _window;

    private void Awake()
    {
        _window.SetActive(false);
    }

    private void OnEnable()
    {
        _levelSession.LevelCompleted += ShowWin;
        _restart.onClick.AddListener(_levelSession.RestartLevel);
    }

    private void OnDisable()
    {
        _levelSession.LevelCompleted -= ShowWin;
        _restart.onClick.RemoveListener(_levelSession.RestartLevel);
    }

    private void ShowWin()
    {
        _window.SetActive(true);
    }
}
