using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EndlessModeCardView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _bestScoreValueText;
    [SerializeField] private GameObject _recordRoot;
    [SerializeField] private GameObject _lockIcon;

    public event Action Clicked;

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClicked);
    }

    private void OnDisable()
    {
        _button?.onClick.RemoveListener(HandleClicked);
    }

    public void Bind(bool unlocked, long bestScore)
    {
        _button.interactable = unlocked;
        _bestScoreValueText.SetText(bestScore.ToString());
        _recordRoot.SetActive(unlocked);
        _lockIcon.SetActive(!unlocked);
    }

    private void HandleClicked()
    {
        if (_button.interactable)
            Clicked?.Invoke();
    }
}
