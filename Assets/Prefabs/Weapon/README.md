# 武器资源

本轮制作铁卫练习剑（weapon_002）、霜华（weapon_003）、紫电（weapon_004）和方块敌人重刃（EnemyWeapon_001）；补齐原测试剑、苔石刀、强化石刀和隼翼誓约的剑尖效果与音频。两个空手配置仅使用音频。

各武器文件夹内存放预制体、WeaponSO、模型、材质/贴图、动画与重写控制器、Effects 和 Audio。原有资源 GUID 保留。

特效与音频配置在 WeaponSO 的每段 combo（AttackData）中：eff 指向 Effects/AttackEffect.prefab，audioClip 指向 Audio 内的音频。CreateData 与 Clone 保留这两个资源引用。WeaponObj.effPos 绑定剑尖 TipPoint，仅提供生成位置；空手可不绑定，使用武器自身位置。

玩家 CheckAttack 与敌人 EnemyCheckAttack 在伤害检测时读取本段 AttackData，播放空间音效，并生成跟随剑尖的拖尾、亮点。新实例开启后 0.35 秒自动销毁；每次判定只触发一次，挥空也播放，多目标不会重复触发。空手仅绑定 audioClip。武器和特效上的旧 AudioSource、预挂效果实例，以及 PlayHandingEffORAudioClip / EndEff 动画事件和回调已移除。音量通过 AudioManager.sfxVolume 控制。

原测试剑 weapon_001 的三段分别使用 SwordSlash_01/02/03.wav，长度为 0.16/0.20/0.18 秒，从提供的剑斩参考音频加工而来，在伤害判定时起音，不添加提前播放事件。制作脚本与试听在 Tools/Weapons/SwordAudio。

三把玩家新剑各提供 Warrior 的待机、走路、跑步及三段攻击和独立覆盖控制器；紫电为双手重剑，持握层只影响手指。其攻击保留脚步、转体和回收，双手轨迹经逐帧握持检查。方块敌人使用转体横扫和前倾下劈；攻击不改变导航根位置，走路和追击则保留原动画的根运动，供 EnemyAI.OnAnimatorMove 移动角色。

刻晴动画已按后续要求移除，等待重做。原 Warrior 预制体未替换，新动作通过装备的 WeaponSO.overrideController 使用。

隼翼誓约当前没有 WeaponSO 和正式攻击动作，保留剑尖位置及独立效果、音频资源，尚未接入背包战斗配置。

预览在 Tools/Weapons/Previews。本次验证见 Tools/Weapons/attack-data-binding-validation.txt 与 attack-data-play-validation.txt；较早的验证报告仅对应当时的制作方案。
