using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class CharacterTime : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private struct Effect
    {
        public int handle;
        public ETimePriority priority;
        public float scale;
        public double endTime;
    }

    private readonly List<Effect> effects = new List<Effect>();
    private int nextHandle = 1;
    private float baseAnimationSpeed = 1f;

    public float Scale { get; private set; } = 1f;
    public float DeltaTime => Time.deltaTime * Scale;
    public bool IsFrozen => Scale == 0f;

    private void Awake()
    {
        if (!animator)
            animator = GetComponent<Animator>();

        if (animator)
            baseAnimationSpeed = animator.speed;
    }

    public int Apply(
        ETimePriority priority,
        float scale,
        float duration)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));

        if (float.IsNaN(duration) ||
            float.IsInfinity(duration) ||
            duration <= 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));

        if (!isActiveAndEnabled)
            return 0;

        int handle = nextHandle++;

        effects.Add(new Effect
        {
            handle = handle,
            priority = priority,
            scale = scale,
            endTime = Time.unscaledTimeAsDouble + duration
        });

        Refresh();
        return handle;
    }

    public void Cancel(int handle)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].handle == handle)
            {
                effects.RemoveAt(i);
                break;
            }
        }

        Refresh();
    }

    public void Clear()
    {
        effects.Clear();
        SetScale(1f);
    }

    private void Update()
    {
        if (effects.Count > 0)
            Refresh();
    }

    private void Refresh()
    {
        double now = Time.unscaledTimeAsDouble;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
           
            if (effects[i].endTime <= now)
            {
                //print($"即将移除{effects[i].handle}");
                effects.RemoveAt(i);
            }
               
        }

        float scale = 1f;
        ETimePriority priority = default;
        bool found = false;

        foreach (Effect effect in effects)
        {
            if (!found || effect.priority > priority)
            {
                found = true;
                priority = effect.priority;
                scale = effect.scale;
            }
            else if (effect.priority == priority)
            {
                // 同优先级时，更小的倍率生效。
                scale = Mathf.Min(scale, effect.scale);
            }
        }

        SetScale(scale);
    }

    private void SetScale(float scale)
    {
        Scale = scale;

        if (animator)
            animator.speed = baseAnimationSpeed * scale;
    }

    private void OnDisable()
    {
        // 回收进对象池时清除效果并恢复动画速度。
        Clear();
    }
}

public enum ETimePriority
{

    Lowest = 0,
    Default = 10,
    SkillEffects = 20,
    PerfectDodge = 30,
    HitStop = 50,
    Highest = 100
}

