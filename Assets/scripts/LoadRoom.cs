using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载器：统一从任意位置调用，支持在加载前基于“目标场景”决定玩家显隐与重生逻辑。
/// </summary>
public class LoadRoom : MonoBehaviour
{
    [Header("目标场景名（在 Build Settings 中确保该场景已添加）")]
    [SerializeField] private string sceneToLoad;

    // 防抖：防止触发多次加载
    private bool loading;

    //调用方法：LoadRoom.LoadScene("SceneName");
    public static void LoadScene(string sceneName)
    {
        // 创建一个临时的 GameObject 来挂载 LoadRoom 组件
        GameObject loaderObject = new("SceneLoader");
        LoadRoom loader = loaderObject.AddComponent<LoadRoom>();
        loader.sceneToLoad = sceneName;
        Debug.Log($"[LoadRoom] Request load scene '{sceneName}' -> spawn loader and start");
        loader.GetLoading();
    }

    public void GetLoading()
    {
        // 若已在加载则忽略
        if (loading)
        {
            Debug.LogWarning("[LoadRoom] GetLoading ignored: already loading.");
            return;
        }
        loading = true;

        Debug.Log($"[LoadRoom] Begin loading pipeline for '{sceneToLoad}'");

        // 在加载前基于“下一个房间”判断是否为“关卡”、是否为主场景
        if (TryGetTargetLevelInfo(sceneToLoad, out int targetLevelIndex, out bool isTargetMainLevel))
        {
            // 目标是 Level
            PlayerControl.EnablePlayer = true;
            int? currentLevelIndex = SceneDatabaseAPI.GetActiveLevelIndex();
            bool isNewLevel = !currentLevelIndex.HasValue || currentLevelIndex.Value != targetLevelIndex;

            Debug.Log($"[LoadRoom] Target is Level#{targetLevelIndex}, isMain={isTargetMainLevel}, isNewLevel={isNewLevel}");

            if (isTargetMainLevel && isNewLevel)
            {
                // 先设置重生点，再在当前场景内复活一次（不重载）
                PlayerControl.SetRespawnPoint(Vector3.zero, sceneToLoad);
                PlayerControl.Respawn(reloadScene: false);
                Debug.Log($"[LoadRoom] Set respawn to (0,0,0) for main level '{sceneToLoad}' and respawned in-place before load.");
            }
        }
        else
        {
            // 目标不是 Level（例如 Menu/Boss/Cutscene 等）
            PlayerControl.EnablePlayer = false;
            Debug.Log($"[LoadRoom] Target '{sceneToLoad}' is not a Level. Player disabled.");
        }

        // 直接异步加载目标场景
        StartCoroutine(LoadSceneCoroutine());
    }

    /// <summary>
    /// 异步加载场景；开始加载，期间可以做其他动作
    /// </summary>
    private IEnumerator LoadSceneCoroutine()
    {
        // 检测场景名是否无效，若无效则加载默认场景
        if (string.IsNullOrEmpty(sceneToLoad) || !Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogWarning($"[LoadRoom] sceneToLoad='{sceneToLoad}' is invalid. Fallback to 'ToBeContinued'");
            sceneToLoad = "ToBeContinued";
        }

        Debug.Log($"[LoadRoom] Start LoadSceneAsync('{sceneToLoad}')");
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);

        // 检查加载操作是否成功
        if (op != null)
        {
            // 等待加载完成
            while (!op.isDone)
            {
                Debug.Log($"[LoadRoom] Loading {sceneToLoad}: {op.progress * 100f}%"); // Debug显示加载进度
                yield return null;
            }
            Debug.Log($"[LoadRoom] Scene {sceneToLoad} loaded successfully."); // 加载完成
        }
        else
        {
            Debug.LogError($"[LoadRoom] Failed to load scene: {sceneToLoad}"); // 加载失败
            yield break;
        }
        // 若需要：可在此处处理加载完成后的状态（例如：禁用交互、播放过场等）
    }

    /// <summary>
    /// 基于 SceneDatabase，获取“目标场景是否为 Level、其关卡序号、是否是该关卡的主场景”。
    /// </summary>
    private static bool TryGetTargetLevelInfo(string sceneName, out int levelIndex, out bool isMainLevel)
    {
        levelIndex = -1;
        isMainLevel = false;

        var db = SceneDatabaseAPI.DB;
        if (db == null || string.IsNullOrEmpty(sceneName)) return false;

        foreach (var e in db.Scenes)
        {
            if (e.DisplayName == sceneName)
            {
                if (e.category != SceneCategory.Level) return false; // 非 Level
                levelIndex = e.levelIndex;

                // 主场景判定：该条目参与关卡流，且是该关卡配置的“主场景”
                string mainName = SceneDatabaseAPI.GetMainLevelSceneName(levelIndex);
                isMainLevel = e.includedInFlow && !string.IsNullOrEmpty(mainName) && mainName == sceneName;
                return true;
            }
        }
        return false;
    }
}