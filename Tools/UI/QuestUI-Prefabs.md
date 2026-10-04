# 任务与对话 UI 预制体

## 位置

| 预制体 | 路径 |
| --- | --- |
| 任务接取框 | Assets/Prefabs/UI/QuestOffer/QuestOfferDialog.prefab |
| 任务面板 | Assets/Prefabs/UI/QuestPanel/QuestPanel.prefab |
| 左侧头像对话框 | Assets/Prefabs/UI/Dialogue/DialogueLeft.prefab |
| 右侧头像对话框 | Assets/Prefabs/UI/Dialogue/DialogueRight.prefab |
| 配套 Canvas | Assets/Prefabs/UI/Shared/QuestUICanvas.prefab |
| 任务列表项 | Assets/Prefabs/UI/Shared/QuestListEntry.prefab |
| 目标行 | Assets/Prefabs/UI/Shared/QuestObjectiveEntry.prefab |
| 奖励项 | Assets/Prefabs/UI/Shared/QuestRewardEntry.prefab |

打开 `Assets/Scenes/UI/QuestUIPreview.unity`，运行后通过顶部四个按钮切换预览。场景包含 EventSystem；项目当前 Input Handling 为 Both，支持预览场景的 StandaloneInputModule。

## 放进场景

先放入 QuestUICanvas，再把需要的窗口拖到它下面，保持本地缩放为 1。CanvasScaler 已设为 Scale With Screen Size、1920×1080、Match 0.5。现有背包 Canvas 的参考分辨率为 800×600，使用配套 Canvas 能保留这些窗口的设计比例。

两个任务窗口居中；两个对话框锚定底部，距底 60。一次显示一个对话框。

## 后续接线

原先生成的展示脚本和重建生成器已移除。接取框和左右对话框保留原生 UI 控件，由你自行绑定文本、头像和按钮事件。

任务面板已经绑定你正在写的 MissionsBar、MissionSlot、MissionAim：

| 字段 | 预制体节点 / 资源 | 布局 |
| --- | --- | --- |
| slotsParent | QuestListScroll/Viewport/Content | VerticalLayoutGroup + ContentSizeFitter（纵向 PreferredSize） |
| aimsParent | DetailScroll/Viewport/Content/ObjectiveList | VerticalLayoutGroup；由上级详情布局计算高度 |
| slotPrefab | Shared/QuestListEntry.prefab | MissionSlot；根节点 Image 接收点击，无 Button |
| aimPrefab | Shared/QuestObjectiveEntry.prefab | MissionAim + 不可交互 Toggle；HorizontalLayoutGroup 排列状态、目标、进度 |
| missionType | DetailScroll/Viewport/Content/QuestMeta/Category | TMP |
| missionName | DetailScroll/Viewport/Content/QuestTitle | TMP |
| missionDescription | DetailScroll/Viewport/Content/Description | TMP |
| itemSlotsPrefab | Assets/Prefabs/slot.prefab | ItemSlot；图标、数量、拖动图标、选中边框、遮罩均已绑定 |
| rewardSlotsPos | DetailScroll/Viewport/Content/RewardList | GridLayoutGroup：64×64，间距 16，固定 8 列；由上级详情布局计算高度 |
| progressingNum | ActiveTab/Label | TMP |
| completedNum | CompletedTab/Label | TMP |

两种行的文本、选中标记和 Toggle 引用已绑定。slots、aims 初始为空，运行时由现有代码发现示例行并复用，多余行失活。player 留空，由 MissionsBar.Awake 查找场景玩家。接取任务 UI 修改 player.Data.missions 后发布 OnMissionsChanged。

奖励格子与两种任务计数已由 MissionsBar 更新。交付信息、页签切换、追踪与关闭按钮仍需后续逻辑接入。

任务面板的任务列表与目标示例行由现有代码发现并复用；面板 RewardList 的固定示例奖励已清空，奖励格子由运行时创建，避免与真实奖励混在一起。接取框仍保留展示用奖励示例。行内节点名固定：目标为 StateMark、ObjectiveText、ProgressText；接取框奖励为 IconFrame/Icon、IconFrame/Quantity、ItemName、ItemCategory；列表项为 Category、QuestTitle、Status、TrackedMarker、SelectionMarker。

长任务列表、长接取文字、长详情均可竖向滚动；操作按钮固定。中文字体使用项目现有华文新魏；主题复用 Astral panel 和 slot，奖励使用项目已有药水与矿石图标。

## 检查

已使用 Unity 6000.3.10f1 编译并生成，重新加载预制体检查组件、中文字体、滚动区域和控件引用，并渲染实际预览截图。截图位于 Tools/UI/Previews。

原重建工具已删除。后续直接编辑预制体，避免覆盖手动接线。

接线检查：主工程 C# 编译零警告、零错误；临时 Unity 工程加载全部 8 个预制体，无 Missing Script。使用现有任务 UI 脚本和临时数据/事件替身，验证了 5 个任务、6 个目标的动态布局、点击选择、完成勾选、缩减与清空时隐藏多余行。未验证真实接取任务事件流程。

奖励接线检查：全部必需引用、9 个预制体的脚本、计数文本、滚动控件与布局组件检查通过。在临时 Unity 工程使用实际 MissionsBar/MissionSlot/MissionAim/ItemSlot 脚本与数据事件替身，验证 5 个任务、6 个目标、10 个奖励，奖励自动换成两行且不与目标区重叠，增减、复用及清空正常。主工程编译零警告、零错误。
