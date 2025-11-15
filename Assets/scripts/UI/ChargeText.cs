using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ChargeText : MonoBehaviour
{
    [SerializeField] private TMP_Text _text; // 可选：在 Inspector 里拖拽
    [SerializeField] private string _format = "{0}/{1}"; // 文本格式：current/max

    private void Awake()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>()
                    ?? GetComponentInChildren<TMP_Text>();
    }

    void OnEnable() => EnergyChargeSystem.OnEnergyChanged += UpdateText;
    void OnDisable() => EnergyChargeSystem.OnEnergyChanged -= UpdateText;

    private void UpdateText(int currentE, int maxE)
    {
        if (_text == null) return;
        _text.SetText(_format, currentE, maxE);
        // 如需满能量高亮，可取消注释：
        // _text.color = (currentE >= maxE) ? new Color32(255, 221, 0, 255) : Color.white;
    }
}
