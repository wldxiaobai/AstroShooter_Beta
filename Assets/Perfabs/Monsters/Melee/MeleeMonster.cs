using UnityEngine;
//tips:有两个点需要改动，一是把destroy玩家改成扣生命值；
//二是增加小怪生命值和硬直
public class MeleeEnemy : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 3f;          // 移动速度

    private Transform player;             // 玩家引用
    private Rigidbody2D rb;               // 刚体组件

    void Start()
    {
        // 获取组件
        rb = GetComponent<Rigidbody2D>();

        // 查找玩家
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
        {
            Debug.LogWarning("未找到tag为Player的对象！");
        }

        // 设置刚体不受重力影响
        if (rb != null)
        {
            rb.gravityScale = 0;
        }
    }

    void Update()
    {
        // 如果玩家不存在，停止移动
        if (player == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 计算朝向玩家的方向
        Vector2 direction = (player.position - transform.position).normalized;

        // 朝玩家移动
        rb.velocity = direction * moveSpeed;

        // 更新怪物朝向
        UpdateFacingDirection(direction);
    }

    void UpdateFacingDirection(Vector2 direction)
    {
        // 根据移动方向更新怪物朝向
        if (direction.x != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction.x > 0 ? 1 : -1);
            transform.localScale = scale;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 如果碰撞到玩家，销毁玩家
        if (collision.gameObject.CompareTag("Player"))
        {
            Destroy(collision.gameObject);
        }
    }
}