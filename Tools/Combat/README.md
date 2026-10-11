# 削韧配置与玩家逻辑

2026-10-10。数值为初始试玩配置，后续按战斗手感调整。

PlayerSO/EnemySO 的 poise 是韧性上限；PlayerObj/EnemyObj 的 poise 是当前韧性。玩家受击先扣韧再发布 Damaged，未破韧保留连招，破韧当次打断。每次受击重置回满倒计时，每秒恢复不超过上限，恢复使用 dtFix。

| 配置 | 韧性 | 恢复/秒 | 回满时间/秒 |
|---|---:|---:|---:|
| MossstoneSentinel | 120 | 10 | 4 |
| MossstoneSentinel_Blade | 120 | 10 | 4 |
| ReinforcedSentinel | 160 | 12 | 4 |
| ReinforcedSentinel_Blade | 160 | 12 | 4 |
| testenemy | 80 | 8 | 3 |
| SampleInventoryPlayer | 100 | 10 | 3 |
| TestPlayer1 | 100 | 10 | 3 |

| 武器 | 各段削韧 |
|---|---|
| EnemyWeapon_001 | 35 / 65 |
| MossstoneBlade | 30 / 40 / 70 |
| MossstoneFists | 45 / 65 |
| ReinforcedBlade | 40 / 55 / 85 |
| ReinforcedFists | 50 / 80 |
| weapon_001 | 30 / 40 / 70 |
| weapon_002 | 20 / 25 / 40 |
| weapon_003 | 40 / 55 / 90 |
| weapon_004 | 50 / 70 / 110 |

已在独立 Unity 6000.3.10f1 工程中编译实际游戏脚本，执行 Editor 模式命中与事件验证：16 项玩家逻辑检查通过；16 份配置、24 段攻击均通过 Unity 反序列化、CreateData 和克隆检查。未进行场景试玩平衡验收。

复验脚本：PlayerPoiseValidation.cs.txt、PoiseConfigValidation.cs.txt；复制到独立工程 Assets/Editor，保留游戏脚本的原始 .meta GUID，使用 -executeMethod PlayerPoiseValidation.Run / PoiseConfigValidation.Run 执行。验证记录分别见 player-poise-validation.txt、poise-config-validation.txt。
