"""Create held-item assets without requiring a running Unity editor."""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets'
FOLDER = ASSETS / 'Prefabs'


def guid(path):
    return re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8'), re.M)[1]


def create():
    FOLDER.mkdir(exist_ok=True)
    for config in sorted((ASSETS / 'Configs/ItemSO').glob('*.asset')):
        data = config.read_text(encoding='utf-8')
        path = FOLDER / (config.stem + '.prefab')
        if path.exists():
            raise RuntimeError(f'Refusing to overwrite {path}')
        if 'Assembly-CSharp::WeaponSO' in data:
            prefab = (ASSETS / 'Art/Weapons/Sword/Sword (1).prefab').read_text(encoding='utf-8')
            prefab = prefab.replace('  m_Name: Sword\n', f'  m_Name: {config.stem}\n', 1)
            prefab = prefab.replace('  - component: {fileID: 2263224534130671834}',
                '  - component: {fileID: 2263224534130671834}\n  - component: {fileID: 11400000}', 1)
            root_id = 6224495756377181913
            prefab += f'''--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid(ASSETS / 'Scripts/Logic/WeaponObj.cs.meta')}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::WeaponObj
  SO: {{fileID: 11400000, guid: {guid(Path(str(config) + '.meta'))}, type: 2}}
  parent: {{fileID: 0}}
'''
        else:
            root_id = 100000
            icon = re.search(r'^  icon: (.+)$', data, re.M)[1]
            icon_guid = re.search(r'guid: (\w+)', icon)[1]
            meta = next(p for p in (ASSETS / 'Art/UI').rglob('*.png.meta') if guid(p) == icon_guid)
            from PIL import Image
            with Image.open(str(meta)[:-5]) as img:
                pixels = max(img.size)
            ppu = float(re.search(r'spritePixelsToUnits: ([\d.]+)', meta.read_text())[1])
            scale = 0.35 * ppu / pixels
            prefab = f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 400000}}
  m_Layer: 0
  m_Name: {config.stem}
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &400000
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children:
  - {{fileID: 400001}}
  m_Father: {{fileID: 0}}
--- !u!1 &100001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 400001}}
  - component: {{fileID: 21200000}}
  m_Layer: 0
  m_Name: Icon
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &400001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100001}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: {scale}, y: {scale}, z: {scale}}}
  m_Children: []
  m_Father: {{fileID: 400000}}
--- !u!212 &21200000
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100001}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_Materials:
  - {{fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}}
  m_SortingLayerID: 0
  m_SortingOrder: 0
  m_Sprite: {icon}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {{x: 1, y: 1}}
  m_SpriteTileMode: 0
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
'''
        prefab_guid = uuid.uuid4().hex
        path.write_text(prefab, encoding='utf-8')
        Path(str(path) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {prefab_guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
        data, count = re.subn(r'^  prefab: .+$', f'  prefab: {{fileID: {root_id}, guid: {prefab_guid}, type: 3}}', data, flags=re.M)
        assert count == 1
        config.write_text(data, encoding='utf-8')
        print(f'{config.stem} -> {path.relative_to(ROOT)}')


if __name__ == '__main__':
    create()
