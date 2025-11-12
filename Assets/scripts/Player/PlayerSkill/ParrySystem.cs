
using UnityEngine;

[RequireComponent(typeof(EnergyChargeSystem))]
public class ParrySystem : MonoBehaviour
{
    [Header("防反设置")]
    public float parryWindow = 0.2f;    // 防反判定时间窗口
    public float cooldownTime = 1f;     // 冷却时间

    private bool isParrying = false;    // 是否正在防反判定期间
    private bool isOnCooldown = false;  // 是否在冷却中
    private float parryTimer = 0f;      // 防反计时器
    private float cooldownTimer = 0f;   // 冷却计时器

    private EnergyChargeSystem energySystem;

    void Awake()
    {
        // 确保有 EnergyChargeSystem 组件
        energySystem = GetComponent<EnergyChargeSystem>();
        if (energySystem == null)
        {
            energySystem = gameObject.AddComponent<EnergyChargeSystem>();
        }
    }

    void Update()
    {
        HandleInput();
        UpdateTimers();
    }

    void HandleInput()
    {
        LevelControl lc = LevelControl.Instance; // 获取关卡控制单例
        // 按下F键且不在冷却中且通关后触发防反
        if (Input.GetKeyDown(KeyCode.F) && !isOnCooldown && !isParrying && lc.IsLevelCompleted("WaterPlanet"))
        {
            StartParry();
        }
    }

    void UpdateTimers()
    {
        // 防反计时器更新
        if (isParrying)
        {
            parryTimer += Time.deltaTime;
            if (parryTimer >= parryWindow)
            {
                EndParry();
            }
        }

        // 冷却计时器更新
        if (isOnCooldown)
        {
            cooldownTimer += Time.deltaTime;
            if (cooldownTimer >= cooldownTime)
            {
                EndCooldown();
            }
        }
    }

    void StartParry()
    {
        isParrying = true;
        parryTimer = 0f;

        // 这里可以添加防反开始的视觉效果或音效
        Debug.Log("防反开始！");
    }

    void EndParry()
    {
        isParrying = false;
        StartCooldown();

        // 这里可以添加防反结束的反馈
        Debug.Log("防反结束");
    }

    void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = 0f;

        // 这里可以添加冷却开始的视觉效果
        Debug.Log("进入冷却时间");
    }

    void EndCooldown()
    {
        isOnCooldown = false;

        // 这里可以添加冷却结束的提示
        Debug.Log("冷却结束，可以再次使用防反");
    }

    // 碰撞检测（使用Trigger）
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isParrying) return;

        if (other.CompareTag("player bullet") || other.CompareTag("Player"))
            return;

        // 销毁被防反的物体
        Destroy(other.gameObject);

        // 通知能量系统：防反成功，置为充满（或按需求改为 AddEnergy）
        if (energySystem != null)
            energySystem.SetFull(true);

        Debug.Log("防反成功！销毁了: " + other.name);
    }

    // 碰撞检测（使用Collider）
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isParrying) return;

        GameObject other = collision.gameObject;

        if (other.CompareTag("player bullet") || other.CompareTag("Player"))
            return;

        Destroy(other);

        if (energySystem != null)
            energySystem.SetFull(true);

        Debug.Log("防反成功！销毁了: " + other.name);
    }

    // 可视化调试信息（在Scene窗口中显示）
    void OnDrawGizmos()
    {
        if (isParrying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1.5f);
        }

        if (isOnCooldown)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}