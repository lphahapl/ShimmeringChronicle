"""Create repeatable inventory fixtures without replacing existing assets."""
from pathlib import Path
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
ITEM_DIR = ROOT / 'Assets/Configs/ItemSO'
CHEST_DIR = ROOT / 'Assets/Configs/ChestSO'


def read(path):
    return path.read_text(encoding='utf-8-sig')


def guid(path):
    return re.search(r'^guid: (\w+)', read(Path(str(path) + '.meta')), re.M)[1]


def create(path, content):
    if not path.exists():
        path.write_text(content, encoding='utf-8')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text(
            f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n'
            'NativeFormatImporter:\n  externalObjects: {}\n'
            '  mainObjectFileID: 11400000\n  userData: \n'
            '  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')


def main():
    specs = [
        ('test_potion', '测试药剂', 20, 'item_001'),
        ('test_herb', '测试草药', 50, 'item_002'),
        ('test_ore', '测试矿石', 99, 'item_003'),
        ('test_quest_crystal', '测试任务晶石', 1, 'item_003'),
        ('test_supply', '测试小份补给', 5, 'item_001'),
    ]
    for key, name, limit, source in specs:
        text = read(ITEM_DIR / f'{source}.asset')
        fields = {'m_Name': key, 'id': key, 'itemName': json.dumps(name, ensure_ascii=False),
                  'description': json.dumps(f'背包交互测试物品，堆叠上限 {limit}；复用现有图标，未配置使用效果。', ensure_ascii=False),
                  'stackCount': str(limit)}
        for field, value in fields.items():
            text = re.sub(rf'^  {field}:.*$', lambda _: f'  {field}: {value}', text, flags=re.M)
        create(ITEM_DIR / f'{key}.asset', text)

    fixtures = {
        'TestChest_Empty': [],
        'TestChest_Mixed': [('test_potion', 7), ('test_herb', 12), None,
                            ('test_ore', 30), ('test_quest_crystal', 1), ('test_supply', 3)],
        'TestChest_StackLimits': [('test_potion', 19), ('test_potion', 5), ('test_potion', 20),
                                 ('test_herb', 49), ('test_herb', 2), ('test_ore', 98),
                                 ('test_ore', 3), ('test_supply', 4), ('test_supply', 2)],
        'TestChest_Full': [(specs[i % len(specs)][0], specs[i % len(specs)][2]) for i in range(16)],
        'TestChest_Weapons': [('weapon_002', 1), ('weapon_003', 1), ('weapon_004', 1),
                              None, ('test_potion', 10), ('test_quest_crystal', 1)],
    }
    header = read(CHEST_DIR / 'testChest.asset').split('  capacity:')[0]
    for number, (name, slots) in enumerate(fixtures.items(), 101):
        text = re.sub(r'^  m_Name:.*$', f'  m_Name: {name}', header, flags=re.M)
        text += f'  capacity: 16\n  num: {number}\n'
        text += '  items:\n' if slots else '  items: []\n'
        for slot in slots:
            if slot is None:
                text += '  - config: {fileID: 0}\n    num: 0\n'
            else:
                key, count = slot
                asset = ITEM_DIR / f'{key}.asset'
                limit = int(re.search(r'^  stackCount: (\d+)', read(asset), re.M)[1])
                assert 0 < count <= limit
                text += f'  - config: {{fileID: 11400000, guid: {guid(asset)}, type: 2}}\n    num: {count}\n'
        assert len(slots) <= 16
        create(CHEST_DIR / f'{name}.asset', text)

    group = ROOT / 'Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset'
    text = read(group)
    for key, *_ in specs:
        item_guid = guid(ITEM_DIR / f'{key}.asset')
        if f'  - m_GUID: {item_guid}\n' in text:
            continue
        entry = (f'  - m_GUID: {item_guid}\n    m_Address: Assets/Configs/ItemSO/{key}.asset\n'
                 '    m_ReadOnly: 0\n    m_SerializedLabels:\n    - ItemSO\n'
                 '    FlaggedDuringContentUpdateRestriction: 0\n')
        anchor = '  m_ReadOnly: 0\n  m_Settings:'
        assert text.count(anchor) == 1
        text = text.replace(anchor, entry + anchor)
    if text != read(group):
        group.write_text(text, encoding='utf-8')
    print('Validated 5 test items, 5 chest configs (16 slots), and ItemSO Addressables entries.')


if __name__ == '__main__':
    main()
