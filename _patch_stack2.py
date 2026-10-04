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


# ══════════════════════════ PlayerObj：接口全实现 ══════════════════════════
write('Assets/Scripts/Logic/PlayerObj.cs', '''using System.Collections.Generic;
using UnityEngine;

public class PlayerObj : MonoBehaviour, IDamageable, IItemOperator
{
    [SerializeField] PlayerSO config;

    private PlayerData data;

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

    public void Start()
    {
        UIManager.Instance.RefreshAll();
    }

    // ───────────────────────── 容器 ─────────────────────────

    /// <summary>IItemProvider：这个组件对外暴露的容器是背包</summary>
    public List<ItemData> GetItemList()
    {
        var d = Data;
        return d == null ? null : d.Bag;
    }

    public bool ContainsItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        var bag = GetItemList();
        return bag != null && FindItem(bag, id) >= 0;
    }

    /// <summary>
    /// 往背包里加东西。先塞现成的堆，堆满了再开新格，受 stackCount 约束。
    /// 返回实际放进去几个 —— 比 count 小就说明有没放进去的。
    ///
    /// 注意：靠 id 反查配置走的是 GameManager，它还没加载完就放不进去（返回 0）。
    /// </summary>
    public int AddItem(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return 0;

        var d = Data;
        if (d == null) return 0;

        var manager = GameManager.Instance;
        if (manager == null || !manager.IsReady)
        {
            Debug.LogWarning("[PlayerObj] GameManager 还没准备好，加不了物品", this);
            return 0;
        }

        var so = manager.GetItem(id);
        if (so == null) return 0;

        d.Bag ??= new List<ItemData>();

        int added = AddInto(d.Bag, so, count);
        if (added <= 0) return 0;

        this.Publish(GameEvents.OnItemsChanged, d.Bag);
        return added;
    }

    /// <summary>
    /// 按 id 扣，扣的是第一个够扣的堆。
    /// 不够扣就一个都不动、返回 false —— 不做"有多少扣多少"，那样调用方很难判断结果。
    ///
    /// 同一个 id 占了多格时（堆满了就会这样）这里只认第一堆，想指定格子用 RemoveItemAt。
    /// </summary>
    public bool RemoveItem(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return false;

        var bag = GetItemList();
        if (bag == null) return false;

        int index = FindItem(bag, id, count);
        if (index < 0) return false;

        TakeFrom(bag, index, count);
        this.Publish(GameEvents.OnItemsChanged, bag);
        return true;
    }

    /// <summary>按格子位置扣。位置由 ItemSlot.index 给。</summary>
    public bool RemoveItemAt(int index, int count)
    {
        if (count <= 0) return false;

        var bag = GetItemList();
        if (bag == null) return false;
        if (index < 0 || index >= bag.Count) return false;

        var item = bag[index];
        if (item == null || item.count < count) return false;

        TakeFrom(bag, index, count);
        this.Publish(GameEvents.OnItemsChanged, bag);
        return true;
    }

    /// <summary>
    /// 拖拽落格：把背包 myIndex 格里的东西放进 other 的 otherIndex 格。
    /// myCount 传 0 表示整格都拿（普通拖拽），正数表示只拿这么多个（拆分拖拽）。
    ///
    /// 两边的列表都被改了，所以两个都要广播 —— 只发自己的，对方那边的界面不会刷。
    /// </summary>
    public bool ExchangeItem(IItemOperator other, int myIndex, int otherIndex, int myCount = 0)
    {
        if (other == null) return false;

        var mine = GetItemList();
        var theirs = other.GetItemList();
        if (mine == null || theirs == null) return false;

        if (!Exchange(mine, myIndex, theirs, otherIndex, myCount)) return false;

        this.Publish(GameEvents.OnItemsChanged, mine);
        this.Publish(GameEvents.OnItemsChanged, theirs);
        return true;
    }

    /// <summary>
    /// 手动喊一声"背包变了"。
    /// 直接改 Data.Bag（测试、或者一次改好几处）之后就靠它，不然界面不会知道。
    /// </summary>
    public void PublishBagChanged()
    {
        var d = Data;
        if (d != null) this.Publish(GameEvents.OnItemsChanged, d.Bag);
    }

    /// <summary>手动喊一声"快捷栏变了"。快捷栏的增删规则还没定，暂时由改它的地方自己调。</summary>
    public void PublishQuickBarChanged()
    {
        var d = Data;
        if (d != null) this.Publish(GameEvents.OnItemsChanged, d.quickBar);
    }

    // ─────────────────── 容器的公共规则 ───────────────────
    // 都是对 List<ItemData> 的纯函数，不碰 MonoBehaviour。
    // 以后有箱子了，这一整块挪到单独的文件里就能直接复用。

    /// <summary>找第一个 id 对得上、而且够扣 count 个的格子。找不到返回 -1。</summary>
    static int FindItem(List<ItemData> list, string id, int count = 1)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item != null && item.id == id && item.count >= count) return i;
        }
        return -1;
    }

    static int FindEmptySlot(List<ItemData> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == null) return i;

        return -1;
    }

    /// <summary>这一格最多堆几个。&lt;=0 当 1 —— 忘配了也不至于整件物品废掉。</summary>
    static int MaxStackOf(ItemData item) => item.stackCount <= 0 ? 1 : item.stackCount;

    /// <summary>
    /// 往列表里塞 count 个：先塞同 id 还没满的堆，剩下的每格开一堆。
    /// 返回实际塞进去几个。
    /// </summary>
    static int AddInto(List<ItemData> list, ItemSO so, int count)
    {
        int remaining = count;

        // 1. 先塞现成的堆
        for (int i = 0; i < list.Count && remaining > 0; i++)
        {
            var item = list[i];
            if (item == null || item.id != so.id) continue;

            int space = MaxStackOf(item) - item.count;
            if (space <= 0) continue;

            int take = Mathf.Min(space, remaining);
            item.count += take;
            remaining -= take;
        }

        // 2. 剩下的开新格，先填空位再往后加
        while (remaining > 0)
        {
            var item = so.CreateItemData();

            int take = Mathf.Min(MaxStackOf(item), remaining);
            item.count = take;
            remaining -= take;

            int empty = FindEmptySlot(list);
            if (empty >= 0) list[empty] = item;
            else list.Add(item);
        }

        return count - remaining;
    }

    /// <summary>
    /// 从第 index 格扣掉 count 个。
    /// 扣空了置 null 而不是删掉 —— 格子有位置，删了后面的会往前串。
    /// </summary>
    static void TakeFrom(List<ItemData> list, int index, int count)
    {
        var item = list[index];

        item.count -= count;
        if (item.count <= 0) list[index] = null;
    }

    /// <summary>
    /// 拖拽落格的核心。对两个 List 的纯函数 —— 跨容器的规则只在这一处。
    ///
    /// 三种落点：
    ///   空     → 搬过去（拆分拖拽要 Clone 一份，不然两格会指向同一个对象）
    ///   同 id  → 合并，被 stackCount 挡住，放不下的留在原格
    ///   不同 id → 整格互换（拆着拖过来的就原地不动）
    ///
    /// 全程先算清楚再写，不会出现"我的扣了、对方的没加"。
    /// </summary>
    static bool Exchange(List<ItemData> mine, int myIndex, List<ItemData> theirs, int otherIndex, int myCount)
    {
        if (myIndex < 0 || myIndex >= mine.Count) return false;
        if (otherIndex < 0 || otherIndex >= theirs.Count) return false;
        if (ReferenceEquals(mine, theirs) && myIndex == otherIndex) return false;

        var from = mine[myIndex];
        if (from == null || from.count <= 0) return false;

        // 拖多少个：0（或超过持有量）表示整格
        int move = (myCount <= 0 || myCount > from.count) ? from.count : myCount;
        var to = theirs[otherIndex];

        // ── 落点空 → 搬过去 ──
        if (to == null)
        {
            if (move >= from.count)
            {
                theirs[otherIndex] = from;
                mine[myIndex] = null;
            }
            else
            {
                var split = from.Clone();
                split.count = move;
                from.count -= move;
                theirs[otherIndex] = split;
            }
            return true;
        }

        // ── 落点同 id → 合并 ──
        if (to.id == from.id)
        {
            int space = MaxStackOf(to) - to.count;
            if (space <= 0) return false;      // 那堆满了，一个也塞不进

            int take = Mathf.Min(space, move);
            to.count += take;
            from.count -= take;
            if (from.count <= 0) mine[myIndex] = null;
            return true;
        }

        // ── 落点不同 id → 整格互换。拆着拖过来的原地不动 ──
        if (move < from.count) return false;

        theirs[otherIndex] = from;
        mine[myIndex] = to;
        return true;
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


# ══════════════════════════ ItemSlot：Bind 带下标 ══════════════════════════
patch('Assets/Scripts/Logic/ItemSlot.cs', [
    ('    public int index;//格子知道自己是第几个\n'
     '\n'
     '    /// <summary>把要显示的那一叠推进来。传 null 表示空格子。</summary>\n'
     '    public void Bind(ItemData data)\n'
     '    {\n'
     '        item = data;\n'
     '        RefreshSlot();\n'
     '    }',

     '    /// <summary>\n'
     '    /// 自己是第几格。由 Bind 填 —— 面板会复用格子，不能只在实例化时设一次。\n'
     '    /// "按位置操作"（RemoveItemAt / ExchangeItem）靠它。\n'
     '    /// 点击之前先判 item == null：空格子的 index 虽然是真的，但没有意义。\n'
     '    /// </summary>\n'
     '    public int index;\n'
     '\n'
     '    /// <summary>把要显示的那一叠、和它在容器里的位置推进来。data 传 null 表示空格子。</summary>\n'
     '    public void Bind(ItemData data, int index)\n'
     '    {\n'
     '        item = data;\n'
     '        this.index = index;\n'
     '        RefreshSlot();\n'
     '    }'),
])


# ══════════════════════════ BagPanel ══════════════════════════
patch('Assets/Scripts/UI/BagPanel.cs', [
    ('this.Subscribe(GameEvents.OnBagChanged, OnBagChanged);',
     'this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);'),
    ('this.UnSubscribe(GameEvents.OnBagChanged, OnBagChanged);',
     'this.UnSubscribe(GameEvents.OnItemsChanged, OnItemsChanged);'),
    ('    void OnBagChanged(List<ItemData> changed)', '    void OnItemsChanged(List<ItemData> changed)'),
    ('            slots[i].Bind(i < count ? items[i] : null);',
     '            slots[i].Bind(i < count ? items[i] : null, i);'),
])


# ══════════════════════════ QuickBar ══════════════════════════
patch('Assets/Scripts/UI/QuickBar.cs', [
    ('this.Subscribe(GameEvents.OnQuickBarChanged, OnQuickBarChanged);',
     'this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);'),
    ('this.UnSubscribe(GameEvents.OnQuickBarChanged, OnQuickBarChanged);',
     'this.UnSubscribe(GameEvents.OnItemsChanged, OnItemsChanged);'),
    ('    void OnQuickBarChanged(List<ItemData> changed)', '    void OnItemsChanged(List<ItemData> changed)'),
    ('            quickSlots[i].Bind(i < count ? items[i] : null);',
     '            quickSlots[i].Bind(i < count ? items[i] : null, i);'),
    ('        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);\n        OnShow();',
     '        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);\n\n'
     '        // 不要直接调 OnShow —— 那会绕过 Show()，IsOpen 一直停在 false。\n'
     '        // 开局刷一次用 Refresh() 就够了。\n'
     '        Refresh();'),
])


# ══════════════════════════ UIBase：补一个"开局先藏起来" ══════════════════════════
patch('Assets/Scripts/UI/UIBase.cs', [
    ('    protected virtual void OnShow() { }',
     '    /// <summary>\n'
     '    /// 开局先藏起来。Awake 里调 —— 直接调 Hide() 没用，\n'
     '    /// 它第一句是 if (!IsOpen) return，而这时 IsOpen 还是 false。\n'
     '    /// </summary>\n'
     '    protected void StartHidden()\n'
     '    {\n'
     '        if (viewRoot != null) viewRoot.SetActive(false);\n'
     '    }\n'
     '\n'
     '    protected virtual void OnShow() { }'),
])

patch('Assets/Scripts/UI/BagPanel.cs', [
    ('        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);\n        Hide();',
     '        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);\n'
     '        StartHidden();      // 不是 Hide()，那个在 Awake 里是空操作'),
])


if FAILED:
    print('\n!!! 失败项：')
    for f in FAILED:
        print('  -', f)
    sys.exit(1)
print('\nOK')
