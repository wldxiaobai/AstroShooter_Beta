// 提供集合枚举等类型
using System.Collections.Generic; // IEnumerable、List 等
using UnityEngine; // Resources、Debug 等
using UnityEngine.SceneManagement; // SceneManager、Scene 等

// 面向运行时的数据库访问辅助 API，避免业务层直接操作 ScriptableObject 细节
public static class SceneDatabaseAPI
{
    // 数据库单例缓存（从 Resources 加载）
    private static SceneDatabase _db; // 缓存已加载的 SceneDatabase 实例

    // 对外暴露的数据库访问点（惰性加载）
    public static SceneDatabase DB
    {
        get
        {
            // 若尚未加载，则从 Resources 读取名为 "SceneDatabase" 的资源
            if (_db == null)
            {
                _db = Resources.Load<SceneDatabase>("SceneDatabase"); // Resources/SceneDatabase.asset
                // 若加载失败，打印错误日志，便于开发期排查
                if (_db == null)
                {
                    Debug.LogError("[SceneDatabaseAPI] 未找到 Resources/SceneDatabase.asset");
                }
            }
            // 返回缓存的数据库实例（可能为 null，调用方需判空）
            return _db;
        }
    }

    // 获取某关卡编号对应的所有场景名称（无扩展名）
    public static IEnumerable<string> GetLevelSceneNames(int levelIndex)
    {
        // 若数据库未加载成功则直接结束迭代
        if (DB == null) yield break;
        // 遍历数据库中该关卡的所有条目
        foreach (var e in DB.GetLevel(levelIndex))
            // 仅当缓存路径有效时才返回名称
            if (!string.IsNullOrEmpty(e.cachedPath))
                // 从路径中提取不带扩展名的场景名
                yield return System.IO.Path.GetFileNameWithoutExtension(e.cachedPath);
    }

    // 获取某关卡的“主场景”名称（若不存在则返回 null）
    public static string GetMainLevelSceneName(int levelIndex)
    {
        // 数据库无效直接返回 null
        if (DB == null) return null;
        // 查询主关卡条目（includedInFlow = true 的 Level）
        var entry = DB.GetMainLevel(levelIndex);
        // 若未找到或路径无效，返回 null
        if (entry == null || string.IsNullOrEmpty(entry.cachedPath)) return null;
        // 返回无扩展名的场景名
        return System.IO.Path.GetFileNameWithoutExtension(entry.cachedPath);
    }

    // 尝试获取当前激活场景对应的关卡编号（若非 Level 或未收录则返回 null）
    public static int? GetActiveLevelIndex()
    {
        // 数据库无效直接返回 null
        if (DB == null) return null;
        // 读取当前激活场景的名称
        string active = SceneManager.GetActiveScene().name;
        // 遍历数据库中的所有条目
        foreach (var e in DB.Scenes)
        {
            // 仅匹配 Level 类别，且显示名称与当前场景名一致
            if (e.category == SceneCategory.Level &&
                e.DisplayName == active)
                // 找到后返回其关卡编号
                return e.levelIndex;
        }
        // 未找到匹配项时返回 null
        return null;
    }

    // 根据场景名获取其分类（存在返回 true，并在 out 中给出分类）
    public static bool TryGetSceneCategory(string sceneName, out SceneCategory category)
    {
        category = default;
        if (string.IsNullOrEmpty(sceneName) || DB == null) return false;
        foreach (var e in DB.Scenes)
        {
            if (e.DisplayName == sceneName)
            {
                category = e.category;
                return true;
            }
        }
        return false;
    }

    // 判断当前激活场景是否存在分类（存在返回 true）
    public static bool TryGetActiveSceneCategory(out SceneCategory category)
    {
        var name = SceneManager.GetActiveScene().name;
        return TryGetSceneCategory(name, out category);
    }

    // 获取当前激活场景分类（找不到则返回 null）
    public static SceneCategory? GetActiveSceneCategory()
    {
        return TryGetActiveSceneCategory(out var cat) ? cat : (SceneCategory?)null;
    }

    // 判断某个场景是否为菜单场景
    public static bool IsMenuScene(string sceneName)
    {
        return TryGetSceneCategory(sceneName, out var cat) && cat == SceneCategory.Menu;
    }
}