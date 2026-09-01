using UnityEngine;
using UnityEngine.UI;

public sealed class LeaderboardSwitchView : MonoBehaviour
{
    [SerializeField] private Button _totalScoreButton;
    [SerializeField] private Button _endlessScoreButton;
    [SerializeField] private CanvasGroup _totalScoreContent;
    [SerializeField] private CanvasGroup _endlessScoreContent;

    private void OnEnable()
    {
        _totalScoreButton.onClick.AddListener(ShowTotalScore);
        _endlessScoreButton.onClick.AddListener(ShowEndlessScore);
        ShowTotalScore();
    }

    private void OnDisable()
    {
        _totalScoreButton.onClick.RemoveListener(ShowTotalScore);
        _endlessScoreButton.onClick.RemoveListener(ShowEndlessScore);
    }

    private void ShowTotalScore()
    {
        SetSelectedContent(_totalScoreContent, _endlessScoreContent);
        SetSelectedButton(_totalScoreButton, _endlessScoreButton);
    }

    private void ShowEndlessScore()
    {
        SetSelectedContent(_endlessScoreContent, _totalScoreContent);
        SetSelectedButton(_endlessScoreButton, _totalScoreButton);
    }

    private static void SetSelectedContent(CanvasGroup selectedContent, CanvasGroup hiddenContent)
    {
        SetContentState(selectedContent, true);
        SetContentState(hiddenContent, false);
    }

    private static void SetContentState(CanvasGroup content, bool isVisible)
    {
        content.alpha = isVisible ? 1f : 0f;
        content.interactable = isVisible;
        content.blocksRaycasts = isVisible;
    }

    private static void SetSelectedButton(Button selectedButton, Button availableButton)
    {
        selectedButton.interactable = false;
        availableButton.interactable = true;
    }
}
