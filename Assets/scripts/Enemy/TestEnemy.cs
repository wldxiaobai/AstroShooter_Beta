using System.Collections;
using UnityEngine;

/// <summary>
/// 测试敌人：
/// - 拥有两个技能：朝玩家发射子弹（Shoot）、朝玩家冲刺（Dash）。
/// - 根据与玩家的距离动态调整权重：近距离更倾向冲刺，远距离更倾向射击。
/// - 死亡时实例化爆炸特效。
/// 依赖基类 EnemySet 的协程时序与钩子：
/// - 在 Start 中向 SkillList 注册技能；
/// - 通过 AdjustSkillWeights 动态修改权重；
/// - 在 OnSkillWillExecute 准备冲刺起止点；
/// - 在 SkillAction 中触发具体行为（发射子弹、启动冲刺协程）；
/// - 在 DieEffectRoutine 中播放爆炸。
/// </summary>
public class TestEnemy : EnemySet
{
    [Header("射击参数")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 12f;

    [Header("冲刺参数")]
    [SerializeField] private float dashDistance = 4f;
    [SerializeField] private float dashTime = 0.25f;
    [SerializeField] private float preferDashDistance = 10.0f; // 小于此距离更偏向冲刺

    [Header("Idle 接近参数")]
    [SerializeField] private float idleApproachSpeed = 2.0f;        // Idle 时的靠近速度（单位/秒）
    [SerializeField] private float idleApproachStopDistance = 6.0f; // 若距离小于等于该值则不再靠近

    [Header("死亡效果")]
    [SerializeField] private GameObject explosionPrefab;   // 爆炸特效（可选）
    [SerializeField] private AudioClip explosionSfx;       // 爆炸音效（可选）
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField] private bool disableCollidersOnDeath = true;

    [Header("变红+抖动参数")]
    [SerializeField] private float shakeDuration = 0.35f;  // 抖动持续时间
    [SerializeField] private float shakeIntensity = 0.25f; // 抖动强度（单位：世界坐标）
    [SerializeField] private Color targetRed = new(1f, 0f, 0f, 1f); // 目标颜色（红色）

    // 计划中的冲刺起止点（在 OnSkillWillExecute 中计算）
    private Vector3 plannedDashStart;
    private Vector3 plannedDashEnd;

    protected override void StartApply()
    {
        // 注册技能（注意：先注册，再调用 base.Start() 开启 AI 循环）
        SkillList.Add(new EnemySkill(
            name: "Shoot",
            action: Shoot,
            weight: 5,
            preDelay: 0.1f,
            postDelay: 0.05f,
            cooldown: 0.8f));

        SkillList.Add(new EnemySkill(
            name: "Dash",
            action: Dash,
            weight: 2,
            preDelay: 0.05f,
            postDelay: dashTime, // 冲刺时长放在后摇中等待（移动在协程里执行）
            cooldown: 3.0f));
    }

    // 根据距离动态调整当前轮权重（越近越倾向冲刺，越远越倾向射击）
    protected override void AdjustSkillWeights()
    {
        Transform player = PlayerControl.Player ? PlayerControl.Player.transform : null;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        foreach (var s in SkillList)
        {
            if (s.Name == "Shoot")
            {
                // 距离远 -> 提高射击概率；距离近 -> 降低
                s.Weight = (dist > 8f) ? 8 : (dist > 3.5f ? 5 : 2);
            }
            else if (s.Name == "Dash")
            {
                // 距离近 -> 提高冲刺概率；距离远 -> 降低
                s.Weight = (dist < preferDashDistance) ? 8 : (dist < 5.5f ? 4 : 1);
            }
        }
    }

