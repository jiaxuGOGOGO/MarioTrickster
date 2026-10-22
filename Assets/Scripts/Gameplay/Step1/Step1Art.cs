// 自动生成：tools_s243/gen_art.py（S243 AI 生成美术 → 16×16 字符画）。改图：改下面的字符画，或把同名 PNG 放进 Assets/Resources/Step1Art/<Key>.png 覆盖。
using System.Collections.Generic;

/// <summary>
/// S243：用户「图示是不是都生成了直观美术？场景、马里奥、捣蛋者能不能更直观」——之前只有 16×16 程序画的图标，角色和地形还是红蓝方块。
/// 这里是 GPT Image 2 生成的原创像素素材（洋红底图 → 切格 → 缩到 16×16 → 吸到项目调色板 → 自动补深色描边），全部过 OverworldArt.Audit。
/// 角色：寻宝人（红帽，马里奥位）5 帧 = 站 / 跑 A / 跑 B / 跳 / 晕；小恶魔（蓝，捣蛋者）5 帧 = 站 / 踮脚 A / 踮脚 B / 跳 / 变身烟。
/// 机关 22 个 + 徽章 8 个替换 Step1Icons 里程序画的旧图（Step1Icons.Pixels 先查这里）；地形 6 块；背景 64×36 低对比（主层黑描边、背景无描边、中间明度）。
/// 参考：主层实心深描边 + 背景低对比无描边 https://www.sandromaglione.com/articles/pixel-art-platformer-level-design-full-guide ；
/// 16×16 角色大头（头约占 45%）、先剪影 https://spritegen.io/guides/how-to-draw-a-pixel-art-character/ ；障碍和背景要拉开明度 https://www.reddit.com/r/gamedev/comments/w5w6xd/ 。
/// S245：香蕉皮 / 铁闸门 / 绊线 / 宝箱按实际大小重画（S244 评审说太弱）。S244 自检后：缩图改成先吸调色板再按格投票（颜色不再发灰发糊）；补出口 / 宝物 / 小怪 / 检查点 / 装饰；封路墙改铁闸门、弹簧改高蘑菇弹簧、香蕉皮重画；单向板 5 → 8 像素。
/// 只换外观：碰撞体、判定框、玩法一律不动（H3）。
/// </summary>
public static class Step1Art
{
    public const int Size = 16;
    /// <summary>S243 补的 4 个颜色（皮肤亮 / 皮肤暗 / 深蓝裤子 / 暗灰）——OverworldArt.Palette 没有的才放这里。</summary>
    public static readonly Dictionary<char, float[]> ExtraPalette = new Dictionary<char, float[]>
    {
        { 'f', new[] { 0.98f, 0.78f, 0.6f } },
        { 'F', new[] { 0.78f, 0.52f, 0.38f } },
        { 'd', new[] { 0.14f, 0.2f, 0.42f } },
        { 'e', new[] { 0.25f, 0.25f, 0.3f } },
    };
    public static readonly Dictionary<string, string[]> Icons = new Dictionary<string, string[]>
    {
        { "FireTrap", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "xxx.............",
            "xeSxx.kkkkkk....",
            "xSssxkooooorkk..",
            "xsssxffwffooork.",
            "xesSxowofooork..",
            "xeSSxkkrooork...",
            "xkexx..kkkkk....",
            "xxx.............",
        } },
        { "CrackFloor", new[] {
            "................",
            "................",
            ".xxxxxxxxxxxxxx.",
            ".xnnnnxknnnnnnx.",
            ".xnnnnxnnnnnnnx.",
            ".xnnnnxnnnnnnnx.",
            ".xnnnNxNnnnnnxx.",
            ".xnNnnnxNnnNNnx.",
            ".xnnnnnnxnNxnnx.",
            ".xnnnnnnxnnnnnx.",
            ".xnnnnnxnnnnnnx.",
            ".xnnnnxxNnnnnnx.",
            ".xnnnnnnxxnnnnx.",
            ".xnnnnnnnxnnnnx.",
            ".xnnnnnnnnxnnnx.",
            ".xxxxxxxxxxxxxx.",
        } },
        { "IronCage", new[] {
            "................",
            "......xxx.......",
            "......xxx.......",
            ".......x........",
            "......xxx.......",
            "......xxx.......",
            "....xxxxxxx.....",
            "....sSSSSSS.....",
            "...xxxxxxxxx....",
            "...xxxxxxxxx....",
            "...xxxxxxxxx....",
            "...xxxxxxxxx....",
            "...xxxxxxxxx....",
            "...xxxxxxxxx....",
            "...xxxxxxxxx....",
            "...xxxxxxxxx....",
        } },
        { "SnareTrap", new[] {
            "................",
            ".......xx.......",
            ".......xx.......",
            ".......xx.......",
            ".......xx.......",
            ".......xNx......",
            ".......NNx......",
            "......nxxN......",
            ".....xx..xx.....",
            "....xx....xx....",
            "....xx....xx....",
            "....x......x....",
            "....xx....xx....",
            "....xx....xx....",
            ".....xx..xn.....",
            "......xxxx......",
        } },
        { "OilBarrel", new[] {
            "................",
            "...xxxxxxxxx....",
            "..xrRRRRRRRrx...",
            "..xFxRRRRRxrx...",
            "..xrrrrrrrRRx...",
            "..xrrrrrrrRRx...",
            "..xrrFrrrrRRx...",
            "..xRNfffffrNx...",
            "..xrrfffffRRx...",
            "..xrrfRRffRRx...",
            "..xrrfroRfRRx...",
            "..xrrffoffRRx...",
            "..xrrNNNNNNRx...",
            "..xrrrrrrrRRx...",
            "..xrrrrrrrRRx...",
            "...xxxxxxxxx....",
        } },
        { "Vent", new[] {
            "................",
            ".....xxxxx......",
            "...xsSSSSSSx....",
            "..xsSSSkSSSSx...",
            ".xSxSxxxxxSxSx..",
            ".xSSxxxxkxxSSx..",
            "xSSxxxxxSxxxSSx.",
            "xSSxxxxxSxxxsSx.",
            "xSSxxxxxSxxxsxx.",
            "xSSxxxxxSxxxSSx.",
            "xSSxxxxxSxxxSSx.",
            ".xSxxxxxSxxsSx..",
            ".xSSSxxxxxxsSx..",
            "..xSSSwwwSSSx...",
            "...xSSSxSSSx....",
            ".....xxxxx......",
        } },
        { "CrackedWall", new[] {
            "................",
            "................",
            ".xxxxxxxxxxxxxx.",
            ".xSSSxSSSSSSSSx.",
            ".xSSSkSSSkSSSSx.",
            ".xeekkxkkkkSkkx.",
            ".xSSkSSSkkSSSSx.",
            ".xSSkSSSxsSSSSx.",
            ".xSkkSSSxSSSSkx.",
            ".xsssskkxskksSx.",
            ".xSSSSSxsSSkSSx.",
            ".xSSSSSxSSSSSSx.",
            ".xSSeSkkxeekSSx.",
            ".xSSSsSSxxSSSSx.",
            ".xSSSsSSSkSSSSx.",
            ".xxxxxxxxxxxxxx.",
        } },
        { "Bush", new[] {
            "................",
            "................",
            ".....xxxxx......",
            "...xxggggGxx....",
            "..xggGgGgGGG....",
            ".xgGggGgggGGxx..",
            ".xggggGggGGGGx..",
            ".kGGGgggGggGGGx.",
            "xgGGggGGGggGGGx.",
            "xGggggGGGGgGGGx.",
            "xGgGGGGggGGGGkx.",
            "xGgGGgGgGGGGGG..",
            ".GGGGGGGGGGGGG..",
            ".xGGGGGGGGGGGx..",
            "..kGGGGGGGkGx...",
            "...xkxxkxxxk....",
        } },
        { "OneWayDoor", new[] {
            "................",
            "...xxxxxxxxx....",
            "..xxnkkkkknxx...",
            "..xnNkNkNNknx...",
            "..xkNNNNNNNkx...",
            "..xkNNNNNNNkx...",
            "..xkNNNxNNNkx...",
            "..xkxxxyxNNkx...",
            "..xkxyyyyxNkx...",
            "..xkxyyyyxNkx...",
            "..xkxxxxxxxxx...",
            "..xkNNNxNxoxx...",
            "..xkNNNNNNxkx...",
            "..xkNNNNNNNkx...",
            "..xkNNNNNNNkx...",
            "..xxxxxxxxxxx...",
        } },
        { "PoisonPool", new[] {
            "................",
            "................",
            "................",
            "................",
            ".........xx.....",
            "........xPx.....",
            "....x....xx.....",
            "...x.x..........",
            "....xx.xxx..x...",
            "......x..kx.xx..",
            "...xxxxkkPxxx...",
            ".xkPPkkxxxPdPkx.",
            "xPPPk..kPPd.dxx.",
            "xPPPPkkPPPPkPPx.",
            ".xxxxdPPPPkkkxx.",
            ".....xxxxx......",
        } },
        { "Glue", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....xxxxxx......",
            "..xyyyyyyyxxxx..",
            "..xxyyyYYyyyyx..",
            "xxxyyyyyyyyyxx..",
            "xoyyyyyyyYYyyyyx",
            "..xxxyyyyyyyyxxx",
            "....xxxx.xxxx...",
        } },
        { "RoomLamp", new[] {
            "................",
            "........xxx.....",
            "........xSx.....",
            ".......xSSSx....",
            ".......xeSex....",
            ".xSx.xxSSSSSxx..",
            ".xSx..xxxxxxxx..",
            ".xSxxxxwywwwx...",
            ".xSxSSxwywwwx...",
            ".xSxxxxwywwwx...",
            ".xSxx.xwywwwx...",
            ".xex..xwywwwx...",
            ".xx....xywyyx...",
            "......xxxxxxx...",
            ".......xxxxx....",
            "........xxx.....",
        } },
        { "SpikeTrap", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..x...x..x...x..",
            "..xx.xx..xx..xx.",
            ".xsx.xsx.ws.xsx.",
            ".xssxwsxxssxxss.",
            "xxxxxxxxxxxxxxxx",
            "xSSSSSSSSSSSSSSx",
            "xxxxxxxxxxxxxxxx",
        } },
        { "Cannon", new[] {
            "................",
            "................",
            "................",
            "............xx..",
            "....xxxxxxxxxSx.",
            "..xSeeeeeeeexxx.",
            ".xeekeeeeeeexxx.",
            ".xeekeeeeeeexxx.",
            ".xeekxxxeeeexxx.",
            ".kkkxnnnxkkkxxx.",
            "..kkxnxnxkkxxSx.",
            ".xnnnnnnnnx.xx..",
            "xnxxnnnnxxx.....",
            "xxnnxnnxnxnx....",
            "xxnnxxxxnxnx....",
            "..xx....xxx.....",
        } },
        { "PickupSpot", new[] {
            "................",
            "................",
            ".xxxxxxxxxxxxxx.",
            ".xyyooooooooyyx.",
            ".xyoooxxxxoooyx.",
            ".xoooxwwwwxooox.",
            ".xooxwwxxwwxoox.",
            ".xooxwxooxwxoox.",
            ".xoooxoxxwwxoox.",
            ".xooooxwwwxooox.",
            ".xooooxwwxoooox.",
            ".xoooooxxooooox.",
            ".xooooxwwxoooox.",
            ".xyoooxwwxoooox.",
            ".xYYoooYooooYYx.",
            ".xxxxxxxxxxxxxx.",
        } },
        { "Crate", new[] {
            "................",
            "................",
            ".xxxxxxxxxxxxxx.",
            ".xnnnnnnnnnnnnx.",
            ".xkkkkkkkkkkkkx.",
            ".xnkkNkNNNnNknx.",
            ".xnNNnnNNnnNnNx.",
            ".xnkkNnNnnNkknx.",
            ".xnkNNNnnNNNknx.",
            ".xnkNNnnNNNNknx.",
            ".xnkknnNNnnNknx.",
            ".xnNnnNNNnnNkNx.",
            ".xnkkNkNNNnNknx.",
            ".xkkkkkkkkkkkkx.",
            ".xnnnnnnnnnnnNx.",
            ".xxxxxxxxxxxxxx.",
        } },
        { "GrassGround", new[] {
            "................",
            "................",
            "................",
            ".xxxxxxxxxxxxxx.",
            ".xggggggggggggx.",
            ".xggggggggggggx.",
            ".xGgGgggxgggxgx.",
            ".xkxkkkgkkkGxxx.",
            ".xNNNkNkNNNkNNx.",
            ".xnNNnnNNnnNnnx.",
            ".xnNNnnnNnnNNnx.",
            ".xNnnnNnnnnNNnx.",
            ".xNNNnnnnNNnnNx.",
            ".xnNNnnNnnnnNNx.",
            ".xkNNNNNNNNkNNx.",
            ".xxxxxxxxxxxxxx.",
        } },
        { "GoalZone", new[] {
            ".....xxxxxx.....",
            "...xskssSsexx...",
            "..xsssssssskx...",
            "..sssskxksssex..",
            ".xssexgggxssee..",
            ".xskxgggggskex..",
            ".xssxgggggxsse..",
            ".xssxgggggxsxx..",
            ".xssxgwwwgxsse..",
            ".xssxgwwggxsse..",
            ".xkxxggwggxkkx..",
            ".xssxgggggxsse..",
            ".xssxgggggxskk..",
            ".xssxgggggxsse..",
            "xxxxxxxxxxxxxxx.",
            "xxxxxxxxxxxxxxx.",
        } },
        { "ControllableBlocker", new[] {
            "................",
            "..xx........xx..",
            "..kk........krx.",
            ".xsxkkkkkkkkxxx.",
            ".xxessesseseesx.",
            ".xskseeseeseeSx.",
            "..keseeseeseekx.",
            "..keseeseeseesx.",
            ".xSeseeseeseeSx.",
            ".xSkseeseeseesx.",
            ".xSeseeseeseeSx.",
            ".xseseeseeseesx.",
            ".xSeseeseeseeSx.",
            ".xskseeseeseesx.",
            ".xSeseeseeseeSx.",
            ".xxx........xxx.",
        } },
        { "BananaPeel", new[] {
            "................",
            "................",
            "................",
            "................",
            "........xx......",
            "........Nx......",
            ".......xgx......",
            ".......kyx......",
            "......xyyx......",
            "......xyyx......",
            "......kyyk......",
            ".....kyyyYx..x..",
            "xkxxkyyyyYYkkx..",
            ".xxxyyyxyyYxx...",
            "xxyyykxxxyyyykx.",
            ".xxxx....xxxxx..",
        } },
        { "SimpleEnemy", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            ".....xxxxxxx....",
            "....x..PPdPPx...",
            "xx.x.xPd.PPPPx..",
            ".xxPPxd.PPPPPx..",
            ".xxxxxxPPPPPPP..",
            ".xPPPPxPPPPPPP..",
            ".xxxxwxxPPPPPx..",
            ".xxxxwPxxxxxxxx.",
            ".xxxxxxxxxPx.xx.",
            "xx....xx..xx....",
            "......xx..x.....",
        } },
        { "Checkpoint", new[] {
            "................",
            ".....xx.........",
            ".....oo.........",
            ".....xxxxx......",
            ".....xxBBBxxx...",
            ".....xxBBBBBx...",
            ".....xxBBBBx....",
            ".....xxxxx......",
            ".....xx.........",
            ".....xx.........",
            ".....xx.........",
            ".....xx.........",
            ".....xx.........",
            "....xssx........",
            "...xsssxx.......",
            "...xxxxxx.......",
        } },
        { "Decor", new[] {
            "................",
            "...xxxxxxxxx....",
            "...FxkkkkkkF....",
            "...nFxxxxFFn....",
            "...xkxFFFNNx....",
            "..xFnxNNNNNnx...",
            "..FFFFFFFnnnn...",
            ".xFfNFFFFFnnnx..",
            ".xFFxFFFFFFnnx..",
            ".xFFFFFFFxnnnx..",
            ".xnxFFFxxFnxnx..",
            "..nnFFFFxnNNN...",
            "..xnnnnnnxNNx...",
            "...NnnnnnxNN....",
            "...xNNNNNNNx....",
            "....xxxxxxx.....",
        } },
        { "SpringPad", new[] {
            "................",
            ".......xx.......",
            "......xyyx......",
            "......kxxk......",
            "......x..x......",
            "................",
            "..xkkkkkkkkkkx..",
            "..xkGGGGgGGGkx..",
            "....xxxxwsxx....",
            ".....xxxwsxx....",
            "....xswxxx......",
            ".....xxwwsxx....",
            ".....xxxwwxx....",
            "....xsSxxxx.....",
            "..xkSSSSSSSkkx..",
            "..xxxxxxxxxxxx..",
        } },
        { "CollapsingPlatform", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".xxxxxxx.xxxxx..",
            "xnnnnnx.xnnnnnx.",
            "xnnnnnx.xnnnnnx.",
            "xnNNNnN.xNNnNnn.",
            "xxxxxxx.xxxxxxx.",
            ".....x..........",
            "......x...x.....",
            "...xx...........",
            "........x.......",
        } },
        { "Collectible", new[] {
            "................",
            "................",
            "................",
            "..xxxxxxxxxxx...",
            ".xyNNNNNNNNNyx..",
            "xykNNNNNNNNNNyx.",
            "xfNNNNNNNNNNNyx.",
            "xyNNNNNNNNNNNyx.",
            "xfNNNxfxyxNNNfx.",
            "xyYyyxyxxxyyYyx.",
            "xYxNNxyxyxNNNyx.",
            "xyNNNxyxyxNNNyx.",
            "xYNNNNNNNNNNNyx.",
            "xyNNNNNNNNNNNyx.",
            "xYyyyyyyyyyyyyx.",
            "xxxxxxxxxxxxxxx.",
        } },
        { "Tripwire", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".xx.........xx..",
            "xnx.........xnx.",
            "xnR.........Rnx.",
            "xnx..rrrRr..xnx.",
            "xnx...xyx...xnx.",
            "xnx...xyY...xnx.",
            "xnx...xkx...xnx.",
            "xnx.........xnx.",
            "xxx.........xxx.",
        } },
        { "BadgeLoot", new[] {
            "................",
            ".xxxxxxxxxxxxx..",
            "xxyxNNNNNNNxyxx.",
            "xyyxYYYYnnnxyyx.",
            "xywxNNNNNNNxyyx.",
            "xyyxRnRRRRRxoyx.",
            "xyyxNxxxxxxxoox.",
            "xooxxxyxyoxxoox.",
            "xxxxxxyxyyxxxxx.",
            "xooxNxooyxNxxox.",
            "xooxNxxxxxNxoox.",
            "xooxRNRRRRRxoox.",
            "xooxxxxxxxxxoox.",
            "xoooooooyyoooox.",
            "xxxxxxxxxxxxxxx.",
            "................",
        } },
        { "BadgeExit", new[] {
            "................",
            "xxxxxxxxxx......",
            "xxxxxxxxxx......",
            "xxGGGGGGxx......",
            "xxGGGGGGxx......",
            "xxGGGGGGxx.xx...",
            "xxGGGGGGxxxwxx..",
            "xxGGGGGGxwwwwxx.",
            "xxGGGxxxxwwwwwx.",
            "xxGGGxxxxwwwwxx.",
            "xxGGGGGGxxxwxx..",
            "xxGGGGGGxx.xx...",
            "xxGGGGGGxx......",
            "xxGGGGGGxx......",
            "xxxxxxxxxx......",
            "................",
        } },
        { "BadgeQuestion", new[] {
            "....xxxxxxxx....",
            "...xyyyyyyyyxx..",
            "..xyyyxxxxxyyx..",
            ".xxyyxwwwwxxyyx.",
            ".xyyxwwxxwwxyyx.",
            ".xyyyxxyxwwxyyx.",
            ".xyyyyyxxwxxyyx.",
            ".xyyyyxxwxxyyyx.",
            ".xyyyyxxwxyyyyx.",
            ".xyyyyyxxyyyyyx.",
            ".xxyyyxxxxyyyyx.",
            "..xyyyxxxxyyyx..",
            "...xxyyyyyyxx...",
            ".....xxyyxx.....",
            ".......xx.......",
            "................",
        } },
        { "BadgeAlert", new[] {
            "....xxxxxxxx....",
            "...xrrrrrrrrxx..",
            "..xrrrxxxxrrrx..",
            ".xxrrrxwwxrrrrx.",
            ".xrrrrxwwxrrrrx.",
            ".xrrrrxwwxrrrrx.",
            ".xrrrrxwwxrrrrx.",
            ".xrrrrxwwxrrrrx.",
            ".xrrrrxxxxrrrrx.",
            ".xrrrrrxxrrrrrx.",
            ".xxrrrxxxxrrrrx.",
            "..xrrrxxxxrrrx..",
            "...xxrrrrrrxx...",
            ".....xxxrxx.....",
            ".......xx.......",
            "................",
        } },
        { "BadgeEye", new[] {
            "................",
            "................",
            "................",
            "....xxxxxxx.....",
            "..xxwwxxxwwxx...",
            ".xxwwxBBBxwwxx..",
            "xxwwxBwxxBxwwxx.",
            "xwwwxBxxxBxwwwx.",
            "xxwwxBxxxBxwwwx.",
            ".xxwwxBBBxwwwx..",
            "..xxwwxxxwwxx...",
            "....xxxxxxx.....",
            "................",
            "................",
            "................",
            "................",
        } },
        { "BadgeStar", new[] {
            ".......x........",
            ".......yx.......",
            "..xx..xyx..xx...",
            "..xx..xyx...x...",
            ".....xyyyx......",
            "....xxyyyxx.....",
            "..xxyyyyyyyxx...",
            "xxxyyyyyyyyyyxx.",
            "xxxxyyyyyyyxxxx.",
            "....xyyyyyx.....",
            ".....xyyyx......",
            "......xyx...x...",
            "..xx..xyx..xx...",
            ".......yx.......",
            ".......xx.......",
            "................",
        } },
        { "BadgeAmbush", new[] {
            "....xxxxxxx.....",
            "...xxrrrrrrx....",
            "..xrrwwwwwrrx...",
            ".xrwwwrrrwwwrx..",
            ".xrwwrrrrrrwrx..",
            "xrwwrrwwwrrwwrx.",
            "xrwrrwwrwwrrwrx.",
            "xrwrrwrrrwrrwrx.",
            "xrwrrwwrwwrrwrx.",
            "xrwwrrwwwrrwwrx.",
            "xxrwrrrrrrrwrrx.",
            ".xrwwwrrrwwwrx..",
            "..xrwwwwwwwrxx..",
            "...xrrrrrrrxx...",
            "....xxxxxxx.....",
            "................",
        } },
        { "BadgeHide", new[] {
            "................",
            "................",
            "....xx..........",
            "....xBxxBBx.x...",
            ".xxxxBBBBxxBx...",
            ".xBBxBBBBxBBx...",
            "..xBBdddddBxxx..",
            ".xxBBxxxxBBddBx.",
            "xbBBBxbBxBdBxx..",
            "xdBbxBbbxdxBbxx.",
            "xxxxxBBxxBBbxBx.",
            "xdBxdBBddxBxdBx.",
            "xxxxxxxxxxxxxxx.",
            "................",
            "................",
            "................",
        } },
    };
    /// <summary>角色帧：Hero = 马里奥位（站 / 跑 A / 跑 B / 跳 / 晕），Imp = 捣蛋者（站 / 踮脚 A / 踮脚 B / 跳 / 变身烟）。</summary>
    public static readonly Dictionary<string, string[]> Frames = new Dictionary<string, string[]>
    {
        { "Hero0", new[] {
            "................",
            "................",
            "................",
            "................",
            "......RRRRR.....",
            ".....RrrrRRk....",
            "....RrrRkNNk....",
            "....RrReoFNk....",
            "...RRRNfSoS.....",
            "....kknfnfn.....",
            "....NkknFFF.....",
            "...NNkRNRk......",
            "...NNNRkRkN.....",
            "....kFNdekn.....",
            ".....kdddk......",
            ".....kNkke......",
        } },
        { "Hero1", new[] {
            "................",
            "................",
            "................",
            "................",
            ".......RRRRR....",
            "......RrrRRRk...",
            ".....RrrRNNNk...",
            ".....RRReYFNk...",
            "....RRRNYSoS....",
            ".....kkNfFfn....",
            "....nRRkNnnN....",
            "....fNRNrRkNn...",
            "....kNkkNNRR....",
            "...Nkdddkdd.....",
            "...kkdd..deN....",
            "..........NN....",
        } },
        { "Hero2", new[] {
            "................",
            "................",
            "................",
            "................",
            ".......RRRRR....",
            "......RrrrRRk...",
            ".....RrrRkNNk...",
            ".....RRReFFNk...",
            "....RRRNFSoN....",
            ".....kknoFfn....",
            "....NNkkNnnF....",
            "....NNkRrRnF....",
            "....kkkkRRnF....",
            "...Nedddddd.....",
            "...kkdd.ddeN....",
            "..........N.....",
        } },
        { "Hero3", new[] {
            "................",
            "................",
            "................",
            "......RRRRk.....",
            ".....RrrrRRk....",
            "....RrrRkNNe....",
            "....RRRkFFNk....",
            "...nnRNFSoSNn...",
            "...FnRnonfnNF...",
            "....kRRnFFnR....",
            "....kNRNRRR.....",
            "....NNkNNk......",
            "....Nkddddd.....",
            ".....ddd.kk.....",
            "....Nek..k......",
            "....NN..........",
        } },
        { "Hero4", new[] {
            "................",
            ".....k..........",
            ".....kk....kk...",
            "...........kk...",
            "......RkkR......",
            ".....RrRNrR.....",
            "....RrrRNNNk....",
            "....RrRenNkk....",
            "...RRRNnnFe.....",
            "....kRnnnnN.....",
            "....NkNoFFk.....",
            "...NNNRkRk......",
            "...NNRRNRkR.....",
            "....kfNkNek.....",
            ".....Ndddk......",
            "......ekkk......",
        } },
        { "Imp0", new[] {
            "................",
            "................",
            ".......x........",
            "......xb....x...",
            "......xbbbbxx...",
            "......bbbbbbb...",
            ".....xbbbxbbbx..",
            ".....xbbxwwbxx..",
            ".....xbbbwwxwx..",
            ".....xbbbxxbxx..",
            "......xbbxxbx...",
            "..x..rxbbbbx....",
            ".xx.xrbbbbbbx...",
            ".xxxrrbbbbbbx...",
            "...xxxxxxxbx....",
            ".......xxxxx....",
        } },
        { "Imp1", new[] {
            "................",
            "................",
            "........x.......",
            "........x....x..",
            ".......xbxbxxx..",
            "......bbbbbbbx..",
            ".....xbbbxbbbx..",
            ".....xbbxwwbbx..",
            ".....xbbbwwxxx..",
            "....rxbbbbxbwx..",
            "...xrrxbbxwxx...",
            "..x.rrbbbbbbb...",
            ".xx..xbbbxxx....",
            ".xxxxxxbbbb.....",
            "..xxxbbxxxb.....",
            ".....xx..xxx....",
        } },
        { "Imp2", new[] {
            "................",
            "................",
            ".......x........",
            "......xx...x....",
            "......xbxxxxx...",
            ".....bbbbbbb....",
            "....xbbbbbbbx...",
            "....xbbbxwbbx...",
            "....xbbbxwxxx...",
            ".....bbbbxwxx...",
            "....xxbbbwwx....",
            "...rrrxbbxbbx...",
            "...xxxbbbbbx....",
            "..xx.xbbbxxx....",
            "..xxxxxxxxbb....",
            "..xxxxxx........",
        } },
        { "Imp3", new[] {
            "................",
            "................",
            "......xx........",
            "......bxxxxx....",
            "......bbbbbx....",
            ".....xbbbbbb....",
            ".....bbbwwbbx...",
            ".....bbbwwxxx...",
            "....xbbbbwwwx...",
            "...xrxbbbxbxx...",
            "...rrxbbbxxBx...",
            "...xxxbbbbxx....",
            ".....xbbbbxx....",
            "..xx.xxbbxbb....",
            "..xxxx..x.x.....",
            "...xx...........",
        } },
        { "Imp4", new[] {
            "................",
            "................",
            "................",
            ".....x..........",
            "....xk...xx.....",
            ".xx.xbkkkbx.xx..",
            "..x.kbbbbbx.xx..",
            "...xbbxbbbx.....",
            "..xwwbxwxbxxkkx.",
            ".xwwwxwwxxwwwwx.",
            "..kwwwwwwwwwwk..",
            "xkwwwwwwwwwwwk..",
            "xwxwwwwwwwwwwx..",
            ".xxwwwwwwwwxwwx.",
            "...xkkwwkxxkkx..",
            "......xx........",
        } },
    };
    /// <summary>地形块：GroundTop 地面最上层（有草）、GroundFill 地面里层、Wall 墙、Platform 单向板（16×8，S244 加粗）、StoneTop、MossWall。</summary>
    public static readonly Dictionary<string, string[]> Tiles = new Dictionary<string, string[]>
    {
        { "GroundTop", new[] {
            "keeeeeeeeeeeeeek",
            "eGGGGGGGGGGGGGGe",
            "eeGeGeeGeGeGGGee",
            "kNeNGNNeNeNeeNNk",
            "eNNNNNNNNNNNNNNe",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "eNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "keeeeeeeeeeeeeek",
        } },
        { "GroundFill", new[] {
            "keNNeNNeeeeeeeek",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNnNNNNNNNNNN",
            "NNNNNNNNnNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "keeeeeeeeeeeeeek",
        } },
        { "Wall", new[] {
            "eeeeeeeeeeeeeeee",
            "eSSSSSSSSSSSSSSe",
            "eSSSSSSSSSSSSSSe",
            "eSSSeeSSSSSSeSSe",
            "eeSSSSSSeSSSSSSe",
            "eSSSSSSSSSSSSSSe",
            "eSSSSSSSSSSSSSSe",
            "eeeSSSeSeeSSSSSk",
            "eSSSSeSSSSSSeSSe",
            "eSSSSSSSSSSSSSSe",
            "eSSSSSSSSSSSeSSe",
            "keeeeeeeeeeeeeek",
            "eeSSSSSSeSSSSSSe",
            "eSSSSSSSSSSSSSSe",
            "eSSSSSSSeSSSSSSe",
            "kkeeeeeekkeeeeek",
        } },
        { "Platform", new[] {
            "eeeeeeeeeeeeeeee",
            "sffffffffffffffs",
            "FffffffffffffffF",
            "SFFFFFFFFFFFFFFS",
            "RkkkRRRRRRRRkkkR",
            ".ee..........ee.",
            ".ee..........ee.",
            ".Rk..........kR.",
        } },
        { "StoneTop", new[] {
            "kkkkkkkkkkkkkkkk",
            "keeeeeeeeeeeeeek",
            "keeeeeeeeeeeeeek",
            "keedkeeeeeekdedk",
            "keeeeeekdedeeeek",
            "keeeeeeeeeeeeeek",
            "keeeeeekeeekekek",
            "keedekekeeeeeedk",
            "keeeeeeekekeeekk",
            "keedeeeedeeeeeek",
            "kekkdeekeeeekeek",
            "kdeeekeedeeekeek",
            "kkeeedeeededeeek",
            "keeeedeeeeeeeeek",
            "keeedeeeeeeeeekk",
            "kkkkkkkkkkkkkkkk",
        } },
        { "MossWall", new[] {
            "eeeeekeeeeeekeek",
            "eSSSSeSSSSSSSSSe",
            "eSSSSSSSSSSSeSSe",
            "eSSSSeSSSSSSeSSe",
            "keSSSSSSeeSSSSSk",
            "eSSSSSSSeSSSSSSe",
            "eSSSSSSSSSSSSSSe",
            "eeeeSeGeeeSSeeSk",
            "eGGSSeGSSSSGeSSe",
            "eSSSSSSSSSSSSSSe",
            "eSSSSeSSSSSSeSSe",
            "keeeeeeeeeeeeeek",
            "eeSGSSSSeSSSSSSe",
            "eSSSSSSSSSSSSSSe",
            "eeSSSSSSeSSSSSSe",
            "kkeeeeeekkeeeeek",
        } },
        // S247：室内的"草地 v"= 稻草垫（和 GrassGround 同轮廓、同大小，只换成干草色）
        { "HayGround", new[] {
            "................",
            "................",
            "................",
            ".xxxxxxxxxxxxxx.",
            ".xyyYyyyYyyyYyx.",
            ".xYyyoyYyyoyyYx.",
            ".xyYyyYyyYyyYyx.",
            ".xkYkkYkkkYkkkx.",
            ".xNNNkNkNNNkNNx.",
            ".xnNNnnNNnnNnnx.",
            ".xnNNnnnNnnNNnx.",
            ".xNnnnNnnnnNNnx.",
            ".xNNNnnnnNNnnNx.",
            ".xnNNnnNnnnnNNx.",
            ".xkNNNNNNNNkNNx.",
            ".xxxxxxxxxxxxxx.",
        } },
    };
    public static readonly float[][] BackgroundPalette =
    {
        new[] { 0.38f, 0.337f, 0.467f },
        new[] { 0.286f, 0.294f, 0.404f },
        new[] { 0.278f, 0.267f, 0.361f },
        new[] { 0.255f, 0.267f, 0.365f },
        new[] { 0.247f, 0.259f, 0.357f },
        new[] { 0.251f, 0.255f, 0.345f },
        new[] { 0.239f, 0.255f, 0.349f },
        new[] { 0.227f, 0.231f, 0.314f },
    };
    /// <summary>64×36 背景（每个字符 = BackgroundPalette 下标）。第一行在最上面。</summary>
    public static readonly string[] Background =
    {
        "7777777777777777777777777777777777777777777777777777777777777777",
        "7777777555555555555555555555555555555555555555577777777777777777",
        "7777755555555555555555556765655565556655555555655555777777777777",
        "5555555555555555555555556655555555555556555555555555555557777777",
        "6556555566655666665566666666666666666666666656655556555557577777",
        "6555665444444444444446644444444446444444666444444455555555557777",
        "6524555333444444444344344443444433344434666444444355555562555777",
        "5525224333344444344334444311134433233444666444433455225552255557",
        "5222225434444444444334443101013444333444644344322655222225225557",
        "5522226664664446666444431001001666666666466663222552222525222557",
        "5222225433443344111444430000101333331134444242323552222223222557",
        "5222226434444441000144330100011333310113333555555252222223322557",
        "5222226333443440000133330000100333100001433555555552222223322527",
        "5222234333233430000033320000000333100001443555555552222223112227",
        "5222214333123330000033320000000333100001333567775552222223101225",
        "5222113222223330000033330011101333100001333555555552222223100255",
        "5222013222223330000033310000000333100001333555555552222223100025",
        "5521013333333330000033310000000331100000333257755552222223100015",
        "5510013333333320000033310000000331100000333255535552222223100015",
        "5500013233333320000033310000000311100000333555555555222223100025",
        "5510013333333330010013330000001312100001333355552365522223100122",
        "2210013333333320000013310000001332100001333535552355522223101122",
        "2211113323322331110013310000001332100001333222222255522253101122",
        "2221113313333331111113311101011333100101332233222255522253101122",
        "2221133333333331111133331111111333111111332222232255522253111122",
        "2222233333333331111133331111111343211111333222222345522253111222",
        "2224444444444444444444444444444464444444444444444445525543111222",
        "2226444444444444444444344444444444444444444444444445555433333222",
        "2224343333444444434444444444443443343333343344434345555543443222",
        "5224344444444444434446443444444444443334344444434345525543333222",
        "5224444444444444444444344444444444444444364444434445522543232225",
        "5224444334443444444443344444433334433344444444444445525543242225",
        "5524444446444444664446666666666664444644664444464445525543243525",
        "5554664466666666666666666666666666666666666666666665525743344777",
        "7777667666666666666666666666666666666666666666646665557744667777",
        "7777777776666666666666666666666666666666666666677777777777777777",
    };

    public enum Pose { Idle, RunA, RunB, Jump, Stunned }

    /// <summary>纯逻辑：根据状态挑一帧（空中 = 跳；晕 = 晕；跑步两帧交替，S244：跑得越快换得越快）。捣蛋者的"晕"帧是变身烟，只在变身那一下用。</summary>
    public static Pose PoseOf(bool grounded, float speedX, bool stunned, float time)
    {
        if (stunned) return Pose.Stunned;
        if (!grounded) return Pose.Jump;
        if (speedX < 0.15f) return Pose.Idle;
        return ((int)(time * RunFps(speedX))) % 2 == 0 ? Pose.RunA : Pose.RunB;
    }

    /// <summary>S244：跑步换帧速度（每秒几帧）= 4 + 速度 × 1.5，夹在 6–12。慢走 = 慢慢迈腿，冲刺 = 腿快速交替（脚步和移动对得上，不"滑步"）。</summary>
    public static float RunFps(float speedX)
    {
        float f = 4f + (speedX < 0f ? -speedX : speedX) * 1.5f;
        return f < 6f ? 6f : f > 12f ? 12f : f;
    }

    /// <summary>S244：换图后画多大、往上挪多少（相对物体中心，单位 = 格）。出口 = 1.5 格高的门、底边踩在地上；宝物 = 0.9 格、放在地上；
    /// 其余 = 原色块大小（0.6–1 格），底边对齐原色块底边。只动外观，碰撞体 / 触发框不变（H3）。</summary>
    public static float[] PlaceOf(string key, float sx, float sy)
    {
        if (key == "GoalZone") return new[] { 1.5f, 0.25f };
        if (key == "Collectible") return new[] { 0.9f, -0.05f };
        float s = FitScale(sx, sy); return new[] { s, FitLift(sy, s) };
    }

    /// <summary>S244：跑步扬尘间隔（秒）。0 = 不扬尘（站着 / 空中）。</summary>
    public static float DustEvery(bool grounded, float speedX) => grounded && (speedX < 0f ? -speedX : speedX) > 2.5f ? 0.22f : 0f;

    /// <summary>S244：落地扬尘有多大（0 = 不扬）：从空中落下、下落速度越大尘越大（0.4–1.4）。</summary>
    public static float LandDust(bool wasGrounded, bool grounded, float fallSpeed)
    {
        if (wasGrounded || !grounded) return 0f;
        float v = fallSpeed < 0f ? -fallSpeed : fallSpeed;
        if (v < 3f) return 0f;
        float s = v / 12f; return s < 0.4f ? 0.4f : s > 1.4f ? 1.4f : s;
    }

    /// <summary>纯逻辑：小恶魔的帧——第 5 帧是变身烟（只在变身 / 现形那一下），平时站 / 踮脚 / 跳。</summary>
    public static Pose ImpPose(bool poof, bool grounded, float speedX, float time) => poof ? Pose.Stunned : PoseOf(grounded, speedX, false, time);

    public static string FrameKey(bool hero, Pose p) => (hero ? "Hero" : "Imp") + (int)p;

    public static bool TryColor(char c, out float[] col)
    {
        if (OverworldArt.Palette.TryGetValue(c, out col)) return true;
        return ExtraPalette.TryGetValue(c, out col);
    }

    /// <summary>字符画 → RGBA（从左下角开始，和 Texture2D.SetPixels 一致）。宽 = 每行长度，高 = 行数。</summary>
    public static float[] Rgba(string[] rows)
    {
        if (rows == null || rows.Length == 0) return null;
        int h = rows.Length, w = rows[0].Length; var px = new float[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            char c = rows[h - 1 - y][x]; int i = (y * w + x) * 4;
            if (c == '.' || !TryColor(c, out var col)) continue;
            px[i] = col[0]; px[i + 1] = col[1]; px[i + 2] = col[2]; px[i + 3] = 1f;
        }
        return px;
    }

    public static float[] BackgroundRgba()
    {
        int h = Background.Length, w = Background[0].Length; var px = new float[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            var col = BackgroundPalette[Background[h - 1 - y][x] - '0']; int i = (y * w + x) * 4;
            px[i] = col[0]; px[i + 1] = col[1]; px[i + 2] = col[2]; px[i + 3] = 1f;
        }
        return px;
    }

    /// <summary>纯逻辑：白盒是 1 格 × (sx, sy) 的色块；像素图是正方形 → 取大的那边（夹在 0.6–1 格），扁的东西图里本来就画在底部。</summary>
    public static float FitScale(float sx, float sy) => FitScale(sx, sy, 0.6f);
    public static float FitScale(float sx, float sy, float min)
    {
        float m = sx > sy ? sx : sy; if (m < 0f) m = -m;
        return m < min ? min : m > 1f ? 1f : m;
    }

    /// <summary>S244：单向板换图后高 newH（像素图 8px = 0.5 格），原来色块高 oldH——往下挪多少，让板子的上表面不变（脚踩的位置 = 碰撞体顶）。</summary>
    public static float TopKeep(float oldH, float newH) => (oldH - newH) * 0.5f;

    /// <summary>纯逻辑：图往上挪多少，让图的底边 = 原来色块的底边（扁机关贴地，不会飘在半空或陷进地里）。</summary>
    public static float FitLift(float sy, float scale) => (scale - (sy < 0f ? -sy : sy)) * 0.5f;

    /// <summary>纯逻辑：地形块用哪张图（物体名前缀 → 图块）。墙 / 平台 / 地面最上层有草、下面是土。</summary>
    public static string TileFor(string objectName, bool hasAirAbove)
    {
        string n = objectName ?? "";
        if (n.StartsWith("Wall_")) return "Wall";
        if (n.StartsWith("OneWayPlatform_")) return "Platform";
        if (n.StartsWith("Ground_") || n.StartsWith("Platform_")) return hasAirAbove ? "GroundTop" : "GroundFill";
        return null;
    }

    /// <summary>S244：这个物体是不是地形（地面 / 墙 / 平台 / 单向板）——机关换图时跳过它们。</summary>
    public static bool IsTerrain(string objectName)
    {
        string n = objectName ?? "";
        return n.StartsWith("Ground_") || n.StartsWith("Wall_") || n.StartsWith("Platform_") || n.StartsWith("OneWayPlatform_");
    }

    // ═════════ S247：地表要符合真实空间（草只长在露天、看得见天的地面上）═════════
    /// <summary>S247：一块地面上方是什么。Open = 露天（往上一直到房间顶都没有遮挡）；Roofed = 上面有楼板 / 天花板 / 单向板；Buried = 紧贴着上面就是实心块（埋在地里，看不见表面）。</summary>
    public enum Cover { Open, Roofed, Buried }

    /// <summary>S247：从物体名 "{元素}_{x}_{y}_w{宽}" 读出起点格和宽度（Wall_3_4 这类没有宽度 = 1）。</summary>
    public static bool TryParseCells(string objectName, out int x0, out int y, out int width)
    {
        x0 = 0; y = 0; width = 1;
        if (string.IsNullOrEmpty(objectName)) return false;
        var p = objectName.Split('_');
        if (p.Length < 3 || !int.TryParse(p[1], out x0) || !int.TryParse(p[2], out y)) return false;
        if (p.Length >= 4 && p[3].Length > 1 && p[3][0] == 'w' && int.TryParse(p[3].Substring(1), out int w)) width = w < 1 ? 1 : w;
        return true;
    }

    /// <summary>S247：rows = 房间字符画（第 0 行在最上面），(x, yWorld) = 世界格（0 = 最下面一行）。solid 判断某个字符是不是实心块（箱子、草地 v、裂地等都算）。
    /// 单向板 '-' 不实心但会挡天（楼上的木板下面不会长草）。</summary>
    public static Cover CoverAt(string[] rows, int x, int yWorld, System.Func<char, bool> solid)
    {
        if (rows == null || rows.Length == 0 || solid == null) return Cover.Open;
        int h = rows.Length, row = h - 1 - yWorld;
        char At(int r) => r < 0 || r >= h || x < 0 || x >= rows[r].Length ? '.' : rows[r][x];
        if (solid(At(row - 1))) return Cover.Buried;
        for (int r = row - 2; r >= 0; r--) { char c = At(r); if (c == '-' || solid(c)) return Cover.Roofed; }
        return Cover.Open;
    }

    /// <summary>S247：一整条合并地面（一张平铺图）按多数格决定：一半以上埋着 = 埋着；否则露天格不少于有顶格 = 露天。
    /// 以前只要有 1 格上面不是"地面/墙"就整条画草 → 箱子、草地 v、裂地下面的那一层也长出一条草。</summary>
    public static Cover StripCover(string[] rows, int x0, int width, int yWorld, System.Func<char, bool> solid)
    {
        int open = 0, roofed = 0, buried = 0;
        for (int i = 0; i < width; i++)
        {
            var c = CoverAt(rows, x0 + i, yWorld, solid);
            if (c == Cover.Open) open++; else if (c == Cover.Roofed) roofed++; else buried++;
        }
        if (buried * 2 > width) return Cover.Buried;
        return open >= roofed ? Cover.Open : Cover.Roofed;
    }

    /// <summary>S247：地形块用哪张图（符合实际版）。草皮 GroundTop 只给"户外 + 露天"的地面；室内地面 = 石板 StoneTop；户外但头顶有楼板 = 裸土 GroundFill；埋在下面 = 土 GroundFill。
    /// 墙 / 单向板与以前相同。</summary>
    public static string TileFor(string objectName, Cover cover, bool outdoor)
    {
        string n = objectName ?? "";
        if (n.StartsWith("Wall_")) return "Wall";
        if (n.StartsWith("OneWayPlatform_")) return "Platform";
        if (!(n.StartsWith("Ground_") || n.StartsWith("Platform_"))) return null;
        if (cover == Cover.Buried) return "GroundFill";
        if (!outdoor) return "StoneTop";
        return cover == Cover.Open ? "GroundTop" : "GroundFill";
    }

    /// <summary>S247：草地 v（遁地不露土包）在室内画成"稻草垫"HayGround——功能不变（仍是软地、仍和普通地面看得出区别），只是屋里不长草。</summary>
    public static string PropArtFor(string key, bool outdoor) => key == "GrassGround" && !outdoor ? "HayGround" : key;

    /// <summary>S247：雨从房间顶往下落，碰到第一块实心块或单向板就停（世界 y = 那块的上表面）。返回 &gt;= 房间高度 - 0.5 = 这一列顶上就被挡住（屋里不下雨）。</summary>
    public static float RainStopY(string[] rows, int x, System.Func<char, bool> solid)
    {
        if (rows == null || rows.Length == 0 || solid == null) return -0.5f;
        int h = rows.Length;
        for (int r = 0; r < h; r++)
        {
            char c = x < 0 || x >= rows[r].Length ? '.' : rows[r][x];
            if (c == '-' || solid(c)) return (h - 1 - r) + 0.5f;
        }
        return -0.5f;
    }
}
