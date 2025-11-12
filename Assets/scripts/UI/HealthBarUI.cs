using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 根据玩家最大血量动态生成血量图标，并按间隔排列；
/// 同时根据当前血量显示/隐藏图标。
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("单个血量图标预制体")]
    [SerializeField] private GameObject singleHP;

    [Header("水平间隔（像素/单位）")]
    [SerializeField] private float spacing = 80f;

    // 已实例化的血量图标
    private readonly List<GameObject> _hpIcons = new();

    void OnEnable()
    {
        BuildIfNeeded();
        PlayerControl.OnPlayerHurt += OnPlayerHurt;
        PlayerControl.OnPlayerHeal += OnPlayerHeal;
        UpdateCurrentHPDisplay();
    }
    
    void OnDisable()
    {
        PlayerControl.OnPlayerHurt -= OnPlayerHurt;
        PlayerControl.OnPlayerHeal -= OnPlayerHeal;
    }
    void OnDestroy()
    {
        PlayerControl.OnPlayerHurt -= OnPlayerHurt;
        PlayerControl.OnPlayerHeal -= OnPlayerHeal;
    }

    void Start()
    {
        BuildIfNeeded();
        UpdateCurrentHPDisplay();
    }

    // 受伤事件响应：仅更新当前血量显示（如果最大血量可能变化，可在此检测并重建）
    private void OnPlayerHurt(int damage, int newHp, int maxHp, bool willDie)
    {
        BuildIfNeeded();
        UpdateCurrentHPDisplay();
    }
    // 回血事件响应：仅更新当前血量显示（如果最大血量可能变化，可在此检测并重建）
    private void OnPlayerHeal(int amount, int newHp, int maxHp, bool wasFullHealth)
    {
        BuildIfNeeded();
        UpdateCurrentHPDisplay();
    }

    // 若尚未构建或数量不匹配则构建
    private void BuildIfNeeded()
    {
        int maxHp = PlayerControl.MaxHP;
        if (_hpIcons.Count == maxHp && _hpIcons.Count > 0) return;
        Rebuild();
    }

    // 重新生成所有血量图标
    public void Rebuild()
    {
        Clear();
        int maxHp = PlayerControl.MaxHP;
        if (singleHP == null)
        {
            Debug.LogWarning("[HealthBarUI] singleHP 预制体未赋值，无法生成血量图标。");
            return;
        }

        for (int i = 0; i < maxHp; i++)
        {
            var icon = Instantiate(singleHP, transform);
            icon.name = $"HP_{i + 1}";
            _hpIcons.Add(icon);
        }

        LayoutIcons();
    }

    // 清空旧的图标
    private void Clear()
    {
        for (int i = 0; i < _hpIcons.Count; i++)
        {
            if (_hpIcons[i] != null)
                Destroy(_hpIcons[i]);
        }
        _hpIcons.Clear();
    }

    // 排列：按 spacing 间隔横向放置，可居中
    private void LayoutIcons()
    {
        if (_hpIcons.Count == 0) return;

        float startX = 0f;

        for (int i = 0; i < _hpIcons.Count; i++)
        {
            float x = startX + (i+1) * spacing;

            // 优先使用 RectTransform（用于 UI Canvas 下）
            var rt = _hpIcons[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(x, 0f);
            }
            else
            {
                _hpIcons[i].transform.localPosition = new Vector3(x, 0f, 0f);
            }
        }
    }

    // 根据当前血量显示/隐藏图标
    private void UpdateCurrentHPDisplay()
    {
        int currentHp = PlayerControl.HP;
        for (int i = 0; i < _hpIcons.Count; i++)
        {
            _hpIcons[i].GetComponent<HPIconDisplay>().SetDisplayability(i < currentHp);
        }
    }
}