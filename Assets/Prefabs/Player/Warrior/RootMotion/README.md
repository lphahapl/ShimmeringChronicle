# Warrior 根运动副本

使用本目录的 WarriorRootMotionPlayer.prefab 测试。原 Character Warrior.prefab、动画、FBX、控制器和 WeaponSO 保持原样，现有场景不会自动替换玩家。复制玩家继续兼容 PlayerObj、背包、交互和攻击事件。

Animations 内有 32 个独立动画副本；Controllers 内有 8 个独立控制器。支持基础动作、原测试剑、铁卫练习剑、霜华和紫电。换装仍读取原 WeaponSO，WarriorRootMotionController 将控制器映射到对应副本，无须修改武器配置。

走路、跑步的根位移按原脚部落地轨迹生成，移动速度由动画步幅决定，约为行走 2.4 m/s、跑步 4.7–4.8 m/s；不再叠加 PlayerSO.speed/runSpeed 的水平位移。跑步副本修正了循环末帧的骨架位置突跳。三段攻击在伤害事件附近缓入、缓出前进，沿用原攻击事件和时长，多次判定的攻击分配多次小步。跳跃从原骨盆轨迹提取约 0.71 m 的竖直根运动，同时抵消骨架上的重复位移；待机、受击和死亡保持原地。

新玩家使用 WarriorRootMotionController（继承 PlayerController），OnAnimatorMove 将动画位移交给 CharacterController，保留碰撞、转向、重力、对话和 CharacterTime 卡肉。跳跃期间使用动画的竖直位移，结束或被打断后恢复重力。PlayerController 只增加 ResolveAnimatorController、MoveCharacter 两个继承入口，默认实现保留原玩家行为。

复制预制体补上了 SampleScene 当前玩家已有的任务引用。放入现有场景时，摄像机、UI 和交互提示等场景引用沿用该场景的设置。

制作、实际 Animator 位移和 Play Mode 碰撞/跳跃/卡肉/换装映射检查见 Tools/Players/WarriorRootMotion；original-integrity.txt 验证 575 个原资源文件校验值不变。
