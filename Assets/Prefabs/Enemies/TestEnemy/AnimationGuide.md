# 方块测试敌人动画

## 资源目录

- 本目录的 `Animations/`：待机、行走、追逐、受击、死亡。
- `../Items/EnemyWeapon_001/Animations/`：默认武器的两段攻击，曲线绑定敌人的 `foot/Cube/hand`，由敌人 Animator 播放，不是武器单独播放。
- `../Items/<物品名>/`：每个物品的 SO 与预制体。专属动画放在该物品的 `Animations/`，共用模型、材质、玩家武器覆盖控制器保持原位置。
- 已移动资产的 meta GUID 保持不变；Addressables 原有地址字符串保留，仍通过 GUID 定位资源。

## 动作与触发

| 动作 | 时长 | 根运动 | 触发 |
| --- | --- | --- | --- |
| Idle | 1.4 秒循环 | 原地呼吸 | 默认状态 |
| Walk | 0.8 秒循环 | 前进 1.2 米/周期 | isWalking=true |
| Chase | 0.48 秒循环 | 前进 1.68 米/周期 | 从 Walk 设置 isRunning=true |
| Hit | 0.35 秒 | 后退 0.15 米 | Hit trigger |
| Die | 0.95 秒 | 根位置不变，模型倒下 | isDied trigger |
| Attack | 0.65 秒 | 横扫前冲 0.45 米 | Attack1 trigger |
| Attack02 | 0.85 秒 | 蓄力下劈前冲 0.7 米 | 由 EnemyAI 在第一段结束后触发 Attack2 |

第一段 0.30 秒发送 PublishEnemyCombo(0)，第二段 0.45 秒发送 PublishEnemyCombo(1)。武器 SO 已有两段伤害配置。死亡 0.93 秒调用 Died 回收到对象池。

根运动只包含水平增量，根 Y 始终为零。Animator 已启用 Apply Root Motion。EnemyAI 使用 Agent 寻路与转向，关闭 Agent 的自动位置更新，在 OnAnimatorMove 中同步动画位置和 Agent；攻击时暂停 Agent 自动转向。

## 预制体尺寸约定

- 根节点位置为 `(0, 0, 0)`，原点在脚底，缩放为 `(1, 1, 1)`。NavMeshAgent、EnemyAI、Animator、EnemyObj、EnemyHPBar 和一个无重力的运动学 Rigidbody 都在根节点。
- `foot` 是动画支点，默认位置为零。`foot/Cube` 位于 `(0, 1, 0)`，保持单位缩放；身体 BoxCollider 直接用 Size `(1.2, 2, 1)` 表达尺寸。
- `foot/Cube/BodyMesh` 只包含网格显示，缩放 `(1.2, 2, 1)`。`foot/Cube/hand` 保持单位缩放，因此挂载的武器不再继承身体的非均匀拉伸。既有攻击动画绑定路径保持有效。
- 根节点 BoxCollider 是感知 Trigger，中心为 `(0, 1, 0)`；运行时由 EnemyAI 调整 Size。NavMeshAgent 的 Height 为 2、Radius 为 0.6、Base Offset 为 0。
- `HealthCanvas` 独立挂在根节点，高度 2.5 米，Rect 大小为 `240×80`，统一缩放 `0.01`；内部 RectTransform 全部为单位缩放，100 UI 单位对应 1 米。这种做法遵循 [Unity World Space UI 的尺寸与缩放约定](https://docs.unity.cn/Packages/com.unity.ugui%403.0/manual/HOWTO-UIWorldSpace.html)。
- 姓名位于上方，血条位于下方，血量数值居中叠在条上；填充条按父级拉伸并留内边距。EnemyHPBar 在 LateUpdate 中让 Canvas 与主摄像机朝向一致，复用时刷新，禁用时取消订阅并停止缓冲动画。
- SampleScene 的敌人实例使用预制体上的 AI/Agent/Rigidbody，已移除旧的 foot 偏移和倾斜覆盖；根节点 Y 调整为 7.216，与原来的脚底支点高度对应。

## 验证

已检查预制体 YAML、内部组件引用、层级、缩放、动画路径与事件时间、死亡位置曲线以及场景覆盖。C# 项目编译通过，有一条 System.Net.Http 版本引用冲突警告。尚未在 Unity 播放验证。运行时可在 Animator 参数面板依次测试 isWalking、isRunning、Attack1、Hit、isDied。
