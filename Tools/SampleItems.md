# 测试物品

新增资源位于 `Assets/Configs/ItemSO`，配套 PNG 位于 `Assets/Art/UI/SampleItems`，按单张 Sprite 导入。

| ID | 名称 | 堆叠上限 | 三连击伤害 | 判定半径 |
| --- | --- | --- | --- | --- |
| weapon_002 | 铁卫练习剑 | 1 | 8 / 12 / 18 | 1.3 / 1.5 / 1.7 |
| weapon_003 | 霜华 | 1 | 12 / 16 / 24 | 1.7 / 1.9 / 2.2 |
| weapon_004 | 紫电 | 1 | 16 / 22 / 32 | 1.4 / 1.6 / 1.9 |
| item_001 | 红玉药剂 | 20 | — | — |
| item_002 | 银叶草 | 50 | — | — |
| item_003 | 星蓝矿晶 | 99 | — | — |

普通物品使用 `BasicItemSO : ItemSO<ItemData>`，也可以在 Project 窗口通过 Create > Configs > Item > BasicItemSO 创建。

所有新增物品均加入 Default Local Group，标签为 `ItemSO`。等待 `GameManager.Instance.Ready` 后，可用 `GameManager.Instance.GetItem("item_001")` 获取配置。

`Assets/Configs/PlayerSO/SampleInventoryPlayer.asset` 是独立的背包测试角色配置。将其赋给角色原来引用 PlayerSO 的字段即可试用，现有场景和 TestPlayer1 未修改。快捷栏包含练习剑和 5 瓶药剂；背包包含另两把剑、18 瓶药剂、48 份草药和 99 块矿晶，便于测试移动、合堆和拆分。

这些是库存与近战伤害测试数据。普通物品没有使用效果；武器没有附加元素状态。现有 12 个物品均已在 `Assets/Prefabs` 下创建同名预制体并关联到 SO 的 prefab 字段。4 把武器复用现有 Sword 模型，分别绑定各自 WeaponSO；8 个普通／测试物品使用各自图标作为约 0.35 米的 SpriteRenderer 显示。它们可供现有手持物品逻辑实例化，尚未添加场景拾取行为，也未为每种物品制作独立三维模型。

Unity 菜单 `Tools > Items > Create Missing Item Prefabs` 可补建缺失预制体并检查关联，结果写入 `Tools/ItemPrefabs-result.txt`。

在项目根目录运行 `python Tools/create_sample_items.py` 可补回缺失的 SO 与 Addressables 条目。脚本保留已有配置和 GUID；需先保留本次生成的六张 PNG。构建游戏时需要按项目原有流程重新构建 Addressables 内容。

## 图标生成

使用内置 image_gen，分别生成六张独立图标。最终提示词保存在 `Tools/sample_item_image_prompts.json`。
