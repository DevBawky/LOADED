#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public sealed class BulletPoolAutoSyncPostprocessor : AssetPostprocessor
{
    private const string BulletDataRoot = "Assets/Scripts/Bullet/SO/";
    private const string SessionInitializedKey =
        "LOADED.BulletPoolAutoSync.Initialized";

    private static bool syncPending;

    static BulletPoolAutoSyncPostprocessor()
    {
        if (SessionState.GetBool(SessionInitializedKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionInitializedKey, true);
        ScheduleSync();
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool authoredBulletChanged = importedAssets.Concat(movedAssets)
            .Any(IsBulletDataAsset);
        bool authoredBulletRemoved = deletedAssets
            .Concat(movedFromAssetPaths)
            .Any(IsPotentialBulletAssetPath);
        if (authoredBulletChanged || authoredBulletRemoved)
        {
            ScheduleSync();
        }
    }

    private static bool IsBulletDataAsset(string assetPath)
    {
        return IsPotentialBulletAssetPath(assetPath)
               && AssetDatabase.LoadAssetAtPath<BulletData>(assetPath) != null;
    }

    private static bool IsPotentialBulletAssetPath(string assetPath)
    {
        return assetPath.StartsWith(
                   BulletDataRoot, StringComparison.Ordinal)
               && assetPath.EndsWith(
                   ".asset", StringComparison.OrdinalIgnoreCase);
    }

    private static void ScheduleSync()
    {
        if (syncPending)
        {
            return;
        }

        syncPending = true;
        EditorApplication.update += TryRunSync;
    }

    private static void TryRunSync()
    {
        if (EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= TryRunSync;
        syncPending = false;

        if (HasDirtyLoadedScene())
        {
            Debug.LogWarning(
                "Bullet pool auto-sync was skipped because a loaded scene "
                + "has unsaved changes. Save scenes, then run Tools > "
                + "LOADED > Sync All Bullet Pools.");
            return;
        }

        try
        {
            BulletPoolSyncBuilder.SyncAllBulletPools();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static bool HasDirtyLoadedScene()
    {
        return Enumerable.Range(0, SceneManager.sceneCount)
            .Select(SceneManager.GetSceneAt)
            .Any(scene => scene.IsValid() && scene.isLoaded && scene.isDirty);
    }
}
#endif
