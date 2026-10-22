# BingoUI

A Hollow Knight: Silksong mod to show counts of various in-game statistics
on the screen. This is primarily intended as an tool for Bingo races.

Settings can be configured in the BepInEx config file,
or by using [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/).

## Spent rosary counter

The spent rosary counter shows the number of spent rosaries. The counter in the HUD and the counter in
the inventory (on the main inventory page) can be toggled separately.
(There is a separate rosary display on the rosary cannon, which cannot be turned into a spent rosary counter.)


## Counters

* The flea counter shows `current_fleas(fleas_in_current_area)`. Vog, Kratt and the large flea are included.
* The maps counter shows total maps obtained.
* The locket counter shows `current_lockets(total_obtained_lockets)`.
* The tool counters (all, red, yellow, blue) show the number of tools obtained. Silk skills are not counted as tools for this purpose.
* The quest counters show the number of quests completed of each type. The global quest counter includes only non-main quests,
and delivery quests are only counted once each. Special quests (such as Mr Mushroom) are counted in this category.
The hunt counter includes both Hunt and Grand Hunt quests.
* The delivery quest counter shows `num_delivery_quests{num_distinct_delivery_quests}`. The Courier's rasher delivery
is not included in this counter (I believe).
* The shard bundle, beast shard, rosary string and rosary necklace counters count
`num_in_inventory(num_ever_picked_up)`. The number in brackets only includes items picked up as shinies,
not bought from machines or shops. The string/necklace counters include all types of string/necklace,
e.g. frayed rosary strings and rosary strings are both included in the strings counter.
* The Shakra locations counter shows the number of distinct scenes in which Shakra has been interacted with.
* The relics counters count the number of relics obtained. The global relics counter shows
`num_relics{num_distinct_relic_types}`. Psalm/Sacred cylinders are not included in this count.
The psalm/sacred cylinder counter includes both types.
* The craftmetal and silkeater counters show `num_owned(num_ever_obtained)`. This includes
craftmetal from shops and silkeaters from Styx.
* The rosary counter shows the number of spent rosaries (there is not enough space for
the number of rosaries in inventory). This defaults to off, and is an alternative to the spent rosary
tracker.

## Adding new counters

To add/request new counters, feel free to make a pull request, issue, or ping me in the modding, speedrunning or bingy Discord.
(Actually it is probably good to ping me in Discord even if you make a PR or issue).

To make new icons, use the template in the assets folder of this repository. Providing an icon makes it
more likely for the new counters to be added sooner.
