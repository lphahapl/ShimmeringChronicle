# 交任务 UI

沿用 Astral 蓝灰面板、浅金细边、米白文字与华文新魏字体。窗口 720×860，完成提示和目标使用绿色；底部固定“稍后交付”和“交付并领取”，正文、目标和奖励在同一滚动区域内。

## 资源与接入

- `Assets/Prefabs/UI/QuestSubmit/QuestSubmitDialog.prefab`：窗口，已绑定 `MissionSubmitPanel` 的文本、头像、目标、奖励和按钮引用。
- `Assets/Prefabs/UI/QuestSubmit/QuestSubmitCanvas.prefab`：包含窗口的配套 Canvas，1920×1080，Match 0.5，排序 30；已放入 SampleScene。
- `Assets/Scenes/UI/QuestSubmitPreview.unity`：静态视觉预览，使用示例目标和奖励，不运行任务结算逻辑。
- `Tools/UI/Previews/QuestSubmitDialog.png`：Unity 原生渲染截图。

`MissionManager.SubmitMission` 通过 `UIManager.Get<MissionSubmitPanel>()` 打开面板，无需增加新的任务事件。testNPC 的已接取对话分支配置“交付任务”选项，触发 mission_002 的交付入口。其他场景直接放入 QuestSubmitCanvas；已有合适 Canvas 的场景也可只放窗口。

## 展示绑定

`MissionSubmitPanel` 实现 `IContainerOwner`，奖励沿用 `ItemContainer`。`GetContainer()` 返回当前奖励容器；奖励格复用 `Assets/Prefabs/slot.prefab`，通过 `ItemSlot.Bind(item, index, reward)` 更新图标和数量。目标复用 `QuestObjectiveEntry` 与 `MissionAim`。

奖励区为 72×72、间距 16、固定 7 列；数量较多时换行。目标和奖励动态创建并复用，多余项隐藏，没有奖励时显示空状态。奖励预览格的 ItemSlot 组件暂时禁用交互，避免交付确认前转移奖励，展示和容器绑定仍正常。

控制脚本位于始终启用的窗口根节点，`viewRoot` 指向它的 ViewRoot 子节点。初始隐藏、关闭和重新打开都只切换视觉子节点，保留 UIManager 注册和按钮监听。每次打开恢复滚动到顶部；目标未完成时禁用交付按钮。

## 结算接口

“交付并领取”已接到现有 `SubmitMission()`。`GrantRewards()` 先通过 PlayerObj.AddItem 发到背包和快捷栏，剩余数量通过 DropItem.Spawn 生成在玩家位置附近：X、Z 分别随机偏移 -0.2～0.2 米，Y 保持玩家位置。奖励按原槽位扣除，生成失败时保留余量供重试；没有剩余的有效物品时返回 true，空槽不影响完成判定。发完后的奖励预览同步隐藏空槽。

发奖期间拦截事件回调重入，领完后重复调用不会再次生成奖励。`MissionSubmitPanel.SubmitMission` 在发奖成功后调用 `MissionManager.CompleteMission`：标记已完成，从 missions 移入 completedMissions，广播 OnMissionsChanged 刷新任务列表，最后关闭面板。每次打开任务重置完成标记，失败重试仅处理剩余奖励。

## 验证

使用 Unity 6000.3.10f1 隔离工程、项目实际任务与容器脚本、已有依赖程序集，完成编译、原生渲染、开关与重开、容器绑定、未完成按钮状态、8 个目标与 12 个奖励的滚动、缩减与无奖励检查。主工程预制体和场景另检查 YAML 与本地引用。后续在隔离工程中验证了项目实际 DialogueUI 处理 testNPC 的交付选项、打开注册面板、领取奖励、更新完成记录、广播刷新和关窗；也验证了失败重试、连续任务、重复领取与实际物理触发拾取。详见 `quest-chain-validation.txt`。主 SampleScene 的 NPC 任务 ID、面板实例、UI 字段、奖励 prefab 与 Addressables 注册完成静态核对；未运行主场景的实景漫游与对话点击。隔离工程使用既有依赖 DLL，忽略了敌人脚本中未使用的渲染器导入，任务、容器、玩家、掉落和对话逻辑使用项目实际脚本。