    // 如距离极近且 Dash 可用，则强制 Dash（可选）
    protected override EnemySkill GetForcedSkill()
    {
        Transform player = PlayerControl.Player ? PlayerControl.Player.transform : null;
        if (player == null) return null;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist < 2f)
        {
            foreach (var s in SkillList)
                if (s.Name == "Dash" && s.CanUse) return s;
        }
        return null;
    }

    // 主体即将执行时，准备冲刺的起止点
    protected override void OnSkillWillExecute(EnemySkill skill)
    {
        if (skill.Name != "Dash") return;

        Vector3 start = transform.position;
        Vector2 dir = GetAimDirNormalized();
        Vector3 end = start + (Vector3)(dir * dashDistance);

        plannedDashStart = start;
        plannedDashEnd = end;
    }

    // Idle 阶段：缓慢靠近玩家，直到距离小于等于 idleApproachStopDistance
    protected override void OnIdleUpdate(float deltaTime)
    {
        Transform player = PlayerControl.Player ? PlayerControl.Player.transform : null;
        if (player == null) return;

        // 计算指向玩家的方向与距离
        Vector2 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude - idleApproachStopDistance;
        Vector2 dir = GetAimDirNormalized() * dist;
        if (dir.magnitude == 0) return;

        // 增加速度，限制最大值
        Vector2 vel = gameObject.GetComponent<Rigidbody2D>().velocity;
        vel += dir;
        if(vel.magnitude >= idleApproachSpeed)
        {
            vel = vel.normalized * idleApproachSpeed;
        }
        gameObject.GetComponent<Rigidbody2D>().velocity = vel;
    }

    // 发射子弹（立即生效的动作）
    private void Shoot()
    {
        if (bulletPrefab == null) return;

        Vector2 dir = GetAimDirNormalized();
        var bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

        // 设置朝向/速度（优先使用刚体）
        if (bullet.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.velocity = dir * bulletSpeed;
        }
        else
        {
            bullet.transform.right = dir;
            bullet.transform.position += (Vector3)(dir * 0.1f);
        }
    }

    // 启动冲刺协程（位移实际在 DashRoutine 中逐帧完成）
    private void Dash()
    {
        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        float t = 0f;
        while (t < dashTime)
        {
            // 若被打断/死亡则立即结束位移
            if (state == EnemyState.Stunned || state == EnemyState.Dead)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dashTime);
            transform.position = Vector3.Lerp(plannedDashStart, plannedDashEnd, k);
            yield return null;
        }
    }

    // 死亡时爆炸（可接入粒子/动画）
    protected override IEnumerator DieEffectRoutine()
    {
        // 1) 可选：禁用全部碰撞，防止尸体与场景交互
        if (disableCollidersOnDeath)
        {
            var cols = GetComponentsInChildren<Collider2D>(includeInactive: true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
        }

        // 2) 逐渐变红 + 剧烈抖动
        var srs = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        var originalColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
            originalColors[i] = srs[i].color;

        Vector3 basePos = transform.position;
        float t = 0f;
        while (t < shakeDuration)
        {
            float p = Mathf.Clamp01(t / shakeDuration);

            // 颜色插值到红色（保持原透明度）
            for (int i = 0; i < srs.Length; i++)
            {
                var from = originalColors[i];
                var to = new Color(targetRed.r, targetRed.g, targetRed.b, from.a);
                srs[i].color = Color.Lerp(from, to, p);
            }

            // 剧烈抖动（随机位移），如需减弱可乘 (1f - p)
            Vector2 offset = Random.insideUnitCircle * shakeIntensity;
            transform.position = basePos + (Vector3)offset;

            t += Time.deltaTime;
            yield return null;
        }

        // 复位位置 & 确保最终为红色
        transform.position = basePos;
        for (int i = 0; i < srs.Length; i++)
        {
            var c = srs[i].color;
            srs[i].color = new Color(targetRed.r, targetRed.g, targetRed.b, c.a);
        }

        // 3) 触发爆炸特效与音效
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        if (explosionSfx != null)
            AudioSource.PlayClipAtPoint(explosionSfx, transform.position, sfxVolume);

        // 4) 让特效创建完毕（基类根据 AutoDestroyOnDeath 决定销毁）
        yield return null;
    }

    // 计算指向玩家的单位方向，若无玩家则返回右方向
    private Vector2 GetAimDirNormalized()
    {
        Transform player = PlayerControl.Player ? PlayerControl.Player.transform : null;
        if (player == null) return Vector2.right;
        Vector2 dir = (player.position - transform.position);
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
    }
}