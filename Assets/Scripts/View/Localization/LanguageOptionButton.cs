using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class LanguageOptionButton : MonoBehaviour
{
    [SerializeField] private GameLanguage _language;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _selectionHighlight;

    public event Action<GameLanguage> Clicked;

    public GameLanguage Language => _language;

    private void Awake()
    {
        if (_button == null)
            throw new InvalidOperationException(nameof(_button));

        if (_selectionHighlight == null)
            throw new InvalidOperationException(nameof(_selectionHighlight));
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(HandleClick);
    }

    public void SetSelected(bool selected)
    {
        _selectionHighlight.SetActive(selected);
    }

    private void HandleClick()
    {
        Clicked?.Invoke(_language);
    }
}
