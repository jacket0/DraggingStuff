using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;

public sealed class LevelChapterCoverView : MonoBehaviour
{
    [SerializeField] private TMP_Text _chapterLabelText;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Image _icon;
    [SerializeField] private RectTransform _iconBadge;
    [SerializeField] private CanvasGroup _content;
    [SerializeField] private GameObject _openFrame;
    [SerializeField] private GameObject _lockedFrame;
    [SerializeField] private GameObject _progressRoot;
    [SerializeField] private Image _progressFill;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private TMP_Text _starsText;
    [SerializeField] private GameObject _lockedRoot;
    [SerializeField] private TMP_Text _unlockText;
    [SerializeField] private Color _openIconColor = new Color32(0xF4, 0xA7, 0x2C, 0xFF);
    [SerializeField] private Color _lockedIconColor = new Color32(0xB9, 0x80, 0x3F, 0xFF);
    [SerializeField, Range(0f, 1f)] private float _lockedContentAlpha = 0.72f;
    [SerializeField, Min(0f)] private float _bobHeight = 4f;
    [SerializeField, Min(0.01f)] private float _bobHalfPeriod = 1.2f;
    [SerializeField] private LocalizedHintText _chapterLabel = new LocalizedHintText("ГЛАВА {0}", "CHAPTER {0}", "BÖLÜM {0}");
    [SerializeField] private LocalizedHintText _unlockLabel = new LocalizedHintText(
        "Откроется после уровня {0}",
        "Unlocks after level {0}",
        "{0}. seviyeden sonra açılır");

    private LevelChapterCoverState _state;
    private bool _hasState;
    private Vector2 _iconBadgeRestPosition;
    private Tween _bob;

    private void Awake()
    {
        _iconBadgeRestPosition = _iconBadge.anchoredPosition;
    }

    private void OnEnable()
    {
        YG2.onSwitchLang += HandleLanguageSwitched;
        _bob = _iconBadge
            .DOAnchorPosY(_iconBadgeRestPosition.y + _bobHeight, _bobHalfPeriod)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        YG2.onSwitchLang -= HandleLanguageSwitched;
        _bob?.Kill();
        _iconBadge.anchoredPosition = _iconBadgeRestPosition;
    }

    public void Bind(LevelChapterCoverState state)
    {
        if (state.Chapter.MechanicIcon == null)
            throw new InvalidOperationException($"{state.Chapter.name}: mechanic icon is missing.");

        _state = state;
        _hasState = true;
        _icon.sprite = state.Chapter.MechanicIcon;
        _icon.color = state.IsUnlocked ? _openIconColor : _lockedIconColor;
        _content.alpha = state.IsUnlocked ? 1f : _lockedContentAlpha;
        _openFrame.SetActive(state.IsUnlocked);
        _lockedFrame.SetActive(!state.IsUnlocked);
        _progressRoot.SetActive(state.IsUnlocked);
        _lockedRoot.SetActive(!state.IsUnlocked);
        _progressFill.fillAmount = (float)state.CompletedLevelCount / state.LevelCount;
        _progressText.SetText($"{state.CompletedLevelCount}/{state.LevelCount}");
        _starsText.SetText($"{state.Stars}/{state.MaximumStars}");
        ApplyTexts(YG2.lang);
    }

    private void HandleLanguageSwitched(string language)
    {
        if (_hasState)
            ApplyTexts(language);
    }

    private void ApplyTexts(string language)
    {
        _chapterLabelText.SetText(string.Format(_chapterLabel.Get(language), _state.ChapterNumber));
        _titleText.SetText(_state.Chapter.Title.Get(language));
        _descriptionText.SetText(_state.Chapter.Description.Get(language));
        _unlockText.SetText(string.Format(_unlockLabel.Get(language), _state.UnlockAfterLevelNumber));
    }
}
