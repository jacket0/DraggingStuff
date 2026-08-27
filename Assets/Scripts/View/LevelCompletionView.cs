using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelCompletionView : MonoBehaviour
{
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private TMP_Text _finalScoreResult;
    [SerializeField] private Button _reviewButton;

    public event Action MenuRequested;
    public event Action RestartRequested;
    public event Action ReviewRequested;

    private void OnEnable()
    {
        _restartButton.onClick.AddListener(RestartButtonClicked);
        _menuButton.onClick.AddListener(MenuButtonClicked);
        _reviewButton.onClick.AddListener(ReviewButtonClicked);
    }

    private void OnDisable()
    {
        _restartButton?.onClick.RemoveListener(RestartButtonClicked);
        _menuButton?.onClick.RemoveListener(MenuButtonClicked);
        _reviewButton.onClick.RemoveListener(ReviewButtonClicked);
    }

    public void Show(long finalScore, bool isReviewAvailable)
    {
        _finalScoreResult.SetText(finalScore.ToString());
        _reviewButton.gameObject.SetActive(isReviewAvailable);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void HideReviewButton()
    {
        _reviewButton.gameObject.SetActive(false);
    }

    private void RestartButtonClicked()
    {
        RestartRequested?.Invoke();   
    }

    private void MenuButtonClicked()
    {
        MenuRequested?.Invoke();
    }

    private void ReviewButtonClicked()
    {
        ReviewRequested?.Invoke();
    }
}
