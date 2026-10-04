using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血条。fill 立刻跟着血量走，buffer 是拖在后面的缓冲条 ——
/// 掉血时能一眼看出这一下掉了多少。
///
/// 它是"唯一的一个"，所以继承 UIBase 归 UIManager 管。
/// 敌人那种"每个敌人一个"的血条不要这样写，那个自己管自己。
/// </summary>
public class PlayerHPBar : UIBase
{
    [Tooltip("血条底图")]
    public Image background;

    [Tooltip("血条本体，立刻跟血量走")]
    public Image fill;

    [Tooltip("缓冲条，延迟一下再慢慢追上来")]
    public Image buffer;

    [Tooltip("玩家。事件是全局广播的，得知道哪个才是自己")]
    [SerializeField] PlayerObj player;

    [Tooltip("受击后隔多久缓冲条才开始动")]
    public float bufferDelay = 0.3f;

    [Tooltip("缓冲条追赶速度：1 = 一秒追完，2 = 半秒，0.5 = 两秒")]
    public float bufferSpeed = 1f;
    public TMP_Text hpPrompt;
    public float maxHP;
    public float nowHP;

    /// <summary>当前在跑的缓冲协程。开新的之前得把旧的停掉</summary>
    Coroutine bufferRoutine;

    protected override void Awake()
    {
        base.Awake();
        this.Subscribe(GameEvents.HpChanged, OnHpChanged);
    }

    protected override void OnDestroy()
    {
        this.UnSubscribe(GameEvents.HpChanged, OnHpChanged);
        base.OnDestroy();       
    }

    /// <summary>初始化用：直接拉到当前血量，不放缓冲动画</summary>
    public override void Refresh()
    {
        if (player == null) return;

        var d = player.Data;
        if (d == null || d.maxHp <= 0f) return;

        SnapTo(d.hp / d.maxHp);
        nowHP = d.hp;
        this.maxHP = d.maxHp;
        UpdateText();
    }

    /// <summary>
    /// 血量变了。(谁, 当前血, 血上限)。
    /// 这个事件是全局广播的，敌人以后也会发，所以要认一下是不是自己。
    /// </summary>
    void OnHpChanged(GameObject who, float hp, float maxHp)
    {
        if (player == null || who != player.gameObject) return;
        nowHP = hp;
        this.maxHP = maxHp;
        
        UpdateBar(maxHp > 0f ? hp / maxHp : 0f);
        UpdateText();
    }

    /// <summary>立刻跳过去，不做缓冲动画。初始化、复活、切场景之后用</summary>
    public void SnapTo(float ratio)
    {
        StopBuffer();
        fill.fillAmount = buffer.fillAmount = Mathf.Clamp01(ratio);
    }

    /// <summary>血条变化，带缓冲动画</summary>
    public void UpdateBar(float ratio)
    {
        fill.fillAmount = Mathf.Clamp01(ratio);
        StopBuffer();

        // 加血：fill 反超 buffer 了，缓冲条直接跟上。
        if (fill.fillAmount >= buffer.fillAmount)
        {
            buffer.fillAmount = fill.fillAmount;
            return;
        }

        bufferRoutine = StartCoroutine(BufferLerp());
    }

    IEnumerator BufferLerp()
    {
        yield return new WaitForSeconds(bufferDelay);

        float start = buffer.fillAmount;
        float end = fill.fillAmount;

        // bufferSpeed 填 0 的话 elapse 永远涨不上去，这条协程就永远不结束了。
        if (bufferSpeed <= 0f)
        {
            buffer.fillAmount = end;
            bufferRoutine = null;
            yield break;
        }

        float elapse = 0f;
        while (elapse < 1f)
        {
            elapse += Time.deltaTime * bufferSpeed;
            buffer.fillAmount = Mathf.Lerp(start, end, elapse);

            yield return null;      
        }

        buffer.fillAmount = end;    
        bufferRoutine = null;
    }

    void StopBuffer()
    {
        if (bufferRoutine == null) return;

        StopCoroutine(bufferRoutine);
        bufferRoutine = null;
    }
    private void UpdateText()
    {
        hpPrompt.text = $"{nowHP}/{maxHP}";
    }

}
