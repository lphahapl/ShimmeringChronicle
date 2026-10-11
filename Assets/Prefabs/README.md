# 资源目录

`Item`、`Weapon`、`Player` 位于同一层级。每个物品、武器或玩家拥有自己的目录：预制体和配置放在目录根部，动画片段、Animator Controller 和 Animator Override Controller 放在 `Animations`，模型放在 `Models`，材质与贴图分别放在 `Materials` 和 `Textures`。没有独立动画或模型的资源直接引用共用资源，不复制资产或建立空目录。

```text
Assets/Prefabs/
  Item/<物品>/
  Weapon/<武器>/
  Weapon/Shared/Models/Sword/
  Player/Warrior/
  Player/KeqingRootMotion/
  Enemies/<敌人>/
  UI/Inventory/
  UI/PlayerHUD/
```

原 Warrior 玩家及两份玩家配置位于 `Player/Warrior`。通用移动、无武器动作和手持姿势位于其 `Animations`；三段剑击位于 `Weapon/weapon_001/Animations`，刻晴生成工具也从那里读取参考动作。刻晴源模型与材质位于 `Player/KeqingRootMotion/Models/Source`。

`Weapon/Shared/Models/Sword` 是多把测试剑共用的模型；`FalconOath` 是独立的长枪美术资源，尚未连接 WeaponSO。普通物品共用的手持重写控制器仍由配置引用。

`Tools` 仅保留战斗验证、玩家/敌人动作生成与验证、长枪模型和贴图生成工具。旧备份、过程截图、阶段性日志和一次性场景修复脚本已清理。此次删除的文件可从项目内 `Temp/folder-cleanup-recovery.zip` 恢复。

资源移动保留原 `.meta` 和 GUID；Addressables 的路径形式地址随资源更新。
