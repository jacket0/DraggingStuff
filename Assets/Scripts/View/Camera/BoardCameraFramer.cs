using System.Collections.Generic;
using UnityEngine;

// Widens the camera's field of view just enough for every shelf to stay in frame and clear of the HUD at the current
// window aspect. The authored field of view is a lower bound, so wherever the authored view already fits it is kept.
// The game is landscape only: outside the supported aspect range the view keeps the nearest supported aspect and the rest
// of the window is filled with black bars, so the camera never zooms out far enough to show the edges of the set.
public sealed class BoardCameraFramer : MonoBehaviour
{
    private const int SearchIterations = 24;
    private const float MaximumTanHalf = 10f;
    private const float DefaultCanvasHeight = 1080f;
    private const float EdgeMargin = 4f;

    [SerializeField] private Camera _camera;
    [SerializeField] private ShelfBoard _board;
    [SerializeField] private GameSession _session;
    [SerializeField] private List<RectTransform> _hudObstacles = new List<RectTransform>();
    [SerializeField, Min(0f)] private float _hudMargin = 4f;
    [SerializeField, Min(0.1f)] private float _minimumAspect = 4f / 3f;
    [SerializeField, Min(0.1f)] private float _maximumAspect = 2f;

    private float _designFieldOfView;
    private List<Vector3[]> _boxes;
    private List<HudObstacle> _obstacles;
    private float _canvasHeight;
    private int _screenWidth;
    private int _screenHeight;
    private bool _isLayoutSettled;
    private Camera _barsCamera;

    public float DesignFieldOfView => _designFieldOfView;

    private void Awake()
    {
        _designFieldOfView = _camera.fieldOfView;
    }

    // The layout is captured on the first frame and once more when the board has settled, because items are still
    // tweening into place right after the level is built.
    private void LateUpdate()
    {
        bool shouldCapture = _boxes == null || !_isLayoutSettled && _session.IsBoardSettled;

        if (!shouldCapture && Screen.width == _screenWidth && Screen.height == _screenHeight)
            return;

        if (shouldCapture)
        {
            Canvas.ForceUpdateCanvases();
            _isLayoutSettled = CaptureLayout() && _session.IsBoardSettled;
        }

        _screenWidth = Screen.width;
        _screenHeight = Screen.height;
        float screenAspect = (float)_screenWidth / _screenHeight;
        float aspect = ClampAspect(screenAspect);
        _camera.rect = aspect < screenAspect
            ? new Rect((1f - aspect / screenAspect) * 0.5f, 0f, aspect / screenAspect, 1f)
            : new Rect(0f, (1f - screenAspect / aspect) * 0.5f, 1f, screenAspect / aspect);
        SetBarsVisible(!Mathf.Approximately(aspect, screenAspect));
        _camera.fieldOfView = ComputeFieldOfView(_designFieldOfView, aspect);
    }

    public float ClampAspect(float screenAspect) => Mathf.Clamp(screenAspect, _minimumAspect, _maximumAspect);

    // The main camera does not clear outside its viewport, so a camera that renders nothing paints the bars black.
    private void SetBarsVisible(bool isVisible)
    {
        if (_barsCamera == null)
        {
            if (!isVisible)
                return;

            _barsCamera = new GameObject("LetterboxBars").AddComponent<Camera>();
            _barsCamera.transform.SetParent(transform, false);
            _barsCamera.depth = _camera.depth - 1f;
            _barsCamera.clearFlags = CameraClearFlags.SolidColor;
            _barsCamera.backgroundColor = Color.black;
            _barsCamera.cullingMask = 0;
            _barsCamera.useOcclusionCulling = false;
        }

        _barsCamera.enabled = isVisible;
    }

    // Board corners are taken in camera space and HUD rectangles relative to their anchors, so both stay valid for any aspect.
    // Returns whether any items were present to measure.
    public bool CaptureLayout()
    {
        List<ShelfColumnView> columns = new List<ShelfColumnView>();

        foreach (Shelf shelf in _board.Shelves)
        {
            if (shelf == null || !shelf.gameObject.activeInHierarchy)
                continue;

            foreach (ShelfColumnView column in shelf.ColumnViews)
            {
                if (column != null && column.DropCollider != null && column.gameObject.activeInHierarchy)
                    columns.Add(column);
            }
        }

        // A column holding items is framed by those items (front and queued previews); an empty column is grown by the
        // largest overhang seen elsewhere, as if it held an item too.
        Vector3 growBelow = Vector3.zero;
        Vector3 growAbove = Vector3.zero;
        Bounds?[] itemBounds = new Bounds?[columns.Count];
        bool hasItems = false;

        for (int index = 0; index < columns.Count; index++)
        {
            Bounds collider = columns[index].DropCollider.bounds;

            foreach (Renderer renderer in columns[index].ItemAnchor.GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = itemBounds[index] ?? renderer.bounds;
                bounds.Encapsulate(renderer.bounds);
                itemBounds[index] = bounds;
                growBelow = Vector3.Max(growBelow, collider.min - renderer.bounds.min);
                growAbove = Vector3.Max(growAbove, renderer.bounds.max - collider.max);
                hasItems = true;
            }
        }

        _boxes = new List<Vector3[]>();

        for (int index = 0; index < columns.Count; index++)
        {
            Bounds bounds = columns[index].DropCollider.bounds;

            if (itemBounds[index].HasValue)
                bounds = itemBounds[index].Value;
            else
                bounds.SetMinMax(bounds.min - growBelow, bounds.max + growAbove);

            _boxes.Add(CreateCameraSpaceCorners(bounds));
        }

        _obstacles = new List<HudObstacle>();
        _canvasHeight = DefaultCanvasHeight;

        foreach (RectTransform obstacle in _hudObstacles)
        {
            RectTransform canvas = (RectTransform)obstacle.GetComponentInParent<Canvas>(true).rootCanvas.transform;
            RectTransform parent = (RectTransform)obstacle.parent;
            Rect rect = GetRectInCanvas(obstacle, canvas);
            Rect parentRect = GetRectInCanvas(parent, canvas);
            float anchor = Mathf.Approximately(obstacle.anchorMin.x, obstacle.anchorMax.x) ? obstacle.anchorMin.x : 0.5f;
            float anchorX = parentRect.xMin + anchor * parentRect.width;
            _canvasHeight = canvas.rect.height;
            _obstacles.Add(new HudObstacle(anchor, parentRect.width - canvas.rect.width, rect.xMin - anchorX, rect.xMax - anchorX, rect.yMin, rect.yMax));
        }

        return hasItems;
    }

