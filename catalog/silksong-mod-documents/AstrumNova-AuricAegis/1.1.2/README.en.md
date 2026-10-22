INSTALLATION
The zip contains AuricAegis.dll and a folder called FSMUtils, place both in BepInEx/plugins/(here).
Note that FSMUtils is a library, i included the dependecy for the latest version, but if that doesnt work go to this link and install version 0.1.0
https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/Silksong_FsmUtil/versions

BOSSFIGHT
- The boss is oriented towards a parry playstyle, most attacks are designed to promote stylish parry plays, for that reason cross stitch gives both you and the boss iframes, meanwhile parries damage the boss, showed by his sprite flashing upon parrying.
- The fight isnt geared towards any stage in the game as parry damage is fixed, and hitting the boss normally is hard to do often, in fact your parry play has to be aggressive enough to allow you to hit the boss.
- Phase 1 is a "tutorial" phase, meant to teach the basics of parrying, the boss will be invulnerable during this phase and will get staggered every now and then disabling the invincibility, hitting the boss while invincible grants some buffer frames for parrying, this is not intentional but it works great for a tutorial phase.
- Phase 2 is the real bossfight, triggers after a stagger ends below 1500 hp, the first attack will always the a double rising slash to show the fight is now in phase 2, now you can hit the boss normally. Parries cannot bring his hp below 1, so a final hit is required to beat the boss.
- Turning screen shake off is recommended!

VERSION 1.1.0 CHANGELOG

Many gave feedback on the length of the fight, and asked for either reduced HP or more damange, so i decided to double the parry damage, now contrary to popular belief the fixed parry damage isnt 7, it was 1 per frame where the player parries and that often resulted in 7 damage per parry, now its 2 damage per frame, it feels much more adequate from what ive tested but let me know!
Now if that is not enough, or not what you wanted, i added config options for STARTING HP, PHASE 2 THRESHOLD, PARRY DAMAGE, and STUN QUOTA, these are located in BepInEx/config/com.astrumnova.auricaegis.cfg, feel free to play around with values that make the boss more fun for you, this doesnt make your clear any less or more valid, as long as its cool its valid.
- Doubled parry damage per frame (1 > 2)
- Added config options for STARTING HP, PHASE 2 THRESHOLD, PARRY DAMAGE, STUN QUOTA

GITHUB
https://github.com/astrum-nova/Auric-Aegis
The code is on github, feel free to check it out and open PRs if you fix something

KNOWN ISSUES
- One playtester noted the first time they enter the fight the vanilla bossfight plays, then restarting will load the actual mod, i couldnt find a way to replicate this nor why this might be happening, i havent seen anyone else mention this either so i cant really find whats causing this, feel free to report if you encounter the same bug and please include the logs (enable logs from BepInEx/config/BepInEx.cfg, a window should open along with the game showing all the logs in realtime).
- There is a very rare occurrence (roughly once every 20 minutes, i tested it) where in phase 2 the boss will try to do a double wind slash, but will recoil onto the wall triggering a wall bounce and reversing the second wind slash, i could only reduce the likelyhood of it happening.
