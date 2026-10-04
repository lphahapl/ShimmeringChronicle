using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable chest. Opening and closing share one reversible legacy animation.</summary>
[RequireComponent(typeof(Animation))]
public sealed class TestChest : InteractBase, IContainerOwner
{
    ItemContainer container;
    public ChestSO chest;
    
    public ChestData ChestData = new ChestData();
    protected override void Awake()
    {
        base.Awake();
        chest.CreateChestData(ChestData);
    }
    public IItemOperator GetContainer(ContainerKind kind = ContainerKind.Default)
    {
        return container ??= new ItemContainer(ChestData.items);
    }

    public const string ClipName = "ChestOpen";
    public bool IsOpen { get; private set; }
    Animation Motion => GetComponent<Animation>();

    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);
    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool value)
    {
        var motion = Motion;
        var state = motion[ClipName];
        if (state == null) return;
        float time = motion.IsPlaying(ClipName) ? Mathf.Clamp(state.time, 0, state.length)
            : (IsOpen ? state.length : 0);
        IsOpen = value;
        motion.Play(ClipName);
        state.time = time;
        state.speed = value ? 1 : -1;
        state.wrapMode = WrapMode.ClampForever;
        if (value) this.Publish(GameEvents.OnChestShow, this);
        else this.Publish(GameEvents.OnChestHide, this);
    }
   

    public override string InteractPrompt() => IsOpen ? "关闭箱子" : "打开箱子";
    protected override bool InteractLogic(GameObject interactor) { Toggle(); return true; }
    [ContextMenu("Preview/Open (Play Mode)")]
    void PreviewOpen() { if (Application.isPlaying) Open(); }
    [ContextMenu("Preview/Close (Play Mode)")]
    void PreviewClose() { if (Application.isPlaying) Close(); }
}
