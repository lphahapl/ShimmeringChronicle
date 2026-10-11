# Warrior 根运动动画

正式玩家为上级目录的 `Character Warrior.prefab`。SampleScene 中的正式玩家已启用根运动，原根运动测试实例、测试预制体和空的 WarriorRootMotionController 脚本已删除。

Animations 保留 32 个独立动画副本；Controllers 保留 8 个控制器。正式玩家使用 Warrior_Base_RootMotion；weapon_001、铁卫练习剑、霜华、紫电的 WeaponSO 直接绑定各自的 RootMotion overrideController，换装由 PlayerController 按配置切换，无需运行时映射。敌人武器配置沿用敌人控制器。

走路、跑步状态的播放速度为 1.25 倍，根运动位移同步随播放速度变化，不叠加 PlayerSO.speed/runSpeed 的水平位移。循环首尾的曲线速度和根位移速度已平滑；走跑切换使用 0.12 秒过渡，连招使用 0.10 秒过渡，攻击收招使用 0.16 秒过渡。跳跃根运动包含约 0.71 m 的高度。

走跑过渡启用 Ordered Interruption。关闭它时，持续移动会反复中断并重启当前过渡，导致状态停在 Idle → Walk/Run。输入链路回归验证见 Tools/Players/WarriorRootMotion/ValidateWarriorTransitions.cs 和 warrior-transition-validation.txt，测试通过 SetMove、Attack、Jump 驱动正式玩家，不使用 Animator.Play 跳进状态。

原测试剑第三段由 3.3 秒裁至 1.7 秒，移除后撤，保留 0.4、0.73333335、1.4 秒的三次伤害事件。骨架原有的前冲归入根运动，角色通过 CC 前移约 2.684 米，收招不再把模型拉回起点；其他武器的前冲距离沿用原值。移动输入可以取消第三段，尚未执行的伤害事件随取消停止；原公共控制器已有的跑步取消前两段行为也保留。

受击动画裁掉后半段，由约 1.133 秒缩短至 0.567 秒，骨架的后移归入根运动，受击后停在新的落点。受击结束使用 0.14 秒过渡回待机、走路或跑步，不再回弹到原位。碰撞和过渡会影响最终位移。

PlayerController.OnAnimatorMove 将根位移交给 CharacterController，并保留碰撞、转向、对话、死亡和卡肉处理。普通动作使用代码重力，跳跃期间使用动画竖直位移，结束或打断后恢复重力；重力与根位移每帧合并调用一次 CC.Move。

此前生成工具和报告记录动画副本的制作过程；正式绑定后的验证见 Tools/Players/WarriorRootMotion/ValidateBoundWarrior.cs 和 warrior-bound-root-motion-validation.txt。
