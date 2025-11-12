using UnityEngine;
using System.Collections;

/// <summary>
/// HurtAnim：监听玩家受伤事件并执行“闪红”视觉反馈。
/// 逻辑：收到 PlayerControl.OnPlayerHurt（非致死） -> 颜色从原色渐红再渐回。
/// </summary>
public class HurtOrHealAnim : MonoBehaviour
{
    // 目标渲染组件（仅使用 SpriteRenderer）
    private SpriteRenderer _spriteRenderer;

    // 记录初始颜色（用于渐变与还原）
    private Color _originalSpriteColor;

    // 当前正在运行的闪红协程引用，确保重复触发时可停止旧协程
    private Coroutine _flashCoroutine;

    /// <summary>
    /// 初始化：缓存 SpriteRenderer 与其初始颜色。
    /// </summary>
    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalSpriteColor = _spriteRenderer.color;
    }

    /// <summary>
    /// 启用时订阅玩家受伤事件。
    /// </summary>
    void OnEnable()
    {
        PlayerControl.OnPlayerHurt += HandlePlayerHurt;
        PlayerControl.OnPlayerHeal += HandlePlayerHeal;
    }

    /// <summary>
    /// 禁用或摧毁时退订事件、停止协程并恢复原色。
    /// 防止静态事件持有引用导致内存泄漏或颜色残留。
    /// </summary>
    private void OnDisableOrDestroyed()
    {
        PlayerControl.OnPlayerHurt -= HandlePlayerHurt;
        PlayerControl.OnPlayerHeal -= HandlePlayerHeal;

        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        SetColor(_originalSpriteColor);
    }
    void OnDisable()
    {
        OnDisableOrDestroyed();
    }
    void OnDestroy()
    {
        OnDisableOrDestroyed();
    }

    /// <summary>
    /// 事件回调：仅在未致死时触发闪红。
    /// </summary>
    private void HandlePlayerHurt(int damage, int hp, int maxHp, bool willDie)
    {
        if (willDie) return; // 死亡不播放闪红
        PlayFlash(Color.red);
    }
    /// <summary>
    /// 事件回调：触发闪绿。
    /// </summary>
    private void HandlePlayerHeal(int amount, int hp, int maxHp, bool wasFullHealth)
    {
        if (wasFullHealth) return; // 回血前已满血，则不播放闪绿
        PlayFlash(Color.green);
    }

    /// <summary>
    /// 开始一次闪红流程：先渐红再渐回。
    /// 可调两个阶段时长。重复调用会重置动画。
    /// </summary>
    private void PlayFlash(Color color, float toRedDuration = 0.05f, float backDuration = 0.15f)
    {
        if (!isActiveAndEnabled || _spriteRenderer == null) return;

        if (_flashCoroutine != null)
        {
            // 停止旧协程，防止重复调用导致颜色错乱
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        _flashCoroutine = StartCoroutine(FlashCoroutine(color, toRedDuration, backDuration));
    }

    /// <summary>
    /// 协程：执行颜色插值。
    /// 阶段1：原色 -> 红色
    /// 阶段2：红色 -> 原色
    /// 支持时长为0（直接跳色）。
    /// </summary>
    private IEnumerator FlashCoroutine(Color color, float toRedDuration, float backDuration)
    {
        if (_spriteRenderer == null) yield break;

        // 渐红
        if (toRedDuration <= 0f)
        {
            SetColor(color);
        }
        else
        {
            float t = 0f;
            while (t < toRedDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / toRedDuration);
                SetColor(Color.Lerp(_originalSpriteColor, color, k));
                yield return null;
            }
            SetColor(color);
        }

        // 渐回
        if (backDuration <= 0f)
        {
            SetColor(_originalSpriteColor);
        }
        else
        {
            float t = 0f;
            while (t < backDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / backDuration);
                SetColor(Color.Lerp(color, _originalSpriteColor, k));
                yield return null;
            }
            SetColor(_originalSpriteColor);
        }

        _flashCoroutine = null;
    }

    /// <summary>
    /// 设置当前显示颜色（封装方便后期扩展）。
    /// </summary>
    private void SetColor(Color c)
    {
        if (_spriteRenderer != null)
            _spriteRenderer.color = c;
    }
}