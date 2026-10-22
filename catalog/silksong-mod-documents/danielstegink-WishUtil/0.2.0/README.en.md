# Wish Util
This library provides utilities necessary for 
creating custom Wishes.

## Creating your Wish
First, you need to make your custom Wish object by 
inheriting from CustomQuest.

The constructor will need to pass in a name that 
serves as your Wish's in-game ID, as well as 
LocalisedStrings for the Wish's display name and 
location.

Your object will also need to override 3 properites:
- GiveAtStart tells the game whether or not to assign 
the Wish to the player on startup.
- QuestType defines what type of Wish this is: 
Hunt, Gather, etc.
	- You can use one of the default quest types by 
	calling GetQuestType.GetType.
	- Alternatively, you can submit a custom type 
	using GetQuestType.BuildCustomType. You will need 
	to provide a LocalisedString for the type's 
	display name, the type's text color, as well as 4
	Sprites for the various icons. 
		- You can find examples for these icons in the
		Resources folder of this mod's Git project 
		(link in Thunderstore).
- GetDescription gives the textual description of 
the Wish, as shown in the Task journal.

## Adding your Wish to Silksong
After you've created your Wish, you need to add it to 
the game. In the Start method of your mod, create an 
instance of your Wish and use QuestData.AddQuest to 
add it to WishUtil's custom Wish list. 

You should probably store the Wish as a variable in 
your mod for ease of reference, but you can find it 
by name using QuestData.GetQuest.

## Interacting with your Wish in Silksong
With your Wish now in the game, you can manipulate it 
in your HarmonyPatches. CustomQuest currently has 3
methods you can call to change your Wish's status:
- Accept() adds the Wish to the Task journal. You can 
skip this part if you set GiveAtStart to true.
- Update() signals the player that they have 
progressed the Wish.
- Complete() tells the game to log the Wish as 
completed.

## Special Thanks
the.ant.57 and MistDZX for testing and feedback