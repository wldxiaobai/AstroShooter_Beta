using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChargeBar : MonoBehaviour
{
    void OnEnable() => EnergyChargeSystem.OnEnergyChanged += UpdateBar;
    void OnDisable() => EnergyChargeSystem.OnEnergyChanged -= UpdateBar;
    private void UpdateBar(int currentE, int maxE)
    {
        gameObject.transform.localScale = new Vector3((float)currentE / maxE, 1, 1);
    }
}
