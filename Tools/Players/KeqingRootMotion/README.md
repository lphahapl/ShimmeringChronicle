# 刻晴动作制作工具

旧动画和动作预览已于 2026-10-11 按要求删除，人物模型及预制体保留，当前处于动作待重做状态。本目录中的旧生成、验证工具仅供参考；运行生成工具会重新生成旧方案。

`BuildKeqingRootMotion.cs` 参考原 Warrior 动作生成刻晴骨骼姿态与根曲线、Generic Avatar、控制器、剑、Prefab 和预览场景；重建时保留已有资源 GUID。`PreviewKeqingRootMotion.cs` 渲染真实角色蒙皮网格，并生成全部动作的正面/侧面关键帧。`AuditKeqingMotion.cs` 逐帧检查手腕、鞋底与转身支撑脚。`ValidateKeqingRootMotion.cs` 在 Play Mode 验证根运动、循环、衔接、输入缓存、命中事件及墙体碰撞。

本次在 `Temp/KeqingRootMotionBuild` 隔离工程中执行，源模型与 meta 从主工程原样复制，工具脚本放入其 `Assets/Editor`，运行脚本放入相同的资源路径。隔离工程还需要原 Warrior Mesh.FBX 和 Walk、Run、1A、2A、3A 动作（含 meta），可放在 `Assets/ReferenceWarrior`，也支持原路径。参考资源只在制作时读取，不成为新增玩家的运行依赖。

Unity 版本为 6000.3.10f1。以 `-batchmode -noUpm -projectPath <隔离工程> -executeMethod BuildKeqingRootMotion.Build -logFile <日志>` 运行生成；渲染入口为 `PreviewKeqingRootMotion.Run`（需图形设备），验证入口为 `ValidateKeqingRootMotion.Run`。三者依次运行，避免同时打开同一工程。

主工程使用 URP，隔离工程为内置管线时需将新增材质改用 URP Lit；原模型导入设置和材质不改。`CreatePreview.py` 仅将渲染帧编码为 GIF，需要 Pillow。`VerifyExport.py` 检查导出资源引用和原文件哈希。

`validation.txt` 保存实际 Play Mode 检查结果，`naturalness-after.txt` 记录最终姿态检查结果。`source-hashes.json` 检查源模型及参考动画内容；游戏脚本会随功能继续修改，不纳入此资源快照。过程截图和旧失败日志已清理，保留最终 GIF 与动作总览。
