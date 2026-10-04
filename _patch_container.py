# -*- coding: utf-8 -*-
import io, sys

FAILED = []

def write(path, text):
    io.open(path, 'w', encoding='gbk', newline='').write(text.replace('\n', '\r\n'))
    print('WROTE  :', path)

def patch(path, pairs):
    s = io.open(path, encoding='gbk').read().replace('\r\n', '\n')
    for old, new in pairs:
        if old not in s:
            FAILED.append('%s: anchor not found: %r' % (path, old[:70]))
            continue
        s = s.replace(old, new, 1)
    io.open(path, 'w', encoding='gbk', newline='').write(s.replace('\n', '\r\n'))
    print('PATCHED:', path)


# ══════════════════ 1. 新建 ItemContainer ══════════════════
write('Assets/Scripts/Logic/ItemContainer.cs', '''using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个物品容器：包着一个 List&lt;ItemData&gt;，实现 IItemOperator。
///
/// 背包、快捷栏各持有一个；以后箱子、商店、掉落物也一样 ——
/// 容器规则只有这一份实现，不用每个持有方抄一遍。
///
/// 改完自己广播 OnItemsChanged，参数就是自己那个列表。
/// 界面收到后拿它跟自己显示的那个比（比引用），对得上才刷新。
/// </summary>
public class ItemContainer : IItemOperator
{
    readonly List<ItemData> items;

    /// <summary>包住一个已有的列表。传 null 会建一个空的，但那样外面就拿不到它了 ——
    /// 所以调用方应该先把列表建好（PlayerObj.EnsureContainers 就是这么做的）。</summary>
    public ItemContainer(List<ItemData> items)
    {
        this.items = items ?? new List<ItemData>();
    }

    /// <summary>容器里的格子。空格子是 null 占位，不往前挤。</summary>
    public List<ItemData> GetItemList() => items;

    public bool ContainsItem(string id) => ItemRules.FindItem(items, id) >= 0;

    /// <summary>这一格能不能被拿去交换。空格不能；以后有"绑定 / 锁定"的物品也在这里挡。</summary>
    public bool CanExchange(int index)
    {
        if (index < 0 || index >= items.Count) return false;

        var item = items[index];
        return item != null && item.count > 0;
    }

    public int AddItem(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return 0;

        // 靠 id 反查配置走 GameManager，它还没加载完就放不进去
        var manager = GameManager.Instance;
        if (manager == null || !manager.IsReady)
        {
            Debug.LogWarning("[ItemContainer] GameManager 还没准备好，加不了物品");
            return 0;
        }

        var so = manager.GetItem(id);
        if (so == null) return 0;

        int added = ItemRules.AddInto(items, so, count);
        if (added <= 0) return 0;

        this.Publish(GameEvents.OnItemsChanged, items);
        return added;
    }

    public bool RemoveItem(string id, int count)
    {
        if (!ItemRules.TakeAcross(items, id, count)) return false;

        this.Publish(GameEvents.OnItemsChanged, items);
        return true;
    }

    public bool RemoveItemAt(int index, int count)
    {
        if (!ItemRules.TakeFrom(items, index, count)) return false;

        this.Publish(GameEvents.OnItemsChanged, items);
        return true;
    }
}
''')


# ══════════════════ 2. 新建 DragDropHandler ══════════════════
write('Assets/Scripts/Logic/DragDropHandler.cs', '''using UnityEngine;

/// <summary>
/// 拖拽落地处理。场景里放一个就行。
///
/// 为什么不放在各个面板里：拖拽是**跨容器**的（背包 ↔ 快捷栏 ↔ 以后的箱子），
/// 而每个面板只知道自己那一半。格子把"谁拖到了谁身上"广播出来，
/// 这里从两个格子各自拿到 (容器, 下标)，一处处理所有组合。
///
/// 想加"能不能放"的预览（高亮 / 红叉），也是在这条链上：
/// 拖拽经过某格时用 ItemRules.CanExchange 判一下，别去试。
/// </summary>
public class DragDropHandler : MonoBehaviour
{
    void OnEnable()
    {
        this.Subscribe(GameEvents.OnItemDropRequested, OnDropRequested);
    }

    void OnDisable()
    {
        this.UnSubscribe(GameEvents.OnItemDropRequested, OnDropRequested);
    }

    /// <summary>
    /// 格子拖到格子上了。可能是同一个容器内部换位，也可能跨容器。
    /// 两个格子各自知道自己属于哪个容器、在第几格，所以这里直接就能换。
    /// </summary>
    void OnDropRequested(ItemSlot from, ItemSlot to)
    {
        if (from == null || to == null) return;

        ItemRules.Exchange(from.Owner, from.index, to.Owner, to.index);
    }
}
''')


