using DG.Tweening;
using System;
using UnityEngine;

public class MoveResolutionPlayer : MonoBehaviour
{
    [SerializeField] private MatchEffectView _matchEffectPrefab;
    [SerializeField] private Transform _resolutionRoot;
    [SerializeField] private Camera _camera;
    [SerializeField] private MatchAudioPlayer _matchAudioPlayer;

    [SerializeField, Min(0.0f)] private float _cameraOffset = 0.04f;
    [SerializeField, Min(0.1f)] private float _mergeDuration = 0.3f;
    [SerializeField, Range(0.0f, 1f)] private float _mergedScale = 0.65f;
    [SerializeField] private Ease _mergeEase = Ease.InCubic;

    [SerializeField, Min(0.01f)] private float _preparatoryDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float _preparatoryScaleCoef = 1.08f;
    [SerializeField] private Ease _preparatoryEase = Ease.OutQuad;

    [SerializeField, Min(0.01f)] private float _impactDuration = 0.05f;
    [SerializeField, Range(0.0f, 1f)] private float _impactScale = 0.12f;
    [SerializeField] private Ease _impactEase = Ease.InQuad;

    [SerializeField, Min(0f)] private float _postExplosionDelay = 0.1f;

    private Sequence _resolutionSequence;

    private void OnDisable()
    {
        _resolutionSequence?.Kill();
        _resolutionSequence = null;
    }

    public void Play(MatchResolution match, Action completed)
    {
        if (match == null)
        {
            completed?.Invoke();
            return;
        }

        ShelfItem centerItem = match.Items[ShelfLayer.SlotCount / 2];
        Vector3 mergePosition = centerItem.transform.position;
        Vector3 effectPosition = GetEffectPosition(centerItem);

        _resolutionSequence?.Kill();
        _resolutionSequence = DOTween.Sequence();

        foreach (var item in match.Items)
        {
            Sequence sequence = CreateItemAnimation(item, centerItem, mergePosition);
            _resolutionSequence.Insert(0f, sequence);
        }

        _resolutionSequence.InsertCallback(_preparatoryDuration, _matchAudioPlayer.PlayMerge);
        _resolutionSequence.AppendCallback(() => PlayExplosion(match, effectPosition));
        _resolutionSequence.AppendInterval(_postExplosionDelay);

        _resolutionSequence.OnComplete(() =>
        {
            _resolutionSequence = null;
            completed?.Invoke();
        });
    }

    private Sequence CreateItemAnimation(ShelfItem item, ShelfItem centerItem, Vector3 mergePosition)
    {
        Transform itemTransform = item.transform;
        itemTransform.SetParent(_resolutionRoot, true);
        Vector3 initialScale = itemTransform.localScale;

        Sequence itemSequence = DOTween.Sequence();

        itemSequence.Append(itemTransform.DOScale(initialScale * _preparatoryScaleCoef, _preparatoryDuration).SetEase(_preparatoryEase));
        itemSequence.Append(itemTransform.DOScale(initialScale * _mergedScale, _mergeDuration).SetEase(_mergeEase));

        if (item != centerItem)
            itemSequence.Join(itemTransform.DOMove(mergePosition, _mergeDuration).SetEase(_mergeEase));

        itemSequence.Append(itemTransform.DOScale(initialScale * _impactScale, _impactDuration).SetEase(_impactEase));
        return itemSequence;
    }

    private Vector3 GetEffectPosition(ShelfItem item)
    {
        Renderer renderer = item.GetComponentInChildren<Renderer>();

        Vector3 center = renderer != null ? renderer.bounds.center : item.transform.position;
        return center - _camera.transform.forward * _cameraOffset;
    }

    private void PlayExplosion(MatchResolution match, Vector3 effectPosition)
    {
        _matchAudioPlayer.PlayExplosion();

        foreach (var item in match.Items)
            item?.Delete();

        MatchEffectView effect = Instantiate(_matchEffectPrefab, effectPosition, Quaternion.identity, _resolutionRoot);

        effect.Play(effectPosition);
    }
}
