using System;
using DG.Tweening;
using UnityEngine;

public sealed class ConditionStarView : MonoBehaviour
{
    private const float StartScale = 0.4f;
    private const float PeakScale = 1.1f;
    private const float EndScale = 0.7f;
    private const float PeakShare = 0.12f;
    private const float SpinDegrees = 220f;
    private const float ArcHeight = 0.35f;

    [SerializeField] private SpriteRenderer _star;
    [SerializeField] private TrailRenderer _trail;
    [SerializeField, Min(0.01f)] private float _worldSize = 0.16f;
    [SerializeField, Min(0.01f)] private float _duration = 0.55f;

    private Vector3 _baseScale;
    private Tween _flight;

    private void Awake()
    {
        _baseScale = Vector3.one * (_worldSize / _star.sprite.bounds.size.x);
    }

    public void Fly(Vector3 start, Func<Vector3> target, float lateralOffset, float delay, Camera camera, Action arrived, Action<ConditionStarView> released)
    {
        Transform cameraTransform = camera.transform;
        Quaternion facing = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);
        Vector3 bend = cameraTransform.up * ArcHeight + cameraTransform.right * lateralOffset;

        transform.SetPositionAndRotation(start, facing);
        transform.localScale = _baseScale * StartScale;
        gameObject.SetActive(false);

        float progress = 0f;
        _flight = DOTween.To(() => progress, value =>
            {
                progress = value;
                Vector3 end = target();
                Vector3 control = 2f * ((start + end) * 0.5f + bend) - (start + end) * 0.5f;
                float inverse = 1f - value;
                transform.position = inverse * inverse * start + 2f * inverse * value * control + value * value * end;
                transform.rotation = facing * Quaternion.Euler(0f, 0f, SpinDegrees * value);
                transform.localScale = _baseScale * EvaluateScale(value);
            }, 1f, _duration)
            .SetEase(Ease.InOutSine)
            .SetDelay(delay)
            .OnStart(() =>
            {
                gameObject.SetActive(true);
                _trail.Clear();
            })
            .OnComplete(() =>
            {
                arrived();
                gameObject.SetActive(false);
                released(this);
            })
            .SetLink(gameObject);
    }

    private static float EvaluateScale(float progress)
    {
        if (progress < PeakShare)
            return Mathf.Lerp(StartScale, PeakScale, progress / PeakShare);

        return Mathf.Lerp(PeakScale, EndScale, (progress - PeakShare) / (1f - PeakShare));
    }
}
