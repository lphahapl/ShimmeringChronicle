# 掉落物

生成一个掉落堆叠：

```csharp
DropItem.Spawn(itemData, position);
```

只保留一个静态生成函数：实例化物品 prefab、挂上 DropItem 并赋值。物品需要配置 prefab；生成不会自动扣除来源容器。

掉落物持续旋转，碰到玩家时通过 PlayerObj.AddItem 拾取。只扣除实际拾取数量，剩余部分留在地上，下次进入触发范围时继续拾取；捡完销毁。

SphereCollider 和 Rigidbody 自动配置为触发器、运动学及无重力。

任务面板的 GrantRewards 已按此规则接入：先入包，装不下的部分在玩家位置加上随机偏移（X、Z 各 -0.2～0.2 米，Y 为 0）生成，再扣除对应奖励槽。生成失败只保留尚未发出的数量，允许重试。

