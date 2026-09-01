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

        SavesYG resolvedSave = cloudSave.idSave >= localSave.idSave ? cloudSave : localSave;
        resolvedSave.idSave = Math.Max(cloudSave.idSave, localSave.idSave);
        resolvedSave.GameProgress = GameProgressMerger.Merge(cloudSave.GameProgress, localSave.GameProgress);
        return resolvedSave;
    }
}