# ══════════════════ 3. PlayerObj：内部改用 ItemContainer ══════════════════
write('Assets/Scripts/Logic/PlayerObj.cs', '''using System.Collections.Generic;
using UnityEngine;

public class PlayerObj : MonoBehaviour, IDamageable, IItemOperator
{
    [SerializeField] PlayerSO config;

    private PlayerData data;

    ItemContainer bag;
    ItemContainer quickBar;

    /// <summary>已经死了。死了之后不再受伤，也不会重复广播死亡</summary>
    public bool IsDead { get; private set; }

    /// <summary>
    /// 外部访问数据。
    /// 懒加载：谁先来读都拿得到，不依赖 Awake 顺序。
    /// </summary>
    public PlayerData Data
    {
        get
        {
            if (data == null)
            {
                if (config == null)
                {
                    Debug.LogError("[PlayerObj] 没有 PlayerSO，拿不到数据", this);
                    return null;
                }
                data = config.CreateData();
            }

            return data;
        }
    }

    /// <summary>背包容器。界面、拖拽处理器要的都是它</summary>
    public IItemOperator Bag
    {
        get { EnsureContainers(); return bag; }
    }

    /// <summary>快捷栏容器。数据和背包一样躺在 PlayerData 里，只是包成了另一个容器</summary>
    public IItemOperator QuickBar
    {
        get { EnsureContainers(); return quickBar; }
    }

    /// <summary>
    /// 懒建两个容器，并且保证它们包的是 PlayerData 里那两个真列表 ——
    /// 列表可能还没建（PlayerSO 没配），这里补一个，不然容器包的是另一个空列表，
    /// 里面的改动 Data 里看不到。
    /// </summary>
    void EnsureContainers()
    {
        var d = Data;
        if (d == null) return;

        d.Bag ??= new List<ItemData>();
        d.quickBar ??= new List<ItemData>();

        bag ??= new ItemContainer(d.Bag);
        quickBar ??= new ItemContainer(d.quickBar);
    }

    public void Start()
    {
        UIManager.Instance.RefreshAll();
    }

    // ─────────────── IItemOperator：PlayerObj 对外代表的是背包 ───────────────
    // 全部转调给背包容器。容器自己会广播，这里不重复发。

    public List<ItemData> GetItemList() => Bag?.GetItemList();

    /// <summary>背包里有没有。想连快捷栏一起问，用 HasItem。</summary>
    public bool ContainsItem(string id) => Bag != null && Bag.ContainsItem(id);

    /// <summary>背包 或 快捷栏 里有没有 —— 也就是"玩家身上有没有这个东西"</summary>
    public bool HasItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        return (Bag != null && Bag.ContainsItem(id))
            || (QuickBar != null && QuickBar.ContainsItem(id));
    }

    public bool CanExchange(int index) => Bag != null && Bag.CanExchange(index);

    public int AddItem(string id, int count) => Bag == null ? 0 : Bag.AddItem(id, count);

    public bool RemoveItem(string id, int count) => Bag != null && Bag.RemoveItem(id, count);

    public bool RemoveItemAt(int index, int count) => Bag != null && Bag.RemoveItemAt(index, count);

    /// <summary>
    /// 手动喊一声"背包变了"。
    /// 直接改 Data.Bag（测试、或者一次改好几处）之后就靠它，不然界面不会知道。
    /// </summary>
    public void PublishBagChanged()
    {
        var d = Data;
        if (d != null) this.Publish(GameEvents.OnItemsChanged, d.Bag);
    }

    /// <summary>手动喊一声"快捷栏变了"。直接改 Data.quickBar 之后用它。</summary>
    public void PublishQuickBarChanged()
    {
        var d = Data;
        if (d != null) this.Publish(GameEvents.OnItemsChanged, d.quickBar);
    }

    // ───────────────────────── 受伤 ─────────────────────────

    public void TakeDamage(float damage, GameObject attacker)
    {
        if (damage <= 0f) return;
        if (IsDead) return;

        var d = Data;
        if (d == null) return;

        d.hp = Mathf.Max(0f, d.hp - damage);    // 减伤只需要下界保底

        // 广播出去 —— 谁关心谁订阅，这里不直接调任何人。
        // 关心方：受击动画、血条、音效、任务统计、成就……
        this.Publish(GameEvents.Damaged, gameObject, damage, attacker);
        this.Publish(GameEvents.HpChanged, gameObject, d.hp, d.maxHp);

        if (d.hp <= 0f)
        {
            IsDead = true;
            this.Publish(GameEvents.Died, gameObject);
        }
    }
}
''')


