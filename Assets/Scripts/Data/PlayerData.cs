using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家的运行时数据。
/// 玩家独有的字段（等级、经验、buff……）加在这里。
/// 注意：这里只加「运行时才会变」的东西，配置永远从 config 读。
/// </summary>
public class PlayerData : BaseData
{ 
    public float runSpeed = 5f;
    public float turnSpeed = 720f;
    /// <summary>这两个是容量，不是当前数量。列表长度 == 容量，空格是 null</summary>
    public int quickBarCapacity = 5;
    public int bagCapacity = 25;

    public List<ItemData> quickBar;
    public List<ItemData> Bag;

    public ItemData HandingItem { get;  set; }
    public WeaponData GetWeaponData()
    {
        return HandingItem != null && HandingItem.count > 0 ? HandingItem as WeaponData : null;
    }
    public Dictionary<string,bool>completedMissions = new Dictionary<string,bool>();//记录已完成的任务
    public Dictionary<string,MissionData>missions = new Dictionary<string,MissionData>();   
}
