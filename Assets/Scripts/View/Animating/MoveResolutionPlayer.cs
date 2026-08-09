using DG.Tweening;
using System;
using UnityEngine;

public class MoveResolutionPlayer : MonoBehaviour
{
    [SerializeField] private MatchEffectView _matchEffectPrefab;
    [SerializeField] private Transform _resolutionRoot;
    [SerializeField] private Camera _camera;

    [SerializeField, Min(0.0f)] private float _cameraOffset = 0.04f;
    [SerializeField, Min(0.1f)] private float _mergeDuration = 0.3f;
    [SerializeField, Range(0.0f, 1f)] private float _mergedScale = 0.65f;
    [SerializeField] private Ease _mergeEase = Ease.InCubic;

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
            item.transform.SetParent(_resolutionRoot, true);

            _resolutionSequence.Insert(0f, item.transform.DOMove(mergePosition, _mergeDuration).SetEase(_mergeEase));
            _resolutionSequence.Insert(0f, item.transform.DOScale(item.transform.localScale * _mergedScale, _mergeDuration).SetEase(_mergeEase));
        }

        _resolutionSequence.OnComplete(() => PlayExplosion(match, effectPosition, completed));
    }

    private Vector3 GetEffectPosition(ShelfItem item)
    {
        Renderer renderer = item.GetComponentInChildren<Renderer>();

        Vector3 center = renderer != null ? renderer.bounds.center : item.transform.position;
        return center - _camera.transform.forward * _cameraOffset;
    }

    private void PlayExplosion(MatchResolution match, Vector3 effectPosition, Action completed)
    {
        foreach (var item in match.Items)
            item?.Delete();

        MatchEffectView effect = Instantiate(_matchEffectPrefab, effectPosition, Quaternion.identity, _resolutionRoot);

        effect.Play(effectPosition, () =>
        {
            Destroy(effect.gameObject);
            _resolutionSequence = null;
            completed?.Invoke();
        });
    }
}
