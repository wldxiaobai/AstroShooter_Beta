using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shot : MonoBehaviour
{
    [Header("最大存活时间（毫秒）")]
    [SerializeField] private int maxExistTime = 500;
    [Header("伤害")]
    [SerializeField] private int damage = 50;
    [Header("速度")]
    [SerializeField] private float speed = 10f;
    [Header("穿透数")]
    [SerializeField] private int pierceCount = 1;
    [Header("有效伤害时间间隔（单位：毫秒）")]
    [SerializeField] private int damageInterval = 200;
    [Header("音效")]
    [SerializeField] private AudioClip hitSound;

    private Coroutine lifeRoutine;

    // 记录每个敌人上次被此子弹击中的时间（秒）
    private readonly Dictionary<EnemySet, float> lastHitTimes = new();
    private float damageIntervalSec;

    private void OnEnable()
    {
        // 发射子弹的初速度
        Vector2 direction = gameObject.GetComponent<Rigidbody2D>().velocity.normalized;
        gameObject.GetComponent<Rigidbody2D>().velocity = direction * speed;

        // 重置命中记录（对象池复用时）
        lastHitTimes.Clear();
        damageIntervalSec = damageInterval / 1000f;

        // 若以后用对象池复用，在 OnEnable 再次启动计时
        lifeRoutine = StartCoroutine(LifeTimer());

        // 播放发射音效（可选）
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }
    }

    private void OnDisable()
    {
        // 复用时清理协程
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
    }

    private IEnumerator LifeTimer()
    {
        // 如果需要忽略 Time.timeScale 可改为 WaitForSecondsRealtime
        yield return new WaitForSeconds(maxExistTime / 1000f);
        Destroy(gameObject);
    }

    private void TryAddEnergy()
    {
        GameObject player = PlayerControl.Player;
        if (player != null && player.TryGetComponent<EnergyChargeSystem>(out var energySystem) && energySystem != null)
        {
            energySystem.AddEnergy();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 检测是否击中敌人（这里假设敌人标签为"Enemy"）
        if (!other.CompareTag("Enemy"))
            return;

        // 兼容敌人组件在父物体上的情况
        var enemy = other.GetComponentInParent<EnemySet>();
        if (enemy == null)
            return;

        float now = Time.time;

        // 间隔判定：同一敌人两次受伤需间隔 damageInterval
        if (lastHitTimes.TryGetValue(enemy, out float lastTime))
        {
            if (now - lastTime < damageIntervalSec)
            {
                // 未到伤害间隔，不计伤害、不消耗穿透
                return;
            }
        }

        // 造成伤害并记录时间
        enemy.Hurt(damage);
        lastHitTimes[enemy] = now;
        Debug.Log($"[Shot] Bullet hit enemy {enemy.gameObject.name}, dealt {damage} damage.");

        // 成功命中才增加充能
        TryAddEnergy();

        // 消耗一次穿透
        pierceCount--;
        if (pierceCount <= 0)
        {
            Destroy(gameObject);
        }
        // 若仍有穿透数，子弹继续存在以命中后续敌人
    }
}