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
    [Header("血条预制体")]
    [SerializeField] private GameObject HBarPerfab; 
    [Header("充能UI预制体")]
    [SerializeField] private GameObject energyUIPrefab; 

    private GameObject existingHBar;
    private GameObject existingEBar;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 进入关卡时生成血条与充能条
        if (SceneDatabaseAPI.TryGetSceneCategory(scene.name, out SceneCategory cat) && cat == SceneCategory.Level)
        {
            if (existingHBar != null)
            {
                Destroy(existingHBar);
                existingHBar = null;
            }
            existingHBar = Instantiate(HBarPerfab);

            if (existingEBar != null)
            {
                Destroy(existingEBar);
                existingEBar = null;
            }
            existingEBar = Instantiate(energyUIPrefab);
        }
    }
}
