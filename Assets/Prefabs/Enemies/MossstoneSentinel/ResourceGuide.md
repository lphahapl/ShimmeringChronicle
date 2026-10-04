# 苔石守卫资源

对应设计稿：`Tools/EnemyConcepts/MossstoneSentinel/MossstoneSentinel-concept-v3.png`。
当前模型是用于游戏测试的分块低多边形版本，约 2.6 米高，根节点缩放为 1，脚底为原点。保留苔藓、石纹、青色晶核与黄铜装饰。

## 使用

身体预制体：`MossstoneSentinel.prefab`。已绑定 EnemyObj、EnemyAI、Animator、NavMeshAgent、EnemyHPBar、手部挂点和血条。
默认 EnemySO 是 `MossstoneSentinel.asset`，默认武器为空物体武器 `MossstoneFists`。
`MossstoneSentinel_Blade.asset` 共用身体预制体，默认武器改为长石刀。它用于验证同一个身体配不同武器，不代表已实现精英敌人或主动换武器逻辑。

```csharp
await GameManager.Instance.LoadingTasks.EnemyComplete;
EnemyManager.Instance.SpawnEnemy("Enemy_MossstoneSentinel", position, rotation);
// 使用持刀配置：
EnemyManager.Instance.SpawnEnemy("Enemy_MossstoneSentinel_Blade", position, rotation);
```

两个 EnemySO 已加入 `EnemySO` 标签；两个 WeaponSO 已加入 `ItemSO` 标签。直接放置预制体时使用项目现有 EnemyManager 完成死亡回收。
没有修改或放置当前场景，也没有新增 EliteEnemyObj。

## 身体动画

`Animations` 包含 Idle、Walk、Chase、Hit、Die 和第三段占位动画。基础控制器默认使用空手攻击动画，两个武器各自带 AnimatorOverrideController。
控制器状态为 Idle / Walk / Chase / Hit / Die / Attack1 / Attack2 / Attack3。
参数：isWalking、isRunning 为 Bool；Hit、isDied、Attack1、Attack2、Attack3 为 Trigger。

移动与攻击保留水平根位移，根节点 Y 始终为 0；受击和待机原地播放。死亡仅倾倒模型关节，不把敌人根节点移入地下。
Walk：1.5 秒移动 1.2 米，即 0.8 米/秒。Chase：1.1 秒移动 1.76 米，即 1.6 米/秒。
现有 EnemyAI.OnAnimatorMove 负责协调动画根位移与 NavMeshAgent。
攻击入口使用零时长过渡，以配合现有 AI 的 Idle 连招复位逻辑，避免过渡期间重复第一段。

## 武器

| 配置 | 连招 | 动画时长 | 命中事件时间 | 根位移 | 连招结束间隔 |
| --- | --- | --- | --- | --- | --- |
| 空手第 1 段 | 直拳 | 1.15 秒 | 0.67 秒 | 0.30 米 | 1.5 秒 |
| 空手第 2 段 | 双拳砸击 | 1.55 秒 | 0.97 秒 | 0.40 米 | 1.5 秒 |
| 刀第 1 段 | 肩部冲撞 | 1.35 秒 | 0.92 秒 | 0.95 米 | 1.8 秒 |
| 刀第 2 段 | 横斩 | 1.35 秒 | 0.80 秒 | 0.35 米 | 1.8 秒 |
| 刀第 3 段 | 蓄力重劈 | 1.85 秒 | 1.19 秒 | 0.55 米 | 1.8 秒 |

空手资源位于 `Assets/Prefabs/Items/MossstoneFists`，WeaponSO ID 为 `enemy_mossstone_fists`。
长石刀资源位于 `Assets/Prefabs/Items/MossstoneBlade`，WeaponSO ID 为 `enemy_mossstone_blade`。
空手预制体没有可见网格，但带 WeaponObj，拥有完整的 WeaponData、攻击判定和覆盖控制器。
每段动画用 PublishEnemyCombo(int) 发布命中，索引从 0 开始；结束前 0.1 秒用 ChangeAttackStatu 恢复攻击状态。Die 在 1.65 秒调用 Died。

## 碰撞与模型

根节点感知 BoxCollider 在 Default 层，BodyCollider 胶囊在 enemy 层。这样玩家武器的 enemy 掩码只检测身体，不会把整个追逐触发范围当成受击面积。
chaseDistance 为 18，现有 AI 将它作为感知 Box 的横向边长。
身体胶囊高 2.58 米、半径 0.58 米；Agent 高 2.6 米、半径 0.62 米。
身体约 3196 个三角形、15 个关节网格，使用 Generic Avatar 与关节 Transform 曲线；材质为项目 URP/Lit。
石纹和苔藓共用 512 像素贴图，苔藓使用 Alpha Clip。

## 验证与预览

`Tools/Enemies/Previews` 保存实际 Unity 模型及攻击姿势渲染。
`Tools/Enemies/play-result.txt` 保存隔离 Unity 项目中、使用工程真实脚本的运行结果：追逐根位移、两套连招、武器数据连接、死亡回收、重生恢复和碰撞分层均通过。
该测试在烘焙的平面 NavMesh 上完成；当前场景的 NavMesh 与实战手感仍需在编辑器中试玩调整。
`Tools/Enemies/BuildMossstoneSentinel.cs` 与 `PolishMossstone.cs` 是资源生成源代码，放在 Assets 外，不参与运行时编译。不要在已存在同名资源的项目中直接重复运行生成器。
