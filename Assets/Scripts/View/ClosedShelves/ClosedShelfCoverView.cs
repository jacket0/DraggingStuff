using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ClosedShelfCoverView : MonoBehaviour
{
    private const float SizePadding = 1.06f;
    private const float CobwebHeightShare = 0.62f;
    private const float TagDepthOffset = 0.03f;
    private const float SwayAngle = 3f;
    private const float SwayDuration = 2.6f;
    private const float PulseScale = 1.18f;
    private const float PulseDuration = 0.22f;

    private const float FinaleDuration = 0.42f;
    private const float DetachTime = 0.40f;
    private const float TagFallDistance = 0.35f;
    private const float TagFallAngle = 18f;
    private const float TagFallDuration = 0.45f;
    private const float WaveDuration = 0.7f;
    private const float CobwebDuration = 0.4f;
    private const float PuffInterval = 0.12f;
    private const int PuffCount = 4;
    private const float RevealTime = 1.10f;
    private const float FlashDuration = 0.45f;
    private const float FinalFlashDuration = 0.6f;
    private const float PopDuration = 0.32f;
    private const float PopOvershoot = 1.4f;
    private const float PopColumnDelay = 0.07f;
    private const float PreviewPopDelay = 0.1f;

    private static readonly int RevealProperty = Shader.PropertyToID("_Reveal");

    [SerializeField] private MeshRenderer _dust;
    [SerializeField] private SpriteRenderer _cobwebLeft;
    [SerializeField] private SpriteRenderer _cobwebRight;
    [SerializeField] private ParticleSystem _motes;
    [SerializeField] private ParticleSystem _puff;
    [SerializeField] private SpriteRenderer _revealFlash;
    [SerializeField] private Transform _tagPivot;
    [SerializeField] private SpriteRenderer _thread;
    [SerializeField] private Transform _tag;
    [SerializeField] private CanvasGroup _tagGroup;
    [SerializeField] private CanvasGroup _tagGlow;
    [SerializeField] private Transform _slotsRoot;
    [SerializeField] private ConditionSlotView _slotPrefab;
    [SerializeField, Min(0.01f)] private float _threadLength = 0.045f;

    private readonly List<ConditionSlotView> _slots = new List<ConditionSlotView>();
    private MaterialPropertyBlock _propertyBlock;
    private ClosedShelfAudioPlayer _audio;
    private Vector2 _size;
    private Vector3 _tagBaseScale;
    private float _reveal;
    private int _pendingStars;
    private int _arrivedStars;
    private bool _isCompleted;
    private bool _isRevealing;
    private bool _isStopped;
    private Tween _sway;
    private Tween _pulse;
    private Tween _punch;
    private Sequence _revealSequence;
    private Sequence _popSequence;

    public event Action<ClosedShelfCoverView> RevealReady;

    public int ShelfIndex { get; private set; }

    public void Initialize(int shelfIndex, ClosedShelfCoverAnchor anchor, Camera camera, IReadOnlyList<ConditionSlot> slots,
        Func<ConditionSlot, Sprite> iconOf, Func<ConditionSlot, Color?> accentOf, ClosedShelfAudioPlayer audio)
    {
        ShelfIndex = shelfIndex;
        _audio = audio;
        _size = anchor.Size * SizePadding;
        _propertyBlock = new MaterialPropertyBlock();

        transform.SetPositionAndRotation(anchor.Center, anchor.FacingRotation(camera));
        _dust.transform.localScale = new Vector3(_size.x, _size.y, 1f);
        SetReveal(0f);

        PlaceCobweb(_cobwebLeft, -1f);
        PlaceCobweb(_cobwebRight, 1f);
        PlaceFlash();
        PlaceMotes();

        _tagPivot.position = anchor.TagPoint - transform.forward * TagDepthOffset;
        _tagPivot.localRotation = Quaternion.identity;
        _tag.localPosition = Vector3.down * _threadLength;
        _tagBaseScale = _tag.localScale;
        PlaceThread();
        _tagGlow.alpha = 0f;

        foreach (ConditionSlot slot in slots)
        {
            ConditionSlotView slotView = Instantiate(_slotPrefab, _slotsRoot);
            slotView.Setup(iconOf(slot), slot.Current, slot.Required, accentOf(slot));
            _slots.Add(slotView);
        }

        _tagPivot.localRotation = Quaternion.Euler(0f, 0f, -SwayAngle);
        _sway = _tagPivot.DOLocalRotate(new Vector3(0f, 0f, SwayAngle), SwayDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    public Vector3 GetSlotTarget(int slotIndex) => _slots[slotIndex].TargetPosition;

    public Vector3 TagBottom
    {
        get
        {
            Vector3[] corners = new Vector3[4];
            ((RectTransform)_slotsRoot).GetWorldCorners(corners);
            return (corners[0] + corners[3]) * 0.5f;
        }
    }

    public void Hop() => Pulse();

    public void ExpectStars(int count) => _pendingStars += count;

    public void ReceiveStar(int slotIndex, int amount)
    {
        _pendingStars = Mathf.Max(0, _pendingStars - 1);

        if (_isStopped)
            return;

        _slots[slotIndex].Add(amount);
        Pulse();
        _audio.PlayStar(_arrivedStars++);
        TryRaiseRevealReady();
    }

    public void MarkCompleted()
    {
        _isCompleted = true;
        TryRaiseRevealReady();
    }

    public void PlayReveal(float delay, bool isFinal, Func<Shelf> reveal, Action open)
    {
        if (_isRevealing || _isStopped)
            return;

        _isRevealing = true;
        _sway.Kill();

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        _revealSequence = sequence;
        float start = delay;

        sequence.InsertCallback(start, () => _audio.PlayChord(isFinal));
        sequence.Insert(start, CreateFinale());
        sequence.Insert(start, _tagGlow.DOFade(1f, FinaleDuration * 0.6f));

        float detach = start + DetachTime;
        sequence.Insert(detach, _tag.DOLocalMoveY(_tag.localPosition.y - TagFallDistance, TagFallDuration).SetEase(Ease.InQuad));
        sequence.Insert(detach, _tag.DOLocalRotate(new Vector3(0f, 0f, TagFallAngle), TagFallDuration).SetEase(Ease.InQuad));
        sequence.Insert(detach, _tagGroup.DOFade(0f, TagFallDuration).SetEase(Ease.InQuad));
        sequence.Insert(detach, _thread.DOFade(0f, TagFallDuration * 0.4f));

        sequence.Insert(detach, DOTween.To(() => _reveal, SetReveal, 1f, WaveDuration).SetEase(Ease.InOutSine));
        sequence.Insert(detach, HideCobweb(_cobwebLeft));
        sequence.Insert(detach, HideCobweb(_cobwebRight));
        sequence.InsertCallback(detach, () =>
        {
            _motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _audio.PlayDust();
        });

        for (int index = 0; index < PuffCount; index++)
        {
            float x = Mathf.Lerp(-0.375f, 0.375f, index / (float)(PuffCount - 1)) * _size.x;
            sequence.InsertCallback(detach + index * PuffInterval, () => EmitPuff(x));
        }

        float revealAt = start + RevealTime;
        float flashDuration = isFinal ? FinalFlashDuration : FlashDuration;
        sequence.InsertCallback(revealAt, () => PopItems(reveal(), open));
        sequence.Insert(revealAt, _revealFlash.DOFade(1f, flashDuration * 0.5f).SetEase(Ease.OutQuad));
        sequence.Insert(revealAt + flashDuration * 0.5f, _revealFlash.DOFade(0f, flashDuration * 0.5f).SetEase(Ease.InQuad));
    }

    public void StopReveal()
    {
        _isStopped = true;
        _revealSequence?.Kill();
        _popSequence?.Complete(true);
    }

    public void PlayRejected(Vector3 direction)
    {
        _punch?.Complete();
        _punch = transform.DOPunchPosition(direction.normalized * 0.04f, 0.35f, 8, 0.6f).SetLink(gameObject);
        Pulse();
    }

    public bool ContainsScreenPoint(Camera camera, Vector2 screenPoint)
    {
        Ray ray = camera.ScreenPointToRay(screenPoint);
        Plane plane = new Plane(transform.forward, transform.position);

        if (!plane.Raycast(ray, out float distance))
            return false;

        Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
        return Mathf.Abs(local.x) <= _size.x * 0.5f && Mathf.Abs(local.y) <= _size.y * 0.5f;
    }

    private void TryRaiseRevealReady()
    {
        if (_isCompleted && !_isRevealing && !_isStopped && _pendingStars == 0)
            RevealReady?.Invoke(this);
    }

    private void Pulse()
    {
        if (_isRevealing)
            return;

        _pulse?.Kill();
        _tag.localScale = _tagBaseScale;
        _pulse = DOTween.Sequence()
            .Append(_tag.DOScale(_tagBaseScale * PulseScale, PulseDuration * 0.4f).SetEase(Ease.OutQuad))
            .Append(_tag.DOScale(_tagBaseScale, PulseDuration * 0.6f).SetEase(Ease.InOutQuad))
            .SetLink(gameObject);
    }

    private Sequence CreateFinale()
    {
        _pulse?.Kill();
        _tag.localScale = _tagBaseScale;
        float step = FinaleDuration / 4f;

        return DOTween.Sequence()
            .Append(_tag.DOScale(_tagBaseScale * 1.26f, step).SetEase(Ease.OutQuad))
            .Append(_tag.DOScale(_tagBaseScale, step).SetEase(Ease.InOutQuad))
            .Append(_tag.DOScale(_tagBaseScale * 1.2f, step).SetEase(Ease.OutQuad))
            .Append(_tag.DOScale(_tagBaseScale * 1.08f, step).SetEase(Ease.InOutQuad));
    }

    private Tween HideCobweb(SpriteRenderer cobweb)
    {
        Transform cobwebTransform = cobweb.transform;

        return DOTween.Sequence()
            .Join(cobweb.DOFade(0f, CobwebDuration))
            .Join(cobwebTransform.DOScale(cobwebTransform.localScale * 0.6f, CobwebDuration))
            .Join(cobwebTransform.DOLocalMoveY(cobwebTransform.localPosition.y + 0.05f, CobwebDuration))
            .SetEase(Ease.OutQuad);
    }

    private void PopItems(Shelf shelf, Action open)
    {
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        _popSequence = sequence;
        IReadOnlyList<ShelfColumnView> columns = shelf.ColumnViews;

        for (int index = 0; index < columns.Count; index++)
        {
            IReadOnlyList<ShelfItem> items = columns[index].Column.Items;
            float delay = index * PopColumnDelay;

            if (items.Count > 0)
            {
                sequence.Insert(delay, CreatePop(items[0]));
                sequence.InsertCallback(delay, _audio.PlayPop);
            }

            if (items.Count > 1)
                sequence.Insert(delay + PreviewPopDelay, CreatePop(items[1]));
        }

        sequence.OnComplete(() =>
        {
            if (_isStopped)
                return;

            open();
            Destroy(gameObject, FinalFlashDuration);
        });
    }

    private static Tween CreatePop(ShelfItem item)
    {
        Transform itemTransform = item.transform;
        itemTransform.localScale = Vector3.zero;
        return itemTransform.DOScale(Vector3.one, PopDuration).SetEase(Ease.OutBack, PopOvershoot).SetLink(item.gameObject);
    }

    private void SetReveal(float value)
    {
        _reveal = value;
        _dust.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(RevealProperty, value);
        _dust.SetPropertyBlock(_propertyBlock);
    }

    private void PlaceCobweb(SpriteRenderer cobweb, float side)
    {
        float height = _size.y * CobwebHeightShare;
        float spriteHeight = cobweb.sprite.bounds.size.y;
        cobweb.flipX = side > 0f;
        cobweb.transform.localPosition = new Vector3(side * _size.x * 0.5f, _size.y * 0.5f, -0.005f);
        cobweb.transform.localScale = Vector3.one * (height / spriteHeight);
    }

    private void PlaceFlash()
    {
        Vector3 spriteSize = _revealFlash.sprite.bounds.size;
        _revealFlash.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        _revealFlash.transform.localScale = new Vector3(_size.x * 1.3f / spriteSize.x, _size.y * 1.8f / spriteSize.y, 1f);
        Color color = _revealFlash.color;
        color.a = 0f;
        _revealFlash.color = color;
    }

    private void PlaceMotes()
    {
        ParticleSystem.ShapeModule shape = _motes.shape;
        shape.scale = new Vector3(_size.x * 0.9f, _size.y * 0.8f, 0.02f);
        _motes.Play(true);
    }

    private void PlaceThread()
    {
        Vector3 spriteSize = _thread.sprite.bounds.size;
        _thread.transform.localPosition = Vector3.zero;
        _thread.transform.localScale = new Vector3(0.006f / spriteSize.x, _threadLength / spriteSize.y, 1f);
    }

    private void EmitPuff(float x)
    {
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = new Vector3(x, UnityEngine.Random.Range(-0.2f, 0.2f) * _size.y, -0.02f),
            applyShapeToPosition = true
        };
        _puff.Emit(emit, 3);
    }
}
