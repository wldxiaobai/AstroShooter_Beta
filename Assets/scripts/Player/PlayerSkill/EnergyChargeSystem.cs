
using UnityEngine;

public class EnergyChargeSystem : MonoBehaviour
{
    [Header("充能设置")]
    public int maxEnergy = 100;                    // 最大充能值
    public int energyPerHit = 1;                   // 每次击中敌人获得的能量
    public KeyCode launchKey = KeyCode.E;          // 发射按键
    public GameObject hugeBulletPrefab;            // 巨大子弹预制体
    public Transform firePoint;                    // 发射点

    private int currentEnergy = 0;                 // 当前充能值
    private bool full = false;                     // 是否充满

    void Start()
    {
        // 如果没有指定发射点，使用玩家自身位置
        if (firePoint == null)
        {
            firePoint = transform;
        }
    }

    void Update()
    {
        // 如果Full为true，强制设置能量为最大值
        if (full && currentEnergy != maxEnergy)
        {
            currentEnergy = maxEnergy;
        }

        // 检测发射巨大子弹
        if (Input.GetKeyDown(launchKey) && currentEnergy >= maxEnergy)
        {
            LaunchHugeBullet();
        }
    }

    // amount 默认 -1 表示使用 energyPerHit
    public void AddEnergy(int amount = -1)
    {
        // 如果已满或Full为true，则不再增加能量
        if (currentEnergy >= maxEnergy || full)
            return;

        int addAmount = (amount == -1) ? energyPerHit : amount;
        currentEnergy += addAmount;
        if (currentEnergy > maxEnergy)
            currentEnergy = maxEnergy;
    }

    public void SetFull(bool isFull)
    {
        full = isFull;
        if (full)
        {
            currentEnergy = maxEnergy;
        }
    }

    public int GetCurrentEnergy()
    {
        return currentEnergy;
    }

    // 获取是否充满
    public bool IsFull()
    {
        return full;
    }

    // 直接设置能量值（会被限制在 0..maxEnergy）
    public void SetEnergy(int energy)
    {
        currentEnergy = Mathf.Clamp(energy, 0, maxEnergy);
        full = (currentEnergy >= maxEnergy);
    }

    // 重置能量
    public void ResetEnergy()
    {
        currentEnergy = 0;
        full = false;
    }

    void LaunchHugeBullet()
    {
        if (hugeBulletPrefab != null && firePoint != null)
        {
            // 实例化巨大子弹
            Instantiate(hugeBulletPrefab, firePoint.position, firePoint.rotation);

            // 重置能量
            currentEnergy = 0;
            full = false;

            Debug.Log("发射巨大子弹！");
        }
        else
        {
            Debug.LogWarning("巨大子弹预制体或发射点未设置！");
        }
    }
}