using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 集中管理所有发射冷却
/// 用法：FireCDManager.TryFire("武器签名", shotCD)
/// </summary>
public static class FireCDManager
{
    // 记录下一次允许触发的时间（Time.time）
    private static readonly Dictionary<string, float> _nextFireTime = new Dictionary<string, float>(32);

    /// <summary>
    /// 尝试触发一次。若当前时间已到达冷却结束则返回 true 并推进下一次时间，否则返回 false。
    /// </summary>
    /// <param name="key">武器/按钮唯一标识，如 "PlayerPrimary" 或 "Enemy#12_Laser"</param>
    /// <param name="cooldown">冷却时长（秒）</param>
    /// <param name="useUnscaled">是否使用不受 timeScale 影响的时间</param>
    public static bool TryFire(string key, float cooldown, bool useUnscaled = false)
    {
        float now = useUnscaled ? Time.unscaledTime : Time.time;

        if (_nextFireTime.TryGetValue(key, out float next))
        {
            if (now < next) return false;
            _nextFireTime[key] = now + Mathf.Max(0f, cooldown);
            return true;
        }
        else
        {
            // 首次使用：立即允许，设定下一次
            _nextFireTime[key] = now + Mathf.Max(0f, cooldown);
            return true;
        }
    }

    /// <summary>
    /// 强制重置某个键的冷却，使下一次调用 TryFire 立即触发。
    /// </summary>
    public static void Reset(string key, bool useUnscaled = false)
    {
        float now = useUnscaled ? Time.unscaledTime : Time.time;
        _nextFireTime[key] = now;
    }

    /// <summary>
    /// 查询剩余冷却时间（未到返回 >0，到期返回 0）。
    /// </summary>
    public static float GetRemaining(string key, bool useUnscaled = false)
    {
        float now = useUnscaled ? Time.unscaledTime : Time.time;
        if (_nextFireTime.TryGetValue(key, out float next))
        {
            float remain = next - now;
            return remain > 0f ? remain : 0f;
        }
        return 0f;
    }

    /// <summary>
    /// 判断现在是否可触发（不消耗冷却）。
    /// </summary>
    public static bool CanFire(string key, bool useUnscaled = false)
    {
        float now = useUnscaled ? Time.unscaledTime : Time.time;
        return !_nextFireTime.TryGetValue(key, out float next) || now >= next;
    }
}