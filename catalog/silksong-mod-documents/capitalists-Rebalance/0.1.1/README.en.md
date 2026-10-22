# Rebalance

A Hollow Knight: Silksong mod to rebalance Hornet as suggested
in [Holland](https://www.youtube.com/channel/UCnVGHE75UsCcjCN2DlIOJLg)'s videos.

Contributions and forks are welcome as long as they comply with the [license](./LICENSE).

## Install
### Simple way
1. Select a profile in your mod loader e.g. [Cogfly](https://github.com/Nix-main/Cogfly) or [r2modman](https://github.com/ebkr/r2modmanPlus)
2. Search for this mod and click install

### Manual
0. Download the mod
#### Cogfly
1. Select `Profiles`
2. Select a profile
3. Click `Import Manually` 
4. Select `capitalists-Rebalance-<version>.zip`

#### r2modman
1. Select `Hollow Knight: Silksong`
2. Select a profile 
3. Click `Settings`
4. Click `Import local mod`
5. Select `capitalists-Rebalance-<version>.zip`

## Build
```bash
dotnet build
```

## Changes

### Clawline

- No longer requires or consumes silk

### Hunter

- Down attack delay reduced
- Taking damage with fully evolved Hunter's crest only drops focus down by the amount of hits required for focus 1 (6
  points)

### Tools

#### Shard Pendant

- Most bosses drop (100) shards upon death (may need testing)

#### Weighted Belt

- No more attack recoil

#### Wreath of Purity

- Now yellow
- Lasts forever

#### Thief's Mark

- Drop chance of each rosary type increased (+ 16.6...%)
- Loss chance fixed to 2%
- Loss cap is fixed to 10 rosaries

#### Magma Bell

- Now yellow

#### Dead Bug's Purse

- Cocoon gives back all lost rosaries

#### Magnetite Brooch

- Attract shards too
- Collect when exiting scene
- ~~Prevent drops from being destroyed by spikes, and attract them early~~ (having trouble with this)

#### Silkspeed Anklets

- No longer requires or consumes silk

#### Ascendant's Grip

- Increases attack range while on wall

### Scuttle Brace

- Makes hornet invulnerable instead of the attack-type dependent evasion

### Druid's Eye/Druid's Eyes

- Also takes effect when enemy is killed
- Gives 3 (Druid's Eye) or 4 (Druid's Eyes) moss pips per enemy killed

### Warding Bell

- Refunds 6 silk when burst

### Pollip Pouch

**Flintslate Synergy**

- Attacks do the same damage as regular flintslate (1.5x instead of 1.3x)

**Plasmium Phial Synergy**

- Purple masks no longer expire on a timer
- As the timer is gone, Plasmium overdose is now possible (behaves the same as regular plasmium overdose, has no extra special effects)
- Needle attacks while having purple masks inflict venom (like the Flintslate synergy, but with default needle damage)

### Sawtooth Circlet

- Volume halved (Does not reduce the volume of the circlet colliding with things)
- No longer collides with bounce pods (Hunter's March red pea pods, Shellwood white flowers...)

### Fractured Mask

- Recovers after binding

### Multibinder

- Bind time reduced

### Injector Band

- Bind time further reduced
- Now applies to Architect's craft bind

### Spool Extender

- Any strand that enters the spool extender fills the entire extender

### Reserve Bind

- Reduced usage delay to almost the same as regular bind
- Refilled when binding at full health

### Claw Mirror/Claw Mirrors
- Increased damage
- Triggers on craft bind as well as regular bind

**Claw Mirror only**
- Increased damage range to about the same as Claw Mirrors

### Memory Crystal
- Increased damage
- Spawns when a Needle Strike is performed

### Snitch Pick
- Creates new rosaries instead of taking them from enemy drops
- Extra hit does the same amount of damage as the needle

### Volt Filament
- Damage multiplier is now 1.2x (originally 1.25x)

### Quick Sling
- When the tool is duplicated:
    - Consumes only one tool
    - Both thrown tools do 70% of usual damage
- Speed up Rosary Cannon and Silkshot (Architect) firing rate
- Reduced cooldown on tools that don't get duplicated