    public float ComputeFieldOfView(float designFieldOfView, float aspect)
    {
        if (_boxes == null)
            CaptureLayout();

        float designTanHalf = Mathf.Tan(designFieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanHalf = Mathf.Max(designTanHalf, FindRequiredTanHalf(_boxes, aspect));
        return 2f * Mathf.Atan(tanHalf) * Mathf.Rad2Deg;
    }

    // Checks arbitrary world bounds (for example the current items) for real overlap with the screen edges and the HUD,
    // without the safety margins used to choose the field of view.
    public bool FitsBounds(IEnumerable<Bounds> bounds, float fieldOfView, float aspect)
    {
        if (_boxes == null)
            CaptureLayout();

        List<Vector3[]> boxes = new List<Vector3[]>();

        foreach (Bounds item in bounds)
            boxes.Add(CreateCameraSpaceCorners(item));

        return Fits(boxes, Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad), aspect, 0f, 0f);
    }

    private float FindRequiredTanHalf(List<Vector3[]> boxes, float aspect)
    {
        float low = 0f;
        float high = MaximumTanHalf;

        if (!Fits(boxes, high, aspect, EdgeMargin, _hudMargin))
            return high;

        for (int iteration = 0; iteration < SearchIterations; iteration++)
        {
            float middle = (low + high) * 0.5f;

            if (Fits(boxes, middle, aspect, EdgeMargin, _hudMargin))
                high = middle;
            else
                low = middle;
        }

        return high;
    }

    private bool Fits(List<Vector3[]> boxes, float tanHalf, float aspect, float edgeMargin, float hudMargin)
    {
        float halfHeight = _canvasHeight * 0.5f;
        float halfWidth = halfHeight * aspect;

        foreach (Vector3[] corners in boxes)
        {
            Rect rect = Project(corners, tanHalf, aspect, halfWidth, halfHeight);

            if (rect.xMin < edgeMargin - halfWidth
                || rect.xMax > halfWidth - edgeMargin
                || rect.yMin < edgeMargin - halfHeight
                || rect.yMax > halfHeight - edgeMargin)
            {
                return false;
            }

            foreach (HudObstacle obstacle in _obstacles)
            {
                if (obstacle.GetRect(halfWidth * 2f, hudMargin).Overlaps(rect))
                    return false;
            }
        }

        return true;
    }

    private Vector3[] CreateCameraSpaceCorners(Bounds bounds)
    {
        Vector3[] corners = new Vector3[8];

        for (int index = 0; index < corners.Length; index++)
        {
            Vector3 direction = new Vector3((index & 1) == 0 ? -1f : 1f, (index & 2) == 0 ? -1f : 1f, (index & 4) == 0 ? -1f : 1f);
            corners[index] = _camera.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, direction));
        }

        return corners;
    }

    private static Rect Project(Vector3[] corners, float tanHalf, float aspect, float halfWidth, float halfHeight)
    {
        float xMin = float.MaxValue;
        float xMax = float.MinValue;
        float yMin = float.MaxValue;
        float yMax = float.MinValue;

        foreach (Vector3 corner in corners)
        {
            float depth = Mathf.Max(corner.z, 0.01f);
            float x = corner.x / (depth * tanHalf * aspect) * halfWidth;
            float y = corner.y / (depth * tanHalf) * halfHeight;
            xMin = Mathf.Min(xMin, x);
            xMax = Mathf.Max(xMax, x);
            yMin = Mathf.Min(yMin, y);
            yMax = Mathf.Max(yMax, y);
        }

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static Rect GetRectInCanvas(RectTransform rectTransform, RectTransform canvas)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector3 min = canvas.InverseTransformPoint(corners[0]);
        Vector3 max = canvas.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private readonly struct HudObstacle
    {
        private readonly float _anchor;
        private readonly float _parentWidthDelta;
        private readonly float _xMin;
        private readonly float _xMax;
        private readonly float _yMin;
        private readonly float _yMax;

        public HudObstacle(float anchor, float parentWidthDelta, float xMin, float xMax, float yMin, float yMax)
        {
            _anchor = anchor;
            _parentWidthDelta = parentWidthDelta;
            _xMin = xMin;
            _xMax = xMax;
            _yMin = yMin;
            _yMax = yMax;
        }

        // The canvas height is fixed (CanvasScaler matches height), so only the horizontal anchor point moves with the aspect.
        // Parents are assumed to stretch across the canvas with a constant inset.
        public Rect GetRect(float canvasWidth, float margin)
        {
            float anchorX = (_anchor - 0.5f) * (canvasWidth + _parentWidthDelta);
            return Rect.MinMaxRect(anchorX + _xMin - margin, _yMin - margin, anchorX + _xMax + margin, _yMax + margin);
        }
    }
}
