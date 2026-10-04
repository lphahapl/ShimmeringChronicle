# ItemSlot 测试结果

日期：2026-09-22。Unity 6000.3.10f1。

## 当前场景实测

进入 SampleScene 的 Play Mode，按 Tab 打开背包，尝试把第一格拖到第二格。未发生交换。运行中 Inspector 确认格子的 Drag Image 和 Mask 均为 None，与 slot.prefab 的未绑定状态一致。没有改动或保存原场景、预制体。

截图：`missing-inspector-references.png`。

## 隔离的 Play Mode 检查

使用真实 slot.prefab、ItemSlot、EventCenter 和生成的武器/物品资源。在临时叠加场景中为格子指定独立的 Image 和高亮遮罩，通过 ExecuteEvents 分发指针、拖拽和落点事件。

23 项检查全部通过。覆盖数据显示、高亮进出、鼠标 XY 坐标、原图标不移动/不隐藏、独立图片属性、刷新同步、落到空格发布一次请求、自身/外部落点取消、禁用清理、拖拽中重新绑定、空格/零数量/缺失引用及误绑定原图标。

这组自动检查验证真实回调与事件分发后的状态，不等同于完整的鼠标射线及最终画面测试。第一次编辑模式测试不具备 MonoBehaviour 的完整运行时生命周期，因此禁用检查改在 Play Mode 下验证，最终通过。

结果：`automated-results.txt`。截图：`play-mode-tests-passed.png`。测试源代码归档：`ItemSlotSmokeCheck.cs.txt`；已移出 Assets，临时对象已销毁，Unity 已退出 Play Mode。

## 当前仍需接线

1. Inspector 给 Drag Image 绑定独立的 Image，并将其初始设为失活，摆好层级和尺寸。每个格子使用各自的预览 Image；当前实现会在刷新时同步图片，不支持多个格子无协调地共享同一个预览。
2. 给 Mask 绑定高亮 Image。
3. 项目目前没有 OnItemDropRequested 的业务订阅者；交换/合堆尚未实现。测试中的临时订阅者只记录请求，不修改物品数据。
