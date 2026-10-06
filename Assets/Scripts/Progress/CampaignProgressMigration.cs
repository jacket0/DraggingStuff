using System.Collections.Generic;

public static class CampaignProgressMigration
{
    private static readonly Dictionary<int, int> ChapterLevelNumbers = new Dictionary<int, int>
    {
        { 1, 1 },
        { 2, 2 },
        { 3, 3 },
        { 4, 4 },
        { 13, 11 },
        { 14, 12 },
        { 15, 13 },
        { 16, 15 },
        { 17, 16 },
        { 18, 17 },
        { 19, 18 },
        { 20, 20 }
    };

    public static bool TryGetChapterLevelNumber(int timedCampaignLevelNumber, out int chapterLevelNumber) =>
        ChapterLevelNumbers.TryGetValue(timedCampaignLevelNumber, out chapterLevelNumber);
}
