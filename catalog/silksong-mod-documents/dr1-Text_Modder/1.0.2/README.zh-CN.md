# 丝之歌 文本模组

一个允许编辑、覆盖和创建丝之歌内部数据文本项的模组。可以用于修改对话、名称、教程文字、菜单选项等。

**[Original Mod](https://www.nexusmods.com/hollowknightsilksong/mods/381)**  
[Source Code](https://github.com/KyleNeubarth/SilkSongTextModder)  
[Text Asset Decrypter](https://github.com/KyleNeubarth/SilkSongTextAssetDecrypter)（一个帮助你查看丝之歌游戏内文本的简易程序）

## 常见问题解答

### 我该如何使用此模组修改文本？

此模组读取位于 **"\空洞骑士 丝之歌\空洞骑士 Silksong_Data\Mods\TextModder"** 中的文件内的文本。
这些文件具有精确的格式，其中你需要指定本地化语言代码、表单ID、条目ID，最后是文本值。所有值均以“>”分隔。
例如，一个包含这行文本的文件会将主菜单中退出按钮的文字（默认为“Quit Game”）更改为“Give Up”。请注意，此设置仅适用于“EN”语言代码。你可以将这行内容放入 **"\Hollow Knight Silksong\Hollow Knight Silksong_Data\Mods\TextModder\giveuptest.txt"** 目录中，下次启动游戏时就会生效。
`EN>MainMenu>MAIN_QUIT>Give Up`

### 我在哪里可以找到“表单”和“条目”？

为了确定你想要修改的文本位于哪个“表”和“条目”中，必须检查现有游戏的 TextAssets。请查看以下进一步说明，或查阅原始版本的 [DecryptedTextSheets](https://www.nexusmods.com/hollowknightsilksong/mods/381?tab=files) 或我上传到 [GitHub](https://github.com/dr-0ne/SilkSongTextModder/tree/1fa162c243c850f521d1288b3e245d4af6b2ad66/DecryptedTextSheets) 的新版本。

### 是否在任何地方有已准备好的 txt 文件用于 xml 文件？

不。但仓库中有一个 Python 脚本，可以将 XML 文件转换为可用的 TXT 文件。
只需将 xml 文件拖拽到 .py 文件上（就像将文件拖入文件夹一样），它就会生成该文件的 txt 版本。请记住，xml 文件必须保留原始名称，否则脚本无法正常工作。

你可以通过点击 *下载原始文件* 按钮从 [here](https://github.com/dr-0ne/SilkSongTextModder/blob/5918b1513f48c8813968d6725258aa9b8ec58037/sheet_parser.py) 下载脚本 <img src="https://files.catbox.moe/cflc80.png" alt="icon" height="24">

### 我可以使用它来修改其他模组中的文本吗？

同样，你也可以添加游戏本体未使用的新的图层和条目。你或许可以将这些用于模组内容，但我尚未测试过任何相关内容。祝你好运！

### 我该如何使用这个创建一个新的模组？

1. 以该模组通常的方式创建文本文件。
2. 然后创建模组本身，并包含所有必要的文件，将文本文件放入你的模组文件夹中（与你通常放置 `manifest.json` 文件的文件夹相同）
3. 最后，在与 txt 文件相同的文件夹中添加一个文件，并将其命名为 "`TextModder`"。**该文件必须恰好名为这个名称！** 不允许使用小写字母，也不允许添加扩展名，仅需名为 "`TextModder`"。

## 我该如何从游戏中提取现有文本以供参考？

内部而言，丝之歌将本地化文本存储在名为“表”（sheets）的大型 XML 文件中。这些“表”是开发者为了组织方便而任意划分的文本集合（例如，EN_Song 表包含了 needolin 非玩家角色合唱音乐的所有文本）。这些表被 TextAssets 存储在资源资产包中（路径为 **"\Hollow Knight Silksong\Hollow Knight Silksong_Data\resource.assets"**）。我使用了 [AssetRipper](https://github.com/AssetRipper/AssetRipper) 对 AssetBundle 进行反编译并查看其内容。

**注意：** 如果使用 AssetRipper，请务必在设置中指定 Unity 版本。丝之歌使用的是 Unity “6000.0.50f1”，该版本信息可通过查看可执行文件的属性获得。

表中包含一些令人啼笑皆非的代码混淆函数。通过使用 [Dnspy](https://github.com/dnSpy/dnSpy) 反编译丝之歌的“Assembly.CSharp.dll”，我找到了该混淆函数，具体内容如下：
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