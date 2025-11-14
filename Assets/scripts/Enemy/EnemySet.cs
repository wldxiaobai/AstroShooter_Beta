using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人基类：统一生命、AI 决策循环、技能执行时序、状态控制（Idle/Deciding/Acting/Stunned/Dead）。
/// 使用协程实现：按节奏决策 + 技能前摇/执行/后摇 + 冷却、硬直与打断管理。
/// 子类可重写以下钩子实现细粒度逻辑：
///   GetForcedSkill          在特定条件下强制使用某技能（跳过常规选择）
///   AdjustSkillWeights      决策前动态调整技能权重（本次选择有效）
///   SelectSkill             常规技能选择策略（权重/评分/随机等）
///   OnSkillPre / OnSkillPost      前摇 / 后摇阶段
///   OnSkillWillExecute      主体即将执行（可做朝向锁定、动画切换）
///   OnSkillBodyExecuted     主体执行后（生成特效、发射子弹等）
///   OnSkillComplete         技能整体完成（含后摇结束）
///   OnSkillInterrupted      技能被打断（区分是否已执行主体）
///   DieEffectRoutine        死亡表现
///   OnIdleEnter/OnIdleUpdate/OnIdleExit  待机阶段进入/停留更新/退出（新增）
/// </summary>
public class EnemySet : MonoBehaviour
{
    /// <summary>
    /// 单个技能的数据与行为封装：基础权重、前/后摇、冷却、名称。
    /// SkillAction 在主体阶段调用，冷却通过协程标记 OnCooldown。
    /// </summary>
    protected class EnemySkill
    {
        public Action SkillAction;      // 技能主体委托
        public int Weight;              // 基础权重（可被动态算法使用）
        public float PreDelay;          // 前摇时间
        public float PostDelay;         // 后摇时间
        public float Cooldown;          // 冷却时间
        public bool OnCooldown;         // 是否处于冷却
        public string Name;             // 调试标识

        public EnemySkill(string name, Action action, int weight = 1, float preDelay = 0f, float postDelay = 0f, float cooldown = 0f)
        {
            Name = name;
            SkillAction = action;
            Weight = Mathf.Max(1, weight);
            PreDelay = preDelay;
            PostDelay = postDelay;
            Cooldown = cooldown;
        }

        public bool CanUse => SkillAction != null && !OnCooldown;

