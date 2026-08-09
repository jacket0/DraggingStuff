using DG.Tweening;
using UnityEngine;

public class ShelfItemView : MonoBehaviour
{
    [SerializeField] private Ease _moveEase = Ease.OutCubic;
    [SerializeField] private float _pickupScaleCoef = 1.08f;
    [SerializeField, Min(0.01f)] private float _pickupDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float _placementDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float _returnDuration = 0.1f;
    [SerializeField] private Transform _visualRoot;

    private Vector3 _initialLocalScale;
    private Quaternion _originRotation = Quaternion.identity;

    private Tween _currentTween;

    private void Awake()
    {
        _initialLocalScale = _visualRoot.localScale;
    }

    public void PlayPickup()
    {
        Vector3 pickupScale = _initialLocalScale * _pickupScaleCoef;

        _currentTween?.Kill();
        _currentTween = _visualRoot.DOScale(pickupScale, _pickupDuration).SetEase(_moveEase);
    }

    public void PlayPlacement()
    {
        _currentTween?.Kill();
        _currentTween = _visualRoot.DOScale(_initialLocalScale, _placementDuration).SetEase(_moveEase);
    }

    public void PlayReturn()
    {
        _currentTween?.Kill();
        _currentTween = _visualRoot.DOLocalMove(_initialLocalScale, _returnDuration).SetEase(_moveEase);
    }

    public void ResetView()
    {

    }
}
