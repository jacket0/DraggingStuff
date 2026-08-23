using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelCardView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _levelNumberText;
    [SerializeField] private GameObject _recordRoot;
    [SerializeField] private TMP_Text _recordValueText;
    [SerializeField] private GameObject _lockIcon;

    private LevelEntry _level;

    public event Action<LevelEntry> Clicked;

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        _button?.onClick.RemoveListener(HandleClick);
    }

    public void Bind(LevelEntry level, bool unlocked, long bestScore)
    {
        _level = level ?? throw new ArgumentNullException(nameof(level));

        bool canBeStarted = unlocked && level.CanBeStarted;

        _levelNumberText.SetText(level.Number.ToString());
        _recordValueText.SetText(bestScore.ToString());

        _button.interactable = canBeStarted;
        _recordRoot.SetActive(canBeStarted);
        _lockIcon.SetActive(!canBeStarted);
    }

    private void HandleClick()
    {
        if (_level == null || !_level.CanBeStarted)
            return;

        Clicked?.Invoke(_level);
    }
}
