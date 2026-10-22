# 装备记录器  
每当进入新区域时，该模组会记录可破坏物品以及掉落念珠或贝壳碎片的敌人。  
这将包括已经被摧毁的可破坏物品（例如爬巢）。然而，它不会包含以特殊方式处理掉落物的对象和敌人，例如念珠雕像堆（代码中称为 GeoRocks）使用 FSMs 的情况。

## 示例条目
```
[Info   :LootLogger] New scene: Coral_02
[Info   :LootLogger] Breakables
[Info   :LootLogger] Shell Shard Fossil Tiny Bumpy: 5 Shell Shards
[Info   :LootLogger] Shell Shard Fossil Tiny Egg: 5 Shell Shards
[Info   :LootLogger] Shell Shard Fossil Tiny Front: 5 Shell Shards
[Info   :LootLogger] Enemies
[Info   :LootLogger] Black Thread States\Normal World\Pilgrim Duo\Pilgrim 03: 5 Rosaries, 2 Shell Shards
[Info   :LootLogger] Black Thread States\Normal World\Pilgrim Duo\Pilgrim 01 (1): 5 Rosaries, 2 Shell Shards
[Info   :LootLogger] Black Thread States\Normal World\Pilgrim Hiker: 4 Rosaries, 2 Shell Rosaries, 3 Shell Shards
[Info   :LootLogger] Black Thread States\Normal World\Coral Judge (1): 4 Rosaries, 3 Shell Rosaries, 5 Shell Shards
[Info   :LootLogger] Blade Spider (3): 3 Shell Shards
[Info   :LootLogger] Blade Spider (1): 3 Shell Shards
[Info   :LootLogger] Blade Spider (2): 3 Shell Shards
[Info   :LootLogger] Blade Spider: 3 Shell Shards
[Info   :LootLogger] Pilgrim Hiker: 4 Rosaries, 2 Shell Rosaries, 3 Shell Shards
[Info   :LootLogger] Coral Conch Shooter: 3 Shell Shards
[Info   :LootLogger] Coral Conch Shooter (1): 3 Shell Shards
```