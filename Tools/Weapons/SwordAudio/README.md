# Sword 三段斩击音效

由用户提供的 `poppop.ai - sword slash sound.mp3` 的实际挥剑片段加工，去掉原文件的长静音，分别调整时长、音高和低频力度。

| AttackData 索引 | 文件 | 时长 | 原动画伤害事件（秒） |
| --- | --- | --- | --- |
| 0 | SwordSlash_01.wav | 0.16 秒 | 0.400 |
| 1 | SwordSlash_02.wav | 0.20 秒 | 0.533、0.867 |
| 2 | SwordSlash_03.wav | 0.18 秒 | 0.400、0.733、1.400 |

绑定在 weapon_001.asset 的 combo[].audioClip，由 WeaponObj 伤害检测播放。同一段的多个伤害事件复用该段声音。音频峰值位于起音后约 10 毫秒，不需要提前播放的动画事件。44.1 kHz、单声道 PCM16，峰值 -3 dBFS，首尾做短淡化。

ThreeSlashPreview.wav 依次试听三个声音；SwordComboTimingPreview.wav 按原动画时间排列六次挥剑。make_slashes.py 是制作脚本，slashes.json 记录参数和音频校验值。参考解码文件保留在隔离工程 Temp/WeaponProduction，仅用于复现制作。
