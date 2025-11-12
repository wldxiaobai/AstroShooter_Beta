using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

public class HPIconDisplay : MonoBehaviour
{
    // 是否启用调试输出（可在 Inspector 中切换）
    [SerializeField] private bool _debug = false;

    // 目标渲染组件
    private Image _image;

    // 记录初始颜色（用于渐变与还原）
    private Color _originalSpriteColor;

    // 当前正在运行的闪红协程引用，确保重复触发时可停止旧协程
    private Coroutine _lerpCoroutine;

    /// <summary>
    /// 初始化：缓存 SpriteRenderer 与其初始颜色。
    /// </summary>
    void Awake()
    {
        _image = GetComponent<Image>();
        if (_image != null)
        {
            _originalSpriteColor = _image.color;
            DebugLog($"Awake: 缓存 SpriteRenderer 成功, 初始颜色={FormatColor(_originalSpriteColor)}");
        }
        else
        {
            DebugLog("Awake: 未找到 SpriteRenderer 组件！（将导致后续显示逻辑失效）");
        }
    }

    /// <summary>
    /// 停止协程并恢复原色。
    /// </summary>
    private void OnDisable()
    {
        if (_lerpCoroutine != null)
        {
            DebugLog("OnDisable: 停止正在运行的渐变协程。");
            StopCoroutine(_lerpCoroutine);
            _lerpCoroutine = null;
        }
        SetColor(_originalSpriteColor);
        DebugLog($"OnDisable: 恢复颜色为初始颜色 {FormatColor(_originalSpriteColor)}");
    }

    /// <summary>
    /// 设置显示可见性（通过颜色变化表现）。dis=false -> 变黑；true -> 还原。
    /// </summary>
    public void SetDisplayability(bool dis, float toDuration = 0.1f)
    {
        DebugLog($"SetDisplayability: dis={dis}, duration={toDuration:0.000}");
        if (!dis)
        {
            // 渐变到黑色
            LerpToColor(Color.black, toDuration);
        }
        else
        {
            LerpToColor(_originalSpriteColor, toDuration);
        }
    }

    /// <summary>
    /// 启动到指定颜色的渐变；若已有协程则先停止。
    /// </summary>
    private void LerpToColor(Color color, float duration)
    {
        // 若目标颜色与当前颜色相同则不处理
        if (color == _image.color) return;

        if (_lerpCoroutine != null)
        {
            DebugLog("LerpToColor: 停止旧协程，启动新协程。");
            StopCoroutine(_lerpCoroutine);
            _lerpCoroutine = null;
        }
        else
        {
            DebugLog("LerpToColor: 无旧协程，直接启动新协程。");
        }
        DebugLog($"LerpToColor: 目标颜色={FormatColor(color)}, duration={duration:0.000}");
        _lerpCoroutine = StartCoroutine(LerpCoroutine(color, duration));
    }

    /// <summary>
    /// 颜色渐变协程
    /// </summary>
    private IEnumerator LerpCoroutine(Color targetColor, float duration)
    {
        if (_image == null)
        {
            DebugLog("LerpCoroutine: SpriteRenderer 为 null，协程直接退出。");
            yield break;
        }

        DebugLog($"LerpCoroutine: 开始渐变，目标颜色={FormatColor(targetColor)}, duration={duration:0.000}");

        if (duration <= 0f)
        {
            SetColor(targetColor);
            DebugLog("LerpCoroutine: duration<=0，直接设为目标颜色。");
        }
        else
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                Color lerped = Color.Lerp(_originalSpriteColor, targetColor, k);
                SetColor(lerped);

                // 适度输出（每 0.1s 或最后一帧）
                if (_debug && (Mathf.Approximately(k, 1f) || Mathf.FloorToInt((t - Time.deltaTime) * 10f) != Mathf.FloorToInt(t * 10f)))
                {
                    DebugLog($"LerpCoroutine: 进度 k={k:0.000}, 当前颜色={FormatColor(lerped)}");
                }

                yield return null;
            }
            SetColor(targetColor);
            DebugLog($"LerpCoroutine: 完成，最终颜色={FormatColor(targetColor)}");
        }

        _lerpCoroutine = null;
        DebugLog("LerpCoroutine: 协程引用清空。");
    }

    /// <summary>
    /// 设置当前显示颜色（封装方便后期扩展）。
    /// </summary>
    private void SetColor(Color c)
    {
        if (_image != null)
        {
            _image.color = c;
        }
    }

    #region Debug Helper
    [Conditional("UNITY_EDITOR")]
    private void DebugLog(string msg)
    {
        if (_debug)
            UnityEngine.Debug.Log($"[HPIconDisplay:{gameObject.name}] {msg}", this);
    }

    private string FormatColor(Color c)
    {
        return $"(r={c.r:0.00}, g={c.g:0.00}, b={c.b:0.00}, a={c.a:0.00})";
    }
    #endregion
}