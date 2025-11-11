using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadRoom : MonoBehaviour
{
    [Header("目标场景名（在 Build Settings 中确保该场景已添加）")]
    [SerializeField] private string sceneToLoad;

    // 防抖：防止触发多次加载
    private bool loading;

    public static void LoadScene(string sceneName)
    {
        // 创建一个临时的 GameObject 来挂载 LoadRoom 组件
        GameObject loaderObject = new GameObject("SceneLoader");
        LoadRoom loader = loaderObject.AddComponent<LoadRoom>();
        loader.sceneToLoad = sceneName;
        loader.GetLoading();
    }

    public void GetLoading()
    {
        // 若已在加载则忽略
        if (loading) return;
        loading = true;

        // 异步加载场景
        StartCoroutine(LoadSceneCoroutine());
    }

    /// <summary>
    /// 异步加载场景；开始加载，期间可以做其他动作
    /// </summary>
    private IEnumerator LoadSceneCoroutine()
    {
        // 检测场景名是否无效，若无效则加载默认场景
        if (string.IsNullOrEmpty(sceneToLoad) || !Application.CanStreamedLevelBeLoaded(sceneToLoad)) sceneToLoad = "ToBeContinued";

        // 开始加载场景，期间可以做其他动作
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);

        // 检查加载操作是否成功
        if (op != null)
        {
            // 等待加载完成
            while (!op.isDone)
            {
                Debug.Log($"Loading {sceneToLoad}: {op.progress * 100f}%"); // Debug显示加载进度
                yield return null;
            }
            Debug.Log($"Scene {sceneToLoad} loaded successfully."); // 加载完成
        }
        else
        {
            Debug.LogError($"Failed to load scene: {sceneToLoad}"); // 加载失败
            yield break;
        }
        // 若需要：可在此处处理加载完成后的状态（例如：禁用交互、播放过场等）
    }
}
