using System;

public readonly struct LevelChapterCoverState
{
    public LevelChapterCoverState(
        LevelChapter chapter,
        int chapterNumber,
        bool isUnlocked,
        int unlockAfterLevelNumber,
        int completedLevelCount,
        int stars)
    {
        Chapter = chapter != null ? chapter : throw new ArgumentNullException(nameof(chapter));
        ChapterNumber = chapterNumber;
        IsUnlocked = isUnlocked;
        UnlockAfterLevelNumber = unlockAfterLevelNumber;
        CompletedLevelCount = completedLevelCount;
        Stars = stars;
    }

    public LevelChapter Chapter { get; }
    public int ChapterNumber { get; }
    public bool IsUnlocked { get; }
    public int UnlockAfterLevelNumber { get; }
    public int CompletedLevelCount { get; }
    public int Stars { get; }
    public int LevelCount => Chapter.Levels.Count;
    public int MaximumStars => LevelCount * LevelStarCalculator.MaximumStars;
}