# ══════════════════ 4. ItemSlot：parent 换成接口 + 修 Bind 的 bug ══════════════════
patch('Assets/Scripts/Logic/ItemSlot.cs', [
    ('using NUnit.Framework;\nusing System.Collections.Generic;\n', 'using System.Collections.Generic;\n'),

    ('    public List<ItemData> parent;\n',
     '    /// <summary>\n'
     '    /// 自己属于哪个容器。拖拽必须知道 —— 交换是两个"位置"之间的操作，\n'
     '    /// 位置 = (容器, 下标)。光知道自己显示的是哪件物品，是推不出位置的。\n'
     '    /// </summary>\n'
     '    IItemOperator owner;\n'
     '\n'
     '    public IItemOperator Owner => owner;\n'),

    ('    /// <summary>把要显示的那一叠、和它在容器里的位置推进来。data 传 null 表示空格子。</summary>\n'
     '    public void Bind(ItemData data, int index)\n'
     '    {\n'
     '        if (item != data || this.index != index) StopDrag();\n'
     '        if(parent==null)\n'
     '        item = data;\n'
     '        this.index = index;\n'
     '        RefreshSlot();\n'
     '    }',

     '    /// <summary>\n'
     '    /// 把要显示的那一叠、它在容器里的位置、和它属于哪个容器推进来。\n'
     '    /// data 传 null 表示空格子；owner 传 null 表示这一格不参与拖拽。\n'
     '    /// </summary>\n'
     '    public void Bind(ItemData data, int index, IItemOperator owner)\n'
     '    {\n'
     '        // 显示的东西换了位置，正在进行的拖拽就作废\n'
     '        if (item != data || this.index != index) StopDrag();\n'
     '\n'
     '        item = data;\n'
     '        this.index = index;\n'
     '        this.owner = owner;\n'
     '        RefreshSlot();\n'
     '    }'),
])


# ══════════════════ 5. BagPanel：数据源改成容器 ══════════════════
patch('Assets/Scripts/UI/BagPanel.cs', [
    ('    /// <summary>当前显示的数据。持引用不持拷贝，所以数据变了刷新一下就同步</summary>\n'
     '    IReadOnlyList<ItemData> items;\n'
     '\n'
     '    /// <summary>外部把数据推进来。传 null 就是清空</summary>\n'
     '    public void Bind(IReadOnlyList<ItemData> items)\n'
     '    {\n'
     '        this.items = items;\n'
     '        Refresh();\n'
     '    }',

     '    /// <summary>当前显示的容器。持引用不持拷贝，所以容器里变了刷新一下就同步</summary>\n'
     '    IItemOperator source;\n'
     '\n'
     '    /// <summary>当前显示的格子。就是 source.GetItemList() 那个列表本身</summary>\n'
     '    IReadOnlyList<ItemData> items;\n'
     '\n'
     '    /// <summary>外部把要显示的容器推进来（背包、箱子、商店都行）。传 null 就是清空。</summary>\n'
     '    public void Bind(IItemOperator source)\n'
     '    {\n'
     '        this.source = source;\n'
     '        Refresh();\n'
     '    }'),

    ('    public override void Refresh()\n'
     '    {\n'
     '        if (items == null && player != null && player.Data != null)\n'
     '            items = player.Data.Bag;\n'
     '\n'
     '        RebuildSlots();',

     '    public override void Refresh()\n'
     '    {\n'
     '        // 没人 Bind 过就自己从玩家身上取 —— PlayerObj 本身就是背包容器\n'
     '        if (source == null && player != null) source = player;\n'
     '\n'
     '        items = source?.GetItemList();\n'
     '\n'
     '        RebuildSlots();'),

    ('            slots[i].Bind(i < count ? items[i] : null, i);',
     '            slots[i].Bind(i < count ? items[i] : null, i, source);'),
])


# ══════════════════ 6. QuickBar：同上 ══════════════════
patch('Assets/Scripts/UI/QuickBar.cs', [
    ('    /// <summary>传入的数据。持引用不持拷贝</summary>\n'
     '    IReadOnlyList<ItemData> items;\n',

     '    /// <summary>要显示的容器（快捷栏）。持引用不持拷贝</summary>\n'
     '    IItemOperator source;\n'
     '\n'
     '    /// <summary>当前显示的格子。就是 source.GetItemList() 那个列表本身</summary>\n'
     '    IReadOnlyList<ItemData> items;\n'),

    ('    public override void Refresh()\n'
     '    {\n'
     '        if (items == null && player != null && player.Data != null)\n'
     '            items = player.Data.quickBar;\n'
     '\n'
     '        int count = items == null ? 0 : items.Count;\n'
     '\n'
     '        for (int i = 0; i < quickSlots.Count; i++)\n'
     '            quickSlots[i].Bind(i < count ? items[i] : null, i);\n'
     '    }',

     '    public override void Refresh()\n'
     '    {\n'
     '        // 没人 Bind 过就自己找玩家的快捷栏容器\n'
     '        if (source == null && player != null) source = player.QuickBar;\n'
     '\n'
     '        items = source?.GetItemList();\n'
     '\n'
     '        int count = items == null ? 0 : items.Count;\n'
     '\n'
     '        for (int i = 0; i < quickSlots.Count; i++)\n'
     '            quickSlots[i].Bind(i < count ? items[i] : null, i, source);\n'
     '    }'),
])


if FAILED:
    print('\n!!! 失败项：')
    for f in FAILED:
        print('  -', f)
    sys.exit(1)
print('\nOK')
