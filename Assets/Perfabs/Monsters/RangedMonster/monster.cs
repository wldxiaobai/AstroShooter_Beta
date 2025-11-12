using UnityEngine;
using System.Collections;

public class monster : MonoBehaviour
{
    [Header("射击设置")]
    public GameObject bulletPrefab;        // 子弹预制体
    public float bulletDamage = 1f;        // 子弹伤害
    public Transform firePoint;           // 发射点

    [Header("移动设置")]
    public float moveSpeed = 3f;          // 移动速度
    public float moveRange = 2f;          // 移动范围

    [Header("时间设置")]
    public float preShootDelay = 1.5f;    // 发射前硬直
    public float postShootDelay = 1.5f;   // 发射后硬直
    public float moveTime = 1f;           // 移动时间

    private Transform player;             // 玩家引用
    private Vector3 initialPosition;      // 初始位置
    private bool isActive = true;         // 是否激活

    void Start()
    {
        // 查找玩家
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
        {
            Debug.LogWarning("未找到tag为Player的对象！");
        }

        initialPosition = transform.position;

        // 开始攻击循环
        StartCoroutine(AttackCycle());
    }

    IEnumerator AttackCycle()
    {
        while (isActive)
        {
            // 发射前硬直
            yield return new WaitForSeconds(preShootDelay);

            // 发射子弹
            if (player != null)
            {
                Shoot();
            }

            // 发射后硬直
            yield return new WaitForSeconds(postShootDelay);

            // 移动到随机位置
            yield return StartCoroutine(MoveToRandomPosition());
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null || player == null) return;

        // 计算射击方向
        Vector2 shootDirection = (player.position - firePoint.position).normalized;

        // 实例化子弹
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        // 设置子弹方向和伤害
        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
        {
            bulletComponent.direction = shootDirection;
            bulletComponent.damage = bulletDamage;
        }
    }

    IEnumerator MoveToRandomPosition()
    {
        Vector3 startPos = transform.position;

        // 在初始位置上下范围内生成随机目标位置
        float randomY = initialPosition.y + Random.Range(-moveRange, moveRange);
        Vector3 targetPos = new Vector3(transform.position.x, randomY, transform.position.z);

        float elapsedTime = 0f;

        while (elapsedTime < moveTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / moveTime;

            // 使用平滑移动
            transform.position = Vector3.Lerp(startPos, targetPos, t);

            yield return null;
        }

        transform.position = targetPos;
    }

    void OnDrawGizmosSelected()
    {
        // 在Scene视图中显示移动范围
        Gizmos.color = Color.yellow;
        Vector3 currentPos = Application.isPlaying ? initialPosition : transform.position;
        Gizmos.DrawLine(
            new Vector3(currentPos.x - 1f, currentPos.y + moveRange, currentPos.z),
            new Vector3(currentPos.x + 1f, currentPos.y + moveRange, currentPos.z)
        );
        Gizmos.DrawLine(
            new Vector3(currentPos.x - 1f, currentPos.y - moveRange, currentPos.z),
            new Vector3(currentPos.x + 1f, currentPos.y - moveRange, currentPos.z)
        );
    }
}