using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 这是一个关卡控制单例类，用于管理游戏中各个关卡的完成状态。
/// 单例：确保全局只有一个静态实例，跨场景常驻。
/// </summary>
public class LevelControl : Singleton<LevelControl>
{
    // 关卡完成状态记录
    private Dictionary<string, bool> isCompleted = new Dictionary<string, bool>()
    {
        {"AsteroidBelt", false},
        {"GrassPlanet", false},
        {"WaterPlanet", false},
        {"GlacierPlanet", false},
        {"SteamPlanet", false},
        {"FlamePlanet", false},
        {"LightningPlanet", false} //预留功能：读档
    };

    // 标记关卡为已完成
    public void CompleteLevel(string levelName)
    {
        if (isCompleted.ContainsKey(levelName))
        {
            isCompleted[levelName] = true;
            Debug.Log($"{levelName} marked as completed.");
        }
        else
        {
            Debug.LogWarning($"Level {levelName} does not exist in the records.");
        }
    }

    // 检查关卡是否已完成
    public bool IsLevelCompleted(string levelName)
    {
        if (isCompleted.ContainsKey(levelName))
        {
            return isCompleted[levelName];
        }
        else
        {
            Debug.LogWarning($"Level {levelName} does not exist in the records.");
            return false;
        }
    }
}
