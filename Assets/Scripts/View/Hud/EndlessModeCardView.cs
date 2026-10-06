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
    [SerializeField] private GameObject _playRoot;
    [SerializeField] private GameObject _lockedHintRoot;

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
        _playRoot.SetActive(unlocked);
        _lockIcon.SetActive(!unlocked);
        _lockedHintRoot.SetActive(!unlocked);
    }

    private void HandleClicked()
    {
        if (_button.interactable)
            Clicked?.Invoke();
    }
}
