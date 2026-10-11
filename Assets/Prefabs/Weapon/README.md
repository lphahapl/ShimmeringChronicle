# 武器资源

本轮制作铁卫练习剑（weapon_002）、霜华（weapon_003）、紫电（weapon_004）和方块敌人重刃（EnemyWeapon_001）；补齐原测试剑、苔石刀、强化石刀和隼翼誓约的剑尖效果与音频。两个空手配置仅使用音频。

各武器文件夹内存放预制体、WeaponSO、模型、材质/贴图、动画与重写控制器、Effects 和 Audio。原有资源 GUID 保留。

特效挂在武器的 `TipPoint/AttackEffect`，默认关闭。预制体的 `WeaponObj.effPos` 绑定这个特效子物体的 Transform；动画事件从手上预制体读取 WeaponObj，用 `PlayHandingEffORAudioClip` 开启、`EndEff` 关闭。重新开启会清除旧拖尾，受击打断、死亡、换装和回收也会结束效果。WeaponSO 和 WeaponData 不再保存 eff/clip，effPos 留空时不会自动查找效果。

效果预制体内有短拖尾、尖端亮点和不循环的 3D AudioSource，音频配置在该 AudioSource.clip，随效果启用播放。空手预制体在 WeaponObj 所在物体上配置 AudioSource，关闭 Play On Awake；没有 effPos 时动画事件直接调用该 AudioSource.Play()。音量调各预制体的 AudioSource.volume。所有敌人攻击统一使用 `PlayHandingEffORAudioClip` / `EndEff` 动画事件控制效果，包括强化石刀的第三招。

三把玩家新剑各提供 Warrior 的待机、走路、跑步及三段攻击和独立覆盖控制器；紫电为双手重剑，持握层只影响手指。其攻击保留脚步、转体和回收，双手轨迹经逐帧握持检查。方块敌人使用转体横扫和前倾下劈；攻击不改变导航根位置，走路和追击则保留原动画的根运动，供 EnemyAI.OnAnimatorMove 移动角色。

刻晴暂缓，本轮没有导入刻晴动作或改写刻晴预制体。原 Warrior 预制体也未替换，新动作通过装备的 WeaponSO.overrideController 使用。

隼翼誓约当前没有 WeaponSO 和正式攻击动作，本轮已补齐其预制体内的效果和音频，尚未接入背包战斗配置。

预览在 `Tools/Weapons/Previews`。原生资源/动作/实例绑定验证、实际 Play Mode 音频开关验证和主工程导入检查记录均在 `Tools/Weapons`。
