using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// 1、进入Level时生成HealthBar
/// 2、保留功能：暂停菜单
/// </summary>
public class UIControl : Singleton<UIControl>
{
    [SerializeField] private GameObject HBar; //血条预制体

    private GameObject existingHBar;

    private void OnEnable()
    {
        // 监听场景完成加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 进入关卡时生成血条
        if (SceneDatabaseAPI.TryGetSceneCategory(scene.name, out SceneCategory cat) && cat == SceneCategory.Level)
        {
            if (existingHBar != null)
            {
                Destroy(existingHBar);
                existingHBar = null;
            }
            existingHBar = Instantiate(HBar);
        }
    }
}
