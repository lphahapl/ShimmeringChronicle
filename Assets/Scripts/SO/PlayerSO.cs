using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Configs/Player/PlayerSO")]
public class PlayerSO : BaseDataSO<PlayerData>
{
   
  
    [Tooltip("跑步速度（米/秒）")]
    public float runSpeed = 5f;
    [Tooltip("转身速度（度/秒）")]
    public float turnSpeed = 720f;
    
    [Tooltip("快捷栏格子数。要和 QuickBar 上配的格子数一致")]
    [Min(0)] public int quickBarCapacity = 5;

    [Tooltip("背包格子数。列表长度就是容量，界面上固定显示这么多格，空格用 null 占位")]
    [Min(0)] public int bagCapacity = 25;

    public List<ItemSlotData> quickBar;
    public List<ItemSlotData> Bag;


    /// <summary>
    /// 玩家独有的配置要在开局写进 PlayerData 的，在这里赋值。
    /// 注意 data 的类型已经是 PlayerData 了 —— 不需要 is 检查，也不需要强转。
    /// </summary>
    protected override void FillExtra(PlayerData data)
    {
        data.runSpeed=runSpeed;
        data.turnSpeed=turnSpeed;

        data.quickBarCapacity = quickBarCapacity;
        data.bagCapacity = bagCapacity;

        // 初始背包存的是配置资产，这里现造运行时数据，并补足到容量（空格是 null）
        data.quickBar = quickBar.ToItemDataList
            
            (quickBarCapacity);
        data.Bag = Bag.ToItemDataList(bagCapacity);
       
       //在这里造额外的字段
    }
}
