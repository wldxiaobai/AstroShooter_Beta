using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("子弹设置")]
    public Vector2 direction = Vector2.right;  // 子弹方向
    public float speed = 8f;                   // 子弹速度
    public float damage = 1f;                  // 子弹伤害
    public float lifeTime = 3f;                // 生存时间

    [Header("视觉效果")]
    public GameObject hitEffect;               // 击中特效

    void Start()
    {
        // 设置子弹tag
        gameObject.tag = "Bullet";

        // 自动销毁
        Destroy(gameObject, lifeTime);

        // 设置子弹朝向
        if (direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    void Update()
    {
        // 直线移动
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    //    void OnTriggerEnter2D(Collider2D collision)
    //    {
    //        // 忽略与发射者的碰撞（如果需要）
    //        // if (collision.CompareTag("Enemy")) return;

    //        // 对玩家造成伤害
    //        if (collision.CompareTag("Player"))
    //        {
    //            // 这里可以添加对玩家的伤害处理
    //            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
    //            if (playerHealth != null)
    //            {
    //                playerHealth.TakeDamage(damage);
    //            }

    //            // 播放击中特效
    //            if (hitEffect != null)
    //            {
    //                Instantiate(hitEffect, transform.position, Quaternion.identity);
    //            }

    //            Destroy(gameObject);
    //        }
    //        // 碰到墙壁或其他障碍物也销毁
    //        else if (!collision.isTrigger && !collision.CompareTag("Bullet"))
    //        {
    //            if (hitEffect != null)
    //            {
    //                Instantiate(hitEffect, transform.position, Quaternion.identity);
    //            }
    //            Destroy(gameObject);
    //        }
    //    }
    //}

    //临时使用 一触即死
    void OnTriggerEnter2D(Collider2D collision)
    {
        // 忽略与发射者的碰撞（如果需要）
        // if (collision.CompareTag("Enemy")) return;

        // 对玩家造成伤害
        if (collision.CompareTag("Player"))
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Destroy(player);
            }

            // 播放击中特效
            if (hitEffect != null)
            {
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
        }
    }
