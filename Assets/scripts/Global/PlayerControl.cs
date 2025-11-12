using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局玩家控制单例：负责玩家实例的生成、可见性切换、血量与重生点管理、复活流程。
/// </summary>
struct RespawnPoint
{
    public Vector3 position;
    public string RoomName;
}

public class PlayerControl : Singleton<PlayerControl>
{
    private const string LogTag = "[PlayerControl]";

    [Header("主角物体预制体")]
    [SerializeField] private GameObject playerPerfab; // 在 Inspector 指定玩家预制体

    [Header("死亡消息")]
    [SerializeField] private GameObject deathMessage;

    // 受伤事件 (damage, newHP, maxHP, willDie)
    public static event Action<int, int, int, bool> OnPlayerHurt;
    // 回血事件 (healamt, newHP, maxHP, wasFullHealth)
    public static event Action<int, int, int, bool> OnPlayerHeal;

    // 指向主角物体（运行时实例）
    public static GameObject Player;
    // 最大血量
    private static int maxHealthPoint = 3;

    // 血量
    private static int HealthPoint = maxHealthPoint;
    public static int HP => HealthPoint;
    public static void GetHurt(int damage)
    {
        int oldHP = HealthPoint;
        HealthPoint -= damage;
        bool willDie = HealthPoint * oldHP <= 0;
        HealthPoint = Mathf.Max(HealthPoint, 0);

        // 广播受伤事件
        try
        {
            OnPlayerHurt?.Invoke(damage, HealthPoint, maxHealthPoint, willDie); 
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} OnPlayerHurt invocation exception: {e.Message}");
        }

        Debug.Log($"{LogTag} GetHurt damage={damage}, HP(old->{oldHP}->{HealthPoint}), willDie={willDie}");

        if (willDie)
        {
            if (Player != null)
                Player.SetActive(false);

            if (Instance.deathMessage != null)
            {
                Instantiate(Instance.deathMessage);
                Debug.Log($"{LogTag} Player died.");
            }
            else
            {
                Debug.LogWarning($"{LogTag} deathMessage prefab is not assigned.");
            }
        }
    }
    public static void Heal(int amount)
    {
        bool wasFullHealth = HealthPoint >= maxHealthPoint;
        HealthPoint += amount;
        if (HealthPoint > maxHealthPoint)
            HealthPoint = maxHealthPoint;

        // 广播回血事件
        try
        {
            OnPlayerHeal?.Invoke(amount, HealthPoint, maxHealthPoint, wasFullHealth);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} OnPlayerHeal invocation exception: {e.Message}");
        }

        Debug.Log($"{LogTag} Heal amount={amount}, HP={HealthPoint}");
    }

    // Player 物体启用与否（全局开关）
    private static bool enablePlayer = false;
    public static bool EnablePlayer
    {
        get => enablePlayer;
        set
        {
            // 更新全局状态
            enablePlayer = value;
            Debug.Log($"{LogTag} EnablePlayer set to {enablePlayer}");

            // 同步到实际 Player 实例
            if (Player != null)
            {
                Player.SetActive(enablePlayer);
                Debug.Log($"{LogTag} Player GameObject active={enablePlayer} (name={Player.name})");
            }
            else
            {
                Debug.Log($"{LogTag} Player instance is null, will sync when instantiated.");
            }
        }
    }

    // 重生点：允许“置空”（未设置）
    private static RespawnPoint? _respawnPoint = null;

    // 是否已设置重生点
    public static bool HasRespawnPoint => _respawnPoint.HasValue;

    /// <summary>
    /// 设置重生点位置与所属房间名。
    /// </summary>
    public static void SetRespawnPoint(Vector3 position, string roomName)
    {
        _respawnPoint = new RespawnPoint
        {
            position = position,
            RoomName = roomName
        };
        Debug.Log($"{LogTag} SetRespawnPoint room='{roomName}', pos={position}");
    }

    /// <summary>
    /// 置空重生点。
    /// </summary>
    public static void ClearRespawnPoint()
    {
        _respawnPoint = null;
        Debug.Log($"{LogTag} ClearRespawnPoint");
    }

    /// <summary>
    /// 复活：
    /// reloadScene = false 只在当前场景内复活（不重载）；
    /// reloadScene = true 先设置血量/位置，再重载目标场景（即使当前已经在该场景中也会重载一次）。
    /// </summary>
    public static void Respawn(bool reloadScene = true)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"{LogTag} Respawn aborted: Instance is null.");
            return;
        }

        Instance.EnsurePlayerInstance();
        if (Player == null)
        {
            Debug.LogWarning($"{LogTag} Respawn aborted: Player instance is null.");
            return;
        }

        if (!_respawnPoint.HasValue)
        {
            Debug.LogWarning($"{LogTag} Respawn aborted: RespawnPoint is not set.");
            return;
        }

        var rp = _respawnPoint.Value;
        string activeScene = SceneManager.GetActiveScene().name;
        Debug.Log($"{LogTag} Respawn begin (reloadScene={reloadScene}) targetRoom='{rp.RoomName}', active='{activeScene}', targetPos={rp.position}");

        if (reloadScene)
        {
            // 预先恢复状态与位置（Player 跨场景常驻，重载后仍在该坐标）
            Player.transform.position = rp.position;
            HealthPoint = maxHealthPoint;
            EnablePlayer = true;

            Debug.Log($"{LogTag} Respawn -> Force reload room '{rp.RoomName}'");
            // 无论是否同一场景都强制重载一次
            LoadRoom.LoadScene(rp.RoomName);
            return;
        }

        // 不重载：仅在当前场景内复活
        Player.transform.position = rp.position;
        HealthPoint = maxHealthPoint;
        EnablePlayer = true;
        Debug.Log($"{LogTag} Respawn in-place done at {rp.position}, HP reset to {HealthPoint}");
    }

    /// <summary>
    /// 单例就绪：常用于跨场景保留对象初始化完成后的同步。
    /// </summary>
    protected override void OnSingletonReady()
    {
        // 单例就绪时确保玩家存在
        EnsurePlayerInstance();
        // 初始启用状态同步
        if (Player != null)
            Player.SetActive(enablePlayer);

        Debug.Log($"{LogTag} OnSingletonReady -> player instantiated={Player != null}, activeState={enablePlayer}");
    }

    /// <summary>
    /// 通过预制体确保玩家实例存在；若不存在则创建为本物体的子物体。
    /// </summary>
    private void EnsurePlayerInstance()
    {
        if (Player != null)
        {
            // 已存在则不重复创建
            return;
        }

        if (playerPerfab == null)
        {
            Debug.LogError($"{LogTag} playerPerfab 未指定，无法创建玩家实例。");
            return;
        }

        // 作为 PlayerControl 所在对象的子物体实例化
        Player = Instantiate(playerPerfab, transform);
        // 可选：归零局部坐标，保持层级干净
        Player.transform.localPosition = Vector3.zero;

        // 按当前全局开关设置启用状态
        Player.SetActive(enablePlayer);

        Debug.Log($"{LogTag} EnsurePlayerInstance -> Created player instance name='{Player.name}', parent='{transform.name}', active={enablePlayer}");
    }
}