using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

struct RespawnPoint
{
    public Vector3 position;
    public string RoomName;
}

public class PlayerControl : Singleton<PlayerControl>
{
    LevelControl lc = LevelControl.Instance; // 获取关卡控制单例
    [Header("主角物体预制体")]
    [SerializeField] private GameObject playerPerfab;
    // 指向主角物体
    public static GameObject Player;

    // 血量
    //血上限，如果过了极寒星就是5，反之3；
    private static int HealthPoint = LevelControl.Instance.IsLevelCompleted("GlacierPlanet") ? 5 : 3;
    public static int HP => HealthPoint;
    
    public static void GetHurt(int damage) { HealthPoint -= damage; }
   //直接用start做回血功能
   
            
        
    // Player物体启用与否
    private static bool enablePlayer = false;
    public static bool EnablePlayer
    {
        get => enablePlayer;
        set
        {
            enablePlayer = value;
            // 同步Player物体的启用状态
            if (Player != null)
                Player.SetActive(enablePlayer);
        }
    }

    // 重生点：允许“置空”（未设置）
    private static RespawnPoint? _respawnPoint = null;

    // 是否已设置重生点
    public static bool HasRespawnPoint => _respawnPoint.HasValue;

    public static void SetRespawnPoint(Vector3 position, string roomName)
    {
        _respawnPoint = new RespawnPoint
        {
            position = position,
            RoomName = roomName
        };
    }

    // 置空重生点
    public static void ClearRespawnPoint()
    {
        _respawnPoint = null;
    }

    public static void Respawn()
    {
        if (Instance == null) return;

        // 确保玩家实例存在（若不存在则用预制体创建）
        Instance.EnsurePlayerInstance();
        if (Player == null) return;

        if (!_respawnPoint.HasValue)
        {
            Debug.LogWarning("[PlayerControl] 重生点未设置，无法执行复活。");
            return;
        }

        var rp = _respawnPoint.Value;

        // 传送到重生点
        Player.transform.position = rp.position;
        // 加载对应房间
        LoadRoom.LoadScene(rp.RoomName);
        // 恢复血量
        HealthPoint = 3;
        // 启用玩家控制
        EnablePlayer = true;
    }

    protected override void OnSingletonReady()
    {
        // 单例就绪时确保玩家存在
        EnsurePlayerInstance();
        // 初始启用状态同步
        if (Player != null)
            Player.SetActive(enablePlayer);
    }

    // 通过预制体确保玩家实例存在；若不存在则创建为本物体的子物体
    private void EnsurePlayerInstance()
    {
        if (Player != null) return;

        if (playerPerfab == null)
        {
            Debug.LogError("[PlayerControl] playerPerfab 未指定，无法创建玩家实例。");
            return;
        }

        // 作为 PlayerControl 所在对象的子物体实例化
        Player = Instantiate(playerPerfab, transform);
        // 可选：归零局部坐标，保持层级干净
        Player.transform.localPosition = Vector3.zero;

        // 按当前全局开关设置启用状态
        Player.SetActive(enablePlayer);
    }
}