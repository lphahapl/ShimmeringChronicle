# 简洁幻想 UI

已修改 SampleScene、Assets/Prefabs/slot.prefab 和 weapon_001.asset。没有修改玩法脚本。

- 背包：蓝灰面板、浅金边、四列物品格、标题与 Tab 提示。
- 快捷栏：五格统一底图，物品图标保持比例。
- 血条：细绿色填充、金色受伤缓冲、米白数值。
- weapon_001：透明背景单手剑，已绑定 ItemSO.icon；背包和快捷栏通过现有 ItemData 获取。
- panel、slot、hp_track 使用 16px 九宫格；所有贴图禁用 mipmaps，保留 alpha。

素材位置：Assets/Art/UI/Astral。旧场景、预制体、武器配置备份在 UI_Backup。

检查：python Tools/UI/validate_theme.py。已通过 YAML 解析、局部引用、五项贴图绑定、透明通道和 Sprite 导入配置检查。没有完成 Unity Play Mode 目视验收。Unity 若仍显示缓存场景，重新打开 SampleScene；Tab 打开背包。

UI 基础几何素材使用 Tools/UI/Build-Frames.ps1 生成；Tools/UI/apply_theme.py 绑定主题。武器图标使用内置 image_gen，未使用 API/CLI。

## 武器图标最终提示词
Use case: stylized-concept. Create one production-ready Unity inventory weapon icon, square 1024x1024, truly transparent background. A single elegant anime fantasy one-handed sword for weapon_001, blade points upper right, hilt lower left, entire sword inside central 80 percent with clear padding. Genshin-like clean cel-painted equipment illustration, restrained silver steel blade, navy leather grip, small muted champagne gold guard, small pale teal jewel. Simple readable silhouette at 64px, polished but not ornate, no glow, no particles, no text, no border, no shadow backdrop. Save output image.
