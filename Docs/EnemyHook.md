# EnemySet 模板运行过程解析

## 核心协程与职责

### 1. AILoop()
- 常驻循环（只要未进入 Dead）。
- 周期：每 `decisionInterval` 秒。
- 在 Acting 或 Stunned 状态下暂停（`yield return null`，等待下一帧再判断）。
- 每轮顺序：
  a) 将状态置为 Deciding  
  b) 调用 `AdjustSkillWeights()`（本轮动态修改权重）  
  c) 调用 `GetForcedSkill()`，若返回非空则使用该技能；否则 `SelectSkill()` 随机/自定义选择  
  d) 有技能则 `StartSkill(skill)`，否则状态回 Idle  
  e) 等待决策间隔，进入下一轮  
- 逻辑重点：AI 不在 Update 中跑，而是通过时间片降低无谓计算。

### 2. SkillRoutine(EnemySkill skill)
- 每次技能独立协程，串行执行完整生命周期。
- 流程：
  a) 状态置为 Acting；记录 `runningSkill` 与标记 `runningSkillBodyExecuted=false`  
  b) 前摇：`yield OnSkillPre(skill)`（内部可能 `WaitForSeconds`）  
  c) 主体执行前钩子：`OnSkillWillExecute(skill)`（不阻塞）  
  d) 主体：`skill.SkillAction?.Invoke()` → `runningSkillBodyExecuted=true`  
  e) 主体后的钩子：`OnSkillBodyExecuted(skill)`  
  f) 冷却：若 `Cooldown>0`，启动独立协程 `SkillCooldown(skill)`（并行计时，不阻塞后摇）  
  g) 后摇：`yield OnSkillPost(skill)`  
  h) 若状态仍非 Dead / Stunned，恢复 Idle  
  i) 若最终归 Idle（未被打断/死亡），调用 `OnSkillComplete(skill)`  
  j) 清理上下文（`runningSkill=null`，`runningSkillBodyExecuted=false`，`currentSkillCo=null`）

### 3. SkillCooldown(skill)
- 只负责给该技能打 `OnCooldown` 标记一段时间。
- 与 `SkillRoutine` 并行（不会阻塞后摇或下一轮决策）。
- 这样下一个决策周期中该技能可能仍在冷却而被过滤。

### 4. StunRoutine(duration)
- 眩晕/硬直协程。
- 流程：状态切换为 Stunned → 等待 duration → 若未 Dead 切回 Idle。
- 若调用 `ApplyStun(interruptCurrentSkill=true)`，则优先 `StopCoroutine(currentSkillCo)` 并触发 `OnSkillInterrupted(runningSkill, runningSkillBodyExecuted)`，再启动该协程。
- 打断后不会调用 `OnSkillComplete`。

### 5. DeathSequence()
- 进入死亡后统一停止：`aiLoopCo`、`currentSkillCo`、`stunCo`。
- 运行 `DieEffectRoutine()`（可扩展淡出、爆炸等）
- 若 `AutoDestroyOnDeath=true`，销毁 GameObject。
- 否则由子类后续自行处理（例如复活、残留尸体交互等）。

---

## 状态转换总览

- **Idle → Deciding**：AI 周期进入决策  
- **Deciding → Acting**：技能被选中并启动 `SkillRoutine`  
- **Acting → Idle**：技能正常完成（不死、不眩晕）  
- **Acting → Stunned**：被 `ApplyStun` 打断（可选 interrupt）  
- **任意 → Dead**：血量降至 0 触发 `OnDeath`  
- **Stunned → Idle**：眩晕时间结束且未死亡  
- **Dead**：终态（除非子类实现复活逻辑）

---

## 并发与停止规则

- 仅允许一个技能协程：`StartSkill` 前若 `currentSkillCo != null` 则 `StopCoroutine(currentSkillCo)`。
- 冷却协程与技能协程并行：冷却不会阻塞后摇和状态回归。
- `StunRoutine` 与 `SkillRoutine` 互斥逻辑：若要求打断技能，会先停技能协程。
- 死亡时统一停止所有行为相关协程，避免遗留动作。

---

## 关键时机与扩展点

- **调整权重**：`AdjustSkillWeights` 在每轮决策前，影响本次 `SelectSkill` 的结果。
- **强制技能**：`GetForcedSkill` 让特殊条件（距离过近、残血）优先覆盖普通选择。
- **按阶段处理**：`OnSkillPre` / `OnSkillWillExecute` / `OnSkillBodyExecuted` / `OnSkillPost` / `OnSkillComplete` 细分前摇→主体→后摇→收尾。
- **打断处理**：`OnSkillInterrupted` 区分主体前（`afterBodyExecuted=false`）与主体后（`true`），便于撤销蓄力或清理持续特效。
- **死亡表现**：`DieEffectRoutine` 自定义视觉/音频/掉落；`AutoDestroyOnDeath` 控制是否自动销毁。

---

## 执行时间模型（单技能示例）

- 总技能执行时间 ≈ `PreDelay` + 主体耗时（通常瞬间） + `PostDelay`
- 冷却时间独立叠加：`Cooldown` 与后摇并行开始
- 决策间隔决定最短再行动间隔：如果决策结束时仍在后摇/眩晕，下一轮会立即 `yield null` 等待直到协程结束；否则会等待 `decisionInterval` 再决策。

---

## 错误与隐患点

- 若在 `PreDelay` 内被眩晕但未设置 `interruptCurrentSkill=true`，前摇仍继续执行，可能逻辑不符预期。
- `AdjustSkillWeights` 若不重置被夸张修改的权重可能导致长期偏斜。
- 在打断时未处理残留粒子 / 音效需在 `OnSkillInterrupted` 清理。
- 如果 `SkillAction` 内再启动长耗时协程（例如持续伤害）且无管理，可能逻辑分散；建议由技能主体内部自管或增加新阶段钩子。

---

## 性能注意

- 尽量避免在 `AdjustSkillWeights` 中做高开销寻路、`Physics2D.Overlap` 等，可按帧分摊或做缓存。
- 频繁 `StopCoroutine` + `StartCoroutine` 的技能切换开销很低，但过度嵌套协程需保持结构清晰。
- 决策频率（`decisionInterval`）过小会近似每帧决策，失去协程节流意义。