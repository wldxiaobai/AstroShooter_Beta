using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestShot : MonoBehaviour
{
    [Header("最大存活时间（毫秒）")]
    [SerializeField] private int maxExistTime = 500;

    private Coroutine lifeRoutine;

    private void OnEnable()
    {
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
        // 检测是否击中玩家
        if (other.CompareTag("Player"))
        {
            PlayerControl.GetHurt(1);
            Debug.Log("[TestShot] Hit Player, dealt 1 damage.");
        }
    }
}