using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YG;

public sealed class PreModerationTests
{
    private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        SetStaticField(typeof(GameProgressRepository), "_current", null);
        SetStaticField(typeof(GameProgressRepository), "_isSynchronized", false);
        SetStaticField(typeof(GameProgressRepository), "_savePending", false);
        SetStaticField(typeof(GameProgressRepository), "_wasAuthorized", false);

        for (int index = _objects.Count - 1; index >= 0; index--)
        {
            if (_objects[index] != null)
                UnityEngine.Object.DestroyImmediate(_objects[index]);
        }

        _objects.Clear();
    }

    [Test]
    public void BonusRowUsesButtonSizeWithoutShrinkingItsVisuals()
    {
        GameObject canvasObject = new GameObject("BonusCanvas", typeof(RectTransform), typeof(Canvas));
        _objects.Add(canvasObject);
        canvasObject.SetActive(false);
        GameObject rowObject = new GameObject("BonusRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowObject.transform.SetParent(canvasObject.transform, false);
        HorizontalLayoutGroup group = rowObject.GetComponent<HorizontalLayoutGroup>();
        group.spacing = 24f;

        for (int index = 0; index < 2; index++)
        {
            GameObject card = new GameObject("BonusCard", typeof(RectTransform), typeof(LayoutElement));
            card.transform.SetParent(rowObject.transform, false);
            card.GetComponent<LayoutElement>().preferredWidth = 200f;
            card.GetComponent<LayoutElement>().preferredHeight = 200f;
            GameObject button = new GameObject("UseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(card.transform, false);
            ((RectTransform)button.transform).sizeDelta = new Vector2(144f, 144f);
            GameObject decoration = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            decoration.transform.SetParent(button.transform, false);
        }

        BonusButtonEdgeLayout layout = rowObject.AddComponent<BonusButtonEdgeLayout>();
        typeof(BonusButtonEdgeLayout).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(layout, null);

        Assert.That(group.enabled, Is.True);
        Assert.That(group.childAlignment, Is.EqualTo(TextAnchor.LowerLeft));
        Assert.That(group.spacing, Is.EqualTo(24f));

        foreach (RectTransform card in rowObject.transform)
        {
            Assert.That(card.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(144f));
            Assert.That(card.GetComponent<LayoutElement>().preferredHeight, Is.EqualTo(144f));
            Button button = card.GetComponentInChildren<Button>(true);
            Assert.That(((RectTransform)button.transform).rect.size, Is.EqualTo(new Vector2(144f, 144f)));
            Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(button.transform.GetChild(0).GetComponent<Image>().raycastTarget, Is.False);
        }
    }

    [Test]
    public void InterstitialCompletionCallbackRunsOnlyOnce()
    {
        GameObject serviceObject = new GameObject("InterstitialService");
        serviceObject.SetActive(false);
        _objects.Add(serviceObject);
        YandexInterstitialAdService service = serviceObject.AddComponent<YandexInterstitialAdService>();
        int completionCount = 0;
        SetField(service, "_completed", new Action(() => completionCount++));
        MethodInfo complete = typeof(YandexInterstitialAdService).GetMethod("HandleCompleted", BindingFlags.Instance | BindingFlags.NonPublic);

        complete.Invoke(service, null);
        complete.Invoke(service, null);

        Assert.That(completionCount, Is.EqualTo(1));
    }

    [Test]
    public void TotalScoreIncludesSixLevelsAndEndlessRecord()
    {
        LevelCatalog catalog = CreateCatalog(6);
        GameProgressData progress = new GameProgressData
        {
            EndlessBestScore = 700
        };

        for (int levelNumber = 1; levelNumber <= 6; levelNumber++)
        {
            progress.Levels.Add(new LevelProgressData
            {
                LevelNumber = levelNumber,
                BestScore = levelNumber * 100,
                IsCompleted = true
            });
        }

        TotalScoreService totalScore = CreateTotalScoreService(catalog, progress);

        Assert.That(totalScore.Value, Is.EqualTo(2800));
    }

    [Test]
    public void TotalScoreThrowsOnOverflow()
    {
        LevelCatalog catalog = CreateCatalog(6);
        GameProgressData progress = new GameProgressData();
        progress.Levels.Add(new LevelProgressData { LevelNumber = 1, BestScore = long.MaxValue, IsCompleted = true });
        progress.Levels.Add(new LevelProgressData { LevelNumber = 2, BestScore = 1, IsCompleted = true });
        TotalScoreService totalScore = CreateTotalScoreService(catalog, progress);

        Assert.Throws<OverflowException>(() => _ = totalScore.Value);
    }

    [Test]
    public void LevelCatalogReturnsLevelsInConfiguredOrder()
    {
        LevelCatalog catalog = CreateCatalog(6);

        for (int index = 0; index < catalog.Levels.Count - 1; index++)
        {
            bool hasNext = catalog.TryGetNext(catalog.Levels[index], out LevelEntry nextLevel);

            Assert.That(hasNext, Is.True);
            Assert.That(nextLevel, Is.SameAs(catalog.Levels[index + 1]));
        }

        Assert.That(catalog.TryGetNext(catalog.Levels.Last(), out LevelEntry missingLevel), Is.False);
        Assert.That(missingLevel, Is.Null);
    }

    [Test]
    public void SaveConflictResolverKeepsBestProgressFromLocalAndCloud()
    {
        SavesYG cloudSave = new SavesYG
        {
            idSave = 3,
            GameProgress = new GameProgressData
            {
                EndlessBestScore = 200,
                Levels = new List<LevelProgressData>
                {
                    new LevelProgressData { LevelNumber = 1, BestScore = 100, IsCompleted = true }
                },
                Bonuses = new List<BonusAmountData>
                {
                    new BonusAmountData { Id = "hint", Amount = 1 }
                }
            }
        };
        SavesYG localSave = new SavesYG
        {
            idSave = 5,
            GameProgress = new GameProgressData
            {
                EndlessBestScore = 300,
                Levels = new List<LevelProgressData>
                {
                    new LevelProgressData { LevelNumber = 1, BestScore = 80, IsCompleted = false },
                    new LevelProgressData { LevelNumber = 2, BestScore = 250, IsCompleted = true }
                },
                Bonuses = new List<BonusAmountData>
                {
                    new BonusAmountData { Id = "hint", Amount = 3 }
                }
            }
        };

        SavesYG resolvedSave = GameProgressSaveConflictResolver.Resolve(cloudSave, localSave);

        Assert.That(resolvedSave.idSave, Is.EqualTo(5));
        Assert.That(resolvedSave.GameProgress.EndlessBestScore, Is.EqualTo(300));
        Assert.That(resolvedSave.GameProgress.Levels.Single(level => level.LevelNumber == 1).BestScore, Is.EqualTo(100));
        Assert.That(resolvedSave.GameProgress.Levels.Single(level => level.LevelNumber == 1).IsCompleted, Is.True);
        Assert.That(resolvedSave.GameProgress.Levels.Single(level => level.LevelNumber == 2).BestScore, Is.EqualTo(250));
        Assert.That(resolvedSave.GameProgress.Bonuses.Single(bonus => bonus.Id == "hint").Amount, Is.EqualTo(3));
    }

    [Test]
    public void ItemTargetUsesExpandedBoundsAndRejectsDistantPointer()
    {
        Camera camera = CreateCamera();
        Shelf shelf = CreateShelf(Vector3.zero, ItemType.Ball);
        ShelfBoard board = CreateBoard(shelf);
        ShelfItemTargetResolver resolver = new ShelfItemTargetResolver(camera, board, 30f);
        Vector2 edge = camera.WorldToScreenPoint(new Vector3(0.5f, 0f, 0f));

        Assert.That(resolver.TryResolve(edge + Vector2.right * 20f, out ShelfItem item), Is.True);
        Assert.That(item, Is.SameAs(shelf.ActiveLayer.Slots[0].Item));
        Assert.That(resolver.TryResolve(edge + Vector2.right * 40f, out _), Is.False);
    }

    [Test]
    public void ItemTargetChoosesFrontItemWhenScreenBoundsOverlap()
    {
        Camera camera = CreateCamera();
        Shelf front = CreateShelf(new Vector3(0.35f, 0f, -1f), ItemType.Ball);
        Shelf back = CreateShelf(Vector3.zero, ItemType.Bear);
        ShelfItemTargetResolver resolver = new ShelfItemTargetResolver(camera, CreateBoard(back, front), 30f);

        Assert.That(resolver.TryResolve(camera.WorldToScreenPoint(Vector3.zero), out ShelfItem item), Is.True);
        Assert.That(item, Is.SameAs(front.ActiveLayer.Slots[0].Item));
    }

    [Test]
    public void ItemTargetChoosesNearestBoundsOutsideBothItems()
    {
        Camera camera = CreateCamera();
        Shelf left = CreateShelf(new Vector3(-0.8f, 0f, 0f), ItemType.Ball);
        Shelf right = CreateShelf(new Vector3(0.8f, 0f, 0f), ItemType.Bear);
        ShelfItemTargetResolver resolver = new ShelfItemTargetResolver(camera, CreateBoard(left, right), 50f);

        Assert.That(resolver.TryResolve(camera.WorldToScreenPoint(new Vector3(0.1f, 0f, 0f)), out ShelfItem item), Is.True);
        Assert.That(item, Is.SameAs(right.ActiveLayer.Slots[0].Item));
    }

    [Test]
    public void ItemTargetIgnoresPreviewLayers()
    {
        Camera camera = CreateCamera();
        Shelf shelf = CreateShelf(Vector3.zero, (ItemType?)null);
        Shelf preview = CreateShelf(Vector3.zero, ItemType.Bear);
        ShelfLayer previewLayer = preview.ActiveLayer;
        previewLayer.transform.SetParent(shelf.transform, true);
        SetField(shelf, "_shelfLayers", new List<ShelfLayer> { shelf.ActiveLayer, previewLayer });
        ShelfItemTargetResolver resolver = new ShelfItemTargetResolver(camera, CreateBoard(shelf), 30f);

        Assert.That(resolver.TryResolve(camera.WorldToScreenPoint(Vector3.zero), out _), Is.False);
    }

    [Test]
    public void DropTargetChoosesNearestEmptySlotOnSelectedShelf()
    {
        Camera camera = CreateCamera();
        Shelf source = CreateShelf(new Vector3(0f, -3f, 0f), ItemType.Ball);
        Shelf target = CreateShelf(Vector3.zero, null, ItemType.Bear, null);
        ShelfDropTargetResolver resolver = new ShelfDropTargetResolver(camera, CreateBoard(source, target), 0, 64f);

        Assert.That(resolver.TryResolve(source.ActiveLayer.Slots[0], camera.WorldToScreenPoint(new Vector3(0.8f, 0f, 0f)), out ShelfSlot slot), Is.True);
        Assert.That(slot, Is.SameAs(target.ActiveLayer.Slots[2]));
    }

    [Test]
    public void DropTargetDoesNotFallThroughOccupiedFrontShelf()
    {
        Camera camera = CreateCamera();
        Shelf source = CreateShelf(new Vector3(0f, -3f, 0f), ItemType.Ball);
        Shelf front = CreateShelf(new Vector3(0f, 0f, -1f), ItemType.Bear);
        Shelf back = CreateShelf(Vector3.zero, (ItemType?)null);
        ShelfDropTargetResolver resolver = new ShelfDropTargetResolver(camera, CreateBoard(source, back, front), 0, 64f);

        Assert.That(resolver.TryResolve(source.ActiveLayer.Slots[0], camera.WorldToScreenPoint(Vector3.zero), out _), Is.False);
    }

    [Test]
    public void DropTargetRejectsMoveThatConsumesLastEmptySlotAfterReveal()
    {
        Camera camera = CreateCamera();
        Shelf source = CreateShelf(new Vector3(0f, -3f, 0f), ItemType.Ball);
        Shelf preview = CreateShelf(new Vector3(0f, -3f, 1f), ItemType.Bear);
        ShelfLayer previewLayer = preview.ActiveLayer;
        previewLayer.transform.SetParent(source.transform, true);
        SetField(source, "_shelfLayers", new List<ShelfLayer> { source.ActiveLayer, previewLayer });
        Shelf target = CreateShelf(Vector3.zero, (ItemType?)null);
        ShelfDropTargetResolver resolver = new ShelfDropTargetResolver(camera, CreateBoard(source, target), 0, 64f);

        Assert.That(resolver.TryResolve(source.ActiveLayer.Slots[0], camera.WorldToScreenPoint(Vector3.zero), out _), Is.False);
    }

    private Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("TargetCamera");
        _objects.Add(cameraObject);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
        return camera;
    }

    private Shelf CreateShelf(Vector3 position, params ItemType?[] items)
    {
        GameObject shelfObject = new GameObject("TargetShelf");
        _objects.Add(shelfObject);
        shelfObject.transform.position = position;
        Shelf shelf = shelfObject.AddComponent<Shelf>();
        GameObject layerObject = new GameObject("ActiveLayer");
        layerObject.transform.SetParent(shelf.transform, false);
        ShelfLayer layer = layerObject.AddComponent<ShelfLayer>();
        List<ShelfSlot> slots = new List<ShelfSlot>();

        for (int index = 0; index < items.Length; index++)
        {
            GameObject slotObject = new GameObject("TargetSlot");
            slotObject.transform.SetParent(layer.transform, false);
            slotObject.transform.localPosition = new Vector3(index - (items.Length - 1) * 0.5f, 0f, 0f);
            ShelfSlot slot = slotObject.AddComponent<ShelfSlot>();
            Transform anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(slot.transform, false);
            SetField(slot, "_itemAnchor", anchor);

            if (items[index].HasValue)
            {
                GameObject itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ShelfItem item = itemObject.AddComponent<ShelfItem>();
                itemObject.AddComponent<ShelfItemTargetView>();
                SetField(item, "_type", items[index].Value);
                slot.PlaceItem(item);
            }

            slots.Add(slot);
        }

        SetField(layer, "_slots", slots);
        SetField(shelf, "_capacity", items.Length);
        SetField(shelf, "_shelfLayers", new List<ShelfLayer> { layer });
        shelf.ValidateLayers();
        return shelf;
    }

    private ShelfBoard CreateBoard(params Shelf[] shelves)
    {
        GameObject boardObject = new GameObject("TargetBoard");
        _objects.Add(boardObject);
        ShelfBoard board = boardObject.AddComponent<ShelfBoard>();
        SetField(board, "_shelves", shelves.ToList());
        return board;
    }

    private TotalScoreService CreateTotalScoreService(LevelCatalog catalog, GameProgressData progress)
    {
        SetStaticField(typeof(GameProgressRepository), "_current", GameProgressMerger.Clone(progress));
        SetStaticField(typeof(GameProgressRepository), "_isSynchronized", true);
        GameObject servicesObject = new GameObject("ScoreServices");
        servicesObject.SetActive(false);
        _objects.Add(servicesObject);

        LevelProgressService levelProgress = servicesObject.AddComponent<LevelProgressService>();
        SetField(levelProgress, "_catalog", catalog);
        EndlessProgressService endlessProgress = servicesObject.AddComponent<EndlessProgressService>();
        TotalScoreService totalScore = servicesObject.AddComponent<TotalScoreService>();
        SetField(totalScore, "_catalog", catalog);
        SetField(totalScore, "_progress", levelProgress);
        SetField(totalScore, "_endlessProgress", endlessProgress);
        return totalScore;
    }

    private LevelCatalog CreateCatalog(int levelCount)
    {
        LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
        _objects.Add(catalog);
        List<LevelEntry> levels = new List<LevelEntry>(levelCount);

        for (int levelNumber = 1; levelNumber <= levelCount; levelNumber++)
        {
            LevelEntry level = ScriptableObject.CreateInstance<LevelEntry>();
            _objects.Add(level);
            SetField(level, "_number", levelNumber);
            levels.Add(level);
        }

        SetField(catalog, "_levels", levels);
        return catalog;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);

        field.SetValue(target, value);
    }

    private static void SetStaticField(Type type, string fieldName, object value)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);

        if (field == null)
            throw new MissingFieldException(type.Name, fieldName);

        field.SetValue(null, value);
    }
}
