# Loot Logger
Whenever you enter a new area, this mod will log breakable 
objects and enemies that drop rosaries or shell shards. 
This will include breakables that have already been 
destroyed (such as craw nests). However, it will not
include objects and enemies who handle their loot drops
in a unqiue way, such as how rosary statue mounds (called 
GeoRocks in code) use FSMs.

## Sample Entry
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