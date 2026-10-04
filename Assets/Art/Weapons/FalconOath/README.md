# 隼翼誓约 / Falcon Oath

根据选定的 B 款概念制作的游戏用长枪模型。

- 直接使用 FalconOath.prefab，已配置 6 个 URP/Lit 材质。
- FalconOath.obj 与 FalconOath.mtl 为可编辑、可跨软件导入的网格源文件。单位米，Y 轴向上，总长 2.25 米。
- 原点位于下方握柄中心；GripPoint 为持握参考，TipPoint 为枪尖参考。
- 5,852 个三角面。枪头新增连续中央棱线与窄刃口倒角；金属羽翼、宝石、尾锥与布带均为实际几何。
- OBJ 包含完整的单位法线与逐面法线引用，避免首次导入时出现缺失法线警告；Unity 预制体继续使用 35 度重算法线以保持现有平滑效果。
- 钢、黑钢、黄铜、皮革和布料各使用三张 1024 像素 PBR 贴图：底色、金属度/光滑度（R/A）、切线空间法线。钢包含细拉丝与轻微划痕；宝石使用高光滑度材质。没有布料骨骼或布料模拟。UV 为可平铺的基础坐标，不是独占烘焙图集。
- 未替换角色现有武器，也未改变场景。可将预制体放入场景检查，再挂到角色手部骨骼调整旋转。

生成脚本：Tools/WeaponConcepts/build_falcon.py。
Unity 重建菜单：Tools > Weapons > Build Falcon Oath。重建会更新该预制体，已有材质参数予以保留。
实际网格预览：Tools/WeaponConcepts/B-Falcon-Oath-model.png。
Unity 导入尺寸验证：unity-validation.txt。
法线回归检查：python Tools/WeaponConcepts/check_falcon_normals.py。
贴图生成：Tools/WeaponConcepts/create_surface_maps.py。材质重新配置菜单：Tools > Art > Upgrade Falcon and Bind Yoimiya。
金属反射效果需要场景的天空环境或 Reflection Probe；没有环境反射的场景会显得偏暗。
