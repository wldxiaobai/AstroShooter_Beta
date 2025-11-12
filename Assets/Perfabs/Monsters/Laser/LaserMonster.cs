using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LaserEnemy : MonoBehaviour
{
    [Header("激光设置")]
    public LineRenderer laserLine;           // 实际激光线条渲染器
    public LineRenderer previewLaserLine;    // 预警激光线条渲染器
    public float laserDamage = 1f;           // 激光伤害
    public float laserRange = 10f;           // 激光射程
    public float laserDuration = 0.5f;       // 激光持续时间
    public float predictionTime = 0.7f;      // 预测玩家位置的时间（秒）

    [Header("攻击设置")]
    public Transform firePoint;              // 发射点
    public float attackInterval = 3f;        // 攻击间隔
    public float preAttackDelay = 1f;        // 攻击前延迟
    public float postAttackDelay = 1f;       // 攻击后延迟

    [Header("移动设置")]
    public float moveSpeed = 2f;             // 移动速度
    public float moveRange = 2f;             // 移动范围

    private Transform player;                // 玩家引用
    private Vector3 initialPosition;         // 初始位置
    private bool isAttacking = false;        // 是否正在攻击
    private bool canAttack = true;           // 是否可以攻击
    private SpriteRenderer spriteRenderer;   // 精灵渲染器

    // 玩家位置追踪
    private Queue<Vector3> playerPositions = new Queue<Vector3>();
    private float positionRecordInterval = 0.05f; // 记录玩家位置的间隔
    private int maxPositionRecords = 15;          // 最多记录的位置数量

    // 固定激光数据
    private Vector3 fixedPredictedPosition;  // 固定的预测位置
    private Vector3 fixedLaserStart;         // 固定激光起点
    private Vector3 fixedLaserEnd;           // 固定激光终点
    private Vector2 fixedLaserDirection;     // 固定激光方向
    private bool hasFixedPrediction = false; // 是否有固定的预测位置

    void Start()
    {
        // 获取精灵渲染器
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 查找玩家
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
        {
            Debug.LogWarning("未找到tag为Player的对象！");
        }

        initialPosition = transform.position;

        // 自动查找激光子对象（如果未在Inspector中设置）
        FindLaserObjects();

        // 初始化激光
        InitializeLaser(laserLine, Color.red);
        InitializeLaser(previewLaserLine, Color.yellow);

        // 开始攻击循环和位置记录
        StartCoroutine(AttackCycle());
        StartCoroutine(RecordPlayerPositions());
    }

    void FindLaserObjects()
    {
        // 查找预警激光
        if (previewLaserLine == null)
        {
            Transform previewLaserObj = transform.Find("PreviewLaser");
            if (previewLaserObj != null)
            {
                previewLaserLine = previewLaserObj.GetComponent<LineRenderer>();
            }
        }

        // 查找实际激光
        if (laserLine == null)
        {
            Transform realLaserObj = transform.Find("RealLaser");
            if (realLaserObj != null)
            {
                laserLine = realLaserObj.GetComponent<LineRenderer>();
            }
        }

        // 查找发射点
        if (firePoint == null)
        {
            Transform firePointObj = transform.Find("FirePoint");
            if (firePointObj != null)
            {
                firePoint = firePointObj;
            }
            else
            {
                // 如果没有找到发射点，使用怪物自身的位置
                firePoint = transform;
            }
        }
    }

    void Update()
    {
        // 如果玩家不存在，返回
        if (player == null) return;

        // 更新朝向玩家
        UpdateFacingDirection();

        // 更新预测位置
        UpdatePredictedPosition();

        // 更新预警激光（仅在非攻击状态下）
        if (!isAttacking)
        {
            UpdatePreviewLaser();
        }

        // 如果在攻击状态，更新固定激光
        if (isAttacking && laserLine != null)
        {
            UpdateFixedLaser();
        }
    }

    void InitializeLaser(LineRenderer line, Color color)
    {
        if (line != null)
        {
            line.enabled = false;
            line.positionCount = 2;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = 0.1f;
            line.endWidth = 0.1f;
        }
    }

    IEnumerator RecordPlayerPositions()
    {
        while (true)
        {
            if (player != null)
            {
                // 记录玩家当前位置
                playerPositions.Enqueue(player.position);

                // 如果记录的位置超过最大数量，移除最旧的位置
                while (playerPositions.Count > maxPositionRecords)
                {
                    playerPositions.Dequeue();
                }
            }

            yield return new WaitForSeconds(positionRecordInterval);
        }
    }

    void UpdatePredictedPosition()
    {
        if (playerPositions.Count > 0)
        {
            // 获取0.7秒前的位置（根据记录间隔计算）
            int targetIndex = Mathf.Max(0, playerPositions.Count - Mathf.RoundToInt(predictionTime / positionRecordInterval));
            Vector3[] positionsArray = playerPositions.ToArray();

            if (targetIndex < positionsArray.Length)
            {
                fixedPredictedPosition = positionsArray[targetIndex];
                hasFixedPrediction = true;
            }
        }
    }

    void UpdatePreviewLaser()
    {
        // 如果玩家不存在或没有预测位置，禁用预警激光
        if (player == null || !hasFixedPrediction || previewLaserLine == null)
        {
            if (previewLaserLine != null)
                previewLaserLine.enabled = false;
            return;
        }

        // 启用预警激光
        previewLaserLine.enabled = true;

        // 设置预警激光起点
        previewLaserLine.SetPosition(0, firePoint.position);

        // 计算预警激光方向（指向预测位置）
        Vector2 laserDirection = (fixedPredictedPosition - firePoint.position).normalized;

        // 射线检测，确定预警激光终点
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, laserDirection, laserRange);
        Vector3 endPoint;

        if (hit.collider != null)
        {
            endPoint = hit.point;
        }
        else
        {
            // 如果没有击中任何物体，预警激光延伸到最大射程
            endPoint = firePoint.position + (Vector3)laserDirection * laserRange;
        }

        // 设置预警激光终点
        previewLaserLine.SetPosition(1, endPoint);
    }

    IEnumerator AttackCycle()
    {
        while (true)
        {
            // 等待攻击间隔
            yield return new WaitForSeconds(attackInterval);

            // 如果可以攻击且玩家存在，开始攻击序列
            if (canAttack && player != null)
            {
                yield return StartCoroutine(AttackSequence());
            }
        }
    }

    IEnumerator AttackSequence()
    {
        canAttack = false;

        // 在预警效果开始前，固定预测位置
        if (hasFixedPrediction)
        {
            // 保存当前的预测位置，预警和攻击都将使用这个固定位置
            Vector3 savedPrediction = fixedPredictedPosition;

            // 禁用预警激光
            if (previewLaserLine != null)
                previewLaserLine.enabled = false;

            // 攻击前预警效果（例如闪烁）
            if (spriteRenderer != null)
            {
                Color originalColor = spriteRenderer.color;
                spriteRenderer.color = Color.red;
                yield return new WaitForSeconds(0.5f);
                spriteRenderer.color = originalColor;
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }

            // 攻击前延迟
            yield return new WaitForSeconds(preAttackDelay);

            // 发射激光（使用固定的预测位置）
            ShootLaser(savedPrediction);

            // 激光持续时间
            yield return new WaitForSeconds(laserDuration);

            // 停止激光
            StopLaser();

            // 攻击后延迟
            yield return new WaitForSeconds(postAttackDelay);

            // 移动到随机位置
            yield return StartCoroutine(MoveToRandomPosition());
        }

        canAttack = true;
    }

    void ShootLaser(Vector3 predictedPosition)
    {
        if (player == null) return;

        // 计算激光方向（指向固定的预测位置）
        fixedLaserDirection = (predictedPosition - firePoint.position).normalized;

        // 射线检测，确定激光终点
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, fixedLaserDirection, laserRange);

        // 设置固定激光起点和终点
        fixedLaserStart = firePoint.position;
        if (hit.collider != null)
        {
            fixedLaserEnd = hit.point;

            // 如果击中玩家，造成伤害
            if (hit.collider.CompareTag("Player"))
            {
                //// 这里可以添加对玩家的伤害处理
                //PlayerHealth playerHealth = hit.collider.GetComponent<PlayerHealth>();
                //if (playerHealth != null)
                //{
                //    playerHealth.TakeDamage(laserDamage);
                //}
                //else
                {
                    // 如果没有生命值组件，直接销毁玩家
                    Destroy(hit.collider.gameObject);
                }
            }
        }
        else
        {
            // 如果没有击中任何物体，激光延伸到最大射程
            fixedLaserEnd = firePoint.position + (Vector3)fixedLaserDirection * laserRange;
        }

        // 启用激光
        if (laserLine != null)
        {
            laserLine.enabled = true;
            isAttacking = true;

            // 设置固定激光位置
            laserLine.SetPosition(0, fixedLaserStart);
            laserLine.SetPosition(1, fixedLaserEnd);
        }
    }

    void UpdateFixedLaser()
    {
        // 实际激光在发射后保持固定位置，不需要更新
    }

    void StopLaser()
    {
        // 禁用激光
        if (laserLine != null)
        {
            laserLine.enabled = false;
        }
        isAttacking = false;
    }

    void UpdateFacingDirection()
    {
        // 更新怪物朝向
        if (player.position.x > transform.position.x)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
        else
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
    }

    IEnumerator MoveToRandomPosition()
    {
        Vector3 startPos = transform.position;

        // 在初始位置上下范围内生成随机目标位置
        float randomY = initialPosition.y + Random.Range(-moveRange, moveRange);
        Vector3 targetPos = new Vector3(transform.position.x, randomY, transform.position.z);

        float elapsedTime = 0f;

        while (elapsedTime < 1f)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / 1f;

            // 使用平滑移动
            transform.position = Vector3.Lerp(startPos, targetPos, t);

            yield return null;
        }

        transform.position = targetPos;
    }
}