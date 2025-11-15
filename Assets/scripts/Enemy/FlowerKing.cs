using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowerKing : EnemySet
{
    [Header("引用与预制体（在 Inspector 里绑定）")]
    public GameObject pollenPrefab;      // 花粉视觉（漂浮效果）
    public GameObject thornPrefab;       // 荆棘（可能是触发器或可视表示）
    public GameObject leafPrefab;        // 叶片（旋转后飞向玩家）
    public GameObject tentaclePrefab;    // 条形触须（长条碰撞）
    public GameObject beePrefab;         // 蜜蜂（Prefab 可包含外观、Animator、Collider、Rigidbody2D）
    public Transform spawnRoot;          // 生成的父节点（可空）

    [Header("技能参数")]
    public int bladesCount = 5;
    public float bladesRadius = 2.2f;
    public float bladesRotateSpeed = 120f; // deg/sec
    public float bladesCloseDistance = 4f; // 距离玩家足够近时发射
    public float pollenFallTime = 0.7f;
    public float tentacleRange = 6f;
    public float dashSpeed = 18f;
    public float windupTime = 0.6f;
    public int beesPerPollen = 3;

    [Header("蜜蜂参数")]
    public int beeMaxHP = 3;              // Inspector 可设置：蜜蜂血量
    public int beeDamageToPlayer = 1;     // Inspector 可设置：蜜蜂碰到玩家造成的伤害
    public int beeHealAmount = 30;        // 每只回到 Boss 时给 Boss 加的血量（保留变量但不再生效）

    [Header("提示/警告")]
    public GameObject redIndicatorPrefab; // 红色矩形提示 prefab（在 thorn 生成前显示）
    public float indicatorWarnTime = 0.6f; // 提示持续时间（秒）

    // 内部状态
    bool stunnedAt60 = false;
    bool stunnedAt30 = false;
    float originalDecisionInterval;
    bool preventHeavyNext = false; // 避免 2/4 连放

    Transform playerT;
    Camera cam;

    // 新增：技能互斥标记和包装器（确保一次只能有一个技能协程在场上运行）
    private bool skillActive = false;

    // 修复：上次技能索引（避免未声明错误）
    private int lastSkillIndex = -1;

    // 巡逻 / 自由移动参数（技能间隙移动）
    public float patrolSpeed = 2.2f;              // 中等速度 (朝玩家的偏移移动速度)
    public float patrolChangeInterval = 3.5f;     // 多久换一次目标
    private Vector3 patrolTarget = Vector3.zero;
    private float patrolTimer = 0f;

    // 取消后坐力相关：如果场景中子弹等对 Boss 施加物理力，这里将使 Boss 使用 Kinematic 以忽略外力
    [Header("调试 / 重力")]
    public bool disableRecoil = true; // Inspector 可选择是否禁用外力推开
    private Rigidbody2D rb;

    // 新增：与玩家的最大允许距离（单位：world units）
    [Header("行为约束")]
    public float maxPlayerDistance = 4.8f;

    protected override void StartApply()
    {
        cam = Camera.main;
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null) playerT = playerGo.transform;

        originalDecisionInterval = decisionInterval;

        // 初始化巡逻目标为当前点（并强制在摄像机与玩家范围内）
        patrolTarget = ClampToCameraViewport(EnforcePlayerDistance(transform.position));

        // 获取 Rigidbody2D 并按需禁用外力（取消后坐力）
        rb = GetComponent<Rigidbody2D>();
        if (rb != null && disableRecoil)
        {
            // 使用 Kinematic 避免被外力推动；并清零速度避免残留动量
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 注册技能（Name 用于调试）
        // 使用 TryStartSkillCoroutine 通过 skillActive 保证互斥
        SkillList.Add(new EnemySkill("PollenThorn", () => TryStartSkillCoroutine(Skill_PollenThorns()), weight: 3, preDelay: 0.6f, postDelay: 0.6f, cooldown: 3f)); // 技能1
        SkillList.Add(new EnemySkill("RotatingBlades", () => TryStartSkillCoroutine(Skill_BladesOrbit()), weight: 2, preDelay: 0.8f, postDelay: 0.6f, cooldown: 5f)); // 技能2
        SkillList.Add(new EnemySkill("CrossTentacles", () => TryStartSkillCoroutine(Skill_TentaclesBurst()), weight: 3, preDelay: 0.5f, postDelay: 0.7f, cooldown: 4f)); // 技能3
        SkillList.Add(new EnemySkill("CornerDashBees", () => TryStartSkillCoroutine(Skill_DashSpawnPollenBees()), weight: 1, preDelay: 0.8f, postDelay: 0.8f, cooldown: 8f)); // 技能4

        // 花粉独立技能（如果需要）
        SkillList.Add(new EnemySkill("PollenField", () => TryStartSkillCoroutine(Skill_PollenAndBees()), weight: 2, preDelay: 0.6f, postDelay: 0.6f, cooldown: 6f));
    }

    // 尝试启动技能主体协程；若已有技能在执行则返回 false 并跳过
    private bool TryStartSkillCoroutine(IEnumerator co)
    {
        if (skillActive)
        {
            Debug.Log("[FlowerKing] 已有技能进行中，跳过本次技能启动。");
            return false;
        }
        skillActive = true;
        StartCoroutine(SkillCoroutineWrapper(co));
        return true;
    }

    // 技能主体包装：运行完后清除标记
    private IEnumerator SkillCoroutineWrapper(IEnumerator co)
    {
        yield return StartCoroutine(co);
        skillActive = false;
    }

    void OnDisable()
    {
        skillActive = false;
    }

    void OnDestroy()
    {
        skillActive = false;
    }

    // 动态权重：若上次放了旋转叶片或大招（index 1/3），禁止下一次选择这些技能
    protected override void AdjustSkillWeights()
    {
        if (preventHeavyNext)
        {
            foreach (var s in SkillList)
            {
                if (s.Name == "RotatingBlades" || s.Name == "CornerDashBees")
                    s.Weight = 0;
            }
        }
    }

    // 强制技：当血量首次低于 60% / 30% 时触发僵直
    protected override EnemySkill GetForcedSkill()
    {
        if (!stunnedAt60 && health <= maxHealth * 0.6f)
        {
            stunnedAt60 = true;
            ApplyStun(4f, interruptCurrentSkill: true);
            // 缩短决策间隔（在 Stun 后进行）
            decisionInterval *= 0.75f;
        }
        if (!stunnedAt30 && health <= maxHealth * 0.3f)
        {
            stunnedAt30 = true;
            ApplyStun(4f, interruptCurrentSkill: true);
            decisionInterval *= 0.75f;
        }
        return null;
    }

    protected override void OnIdleEnter() { }

    float idleTimer = 0f;
    Vector3 idleBasePos;
    protected override void OnIdleUpdate(float deltaTime)
    {
        if (idleBasePos == Vector3.zero) idleBasePos = transform.position;
        idleTimer += deltaTime;

        // 确保摄像机存在
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            // fallback 只做轻微抖动
            float bx = Mathf.Sin(idleTimer * 0.6f) * 0.3f;
            float by = Mathf.Sin(idleTimer * 0.9f) * 0.2f;
            transform.position = ClampToCameraViewport(EnforcePlayerDistance(idleBasePos + new Vector3(bx, by, 0)));
            return;
        }

        // 技能间隙巡逻移动（只在没有技能执行时进行）
        patrolTimer += deltaTime;
        if (!skillActive)
        {
            // 计算摄像机边界（世界坐标）
            float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
            Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, zDist));
            Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, zDist));

            // 选择以玩家方向为主的目标：在当前点与玩家间取一个偏向玩家的点，并加小随机偏移
            Vector3 targetBase;
            if (playerT != null)
            {
                // 0.35~0.6 的插值让移动整体朝玩家，但不直接贴近玩家
                float bias = UnityEngine.Random.Range(0.35f, 0.6f);
                targetBase = Vector3.Lerp(transform.position, playerT.position, bias);
            }
            else
            {
                targetBase = transform.position;
            }

            // 加入少量随机偏移以使轨迹更自然
            float randR = UnityEngine.Random.Range(0.3f, 1.1f);
            float randAng = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 randOffset = new Vector3(Mathf.Cos(randAng), Mathf.Sin(randAng), 0) * randR * 0.3f;

            Vector3 candidate = targetBase + randOffset;

            // 强制使 candidate 与玩家的距离不超过 maxPlayerDistance
            candidate = EnforcePlayerDistance(candidate);

            // 如果目标无效或到达或超时，选新目标（带边距）
            if (patrolTarget == Vector3.zero || Vector3.Distance(transform.position, patrolTarget) < 0.25f || patrolTimer >= patrolChangeInterval)
            {
                patrolTimer = 0f;
                float margin = 0.06f;
                // 将 candidate 投影到 viewport 范围内，避免越界
                Vector3 vp = cam.WorldToViewportPoint(candidate);
                vp.x = Mathf.Clamp(vp.x, margin, 1f - margin);
                vp.y = Mathf.Clamp(vp.y, margin, 1f - margin);
                vp.z = zDist;
                Vector3 candWorld = cam.ViewportToWorldPoint(vp);
                // 最后以摄像机边界微留白限制位置
                candWorld.x = Mathf.Clamp(candWorld.x, bl.x + 0.1f, tr.x - 0.1f);
                candWorld.y = Mathf.Clamp(candWorld.y, bl.y + 0.1f, tr.y - 0.1f);

                // 再次 enforce 与玩家距离，防止 viewport 投影把位置推远
                patrolTarget = EnforcePlayerDistance(candWorld);
            }

            // 平滑移动到巡逻点（MoveTowards 结合 deltaTime 保证帧率独立）
            Vector3 basePos = Vector3.MoveTowards(transform.position, patrolTarget, patrolSpeed * deltaTime);

            // 轻微上下/左右浮动叠加（基于全局 timer）
            float bx = Mathf.Sin(idleTimer * 0.6f) * 0.12f;
            float by = Mathf.Sin(idleTimer * 0.9f) * 0.08f;
            transform.position = ClampToCameraViewport(EnforcePlayerDistance(basePos + new Vector3(bx, by, 0)));

            // 更新 idleBasePos 为当前位置基点（用于被打断时恢复）
            idleBasePos = transform.position;
        }
        else
        {
            // 如果正在施放技能，保留轻微漂浮但不要强制定位（防止瞬移）
            float bx = Mathf.Sin(idleTimer * 0.6f) * 0.12f;
            float by = Mathf.Sin(idleTimer * 0.9f) * 0.08f;
            transform.position = ClampToCameraViewport(EnforcePlayerDistance(idleBasePos + new Vector3(bx, by, 0)));
        }
    }

    protected override void OnIdleExit()
    {
        // 记录当前位置为新的基础点
        idleBasePos = transform.position;
    }

    // 技能实现 ----
    IEnumerator Skill_PollenThorns()
    {
        preventHeavyNext = false;

        if (cam == null) cam = Camera.main;
        // 计算摄像机左右世界坐标用于随机选 X（保持在可视水平内）
        float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
        float left = cam.ViewportToWorldPoint(new Vector3(0f, 0f, zDist)).x;
        float right = cam.ViewportToWorldPoint(new Vector3(1f, 0f, zDist)).x;
        float x = Mathf.Lerp(left, right, UnityEngine.Random.value);

        // 计算底部 Y（准确映射摄像机底边）
        float bottomY = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, zDist)).y;
        Vector3 thornPos = new Vector3(x, bottomY + 0.12f, 0);

        // 生成红色矩形提示（如果设置了 prefab）
        GameObject indicator = null;
        if (redIndicatorPrefab != null)
        {
            // indicator 不应成为 Boss 的子对象（避免随 Boss 移动）
            if (spawnRoot != null && spawnRoot != transform)
                indicator = Instantiate(redIndicatorPrefab, thornPos, Quaternion.identity, spawnRoot);
            else
                indicator = Instantiate(redIndicatorPrefab, thornPos, Quaternion.identity);
        }

        // 提示持续 indicatorWarnTime 秒，然后生成荆棘
        float warn = Mathf.Max(0.01f, indicatorWarnTime);
        float t = 0f;
        while (t < warn)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // 销毁提示
        if (indicator != null) Destroy(indicator);

        // 生成荆棘
        if (thornPrefab != null)
        {
            // thorn 不应绑定到 Boss（避免跟随移动）。若 spawnRoot 指向 boss（或未指定），则不设置 parent。
            GameObject thorn;
            if (spawnRoot != null && spawnRoot != transform)
                thorn = Instantiate(thornPrefab, thornPos, Quaternion.identity, spawnRoot);
            else
                thorn = Instantiate(thornPrefab, thornPos, Quaternion.identity);
            Destroy(thorn, 6f); // 荆棘存在一段时间
        }

        // 检测玩家是否在荆棘范围（使用半径判断）
        if (playerT != null)
        {
            float d = Vector3.Distance(playerT.position, thornPos);
            if (d <= 1.2f)
            {
                PlayerControl.GetHurt(1);
            }
        }

        yield break;
    }

    IEnumerator Skill_BladesOrbit()
    {
        preventHeavyNext = true;

        List<GameObject> leaves = new List<GameObject>();
        float dir = UnityEngine.Random.value > 0.5f ? 1f : -1f;

        // spawn leaves around boss
        for (int i = 0; i < bladesCount; i++)
        {
            float ang = i * (360f / bladesCount) * Mathf.Deg2Rad;
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * bladesRadius;
            if (leafPrefab != null)
            {
                var go = Instantiate(leafPrefab, pos, Quaternion.identity, spawnRoot);
                leaves.Add(go);
            }
        }

        float elapsed = 0f;
        bool fired = false;
        // rotate until fired
        while (!fired)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < leaves.Count; i++)
            {
                if (leaves[i] == null) continue;
                Vector3 rel = leaves[i].transform.position - transform.position;
                float angle = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
                angle += dir * bladesRotateSpeed * Time.deltaTime;
                Vector3 newPos = transform.position + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * bladesRadius;
                leaves[i].transform.position = newPos;
                leaves[i].transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
            }

            // 当 Boss 距离玩家足够接近时发射叶片
            if (playerT != null && Vector3.Distance(transform.position, playerT.position) <= bladesCloseDistance)
            {
                fired = true;
                // 每片向玩家飞去
                foreach (var leaf in leaves)
                {
                    if (leaf == null) continue;
                    StartCoroutine(LeafFlyToPlayer(leaf, leaves));
                }
            }

            // 超时保护：如果旋转超过一定时间，则随机发射
            if (elapsed > 6f && !fired)
            {
                fired = true;
                foreach (var leaf in leaves)
                {
                    if (leaf == null) continue;
                    StartCoroutine(LeafFlyToPlayer(leaf, leaves));
                }
            }

            yield return null;
        }

        // 等待直到所有叶片被销毁或超时（避免下个技能提前开始）
        float waitTimeout = 6f;
        float waitTimer = 0f;
        while (waitTimer < waitTimeout)
        {
            waitTimer += Time.deltaTime;
            // 清理 null 条目
            leaves.RemoveAll(g => g == null);
            if (leaves.Count == 0) break;
            yield return null;
        }

        // 如果仍有残留，销毁
        foreach (var l in leaves) if (l != null) Destroy(l);
        leaves.Clear();

        // 小后摇保证视觉连贯
        yield return new WaitForSeconds(0.2f);
        yield break;
    }

    // 当叶片相互接触时应销毁：传入 leaves 列表供检测
    IEnumerator LeafFlyToPlayer(GameObject leaf, List<GameObject> leavesList)
    {
        if (leaf == null) yield break;
        float spd = 10f;
        float life = 4f;
        float t = 0f;
        Vector3 target = (playerT != null) ? playerT.position : leaf.transform.position + Vector3.down * 5f;

        while (t < life)
        {
            t += Time.deltaTime;
            if (leaf == null) break;

            // 获取最新目标（玩家位置可能移动）
            target = (playerT != null) ? playerT.position : target;
            Vector3 dir = (target - leaf.transform.position).normalized;
            leaf.transform.position += dir * spd * Time.deltaTime;

            // 碰到玩家判定
            if (playerT != null && Vector3.Distance(leaf.transform.position, playerT.position) < 0.6f)
            {
                PlayerControl.GetHurt(1);
                // 从列表中移除并销毁
                leavesList.Remove(leaf);
                Destroy(leaf);
                yield break;
            }

            // 检测与其他叶片碰撞（基于距离，确保 leaf prefab 无强制 collider 要求）
            for (int i = leavesList.Count - 1; i >= 0; i--)
            {
                var other = leavesList[i];
                if (other == null || other == leaf) continue;
                if (Vector3.Distance(leaf.transform.position, other.transform.position) < 0.35f)
                {
                    // 销毁两片叶子并从列表移除
                    leavesList.Remove(other);
                    leavesList.Remove(leaf);
                    Destroy(other);
                    Destroy(leaf);
                    yield break;
                }
            }

            yield return null;
        }

        // 生命结束，清理
        if (leaf != null)
        {
            leavesList.Remove(leaf);
            Destroy(leaf);
        }
    }

    IEnumerator Skill_TentaclesBurst()
    {
        preventHeavyNext = false;

        // 对齐玩家：尝试面向玩家并等待短暂对齐（由 preDelay 控制）
        if (playerT != null)
        {
            Vector3 dir = (playerT.position - transform.position).normalized;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, ang);
        }

        // 发出 6 条触须（水平左右以及四个 45 度）
        Vector2[] dirs = new Vector2[]
        {
            Vector2.left, Vector2.right,
            new Vector2(1,1).normalized, new Vector2(-1,1).normalized, new Vector2(1,-1).normalized, new Vector2(-1,-1).normalized
        };

        foreach (var d in dirs)
        {
            SpawnTentacle(transform.position, d, tentacleRange);
        }

        yield return new WaitForSeconds(0.8f);
        yield break;
    }

    void SpawnTentacle(Vector3 origin, Vector2 dir, float range)
    {
        if (tentaclePrefab != null)
        {
            var t = Instantiate(tentaclePrefab, origin, Quaternion.identity, spawnRoot);
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            t.transform.rotation = Quaternion.Euler(0, 0, ang);
            // 碰撞判定（保持原逻辑）
            if (playerT != null)
            {
                Vector3 toPlayer = playerT.position - origin;
                float proj = Vector2.Dot(new Vector2(toPlayer.x, toPlayer.y), dir);
                float lateral = Vector2.Dot(new Vector2(toPlayer.x, toPlayer.y), new Vector2(-dir.y, dir.x));
                if (proj > 0 && proj <= range && Mathf.Abs(lateral) < 0.8f)
                {
                    PlayerControl.GetHurt(1);
                }
            }
            Destroy(t, 1.2f);
        }
        else
        {
            // 无 prefab 时直接以射线判定
            if (playerT != null)
            {
                Vector3 toPlayer = playerT.position - origin;
                float proj = Vector2.Dot(new Vector2(toPlayer.x, toPlayer.y), dir);
                float lateral = Vector2.Dot(new Vector2(toPlayer.x, toPlayer.y), new Vector2(-dir.y, dir.x));
                if (proj > 0 && proj <= range && Mathf.Abs(lateral) < 0.8f)
                {
                    PlayerControl.GetHurt(1);
                }
            }
        }
    }

    IEnumerator Skill_DashSpawnPollenBees()
    {
        lastSkillIndex = 3;
        preventHeavyNext = true; // 下次不可放 2/4

        // 选择左上或右上
        if (cam == null) cam = Camera.main;
        Vector3 vpLeftTop = cam.ViewportToWorldPoint(new Vector3(0.15f, 0.9f, Mathf.Abs(cam.transform.position.z - transform.position.z)));
        Vector3 vpRightTop = cam.ViewportToWorldPoint(new Vector3(0.85f, 0.9f, Mathf.Abs(cam.transform.position.z - transform.position.z)));
        Vector3 corner = (UnityEngine.Random.value > 0.5f) ? vpLeftTop : vpRightTop;

        // 强制 corner 在与玩家最大距离内
        corner = EnforcePlayerDistance(corner);

        // 前摇由 base 的 preDelay 管理
        // 移动到 corner 位置（平滑）
        float moveT = 0f;
        Vector3 start = transform.position;
        float moveDur = 0.6f;
        while (moveT < moveDur)
        {
            moveT += Time.deltaTime;
            transform.position = ClampToCameraViewport(EnforcePlayerDistance(Vector3.Lerp(start, corner, moveT / moveDur)));
            yield return null;
        }

        // 冲刺：向屏幕下方中间冲刺
        Vector3 bottomCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.1f, Mathf.Abs(cam.transform.position.z - transform.position.z)));
        bottomCenter = EnforcePlayerDistance(bottomCenter);
        // dash windup
        yield return new WaitForSeconds(windupTime);
        // dash
        while (Vector2.Distance(transform.position, bottomCenter) > 0.2f)
        {
            transform.position = ClampToCameraViewport(EnforcePlayerDistance(Vector3.MoveTowards(transform.position, bottomCenter, dashSpeed * Time.deltaTime)));
            yield return null;
        }

        // 冲刺后缓慢回到起始位置（平滑过渡，避免瞬移）
        Vector3 returnStart = transform.position;
        Vector3 returnTarget = EnforcePlayerDistance(start);
        float returnDur = 1.4f;
        float rt = 0f;
        while (rt < returnDur)
        {
            rt += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rt / returnDur));
            transform.position = ClampToCameraViewport(EnforcePlayerDistance(Vector3.Lerp(returnStart, returnTarget, s)));
            yield return null;
        }
        // 更新基点，保证后续漂浮和平滑移动正确
        idleBasePos = transform.position;

        yield break;
    }

    IEnumerator Skill_PollenAndBees()
    {
        // 花粉随机生成：在玩家周围生成若干花粉点，每个花粉点吸引若干蜜蜂（蜜蜂从 FlowerKing 身上飞出 -> 朝花粉 -> 返回 FlowerKing）
        if (cam == null) cam = Camera.main;
        if (cam == null) yield break;

        int pollenCount = UnityEngine.Random.Range(2, 6);
        float minRadius = 1.0f;                                                                     
        float maxRadius = 3.0f;
        float pollenLife = 6f;

        List<GameObject> pollenInstances = new List<GameObject>();
        List<GameObject> bees = new List<GameObject>();

        Vector3 center = (playerT != null) ? playerT.position : transform.position;
        for (int i = 0; i < pollenCount; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float r = UnityEngine.Random.Range(minRadius, maxRadius);
            Vector3 pos = center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * r;

            // 限制到 viewport 内
            Vector3 vp = cam.WorldToViewportPoint(pos);
            vp.x = Mathf.Clamp(vp.x, 0.05f, 0.95f);
            vp.y = Mathf.Clamp(vp.y, 0.05f, 0.95f);
            pos = cam.ViewportToWorldPoint(vp);

            GameObject pollenInstance = null;
            if (pollenPrefab != null)
            {
                pollenInstance = Instantiate(pollenPrefab, pos, Quaternion.identity, spawnRoot);
                pollenInstances.Add(pollenInstance);
                StartCoroutine(PollenFloatRoutine(pollenInstance));
            }

            // spawn bees 从 boss 周围飞出（增大 spawn 半径，避免立刻被视为碰到 boss）
            for (int b = 0; b < Mathf.Max(1, beesPerPollen); b++)
            {
                float spawnRadius = 0.7f;
                Vector2 rand2 = UnityEngine.Random.insideUnitCircle * spawnRadius;
                Vector3 spawn = transform.position + new Vector3(rand2.x, rand2.y, 0f);

                if (beePrefab != null)
                {
                    var bee = Instantiate(beePrefab, spawn, Quaternion.identity, spawnRoot);
                    bees.Add(bee);

                    BeeUnit bu = bee.GetComponent<BeeUnit>();
                    if (bu == null) bu = bee.AddComponent<BeeUnit>();
                    bu.Init(this, beeMaxHP, beeDamageToPlayer, beeHealAmount);

                    GameObject targetPollen = pollenInstance;
                    StartCoroutine(BeeFlyToPollenAndBack(bee, targetPollen));

                    yield return new WaitForSeconds(0.05f);
                }
            }

            yield return new WaitForSeconds(0.15f);
        }

        // 等待所有蜜蜂结束或超时（保证技能完全结束）
        float timeout = 8f;
        float timer = 0f;
        while (timer < timeout)
        {
            timer += Time.deltaTime;
            bees.RemoveAll(b => b == null);
            if (bees.Count == 0) break;
            yield return null;
        }

        // 保险：销毁剩余花粉与蜜蜂
        foreach (var p in pollenInstances) if (p != null) Destroy(p, pollenLife);
        foreach (var b in bees) if (b != null) Destroy(b);

        yield break;
    }

    private IEnumerator PollenFloatRoutine(GameObject pollen)
    {
        if (pollen == null) yield break;
        float amplitude = 0.12f;
        float frequency = 1.2f;
        Vector3 basePos = pollen.transform.position;
        float t = 0f;
        while (pollen != null)
        {
            t += Time.deltaTime;
            pollen.transform.position = basePos + Vector3.up * Mathf.Sin(t * frequency) * amplitude;
            yield return null;
        }
    }

    // 蜜蜂往返逻辑（包含起飞 grace 阶段以避免一生成就被 Boss 判定销毁）
    private IEnumerator BeeFlyToPollenAndBack(GameObject bee, GameObject targetPollen)
    {
        if (bee == null) yield break;

        BeeUnit bu = bee.GetComponent<BeeUnit>();
        int damageToPlayer = (bu != null) ? bu.damageToPlayer : beeDamageToPlayer;
        int healAmount = (bu != null) ? bu.healAmount : beeHealAmount;

        float toPollenSpeed = 5f;
        float toBossSpeed = 6f;
        float detectPlayerDist = 0.6f;
        float detectBossDist = 0.4f;
        float pickupDist = 0.35f;

        // grace 时间避免刚生成就被判为到达 boss
        float graceTime = 0.25f;
        float graceTimer = 0f;

        float timer = 0f;
        while (bee != null && (targetPollen == null ? false : Vector3.Distance(bee.transform.position, targetPollen.transform.position) > pickupDist))
        {
            timer += Time.deltaTime;
            graceTimer += Time.deltaTime;

            Vector3 targetPos = (targetPollen != null) ? targetPollen.transform.position : transform.position;
            Vector3 dir = (targetPos - bee.transform.position).normalized;
            bee.transform.position += dir * toPollenSpeed * Time.deltaTime;

            if (playerT != null && Vector3.Distance(bee.transform.position, playerT.position) < detectPlayerDist)
            {
                PlayerControl.GetHurt(damageToPlayer);
                Destroy(bee);
                yield break;
            }

            // 在 graceTime 之前忽略与 boss 的靠近判定
            if (graceTimer >= graceTime)
            {
                if (Vector3.Distance(bee.transform.position, transform.position) < detectBossDist)
                {
                    Destroy(bee);
                    yield break;
                }
            }

            if (timer > 8f) { Destroy(bee); yield break; }
            yield return null;
        }

        if (targetPollen != null)
        {
            Destroy(targetPollen);
            targetPollen = null;
        }

        // 返回 boss
        timer = 0f;
        while (bee != null && Vector3.Distance(bee.transform.position, transform.position) > detectBossDist)
        {
            Vector3 dir = (transform.position - bee.transform.position).normalized;
            bee.transform.position += dir * toBossSpeed * Time.deltaTime;

            if (playerT != null && Vector3.Distance(bee.transform.position, playerT.position) < detectPlayerDist)
            {
                PlayerControl.GetHurt(damageToPlayer);
                Destroy(bee);
                yield break;
            }

            timer += Time.deltaTime;
            if (timer > 6f) { Destroy(bee); yield break; }
            yield return null;
        }

        if (bee != null)
        {
            // 取消原先的回血逻辑：蜜蜂回归不再为 Boss 回血，只销毁蜜蜂
            Destroy(bee);
        }
    }

    protected override void OnSkillWillExecute(EnemySkill skill)
    {
        // leftover hook
    }

    protected override void OnSkillComplete(EnemySkill skill)
    {
        if (skill.Name != "RotatingBlades" && skill.Name != "CornerDashBees")
            preventHeavyNext = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (other.CompareTag("player bullet") || other.CompareTag("PlayerBullet"))
        {
            int damage = 1;
            Hurt(damage);
            Destroy(other.gameObject);

            // 立即清零速度以防子弹产生的力把 Boss 推走（保险）
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;
        var other = collision.collider;
        if (other == null) return;
        if (other.CompareTag("player bullet") || other.CompareTag("PlayerBullet"))
        {
            Hurt(1);
            Destroy(collision.gameObject);

            // 立即清零速度以防子弹产生的力把 Boss 推走（保险）
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    // 辅助扩展
    // 把世界坐标限制到摄像机 viewport 内（保持 zDist）
    private Vector3 ClampToCameraViewport(Vector3 worldPos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return worldPos;

        // 保持与 Boss z 的一致性
        float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        const float margin = 0.02f; // 留一点边距，避免碰到屏幕边缘
        vp.x = Mathf.Clamp(vp.x, margin, 1f - margin);
        vp.y = Mathf.Clamp(vp.y, margin, 1f - margin);
        vp.z = zDist;
        return cam.ViewportToWorldPoint(vp);
    }

    // 强制与玩家距离不超过 maxPlayerDistance
    private Vector3 EnforcePlayerDistance(Vector3 desired)
    {
        if (playerT == null) return desired;
        Vector3 dir = desired - playerT.position;
        float d = dir.magnitude;
        if (d <= maxPlayerDistance) return desired;
        if (d == 0f) return playerT.position + Vector3.right * maxPlayerDistance;
        return playerT.position + dir.normalized * maxPlayerDistance;
    }
}

static class TransformExtensions
{
    public static void LookAt2D(this Transform t, Vector3 worldPos)
    {
        Vector3 dir = worldPos - t.position;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        t.rotation = Quaternion.Euler(0, 0, ang);
    }
}

/// BeeUnit：附加在蜜蜂实例上，处理被玩家子弹伤害、暴露参数等。
public class BeeUnit : MonoBehaviour
{
    FlowerKing owner;
    public int maxHP = 3;
    public int hp = 3;
    public int damageToPlayer = 1;
    public int healAmount = 30;

    public void Init(FlowerKing owner, int maxHP, int damageToPlayer, int healAmount)
    {
        this.owner = owner;
        this.maxHP = Mathf.Max(1, maxHP);
        this.hp = this.maxHP;
        this.damageToPlayer = damageToPlayer;
        this.healAmount = healAmount;
    }

    public void TakeDamage(int d)
    {
        hp -= d;
        if (hp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (other.CompareTag("player bullet") || other.CompareTag("PlayerBullet"))
        {
            int damage = 1;
            var shot = other.GetComponent<Shot>();
            // if (shot != null) damage = shot.damage; // 如果有 damage 字段可启用
            TakeDamage(damage);
            Destroy(other.gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;
        var other = collision.collider;
        if (other == null) return;
        if (other.CompareTag("player bullet") || other.CompareTag("PlayerBullet"))
        {
            TakeDamage(1);
            Destroy(collision.gameObject);
        }
    }
}
