using System.Collections;
using UnityEngine;

public class Invincibility : MonoBehaviour
{
    [Header("无敌时间")]
    [SerializeField] private float invincibilityDuration = 2f;

    [Header("无敌效果")]
    public Material invincibleMaterial;      // 无敌时替换的材质（虚化、泛白等）
    private Material originalMaterial;       // 原始材质
    private SpriteRenderer playerRenderer;   // 玩家精灵渲染组件

    [Header("行为设置")]
    [Tooltip("在无敌期间再次受伤时，是否刷新无敌计时")]
    [SerializeField] private bool refreshOnRepeatedHit = true;

    private bool isInvincible = false;
    public bool IsInvincibleState => isInvincible;

    private string _originalTag;             // 进入无敌前的原始 Tag
    private Coroutine _invCoroutine;         // 当前无敌协程句柄

    private void Awake()
    {
        // 在订阅事件前完成必要初始化，避免回调早于 Start 触发
        playerRenderer = GetComponent<SpriteRenderer>();
        if (playerRenderer != null)
        {
            // 注意：访问 .material 会实例化材质副本；若需要共用材质可使用 .sharedMaterial
            originalMaterial = playerRenderer.material;
        }

        if (invincibleMaterial == null)
        {
            CreateDefaultInvincibleMaterial();
        }

        _originalTag = gameObject.tag; // 记录原始 Tag，结束无敌时还原
    }

    private void OnEnable()
    {
        PlayerControl.OnPlayerHurt += ActivateInvincibility;
    }

    private void OnDisable()
    {
        PlayerControl.OnPlayerHurt -= ActivateInvincibility;

        // 组件被禁用时确保清理状态
        if (_invCoroutine != null)
        {
            StopCoroutine(_invCoroutine);
            _invCoroutine = null;
        }
        if (isInvincible)
        {
            SetInvincible(false);
        }
    }

    // 由受伤事件触发；若 willDie 为真通常无需进入无敌（避免与死亡流程冲突）
    public void ActivateInvincibility(int damage, int newHP, int maxHP, bool willDie)
    {
        if (willDie) return;

        if (isInvincible)
        {
            if (refreshOnRepeatedHit)
            {
                // 刷新计时：重启协程
                if (_invCoroutine != null) StopCoroutine(_invCoroutine);
                _invCoroutine = StartCoroutine(InvincibilityCoroutine());
            }
            // 若不刷新则忽略
            return;
        }

        _invCoroutine = StartCoroutine(InvincibilityCoroutine());
    }

    private IEnumerator InvincibilityCoroutine()
    {
        SetInvincible(true);
        yield return new WaitForSeconds(invincibilityDuration); // 受 Time.timeScale 影响；需要不受暂停影响可改为 WaitForSecondsRealtime
        SetInvincible(false);
        _invCoroutine = null;
    }

    // 切换视觉与标记状态（注意：与其他也会切 Tag 的系统需协调）
    private void SetInvincible(bool invincible)
    {
        // 视觉效果（材质切换）
        if (playerRenderer != null && invincibleMaterial != null && originalMaterial != null)
        {
            playerRenderer.material = invincible ? invincibleMaterial : originalMaterial;
        }

        // 用 Tag 标记无敌，便于其他脚本识别
        // 警告：若项目中还有其它功能（如 Dash）也会改 Tag，建议统一到一个“无敌管理”或使用 Layer/标志位代替 Tag 冲突
        gameObject.tag = invincible ? "Invincible" : _originalTag;

        isInvincible = invincible;
    }

    // 创建一个简单半透明材质作为默认无敌效果
    private void CreateDefaultInvincibleMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        Material material = new(shader)
        {
            color = new Color(1f, 1f, 1f, 0.35f)
        };
        invincibleMaterial = material;
    }
}
