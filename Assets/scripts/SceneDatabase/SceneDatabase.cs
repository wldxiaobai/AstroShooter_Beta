// 引用基础类型与集合
using System; // 提供 [Serializable]、属性等
using System.Collections.Generic; // 提供 List、IReadOnlyList、IEnumerable
using UnityEngine; // 提供 ScriptableObject、SerializeField、Tooltip 等

#if UNITY_EDITOR
using UnityEditor; // 仅在编辑器中使用的 API（AssetDatabase、EditorUtility）
#endif

// 用于为场景打分类标签
public enum SceneCategory
{
    Menu,     // 菜单类场景
    Level,    // 关卡类场景
    Boss,     // Boss 战场景
    Cutscene, // 过场/剧情场景
    Other     // 其他类型场景
}

// 可序列化的场景条目，承载一个场景的元信息
[Serializable]
public class SceneEntry
{
    // 在编辑器中直接引用场景资源（SceneAsset）；构建后运行时不依赖该字段
#if UNITY_EDITOR
    public SceneAsset sceneAsset; // 仅编辑器可见，用于在 Inspector 拖拽场景
#endif

    [Tooltip("分类")]
    public SceneCategory category; // 场景分类（Menu/Level/Boss/...）

    [Tooltip("关卡序号（仅当分类为 Level 时使用）")]
    public int levelIndex; // 关卡编号（仅 Level 类别时有意义）

    [Tooltip("是否加入自动关卡流转")]
    public bool includedInFlow = true; // 是否参与“顺序关卡流”枚举

    // 以下两个为运行时可用的缓存，不依赖 UnityEditor
    public string cachedPath; // 缓存的场景路径（Assets/.../Scene.unity）
    public string cachedGuid; // 缓存的场景 GUID（用于检测引用丢失）

    // 仅用于显示/运行时获取场景名（无扩展名）
    public string DisplayName
    {
        get
        {
            // 若有路径缓存，从路径提取文件名（不含扩展名）
            if (!string.IsNullOrEmpty(cachedPath))
                return System.IO.Path.GetFileNameWithoutExtension(cachedPath);
            // 否则返回缺失占位
            return "<Missing>";
        }
    }
}

// ScriptableObject 数据库：集中维护项目中的场景分类与元数据
[CreateAssetMenu(fileName = "SceneDatabase", menuName = "Game/Scene Database")]
public class SceneDatabase : ScriptableObject
{
    // 序列化的场景条目列表；使用 C# 9 的 target-typed new 简化构造
    [SerializeField]
    private List<SceneEntry> scenes = new();

    // 只读暴露列表，避免外部直接修改底层集合
    public IReadOnlyList<SceneEntry> Scenes => scenes;

    // 运行时查询：按分类枚举所有场景条目
    public IEnumerable<SceneEntry> GetByCategory(SceneCategory cat)
    {
        // 顺序遍历全部条目，筛选出分类匹配的条目
        foreach (var s in scenes)
            if (s.category == cat)
                yield return s; // 逐个返回（惰性迭代）
    }

    // 获取指定关卡序号的所有条目（一个关卡可由多个子场景组成）
    public IEnumerable<SceneEntry> GetLevel(int levelIndex)
    {
        // 仅筛选 Level 类别且 levelIndex 匹配的条目
        foreach (var s in scenes)
            if (s.category == SceneCategory.Level && s.levelIndex == levelIndex)
                yield return s;
    }

    // 获取“主关卡”条目（若你约定每个关只有一个主场景，且 includedInFlow=true）
    public SceneEntry GetMainLevel(int levelIndex)
    {
        // 查找第一个满足 Level 类别、编号匹配、且参与关卡流的条目
        foreach (var s in scenes)
            if (s.category == SceneCategory.Level && s.levelIndex == levelIndex && s.includedInFlow)
                return s; // 找到立即返回
        return null; // 未找到则返回空
    }

    // 顺序遍历“关卡流”（用于关卡推进）：仅包含 Level 且 includedInFlow=true 的条目
    public IEnumerable<SceneEntry> EnumerateLevelFlow()
    {
        // 将满足条件的条目复制到临时列表
        var list = new List<SceneEntry>();
        foreach (var s in scenes)
            if (s.category == SceneCategory.Level && s.includedInFlow)
                list.Add(s);
        // 按关卡编号升序排序，形成推进顺序
        list.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));
        // 返回排序后的序列（一次性集合也可直接返回 list）
        return list;
    }

#if UNITY_EDITOR
    // 当资源在编辑器中变更（如拖拽、重命名）时回调，用于同步缓存
    private void OnValidate()
    {
        // 标记是否发生修改，便于标记资源为 dirty
        bool changed = false;
        // 遍历所有条目，依据 sceneAsset 同步 cachedPath 与 cachedGuid
        foreach (var e in scenes)
        {
            if (e.sceneAsset != null)
            {
                // 获取场景的资源路径与 GUID
                string path = AssetDatabase.GetAssetPath(e.sceneAsset);
                string guid = AssetDatabase.AssetPathToGUID(path);
                // 若缓存与真实值不一致则更新缓存
                if (e.cachedPath != path || e.cachedGuid != guid)
                {
                    e.cachedPath = path;
                    e.cachedGuid = guid;
                    changed = true; // 标记有变化
                }
            }
            else
            {
                // 若 editor 引用丢失，但缓存里仍有路径，则清空缓存
                if (!string.IsNullOrEmpty(e.cachedPath))
                {
                    e.cachedPath = null;
                    changed = true;
                }
            }
        }
        // 若有变化，标记资源已修改，便于保存
        if (changed)
        {
            EditorUtility.SetDirty(this);
        }
    }

    // 右键菜单功能：可用于从 Build Settings 同步（示例骨架）
    [ContextMenu("Sync From Build Settings")]
    private void SyncFromBuildSettings()
    {
        // 用于收集构建设置中的场景 GUID
        var buildSceneGuids = new HashSet<string>();
        // 构建设置中场景数量
        int count = UnityEditor.SceneManagement.EditorSceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            // 这里示例性地获取设置中的场景路径（注意：实际项目可根据需要完善）
            string path = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup()[i].path;
            // 将路径转换为 GUID
            string guid = AssetDatabase.AssetPathToGUID(path);
            // 收集 GUID
            buildSceneGuids.Add(guid);
        }
        // 实际同步逻辑可依据 GUID 对比增删 SceneEntry，此处仅为采集示例
    }
#endif
}