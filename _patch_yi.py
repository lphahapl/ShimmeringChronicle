import io

def patch(path, pairs):
    s = io.open(path, encoding='gbk').read().replace('\r\n', '\n')
    for old, new in pairs:
        assert old in s, '%s: anchor not found:\n%s' % (path, old[:60])
        s = s.replace(old, new, 1)
    io.open(path, 'w', encoding='gbk', newline='').write(s.replace('\n', '\r\n'))
    print('patched:', path)


# ---------- PlayerSO.cs：列表换成配置侧格子 ----------
patch('Assets/Scripts/SO/PlayerSO.cs', [
    ('public List<ItemData> quickBar;', 'public List<ItemSlotData> quickBar;'),
    ('public List<ItemData> Bag;',      'public List<ItemSlotData> Bag;'),
])

# ---------- ItemSO.cs：删掉 initialCount ----------
patch('Assets/Scripts/SO/ItemSO.cs', [
    ('    public GameObject prefab;\n'
     '\n'
     '    [Tooltip("初始数量。转成运行时数据时填进 ItemData.count，开局背包 / 掉落默认给几个")]\n'
     '    public int initialCount = 1;\n'
     '\n'
     '    /// <summary>',
     '    public GameObject prefab;\n'
     '\n'
     '    /// <summary>'),
    ('            icon        = icon,\n'
     '            count       = initialCount,\n',
     '            icon        = icon,\n'),
])

# ---------- ItemData.cs：注释里的来源改了 ----------
patch('Assets/Scripts/Data/ItemData.cs', [
    ('    /// 堆叠数量。初始值从 ItemSO.initialCount 拷过来，之后是纯运行时的 ——\n',
     '    /// 堆叠数量。开局从配置侧的 ItemSlotData.num 填进来，之后是纯运行时的 ——\n'),
])
