using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shot : MonoBehaviour
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
}