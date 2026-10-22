# Silksong Text Modder

A mod that allows editing, overriding, and creation of new text items of Silksong's internal data. This can be used to change dialogue, names, tutorial text, menu items, etc.

**[Original Mod](https://www.nexusmods.com/hollowknightsilksong/mods/381)**  
[Source Code](https://github.com/KyleNeubarth/SilkSongTextModder)  
[Text Asset Decrypter](https://github.com/KyleNeubarth/SilkSongTextAssetDecrypter) (Simple program to help you view Silksong's ingame text)

## FAQ

### How do I modify text using this mod?

This mod reads text in files located in **"\Hollow Knight Silksong\Hollow Knight Silksong_Data\Mods\TextModder"**.  
These files have a precise format where you specify the localization language code, Sheet ID, Entry ID, and finally text value. All values are seperated by ">".
For example, a file with this line of text will override the main menu's quit button text (normally "Quit Game") with "Give Up". Note that this will only apply to the "EN" language code. You could put this line in **"\Hollow Knight Silksong\Hollow Knight Silksong_Data\Mods\TextModder\giveuptest.txt"** and it will apply the next time you start the game.  
`EN>MainMenu>MAIN_QUIT>Give Up`

### Where can I find the "sheet" and "entry"?

In order to figure out which "sheet" and "entry" the text you want to modify lives, you must inspect the existing game's TextAssets. See further instructions below or check out the [DecryptedTextSheets](https://www.nexusmods.com/hollowknightsilksong/mods/381?tab=files) from the original or the **new ones** I made and uploaded on [GitHub](https://github.com/dr-0ne/SilkSongTextModder/tree/1fa162c243c850f521d1288b3e245d4af6b2ad66/DecryptedTextSheets).

### Are there mod ready txt files of the xml files anywhere?

No. But there is a python script in the git repository that converts the xml files into working txt ones.  
To use it just drag the xml files onto the .py file (just like dragging files into a folder) and it will spit out the txt version of the file. Just remember that the xmls have to have the original names for the script to work correctly.

You can download the script from [here](https://github.com/dr-0ne/SilkSongTextModder/blob/5918b1513f48c8813968d6725258aa9b8ec58037/sheet_parser.py) by pressing the *download raw button* <img src="https://files.catbox.moe/cflc80.png" alt="icon" height="24">

### Can I use this to mod text in other mods?

It is also possible to add new sheets and entries which the vanilla game does not use. You may be able to use these for modded content, but I haven't been able to test any of that. Good luck!

### How to I make a new mod using this?

1. Create the text file the same way you would do it normally for this mod.  
2. Then create the mod itself with all the necessery files and place the text file into your mod folder (same folder as where you would normally place your `manifest.json` file)  
3. And lastly add a file into the same folder as the txt file and call it "`TextModder`". **This file has to be called exactly that!** No lower case letters no extansions, just "`TextModder`".

## How do I extract existing text from the game for reference?

Internally, Silksong keeps localized text in large XML tables called "sheets". Sheets are arbitrary groupings of text that the developers thought was helpful for organization (EN_Song contains all the text for needolin NPC sing along music, for example). These sheets are TextAssets stored in the resources assetbundle (**"\Hollow Knight Silksong\Hollow Knight Silksong_Data\resource.assets"**). I used [AssetRipper](https://github.com/AssetRipper/AssetRipper) to decompile the AssetBundle and view the contents.

**Note:** If using AssetRipper remember to specify the Unity Version in it's settings. Silksong is using Unity "6000.0.50f1", which can be found by looking at the properties of the executable.

Sheets have some laughable code obfuscation function which they are run through. Via decompiling Silksong's "Assembly.CSharp.dll" with [Dnspy](https://github.com/dnSpy/dnSpy) I found the obfuscation function which is pasted below.
```csharp
Decryption Functionpublic static byte[] Decrypt(byte[] encryptedBytes)
{
  byte[] result;
  using (RijndaelManaged rijndaelManaged = new RijndaelManaged
{
 Key = _keyArray,
 Mode = CipherMode.ECB,
 Padding = PaddingMode.PKCS7
})
  {
result = rijndaelManaged.CreateDecryptor().TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
  }
  return result;
}
private static readonly byte[] _keyArray = Encoding.UTF8.GetBytes("UKu52ePUBwetZ9wNX88o54dnfKRu0T1l"); 
```