        public static EnemySkill GetRandomSkill(List<EnemySkill> skills)
        {
            int sum = 0;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].CanUse) sum += skills[i].Weight;
            if (sum == 0) return null;
            int rand = UnityEngine.Random.Range(0, sum);
            foreach (var s in skills)
            {
                if (!s.CanUse) continue;
                if (rand < s.Weight) return s;
                rand -= s.Weight;
            }
            return null;
        }
    }

    /// <summary>
    /// 敌人状态机。
    /// </summary>
    protected enum EnemyState
    {
        Idle,
        Deciding,
        Acting,
        Stunned,
        Dead
    }

    [Header("生命相关")]
    [SerializeField] protected int maxHealth = 50;
    protected int health;
    public int Health => health;

    [Header("AI 参数")]
    [SerializeField] protected float decisionInterval = 0.4f;
    [SerializeField] protected bool autoStartAI = true;

    [Header("死亡行为")]
    [Tooltip("为 true 时，死亡序列结束后自动销毁 GameObject；为 false 时需在子类自行销毁。")]
    [SerializeField] protected bool autoDestroyOnDeath = true;
    /// <summary>
    /// 是否在死亡序列结束后自动销毁对象。子类可重写改变行为。
    /// </summary>
    protected virtual bool AutoDestroyOnDeath => autoDestroyOnDeath;

    protected EnemyState state = EnemyState.Idle;
    protected List<EnemySkill> SkillList = new();

    // 协程引用
    Coroutine aiLoopCo;
    Coroutine currentSkillCo;
    Coroutine deathCo;
    Coroutine stunCo;
    Coroutine idleCo; // 新增：Idle 监测循环

    // 技能执行上下文（用于打断区分阶段）
    protected EnemySkill runningSkill;
    protected bool runningSkillBodyExecuted;

    protected virtual void Awake()
    {
        if (health <= 0) health = maxHealth;
    }

    protected virtual void Start()
    {
        SetMaxHealth(maxHealth, fullHealth: true);
        StartApply();
        if (autoStartAI) StartAILoop();

        // 启动 Idle 监测循环（独立于 AI 决策）
        idleCo ??= StartCoroutine(IdleRoutine());
    }

    /// <summary>
    /// 用于代替 Start 注册技能、应用生成时的效果。
    /// </summary>
    protected virtual void StartApply() { }

    // ===================== 决策相关钩子 =====================

    /// <summary>
    /// 决策前动态调整技能权重（仅影响本轮选择）。
    /// </summary>
    protected virtual void AdjustSkillWeights() { }

    /// <summary>
    /// 特定条件下强制技能（跳过权重挑选）。
    /// </summary>
    protected virtual EnemySkill GetForcedSkill() => null;

    /// <summary>
    /// 常规技能选择（默认按权重随机）。可在子类重写成评分制。
    /// </summary>
    protected virtual EnemySkill SelectSkill()
    {
        return EnemySkill.GetRandomSkill(SkillList);
    }

    // ===================== 技能阶段钩子 =====================

    /// <summary>
    /// 前摇阶段（播放准备动画 / 锁定方向）。
    /// </summary>
    protected virtual IEnumerator OnSkillPre(EnemySkill skill)
    {
        if (skill.PreDelay > 0f)
            yield return new WaitForSeconds(skill.PreDelay);
    }

    /// <summary>
    /// 主体即将执行（在调用 SkillAction 前触发）。
    /// </summary>
    protected virtual void OnSkillWillExecute(EnemySkill skill) { }

    /// <summary>
    /// 主体执行完毕（SkillAction.Invoke 后）。
    /// </summary>
    protected virtual void OnSkillBodyExecuted(EnemySkill skill) { }

    /// <summary>
    /// 后摇阶段（收招、缓冲、残留特效等待）。
    /// </summary>
    protected virtual IEnumerator OnSkillPost(EnemySkill skill)
    {
        if (skill.PostDelay > 0f)
            yield return new WaitForSeconds(skill.PostDelay);
    }

    /// <summary>
    /// 技能完整结束（后摇完成且未被打断/死亡）。
    /// </summary>
    protected virtual void OnSkillComplete(EnemySkill skill) { }

    /// <summary>
    /// 技能被打断（眩晕/强制状态切换）。
    /// </summary>
    protected virtual void OnSkillInterrupted(EnemySkill skill, bool afterBodyExecuted) { }

    // ===================== Idle 阶段钩子 =====================

    /// <summary>
    /// 进入 Idle 一次触发（便于播放待机动画、重置状态）。
    /// </summary>
    protected virtual void OnIdleEnter() { }

    /// <summary>
    /// Idle 持续期间每帧调用（传入 deltaTime，便于做轻量巡逻/面向/呼吸动画）。
    /// </summary>
    protected virtual void OnIdleUpdate(float deltaTime) { }

    /// <summary>
    /// 离开 Idle 时触发（收尾待机动作）。
    /// </summary>
    protected virtual void OnIdleExit() { }

    /// <summary>
    /// Idle 监测循环：无需修改任何状态赋值点，即可获得进入/停留/退出事件。
    /// </summary>
    protected virtual IEnumerator IdleRoutine()
    {
        bool wasIdle = false;
        while (state != EnemyState.Dead)
        {
            if (state == EnemyState.Idle)
            {
                if (!wasIdle)
                {
                    OnIdleEnter();
                    wasIdle = true;
                }
                OnIdleUpdate(Time.deltaTime);
            }
            else if (wasIdle)
            {
                OnIdleExit();
                wasIdle = false;
            }

            yield return null; // 每帧检测
        }

        // 确保死亡时也能正确收尾
        if (wasIdle)
        {
            OnIdleExit();
        }
    }

    // ===================== 主 AI 循环 =====================

    protected virtual IEnumerator AILoop()
    {
        while (state != EnemyState.Dead)
        {
            if (state == EnemyState.Stunned || state == EnemyState.Acting)
            {
                yield return null;
                continue;
            }

            state = EnemyState.Deciding;

            // 动态权重调整（在强制技能判定前执行）
            AdjustSkillWeights();

            EnemySkill skill = GetForcedSkill();
            skill ??= SelectSkill();
            // 以上代码相当于：
            // if(skill == null)
            // skill = SelectSkill();

            if (skill != null)
            {
                StartSkill(skill);
            }
            else
            {
                state = EnemyState.Idle;
            }

            yield return new WaitForSeconds(decisionInterval);
        }
    }

    protected void StartAILoop()
    {
        aiLoopCo ??= StartCoroutine(AILoop());
    }

    protected void StartSkill(EnemySkill skill)
    {
        if (currentSkillCo != null)
            StopCoroutine(currentSkillCo);
        currentSkillCo = StartCoroutine(SkillRoutine(skill));
    }

    protected virtual IEnumerator SkillRoutine(EnemySkill skill)
    {
        state = EnemyState.Acting;
        runningSkill = skill;
        runningSkillBodyExecuted = false;

        // 前摇
        yield return OnSkillPre(skill);

        // 主体执行前
        OnSkillWillExecute(skill);

        // 主体
        skill.SkillAction?.Invoke();
        runningSkillBodyExecuted = true;

        // 主体执行后钩子
        OnSkillBodyExecuted(skill);

        // 冷却（不阻塞后摇）
        if (skill.Cooldown > 0f)
            StartCoroutine(SkillCooldown(skill));

        // 后摇
        yield return OnSkillPost(skill);

        // 状态收尾
        if (state != EnemyState.Dead && state != EnemyState.Stunned)
            state = EnemyState.Idle;

        // 完整结束（未被打断）
        if (state == EnemyState.Idle)
            OnSkillComplete(skill);

        runningSkill = null;
        runningSkillBodyExecuted = false;
        currentSkillCo = null;
    }

    IEnumerator SkillCooldown(EnemySkill skill)
    {
        skill.OnCooldown = true;
        yield return new WaitForSeconds(skill.Cooldown);
        skill.OnCooldown = false;
    }

    // ===================== 控制 / 打断 =====================

    public void ApplyStun(float duration, bool interruptCurrentSkill = true)
    {
        if (state == EnemyState.Dead) return;

        if (interruptCurrentSkill && currentSkillCo != null)
        {
            StopCoroutine(currentSkillCo);
            currentSkillCo = null;
            if (runningSkill != null)
                OnSkillInterrupted(runningSkill, runningSkillBodyExecuted);

            runningSkill = null;
            runningSkillBodyExecuted = false;
        }

        if (stunCo != null) StopCoroutine(stunCo);
        stunCo = StartCoroutine(StunRoutine(duration));
    }

    IEnumerator StunRoutine(float duration)
    {
        state = EnemyState.Stunned;
        yield return new WaitForSeconds(duration);
        if (state != EnemyState.Dead)
            state = EnemyState.Idle;
    }

    // ===================== 生命 / 死亡 =====================

    public void Hurt(int damage)
    {
        if (state == EnemyState.Dead) return;
        health -= damage;
        if (health <= 0)
            OnDeath();
    }

    public void Heal(int amount)
    {
        if (state == EnemyState.Dead) return;
        health = Mathf.Min(maxHealth, health + amount);
    }

    /// <summary>
    /// 死亡表现协程：默认空；子类可淡出、爆炸、掉落等。
    /// </summary>
    protected virtual IEnumerator DieEffectRoutine()
    {
        yield return null;
    }

    /// <summary>
    /// 死亡入口：统一停止所有运行协程，进入死亡序列。
    /// </summary>
    protected virtual void OnDeath()
    {
        if (state == EnemyState.Dead) return;

        state = EnemyState.Dead;

        if (aiLoopCo != null) StopCoroutine(aiLoopCo);
        if (currentSkillCo != null) StopCoroutine(currentSkillCo);
        if (stunCo != null) StopCoroutine(stunCo);
        if (idleCo != null) StopCoroutine(idleCo); // 新增：停止 Idle 监测循环

        runningSkill = null;
        runningSkillBodyExecuted = false;
        currentSkillCo = null;

        deathCo ??= StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        yield return DieEffectRoutine();
        if (AutoDestroyOnDeath)
            Destroy(gameObject);
    }

    protected virtual void SetMaxHealth(int max, bool fullHealth = false)
    {
        maxHealth = max;
        if (fullHealth) health = maxHealth;
    }

    // ===================== 调试辅助 =====================
    // 在 Update 中检查死亡条件，方便调试时直接修改 health 变量
    void Update()
    {
        if (health <= 0 && state != EnemyState.Dead)
            OnDeath();
    }
}