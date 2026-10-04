"""Create missing sample item assets; preserve existing assets and GUIDs."""
from pathlib import Path
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
ITEMS = [
    ('weapon_002', '铁卫练习剑', '均衡的练习用直剑，适合测试基础三连击。', 1, [8, 12, 18], [1.3, 1.5, 1.7]),
    ('weapon_003', '霜华', '攻击范围较大的寒蓝长剑；当前仅配置物理伤害，不附带冰冻效果。', 1, [12, 16, 24], [1.7, 1.9, 2.2]),
    ('weapon_004', '紫电', '高伤害的紫晶长剑；当前不附带雷元素效果。', 1, [16, 22, 32], [1.4, 1.6, 1.9]),
    ('item_001', '红玉药剂', '补给类测试物品，可堆叠 20 瓶。暂未实现使用和回血效果。', 20, [], []),
    ('item_002', '银叶草', '常见的炼金材料，可堆叠 50 份。', 50, [], []),
    ('item_003', '星蓝矿晶', '武器锻造用的晶矿，可堆叠 99 份。暂未实现合成效果。', 99, [], []),
]

def guid_for(path, importer=''):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
    guid = uuid.uuid4().hex
    meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\n' + importer, encoding='utf-8')
    return guid

def main():
    configs = ROOT / 'Assets/Configs/ItemSO'
    icons = ROOT / 'Assets/Art/UI/SampleItems'
    icons.mkdir(parents=True, exist_ok=True)
    guid_for(icons, 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    texture_template = (ROOT / 'Assets/Art/UI/Astral/weapon_001_icon.png.meta').read_text()
    group = ROOT / 'Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset'
    group_text = group.read_text(encoding='utf-8')
    entries = ''
    manifest = []
    for id, name, description, stack, damage, radius in ITEMS:
        icon = icons / f'{id}.png'
        if not icon.exists():
            raise FileNotFoundError(icon)
        icon_meta = Path(str(icon) + '.meta')
        if not icon_meta.exists():
            icon_meta.write_text(re.sub(r'^guid: \w+', 'guid: ' + uuid.uuid4().hex, texture_template, flags=re.M), encoding='utf-8')
        icon_guid = guid_for(icon)
        weapon = bool(damage)
        script_guid = '2ef66cb190b9b214daaecf638b5abe31' if weapon else '6b904376b32f4df09f3b6bcb7d8e13ac'
        kind = 'WeaponSO' if weapon else 'BasicItemSO'
        asset = configs / f'{id}.asset'
        if not asset.exists():
            text = f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: {id}
  m_EditorClassIdentifier: Assembly-CSharp::{kind}
  id: {id}
  itemName: {json.dumps(name, ensure_ascii=False)}
  description: {json.dumps(description, ensure_ascii=False)}
  icon: {{fileID: 21300000, guid: {icon_guid}, type: 3}}
  stackCount: {stack}
  prefab: {{fileID: 0}}
'''
            if weapon:
                text += '  combo:\n'
                for index, (d, r) in enumerate(zip(damage, radius)):
                    text += f'  - damagePerHit: {d}\n    radius: {r}\n    judgeOffset: {{x: 0, y: 0, z: {0.5 if index == 2 else 0.3}}}\n'
            asset.write_text(text, encoding='utf-8')
        asset_guid = guid_for(asset, 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        if f'  - m_GUID: {asset_guid}\n' not in group_text:
            entries += f'  - m_GUID: {asset_guid}\n    m_Address: Assets/Configs/ItemSO/{id}.asset\n    m_ReadOnly: 0\n    m_SerializedLabels:\n    - ItemSO\n    FlaggedDuringContentUpdateRestriction: 0\n'
        manifest.append(dict(id=id, name=name, type=kind, stackCount=stack, damage=damage, radius=radius, guid=asset_guid))
    if entries:
        anchor = '  m_ReadOnly: 0\n  m_Settings:'
        assert group_text.count(anchor) == 1
        group.write_text(group_text.replace(anchor, entries + anchor), encoding='utf-8')
    (ROOT / 'Tools/sample_items.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    player = ROOT / 'Assets/Configs/PlayerSO/SampleInventoryPlayer.asset'
    if not player.exists():
        source = ROOT / 'Assets/Configs/PlayerSO/TestPlayer1.asset'
        text = source.read_text(encoding='utf-8').split('  quickBar:')[0]
        text = re.sub(r'  m_Name: .*', '  m_Name: SampleInventoryPlayer', text)
        text = re.sub(r'  targetName: .*', '  targetName: "背包测试角色"', text)
        text = re.sub(r'  description: .*', '  description: "包含三种武器及三种堆叠物品的测试配置"', text)
        by_id = {item['id']: item['guid'] for item in manifest}
        def slot(id, count):
            return f'  - config: {{fileID: 11400000, guid: {by_id[id]}, type: 2}}\n    num: {count}\n'
        text += '  quickBar:\n' + slot('weapon_002', 1) + slot('item_001', 5)
        text += '  Bag:\n'
        for id, count in [('weapon_003', 1), ('weapon_004', 1), ('item_001', 18), ('item_002', 48), ('item_003', 99)]:
            text += slot(id, count)
        player.write_text(text, encoding='utf-8')
        guid_for(player, 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    print(f'Created/checked {len(manifest)} sample items and Addressables entries.')

if __name__ == '__main__':
    main()
