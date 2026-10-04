# 苔石守卫设计稿 v1

Generated with the built-in image_gen tool. This is a concept proposal; model, animations, scripts and prefab have not yet been created.

## Prompt

Use case: stylized-concept. Asset type: first enemy design concept sheet for the Unity fantasy action RPG ShimmeringChronicle. Primary request: create a coherent, beautiful, practical-to-model concept sheet for a NEW enemy named 苔石守卫 (Mossstone Sentinel). Landscape concept art board, warm pale parchment background, restrained thin champagne-gold separators, dark blue-gray typography. Original anime fantasy art direction rendered as a clean faceted low-poly 3D game character, readable chunky silhouettes and simple materials, consistent with a small Unity prototype set in a green woodland. Central large three-quarter full-body view plus smaller front, side and back orthographic views on the right, same exact design and proportions in every view. Bottom strip shows two successive attack poses with its weapon: 01 a telegraphed horizontal sweep with a small forward step, 02 a raised two-handed overhead smash and short forward lunge. Give the creature a compact humanoid stone golem body approx 2.1m tall, broad angular stone shoulders, segmented articulated stone arms and legs, dark visible gaps at joints for straightforward transform-based animation, heavy feet, no floating unattached limbs. Slate-blue stone, muted moss green shoulder and brow accents, a modest luminous turquoise crystal in the chest and two narrow turquoise eye slits. A simple carved stone face conveying an ancient guardian, not a human. A few aged brass bands or geometric runic accents, economical detail suitable for a low-poly mesh. Weapon: one hefty single-handed stone cleaver with a short wooden grip, an angular slate blade, a small turquoise inset near the guard; held in the right hand, also shown separately in a small weapon inset. Avoid excessive spikes, branches, trailing fabric, chains, complex vegetation, textures full of noise, photorealism, hyper-detailed sculpture, HUD screenshots, code or software panels. Label the board with exactly '苔石守卫', with small section labels '造型', '武器', '01 横扫', '02 重劈'. Consistent weapon and body design across ALL views and poses, full feet and weapon visible, generous clean spacing. This is a design proposal, not a screenshot of implemented assets.

## 后续制作范围

- 模型：约 2.1 米高的分节石质守卫，灰蓝石材、苔绿点缀、青绿晶核与眼睛、少量旧黄铜装饰。
- 武器：短柄石刃，单独武器预制体与 WeaponSO。
- 动画：待机、行走、追逐、受击、死亡、横扫与重劈；移动与攻击带水平根运动。
- 装配：接入现有 EnemyObj、EnemyAI、EnemyHPBar、NavMeshAgent、感知 Trigger 与运动学 Rigidbody；绑定武器挂点和血条引用。
- 配置：独立 EnemySO 与控制器，接入 EnemySO Addressables 标签及现有对象池生成流程。

实际制作前以用户对本稿的修改意见为准。
