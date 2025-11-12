using UnityEngine;
using System.Collections;

public class Dash : MonoBehaviour
{
    [Header("冲刺设置")]
    public float dashDistance = 5f;          // 冲刺距离
    public float dashDuration = 0.2f;        // 冲刺持续时间
    public float cooldown = 0.5f;            // 冷却时间
    public LayerMask obstacleLayers;         // 障碍物层级

    [Header("无敌效果")]
    public Material invincibleMaterial;      // 无敌时的材质（虚化效果）
    private Material originalMaterial;       // 原始材质

    private bool isDashing = false;          // 是否正在冲刺
    private bool isCooldown = false;         // 是否在冷却中
    private float dashTimer = 0f;            // 冲刺计时器
    private SpriteRenderer playerRenderer;   // 玩家精灵渲染组件
    private Collider2D playerCollider;       // 玩家2D碰撞体
    private Vector2 lastMoveDirection;       // 记录最后移动方向
    private Rigidbody2D rb;                  // 2D刚体组件
    LevelControl lc = LevelControl.Instance; // 获取关卡控制单例
    void Start()
    {
       
        if (!lc.IsLevelCompleted("AsteroidBelt"))
        {
            // 前置关卡完成，设置为可交互
            this.enabled = false;
        }
        // 获取组件引用
        playerRenderer = GetComponent<SpriteRenderer>();
        playerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        // 保存原始材质
        if (playerRenderer != null)
        {
            originalMaterial = playerRenderer.material;
        }

        // 如果没有指定无敌材质，创建一个半透明的默认材质
        if (invincibleMaterial == null)
        {
            CreateDefaultInvincibleMaterial();
        }

        // 初始化移动方向为右方
        lastMoveDirection = Vector2.right;
    }

    void Update()
    {
        // 更新移动方向
        UpdateMoveDirection();

        // 检测空格键按下且不在冷却中
        if (Input.GetKeyDown(KeyCode.Space) && !isDashing && !isCooldown)
        {
            StartDash();
        }

        // 冲刺状态更新
        if (isDashing)
        {
            UpdateDash();
        }
    }

    void UpdateMoveDirection()
    {
        // 获取输入方向
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 如果有输入，更新最后移动方向
        Vector2 inputDirection = new Vector2(horizontal, vertical);
        if (inputDirection.magnitude > 0.1f)
        {
            lastMoveDirection = inputDirection.normalized;

            // 如果角色有精灵渲染器，可以根据方向翻转精灵
            if (playerRenderer != null && horizontal != 0)
            {
                playerRenderer.flipX = horizontal < 0;
            }
        }

        // 如果没有输入但有速度，使用速度方向
        else if (rb != null && rb.velocity.magnitude > 0.1f)
        {
            lastMoveDirection = rb.velocity.normalized;
        }
    }

    void StartDash()
    {
        // 如果没有移动方向，使用角色右方
        if (lastMoveDirection == Vector2.zero)
        {
            lastMoveDirection = Vector2.right;
        }

        // 开始冲刺协程
        StartCoroutine(PerformDash(lastMoveDirection));
    }

    IEnumerator PerformDash(Vector2 direction)
    {
        isDashing = true;
        isCooldown = true;

        // 保存当前速度（如果有刚体）
        Vector2 originalVelocity = Vector2.zero;
        if (rb != null)
        {
            originalVelocity = rb.velocity;
            rb.velocity = Vector2.zero; // 停止当前移动
        }

        // 启用无敌状态
        if (lc.IsLevelCompleted("LightningPlanet"))
        {
            // 前置关卡完成，设置为可交互
            SetInvincible(true);
        }
        

        Vector2 startPosition = transform.position;
        Vector2 targetPosition = CalculateDashTargetPosition(startPosition, direction);
        float elapsedTime = 0f;

        // 冲刺移动
        while (elapsedTime < dashDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / dashDuration;

            // 使用平滑移动
            transform.position = Vector2.Lerp(startPosition, targetPosition, progress);

            yield return null;
        }

        // 确保最终位置准确
        transform.position = targetPosition;

        // 结束冲刺
        isDashing = false;
        SetInvincible(false);

        // 恢复速度（如果有刚体）
        if (rb != null)
        {
            rb.velocity = originalVelocity;
        }

        // 开始冷却
        StartCoroutine(StartCooldown());
    }

    Vector2 CalculateDashTargetPosition(Vector2 startPos, Vector2 direction)
    {
        Vector2 targetPos = startPos + direction * dashDistance;

        // 检测冲刺路径上的障碍物
        RaycastHit2D hit = Physics2D.Raycast(startPos, direction, dashDistance, obstacleLayers);
        if (hit.collider != null)
        {
            // 如果有障碍物，停在障碍物前一小段距离
            targetPos = hit.point - direction * 0.3f;
        }

        return targetPos;
    }

    void SetInvincible(bool invincible)
    {
        // 视觉效果
        if (playerRenderer != null && invincibleMaterial != null && originalMaterial != null)
        {
            playerRenderer.material = invincible ? invincibleMaterial : originalMaterial;
        }

        // 方法2：通过禁用碰撞体实现无敌（简单但可能影响其他碰撞）
        if (playerCollider != null)
        {
            playerCollider.enabled = !invincible;
        }

        // 方法3：使用标签而不是图层
        // gameObject.tag = invincible ? "Invincible" : "Player";
    }

    IEnumerator StartCooldown()
    {
        yield return new WaitForSeconds(cooldown);
        isCooldown = false;
    }

    void UpdateDash()
    {
        dashTimer += Time.deltaTime;
        if (dashTimer >= dashDuration)
        {
            dashTimer = 0f;
        }
    }

    void CreateDefaultInvincibleMaterial()
    {
        // 创建一个简单的半透明材质用于无敌效果
        invincibleMaterial = new Material(Shader.Find("Sprites/Default"));
        invincibleMaterial.color = new Color(1, 1, 1, 0.5f); // 半透明
    }

    // 可视化调试（在Scene视图中显示冲刺方向）
    void OnDrawGizmosSelected()
    {
        // 显示移动方向
        Gizmos.color = Color.blue;
        Vector3 direction3D = new Vector3(lastMoveDirection.x, lastMoveDirection.y, 0);
        Gizmos.DrawRay(transform.position, direction3D * 2f);

        // 显示冲刺状态
        if (Application.isPlaying && isDashing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
        else if (!isCooldown)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
        else
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }

    // 公共方法，用于UI显示冷却状态
    public bool IsOnCooldown()
    {
        return isCooldown;
    }

    public float GetCooldownProgress()
    {
        return isCooldown ? 0f : 1f;
    }
}