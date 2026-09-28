using DG.Tweening;
using UnityEngine;

public sealed class TargetShelfMarkerView : MonoBehaviour
{
    private const float WallClearance = 0.025f;
    private const float FrontClearance = 0.012f;
    private const float FrameInset = 0.97f;
    private const float FillAlpha = 0.3f;
    private const float FillOverscan = 1.08f;
    private const float BackFrameAlpha = 0.45f;
    private const float PulseMinimumAlpha = 0.55f;
    private const float PulseDuration = 0.9f;
    private const float BounceScale = 1.05f;
    private const float BounceDuration = 0.2f;
    private const float HideDuration = 0.3f;

    [SerializeField] private SpriteRenderer _frontFrame;
    [SerializeField] private SpriteRenderer _backFrame;
    [SerializeField] private SpriteRenderer _fill;

    private Tween _pulse;
    private Tween _bounce;
    private bool _isHidden;

    public void Show(ClosedShelfCoverAnchor anchor, Color color)
    {
        Vector3 back = anchor.BackCenter - anchor.DepthAxis * WallClearance;
        Vector3 front = anchor.FrontCenter - anchor.DepthAxis * FrontClearance;
        transform.SetPositionAndRotation((back + front) * 0.5f, anchor.DepthRotation);

        Vector2 size = anchor.Size * FrameInset;
        PlaceFrame(_frontFrame, front, size, color, 1f);
        PlaceFrame(_backFrame, back, size, color, BackFrameAlpha);

        Vector3 fillSprite = _fill.sprite.bounds.size;
        _fill.transform.position = back + anchor.DepthAxis * 0.002f;
        _fill.transform.localRotation = Quaternion.identity;
        Vector2 fillSize = anchor.Size * FillOverscan;
        _fill.transform.localScale = new Vector3(fillSize.x / fillSprite.x, fillSize.y / fillSprite.y, 1f);
        _fill.color = new Color(color.r, color.g, color.b, FillAlpha);

        _pulse = _frontFrame.DOFade(PulseMinimumAlpha, PulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    public void Bounce()
    {
        if (_isHidden)
            return;

        _bounce?.Complete();
        transform.localScale = Vector3.one;
        _bounce = DOTween.Sequence()
            .Append(transform.DOScale(BounceScale, BounceDuration * 0.4f).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(1f, BounceDuration * 0.6f).SetEase(Ease.InOutQuad))
            .SetLink(gameObject);
    }

    public void Hide()
    {
        if (_isHidden)
            return;

        _isHidden = true;
        _pulse?.Kill();
        _bounce?.Complete();
        DOTween.Sequence()
            .Join(_frontFrame.DOFade(0f, HideDuration))
            .Join(_backFrame.DOFade(0f, HideDuration))
            .Join(_fill.DOFade(0f, HideDuration))
            .OnComplete(() => Destroy(gameObject))
            .SetLink(gameObject);
    }

    private static void PlaceFrame(SpriteRenderer frame, Vector3 position, Vector2 size, Color color, float alpha)
    {
        frame.transform.position = position;
        frame.transform.localRotation = Quaternion.identity;
        frame.drawMode = SpriteDrawMode.Sliced;
        frame.size = size;
        frame.color = new Color(color.r, color.g, color.b, alpha);
    }
}
