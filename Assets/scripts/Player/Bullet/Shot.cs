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

    private Coroutine lifeRoutine;

    private void OnEnable()
    {
        // 发射子弹的初速度
        Vector2 direction = gameObject.GetComponent<Rigidbody2D>().velocity.normalized;
        gameObject.GetComponent<Rigidbody2D>().velocity = direction * speed;

        // 若以后用对象池复用，在 OnEnable 再次启动计时
        lifeRoutine = StartCoroutine(LifeTimer());
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

    void OnTriggerEnter2D(Collider2D other)
    {
        // 检测是否击中敌人（这里假设敌人标签为"Enemy"）
        if (other.CompareTag("Enemy"))
        {
            // 查找玩家的充能系统（改为 EnergyChargeSystem）
            GameObject player = PlayerControl.Player;
            if (player != null)
            {
                if (player.TryGetComponent<EnergyChargeSystem>(out var energySystem))
                {
                    // 增加充能
                    energySystem.AddEnergy();
                }
            }

            // 这里可以添加击中敌人的其他效果
            // Destroy(gameObject); // 如果需要销毁子弹，取消注释
        }
    }
}