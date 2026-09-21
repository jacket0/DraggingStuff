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
    [SerializeField] private GameObject _timeRoot;
    [SerializeField] private TMP_Text _bestTimeText;
    [SerializeField] private StarRatingView _starRating;
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

    public void Bind(LevelEntry level, bool unlocked, long bestScore, long bestTimeMilliseconds, int stars)
    {
        _level = level ?? throw new ArgumentNullException(nameof(level));

        bool canBeStarted = unlocked && level.CanBeStarted;

        _levelNumberText.SetText(level.Number.ToString());
        _recordValueText.SetText(bestScore.ToString());

        bool hasCompletion = canBeStarted && bestTimeMilliseconds > 0;

        if (_timeRoot != null)
            _timeRoot.SetActive(hasCompletion);

        if (_bestTimeText != null && hasCompletion)
            _bestTimeText.SetText(TimeTextFormatter.FormatMilliseconds(bestTimeMilliseconds));

        if (_starRating != null)
        {
            _starRating.gameObject.SetActive(hasCompletion);

            if (hasCompletion)
                _starRating.SetRating(stars);
        }

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
