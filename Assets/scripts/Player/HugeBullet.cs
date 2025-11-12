using UnityEngine;

public class HugeBullet : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 10;
    public float lifetime = 5f;

    void Start()
    {
        // 自动销毁，避免内存泄漏
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // 向前移动
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 击中敌人造成伤害
        if (other.CompareTag("Enemy"))
        {
            // 这里可以添加伤害敌人的代码
            Debug.Log($"巨大子弹击中敌人，造成{damage}点伤害");

            // 可以添加爆炸效果等
            Destroy(gameObject);
        }

        // 忽略玩家和玩家子弹
        if (other.CompareTag("Player") || other.CompareTag("player bullet"))
        {
            return;
        }
    }
}