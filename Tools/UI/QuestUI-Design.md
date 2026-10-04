# 任务 UI 设计稿

本稿延续项目现有蓝灰面板、浅金细边、米白文字、绿色完成状态。现已制作对应 Unity 预制体，展示逻辑由你自行编写。示例人物、任务和奖励名称用于验证排版。

## 接取对话框

Canvas 参考分辨率 1920×1080，实际预制体窗口为 720×860，居中；长文本区域可滚动，底部操作固定可见。

从上到下：窗口标题 → NPC 名称与身份 → 任务类别与名称 → NPC 委托原话 → 目标列表 → 奖励列表 → 暂不接取 / 接取委托。

- 接取前目标展示需求数量，不展示虚构的当前进度。
- 暂不接取、关闭都不改变任务状态；后续由你写的对话流程决定返回上一层还是结束对话。
- 接取成功后按钮禁用，提示已接取，加入任务面板并自动追踪；实际游戏可随后关闭窗口。设计稿保留窗口便于对照。
- NPC 徽记可换为头像；当前无人物头像素材，不强制依赖头像字段。

## 任务面板

实际预制体窗口为 1040×840，居中；左侧列表 250，右侧详情 662。长列表与长详情分别滚动，标题和操作区固定。

标题 → 进行中 / 已完成 → 左侧任务列表 + 右侧详情。

列表显示类别、名称、进行中 / 待交付状态、追踪标记。详情显示任务描述、目标与逐项数量、交付 NPC 与地点、奖励、追踪按钮。

- 待交付仍属于进行中列表，以绿色文字区分。目标达成不等于已领取奖励。
- 任务面板只管理查看与追踪，不在这里远程领取 NPC 奖励。交付与领取由后续 NPC 对话流程处理。
- 同时追踪一个任务；切换时替换旧目标，不影响任务本身的进度。
- 已完成详情保留描述、目标与奖励记录，不再提供追踪操作。
- 尚无任务时显示“暂无进行中的任务”，详情区域不留旧任务；没有完成记录时显示“还没有完成的任务”。

## Unity 控件拆分建议

```text
QuestOfferDialog
  Header / CloseButton
  SpeakerInfo
  ContentScroll
    QuestTitle / OfferText
    ObjectiveList
    RewardList
  Footer / DeclineButton / AcceptButton

QuestPanel
  Header / CloseButton
  StatusTabs
  QuestListScroll / QuestListEntry
  DetailScroll
    QuestTitle / Description
    ObjectiveList / ObjectiveEntry
    TurnInInfo
    RewardList / RewardEntry
  Footer / TrackButton
```

复用 `Assets/Art/UI/Astral/panel.png` 和 `slot.png` 的九宫格。奖励格显示物品图标与数量，名称在格旁；实际图标由对应 ItemSO 提供。接取框和详情共用 ObjectiveEntry、RewardEntry 的视觉组件。

## 反推数据时的边界

接取原话与任务面板描述用途不同，建议分别表达，避免面板里反复显示 NPC 的整段口语。目标和奖励按列表布局，不固定为两项。

任务标题、说明、目标要求、奖励配置等属于定义；是否接取、当前计数、是否交付、是否追踪属于运行时数据，不要把玩家进度写回 SO。NPC 名称、物品名称和图标尽量引用已有配置，不为这两个面板重复维护。

设计稿里的交互只演示接取、查看和追踪；未决定你的对话节点类型、条件系统或事件接口。
