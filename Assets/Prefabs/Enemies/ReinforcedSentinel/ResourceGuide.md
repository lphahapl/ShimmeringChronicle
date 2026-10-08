# 强化守卫

原苔石守卫和强化守卫现在是两种独立的敌人。旧 `MossstoneSentinel`、`MossstoneFists`、`MossstoneBlade` 资源保持原样；原 V2 资源改名为 `ReinforcedSentinel`、`ReinforcedFists`、`ReinforcedBlade`，保留原 V2 的 GUID，不破坏已有场景或配置的引用。

## 模型与移动

约 3 米高、根节点缩放为 1。宽胸背、固定肩甲、石质护面、低眉眼缝、冠脊和厚重前臂。身体约 4576 个三角形，15 个关节网格，URP/Lit 材质，共享 512 像素石纹和苔藓贴图。

Walk 为 0.7 米/秒，Chase 为 1.6 米/秒。生命值 260。仍使用现有 EnemyObj、EnemyAI 和 Generic Avatar，没有新增 EliteEnemyObj。

## 攻击

| 动作 | 总时长 | 命中事件 | 解除攻击锁定 | 最终水平根位移 |
| --- | --- | --- | --- | --- |
| 空手第一击：左手重拳 | 1.80 秒 | 0.70 秒 | 1.72 秒 | 0.12 米 |
| 空手第二击：弯肘双小臂砸地 | 2.40 秒 | 1.02 秒 | 2.32 秒 | 0 米 |
| 持刀第一击：身体冲撞 | 2.15 秒 | 1.03 秒 | 2.07 秒 | 0.70 米 |
| 持刀第二击：大幅横斩 | 2.55 秒 | 1.15 秒 | 2.47 秒 | 0 米 |
| 持刀第三击：弯肘左小臂砸地 | 3.20 秒 | 1.51 秒 | 3.12 秒 | 0 米 |

左拳蓄力后伸肩、伸肘打向前方，命中时左手关节相对根节点向前约 1.48 米；右手保持护身。
身体冲撞有小幅前移、惯性和制动，最终前移从旧 V2 的 1.56 米缩短为 0.70 米。
横斩保留大幅扭腰、刀身过冲和缓慢收回。

两种砸地都朝向正前方，躯干前倾约 80 度、膝盖深屈，手肘约 90 度弯曲，小臂朝前并接近水平。命中时小臂下沿距地约 0.004 米，使用小臂接触地面，保持低姿态后再缓慢起身。空手用双小臂砸地；持刀用左小臂砸地，右手抬高、收刀在侧面。第三击不侧身，也不再用刀劈地。砸地命中判定中心保持约 0.45 米。

重拳微步前移，冲撞保留水平根运动；横斩和两种砸地的根位置曲线全程为零，蓄力与起身也不产生位移。所有动作的根节点 Y 曲线恒为 0。

待机和攻击使用前后错脚站姿，两脚前后间隔约 0.46 米。重拳前脚小步跟进，后脚在收拳时补步；冲撞前脚抬起迈步，后脚蹬地后跟进，再以错脚姿势制动。原地砸地与横斩固定双脚，砸地时髋部后坐、弯腰下压，躯干动作不会带动根节点滑动。脚步和腿部双骨解算在制作阶段烘焙成旋转曲线，运行时不添加碰撞求解或 IK。
所有攻击有蓄力、命中、过冲、停顿与慢速回收。空手全套间隔 1.3 秒，持刀全套间隔 1.6 秒。

控制器仍使用 Attack1 / Attack2 / Attack3 Trigger。PublishEnemyCombo 索引为 0 / 1 / 2；ChangeAttackStatu 在回收末尾触发。无下一段时 normalizedTime 1.03 返回 Idle，保留结束事件缓冲。

## 使用

- 身体预制体：`Assets/Prefabs/Enemies/ReinforcedSentinel/ReinforcedSentinel.prefab`。
- 默认空手配置：`ReinforcedSentinel.asset`，ID `Enemy_ReinforcedSentinel`。
- 持刀配置：`ReinforcedSentinel_Blade.asset`，ID `Enemy_ReinforcedSentinel_Blade`。
- 空手武器：`Assets/Prefabs/Items/ReinforcedFists`，ID `enemy_reinforced_fists`。
- 长石刀：`Assets/Prefabs/Items/ReinforcedBlade`，ID `enemy_reinforced_blade`。

两套 EnemySO 共用强化守卫身体，默认武器不同。空手武器是带 WeaponObj 的空物体，有独立 WeaponData 和二连击配置。四个配置保留各自 GUID，Addressables 路径已更新，EnemySO / ItemSO 标签保留。

```csharp
await GameManager.Instance.LoadingTasks.EnemyComplete;
EnemyManager.Instance.SpawnEnemy("Enemy_ReinforcedSentinel", position, rotation);
// 或持刀：
EnemyManager.Instance.SpawnEnemy("Enemy_ReinforcedSentinel_Blade", position, rotation);
```

也可拖入强化守卫预制体，并将 EnemyObj.SO 指向持刀配置测试。现有场景未替换旧敌人。预制体脚本、血条和武器挂点引用已绑定。感知 BoxCollider 使用 Default 层，身体胶囊使用 enemy 层。

## 检查与预览

`Tools/Enemies/MossstoneSentinelV2/Previews/fists-two-hit.gif` 与 `blade-three-hit.gif` 是固定镜头的 Unity 原生模型完整连招预览，地面网格保持静止，便于观察根位移。`Mossstone-left-Reinforced-right.png` 左侧是原苔石守卫，右侧是强化守卫。PNG 展示蓄力、命中和后摇关键姿态。

隔离 Unity 工程使用本项目真实 EnemyAI / EnemyObj 验证移动、两套连招事件顺序、命中后的攻击锁定、整套间隔、武器数据连接、死亡回收与重生。测试副本只移除了四条无用的 STP/ShaderGraph using，主工程游戏脚本没有修改。

按 60 FPS 及所有事件/关键帧采样，检查手、前臂、刀身与头部/躯干的穿模，脚底与刀身的地面高度。额外检查砸地命中时的小臂高度、肘角、水平姿态、前伸距离、全程不侧身与小臂/手不穿地。肩甲/关节的连接处不属于此检测范围。平地测试通过；实际坡面和墙边效果仍需场景试玩。

`reinforced-contact-result.txt` 记录小臂触地、弯肘与左拳前伸数据；`v2-play-result.txt` 记录 Play Mode 检查；`v1-preserved-result.txt` 记录原苔石守卫 100 个资源和 meta 文件的保留情况：99 个文件与前次快照一致，另一个控制器的外部编辑已原样保留。工具目录保留原 V2 命名用于追溯生成过程，不参与运行时编译。

本次错脚与位移修改只更新五段攻击动画和强化守卫待机动画，保留所有 GUID；预制体、SO 和主工程脚本保持原样。`stance-preserved-result.txt` 对照本次修改前的快照检查旧敌人全部 100 个资源/meta；`reinforced-stance-result.txt` 记录脚步、根位移和地面检查，`v2-play-timing.txt` 记录实际连招的根位移。
