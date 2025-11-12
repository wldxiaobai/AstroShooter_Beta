using UnityEngine;
using System.Collections;

public class Defence : MonoBehaviour
{
    [Header("充能设置")]
    public int maxEnergy = 100;                    // 最大充能值
    public int energyPerHit = 1;                   // 每次击中敌人获得的能量
    public KeyCode launchKey = KeyCode.E;          // 发射按键
    public GameObject hugeBulletPrefab;            // 巨大子弹预制体
    public Transform firePoint;                    // 发射点

    
    private int currentEnergy = 0;                 // 当前充能值
    private bool full = false;                     // 是否充满
    [Header("防反设置")]
    public float parryWindow = 0.2f;    // 防反判定时间窗口
    public float cooldownTime = 1f;     // 冷却时间

    private bool isParrying = false;    // 是否正在防反判定期间
    private bool isOnCooldown = false;  // 是否在冷却中
    private float parryTimer = 0f;      // 防反计时器
    private float cooldownTimer = 0f;   // 冷却计时器
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
        HandleInput();
        UpdateTimers();
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

    public void AddEnergy(int amount = -1)
    {
        // 如果已满或Full为true，则不再增加能量
        if (currentEnergy >= maxEnergy || full)
            return;

        // 使用指定值或默认值
        int addAmount = (amount == -1) ? energyPerHit : amount;

        currentEnergy += addAmount;

        // 限制能量不超过最大值
        if (currentEnergy > maxEnergy)
        {
            currentEnergy = maxEnergy;
        }

        
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

            // 这里可以添加发射音效或特效
        }
        else
        {
            Debug.LogWarning("巨大子弹预制体或发射点未设置！");
        }
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

    // 直接设置能量值
    public void SetEnergy(int energy)
    {
        currentEnergy = Mathf.Clamp(energy, 0, maxEnergy);
       
    }

    // 重置能量
    public void ResetEnergy()
    {
        currentEnergy = 0;
        full = false;
        
    }

    void HandleInput()
    {
        LevelControl lc = LevelControl.Instance; // 获取关卡控制单例
        // 按下F键且不在冷却中且通关后触发防反
        if (Input.GetKeyDown(KeyCode.F) && !isOnCooldown && !isParrying&& lc.IsLevelCompleted("WaterPlanet"))
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
        if (isParrying)
        {
            // 检查碰撞物体是否是玩家子弹，如果是则忽略
            if (other.CompareTag("player bullet"))
            {
                return;
            }

            // 检查碰撞物体是否有Player标签，如果是玩家则忽略
            if (other.CompareTag("Player"))
            {
                return;
            }

            // 销毁被防反的物体
            Destroy(other.gameObject);
            full = true;
            Debug.Log("防反成功！销毁了: " + other.name);

            // 这里可以添加防反成功的特效或音效
        }
    }

    // 碰撞检测（使用Collider）
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isParrying)
        {
            GameObject other = collision.gameObject;

            // 检查碰撞物体是否是玩家子弹，如果是则忽略
            if (other.CompareTag("player bullet"))
            {
                return;
            }

            // 检查碰撞物体是否有Player标签，如果是玩家则忽略
            if (other.CompareTag("Player"))
            {
                return;
            }

            // 销毁被防反的物体
            Destroy(other);
            Debug.Log("防反成功！销毁了: " + other.name);

            // 这里可以添加防反成功的特效或音效
        }
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