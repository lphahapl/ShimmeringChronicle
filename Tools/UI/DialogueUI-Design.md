# 底部对话框

沿用任务 UI 的蓝灰底、浅金细边、米白正文。头像在左和头像在右是同一对话框的两种布局状态。

- 左：头像与姓名在左，正文在右。
- 右：正文在左，头像与姓名在右。
- 正文始终左对齐；镜像布局不镜像文字或头像图片。
- 名字放在头像下方，身份说明可选。头像目前为占位，正式版使用人物 Sprite。
- 每次只显示当前说话者。左右位置由对话数据或调用代码指定，不按句子奇偶自动轮换，同一人可以连续说多句。
- “继续”固定在窗口右下角，双方切换时保持位置，避免操作目标跳动。

## Unity 布局建议

实际预制体 Canvas 参考分辨率为 1920×1080，底部居中锚点，窗口宽 1560、高 300，距底 60。头像 144×144，头像加姓名整体 190 宽，正文 30 号字。窗口左右状态保持同样尺寸。

```text
DialoguePanel
  Background
  LeftLayout
    Speaker / Portrait / Name / OptionalIdentity
    DialogueText
  RightLayout
    DialogueText
    Speaker / Portrait / Name / OptionalIdentity
  ContinueButton
```

可以保留两个布局根节点，由一个展示函数选中其一并赋值；继续按钮共用。也可以只用一套控件，通过调整子节点顺序和间距切换，避免两份组件引用。两种方式都不要用负缩放翻转整个布局。

最小展示输入：说话者名称、头像、正文、头像侧别。姓名与头像可从说话者配置读取，不必在每一句里重复填写。对话推进、选项、任务接取由之后的流程处理，此稿不预设其数据结构。

当前交付的是 `DialogueLeft.prefab` 和 `DialogueRight.prefab` 两个预制体，头像、姓名与正文由你编写的脚本绑定；左右切换由外部流程控制。

长句按正文容量分页，不缩小字体挤进一页。若以后加打字机效果，第一次点击补全本句，显示完整后再点击进入下一句。交替说话时保持面板位置，避免整窗滑来滑去影响阅读。

## 对话选项按钮

`Assets/Prefabs/UI/Dialogue/DialogueChoiceButton.prefab` 复用现有 Astral slot 背景与华文新魏字体。宽 320、单行最小高度 64，字号 26、米白正文，左右留白 24、上下留白 16。悬停和键盘选中提亮，按下变暗，不可用时背景与文字同时变暗。

根节点包含 Image、DialogueChoiceButton、LayoutElement 和 HorizontalLayoutGroup，唯一子节点 Label 为 TMP 文本。HorizontalLayoutGroup 根据换行文字计算期望高度；LayoutElement 不固定 preferredHeight。放在控制子项高度的纵向布局中，长选项会自动撑高。文字不接收射线，整行背景接收点击。

DialogueChoiceButton 继承 Unity Button，仅补充不可用状态的文字颜色，并提供 Label 引用。使用继承的 onClick 和 interactable 接线；对话条件、节点跳转、任务事件和选择历史仍由对话流程管理。

```csharp
var view = Instantiate(choicePrefab, choicesParent)
    .GetComponent<DialogueChoiceButton>();
view.Label.text = choice.text;
view.onClick.AddListener(() => OnChoose(choice));
```

若复用按钮，绑定新节点前清理之前由流程注册的点击回调，避免一次点击处理旧选项。选择后将本组选项的 interactable 设为 false，再执行对话推进。

选项列表放在对话框上方靠右，右边缘与框内侧对齐，底部距对话框顶部 16。1920×1080 下，Canvas 右下锚点的位置为 (-228, 376)、宽度 320，pivot 为 (1, 0)，列表向上增长。使用 VerticalLayoutGroup（间距 10、控制子项宽高、不强制扩展宽度）和 ContentSizeFitter（纵向 PreferredSize、横向 Unconstrained）。左右头像切换时选项位置保持一致。

`Assets/Scenes/UI/DialogueChoicePreview.unity` 展示短选项、换行选项、多个选项和不可用选项。预览中的继续按钮隐藏。选项列表的高度上限与滚动区域由最终对话面板控制：超过约 320 时使用 ScrollRect 的独立 Viewport，不压缩字号；列表填充并完成布局后回到顶部。单个按钮不承担列表滚动。

## DialogueUI 数据绑定

左右对话预制体根节点均已挂载 `Assets/Scripts/UI/DialogueUI.cs`，并绑定以下引用。脚本直接将 SpeakerInfo 的数据赋给控件，不再重复保存头像、姓名和身份说明。

| 字段 | 节点 / 资源 |
| --- | --- |
| portraitImage | SpeakerInfo/PortraitFrame/Portrait |
| portraitPlaceholder | SpeakerInfo/PortraitFrame/PortraitPlaceholder |
| speakerNameText | SpeakerInfo/SpeakerName |
| descriptionText | SpeakerInfo/SpeakerIdentity |
| content | DialogueText |
| buttonPrefab | DialogueChoiceButton.prefab |
| btnPos | ChoicesPanel/Viewport/Content |
| choicesScroll | ChoicesPanel 的 ScrollRect |

调用 `BindData(DialogNode)` 显示说话者与正文，并按 choices 绑定按钮。没有头像时显示占位；没有说话者时清空姓名、说明和头像。有选项时显示选项文字；没有选项时，nextNodeID 非空显示“继续”，为空显示“结束”。传入 null 会清空文字并隐藏按钮区域。

`CreateBtn(int)` 维护可见按钮数量，复用已生成的按钮并隐藏多余项。BindData 会清理旧节点的点击回调并解除上次选择的锁定。选中后禁用本组选项，避免重复提交。

点击通过项目的 EventCenter 发布，事件键在 GameEvents 中定义：`OnDialogueChoiceSelected` 的参数为 `(DialogueUI source, DialogChoice choice)`，`OnDialogueContinueRequested` 的参数为 `(DialogueUI source, string nextNodeID)`。传出空 ID 时结束对话。显示脚本不会自行执行任务动作或查找下一节点。

当前对话流程使用 Subscribe 监听，结束对话时使用相同处理函数 UnSubscribe。处理函数根据 source 检查当前 UI；多个 NPC 共用 UI 时，还需确保只有当前对话流程处理输入。旧的实例事件已移除。

ChoicesPanel 默认隐藏，绑定有效节点后显示，锚定在对话框右上方、距框顶 16。纵向布局保留长文字的实际高度，面板高度最大 320，超过后可以滚动。每次绑定新节点回到列表顶部。绑定时面板未显示的情况，会在 OnEnable 中刷新布局。

验证：独立 Unity 6000.3.10f1 工程编译与预制体加载通过，验证了双方控件引用、说话者与头像显示、换行选项、按钮池增减、选择对象与重复提交拦截、换节点清理旧监听、继续与结束回调、null 清空、8 个选项的滚动高度上限。隐藏后显示的布局刷新通过模拟 OnEnable 检查；未验证完整对话树和任务动作流程。
