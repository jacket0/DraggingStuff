using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG.LanguageLegacy;

public sealed class TutorialView : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TMP_Text _pageNumberText;
    [SerializeField] private List<LanguageYG> _titleLocalizations = new List<LanguageYG>();
    [SerializeField] private List<LanguageYG> _descriptionLocalizations = new List<LanguageYG>();
    [SerializeField] private LanguageYG _nextButtonLocalization;
    [SerializeField] private LanguageYG _finishButtonLocalization;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _closeButton;

    private IReadOnlyList<TutorialPage> _pages;
    private int _pageIndex;
    private bool _isInitialized;

    public event Action Closed;

    public void Initialize()
    {
        if (_isInitialized)
            return;

        ValidateDependencies();

        _previousButton.onClick.AddListener(ShowPreviousPage);
        _nextButton.onClick.AddListener(ShowNextPage);
        _closeButton.onClick.AddListener(Close);

        _isInitialized = true;
    }

    private void OnDestroy()
    {
        if (!_isInitialized)
            return;

        _previousButton.onClick.RemoveListener(ShowPreviousPage);
        _nextButton.onClick.RemoveListener(ShowNextPage);
        _closeButton.onClick.RemoveListener(Close);
    }

    public void Show(IReadOnlyList<TutorialPage> pages)
    {
        if (!_isInitialized)
            throw new InvalidOperationException(nameof(Initialize));

        if (pages == null || pages.Count == 0)
            throw new ArgumentException(nameof(pages));

        if (_titleLocalizations.Count != pages.Count || _descriptionLocalizations.Count != pages.Count)
            throw new InvalidOperationException(nameof(pages));

        _pages = pages;
        _pageIndex = 0;
        gameObject.SetActive(true);
        Render();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void ShowPreviousPage()
    {
        if (_pageIndex <= 0)
            return;

        _pageIndex--;
        Render();
    }

    private void ShowNextPage()
    {
        if (_pageIndex >= _pages.Count - 1)
        {
            Close();
            return;
        }

        _pageIndex++;
        Render();
    }

    private void Close()
    {
        Hide();
        Closed?.Invoke();
    }

    private void Render()
    {
        TutorialPage page = _pages[_pageIndex];

        _image.sprite = page.Image;
        _image.enabled = page.Image != null;
        SelectLocalization(_titleLocalizations, _pageIndex);
        SelectLocalization(_descriptionLocalizations, _pageIndex);
        _pageNumberText.SetText("{0}/{1}", _pageIndex + 1, _pages.Count);
        _previousButton.gameObject.SetActive(_pageIndex > 0);

        bool isLastPage = _pageIndex == _pages.Count - 1;
        SelectLocalization(isLastPage ? _finishButtonLocalization : _nextButtonLocalization,
            _nextButtonLocalization,
            _finishButtonLocalization);
    }

    private static void SelectLocalization(IReadOnlyList<LanguageYG> localizations, int selectedIndex)
    {
        for (int index = 0; index < localizations.Count; index++)
        {
            LanguageYG localization = localizations[index];
            bool isSelected = index == selectedIndex;
            localization.enabled = isSelected;

            if (isSelected && localization.gameObject.activeInHierarchy)
                localization.SwitchLanguage();
        }
    }

    private static void SelectLocalization(LanguageYG selected, LanguageYG first, LanguageYG second)
    {
        first.enabled = first == selected;
        second.enabled = second == selected;

        if (selected.gameObject.activeInHierarchy)
            selected.SwitchLanguage();
    }

    private void ValidateDependencies()
    {
        if (_image == null)
            throw new InvalidOperationException(nameof(_image));

        if (_pageNumberText == null)
            throw new InvalidOperationException(nameof(_pageNumberText));

        if (_titleLocalizations == null || _titleLocalizations.Exists(localization => localization == null))
            throw new InvalidOperationException(nameof(_titleLocalizations));

        if (_descriptionLocalizations == null || _descriptionLocalizations.Exists(localization => localization == null))
            throw new InvalidOperationException(nameof(_descriptionLocalizations));

        if (_nextButtonLocalization == null)
            throw new InvalidOperationException(nameof(_nextButtonLocalization));

        if (_finishButtonLocalization == null)
            throw new InvalidOperationException(nameof(_finishButtonLocalization));

        if (_previousButton == null)
            throw new InvalidOperationException(nameof(_previousButton));

        if (_nextButton == null)
            throw new InvalidOperationException(nameof(_nextButton));

        if (_closeButton == null)
            throw new InvalidOperationException(nameof(_closeButton));
    }
}
