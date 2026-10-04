using System;
using YG;

public static class GameProgressSaveConflictResolver
{
    public static SavesYG Resolve(SavesYG cloudSave, SavesYG localSave)
    {
        if (cloudSave == null)
            throw new ArgumentNullException(nameof(cloudSave));

        if (localSave == null)
            throw new ArgumentNullException(nameof(localSave));

        SavesYG newerSave = cloudSave.idSave >= localSave.idSave ? cloudSave : localSave;
        SavesYG olderSave = newerSave == cloudSave ? localSave : cloudSave;
        newerSave.GameProgress = GameProgressMerger.Merge(newerSave.GameProgress, olderSave.GameProgress);
        return newerSave;
    }
}
