using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shooting : MonoBehaviour
{
    [SerializeField] private GameObject shotPrefab;
    [SerializeField] private float shotCD = 0.2f;

    // 推荐：将此键常量化，确保同一武器统一使用
    private const string PrimaryFireKey = "PrimaryFire";

    private bool GetMouseButtonGivenCD(int button)
    {
        // 按住期间尝试触发；首发立即；后续依赖 FireCDManager 控制节奏
        if (!Input.GetMouseButton(button))
            return false;

        // 如果想在暂停时仍能连射，把 TryFire 第三个参数设为 true
        return FireCDManager.TryFire(PrimaryFireKey, shotCD);
    }

    // Update is called once per frame
    void Update()
    {
        // 计算鼠标位置与玩家位置的偏移量
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float deltaX = mouseWorld.x - transform.position.x;
        float deltaY = mouseWorld.y - transform.position.y;

        // 计算角度并设置玩家旋转
        float angle = Mathf.Atan2(deltaY, deltaX) * Mathf.Rad2Deg;
        gameObject.transform.rotation = Quaternion.Euler(0, 0, angle);

        if (GetMouseButtonGivenCD(0))
        {
            // 实例化子弹并记录在shot变量中以便后续使用
            var shot = Instantiate(
                shotPrefab, 
                transform.position, 
                Quaternion.identity
                );

            // 计算角度并设置子弹旋转
            shot.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 给子弹添加初速度
            Rigidbody2D rb = shot.GetComponent<Rigidbody2D>();
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0;
            rb.velocity = new Vector2(deltaX, deltaY).normalized * 40f;
        }

        // 如果需要 UI 显示剩余冷却，可读取：
        // float remain = FireCDManager.GetRemaining(PrimaryFireKey);
    }
}
