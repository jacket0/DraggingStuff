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
    [SerializeField] private GameObject _playRoot;
    [SerializeField] private GameObject _currentHighlight;
    [SerializeField] private GameObject _newRibbon;
    [SerializeField] private CanvasGroup _sticker;
    [SerializeField] private Image _stickerIcon;
    [SerializeField] private TMP_Text _stickerCountText;
    [SerializeField] private Image _lockedSilhouette;
    [SerializeField, Range(0f, 1f)] private float _lockedStickerAlpha = 0.55f;

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

    public void Bind(LevelCardState state)
    {
        _level = state.Level;

        bool canBeStarted = state.IsUnlocked && state.Level.CanBeStarted;
        bool isCurrent = canBeStarted && state.IsCurrent;
        bool hasCompletion = canBeStarted && state.BestTimeMilliseconds > 0;
        bool hasMechanic = state.MechanicIcon != null;

        _levelNumberText.SetText(state.Level.Number.ToString());
        _recordValueText.SetText(state.BestScore.ToString());
        _timeRoot.SetActive(hasCompletion);

        if (hasCompletion)
            _bestTimeText.SetText(TimeTextFormatter.FormatMilliseconds(state.BestTimeMilliseconds));

        _starRating.gameObject.SetActive(hasCompletion);

        if (hasCompletion)
            _starRating.SetRating(state.Stars);

        _button.interactable = canBeStarted;
        _recordRoot.SetActive(canBeStarted && !isCurrent);
        _playRoot.SetActive(isCurrent);
        _currentHighlight.SetActive(isCurrent);
        _lockIcon.SetActive(!canBeStarted);
        _newRibbon.SetActive(state.IsNew);
        BindMechanic(state, hasMechanic, canBeStarted);
    }

    private void BindMechanic(LevelCardState state, bool hasMechanic, bool canBeStarted)
    {
        _sticker.gameObject.SetActive(hasMechanic);
        _lockedSilhouette.gameObject.SetActive(hasMechanic && !canBeStarted);

        if (!hasMechanic)
            return;

        _sticker.alpha = canBeStarted ? 1f : _lockedStickerAlpha;
        _stickerIcon.sprite = state.MechanicIcon;
        _stickerCountText.SetText($"×{state.MechanicElementCount}");
        _lockedSilhouette.sprite = state.MechanicIcon;
    }

    private void HandleClick()
    {
        if (_level == null || !_level.CanBeStarted)
            return;

        Clicked?.Invoke(_level);
    }
}
