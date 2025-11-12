
using UnityEngine;

public class EnergyChargeSystem : MonoBehaviour
{
    [Header("充能设置")]
    public int maxEnergy = 100;                    // 最大充能值
    public int energyPerHit = 1;                   // 每次击中敌人获得的能量
    public KeyCode launchKey = KeyCode.E;          // 发射按键
    public GameObject hugeBulletPrefab;            // 巨大子弹预制体
    public Transform firePoint;                    // 发射点
    [SerializeField] private bool _debugFull = false; // 调试用，启动时是否满能量

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
        // 调试用，启动时满能量
        if (_debugFull) currentEnergy = maxEnergy;

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
            // 实例化子弹并记录在shot变量中以便后续使用
            var shot = Instantiate(
                hugeBulletPrefab,
                transform.position,
                Quaternion.identity
                );

            // 计算鼠标位置与玩家位置的偏移量
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float deltaX = mouseWorld.x - transform.position.x;
            float deltaY = mouseWorld.y - transform.position.y;

            // 计算角度并设置子弹旋转
            float angle = Mathf.Atan2(deltaY, deltaX) * Mathf.Rad2Deg;
            shot.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 给子弹添加初速度
            Rigidbody2D rb = shot.GetComponent<Rigidbody2D>();
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0;
            rb.velocity = new Vector2(deltaX, deltaY).normalized * 15f;

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