using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public sealed class StarRatingView : MonoBehaviour
{
    [SerializeField] private List<Image> _stars = new List<Image>(3);
    [SerializeField] private Sprite _filledSprite;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Image _shineImage;
    [SerializeField, Min(0.01f)] private float _revealDuration = 0.3f;
    [SerializeField] private List<AudioClip> _revealClips = new List<AudioClip>(3);
    [SerializeField] private AudioMixerGroup _outputAudioMixerGroup;
    [SerializeField, Range(0f, 1f)] private float _revealVolume = 0.6f;

    private Sequence _revealSequence;

    public event Action<int> StarRevealed;

    public void SetRating(int rating)
    {
        if (rating < 0 || rating > 3)
            throw new ArgumentOutOfRangeException(nameof(rating));

        if (_stars == null || _stars.Count != 3 || _stars.Exists(star => star == null))
            throw new InvalidOperationException(nameof(_stars));

        if (_filledSprite == null)
            throw new InvalidOperationException(nameof(_filledSprite));

        if (_emptySprite == null)
            throw new InvalidOperationException(nameof(_emptySprite));

        for (int i = 0; i < _stars.Count; i++)
            _stars[i].sprite = i < rating ? _filledSprite : _emptySprite;
    }

    public void RevealRating(int rating, float delay)
    {
        if (delay < 0f)
            throw new ArgumentOutOfRangeException(nameof(delay));

        SetRating(rating);
        _revealSequence?.Kill();

        Sequence sequence = DOTween.Sequence().AppendInterval(delay);

        for (int i = 0; i < _stars.Count; i++)
        {
            RectTransform star = _stars[i].rectTransform;
            star.localScale = i < rating ? Vector3.zero : Vector3.one;

            if (i >= rating)
                continue;

            int starIndex = i;
            sequence.AppendCallback(() => HandleStarRevealStarted(starIndex));
            sequence.Append(star.DOScale(Vector3.one, _revealDuration).SetEase(Ease.OutBack));
        }

        if (_shineImage != null && rating > 0)
        {
            _shineImage.gameObject.SetActive(true);
            _shineImage.color = new Color(1f, 1f, 1f, 0f);
            _shineImage.rectTransform.localRotation = Quaternion.identity;
            sequence.Join(_shineImage.DOFade(1f, _revealDuration));
            sequence.Join(_shineImage.rectTransform.DORotate(new Vector3(0f, 0f, 90f), _revealDuration));
            sequence.Append(_shineImage.DOFade(0f, _revealDuration));
            sequence.OnComplete(() => _shineImage.gameObject.SetActive(false));
        }

        _revealSequence = sequence;
        sequence.SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void OnDisable()
    {
        _revealSequence?.Kill();
        _revealSequence = null;
    }

    private void HandleStarRevealStarted(int starIndex)
    {
        if (starIndex < _revealClips.Count)
            UiAudioPlayer.Play(_revealClips[starIndex], _outputAudioMixerGroup, _revealVolume);

        StarRevealed?.Invoke(starIndex);
    }
}
