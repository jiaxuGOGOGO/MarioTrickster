// 自动生成：tools_s245/gen_world.py（S245 AI 生成美术 → 16×16 字符画）。改图：改下面的字符画，或把同名 PNG 放进 Assets/Resources/Step1Art/<Key>.png 覆盖（小镇格子也认 Resources/OverworldArt/<Key>.png）。
using System.Collections.Generic;

/// <summary>
/// S245：用户「美术素材是否覆盖全面 包括天气特效和各自的技能及道具场景以及是否支持在自定义搭建的时候的充分拓展 地上小镇大地图的情景 远处的山脉 还有进入房间内的装饰道具」。
/// 自检：小镇的草 / 路 / 房子 / 树 / 水 / 山 都还是色块；天气只有一层全屏染色；炸弹 / 炮弹 / 遁地土包是黑方块，挑衅只有一行字；装饰只有一个陶罐；11 种工坊元素（摆锤、锯片、移动平台…）还是白盒；没有远山。
/// 这里补齐：小镇 16 块（俯视 3/4 角度，像星露谷）、房间装饰 12 件（老宅：画 / 书架 / 烛台 / 盆栽 / 座钟 / 拱窗 / 地毯 / 挂旗 / 盔甲 / 木桶 / 吊灯 / 蛛网）、
/// 天气与技能 12 个（雨 / 水花 / 酸雨 / 落叶 / 雾 / 雪 / 炸弹 / 炮弹 / 挑衅气泡 / 土包 / 钩爪 / 雷云）、工坊元素 11 个、远山 + 近丘两条视差长图。
/// 全部是 GPT Image 2 生成的原创图（洋红底 → 切格 → 先吸调色板再按格投票缩到 16×16 → 补深色描边），用 OverworldArt.Audit 过审，并且让 AI 在"不给提示"的情况下逐个说出是什么（tools_s245/review*.json）。
/// 参考：星露谷天气 = 雨 / 风（落叶）/ 雷雨 / 雪 / 绿雨，每种都有自己的画面 https://stardewvalleywiki.com/Weather ；
/// 平台游戏的素材包通常按"地形块 + 道具 + 背景层"分（例：124 件村庄道具 + 草地图块 + 树）https://cainos.itch.io/pixel-art-platformer-village-props 。
/// 只换外观：碰撞体、判定、玩法一律不动（H3）；背景装饰压暗、不加碰撞体、不挡机关（H6：主层的东西才显眼）。
/// </summary>
public static class WorldArt
{
    public const int Size = 16;
    /// <summary>小镇格子（俯视）：TGrass 草 / TPath 石子路 / TRoof 屋顶 / TWall 墙面（带窗和门）/ TTree 树 / TWater 水 / TFence 栅栏 / TTallGrass 高草 / TMud 泥地 / TMountain 山 / THill 山丘 / TCave 山洞 / TDoor 房间门 / THome 马里奥的家 / TSpawn 你的出生帐篷 / TSign 路牌。前 6 个（草 / 路 / 屋顶 / 墙 / 水 / 泥）是满格地面，能无缝平铺。</summary>
    public static readonly Dictionary<string, string[]> Town = new Dictionary<string, string[]>
    {
        { "TGrass", new[] {
            "gggggggggggggggg",
            "gggggggggggggggg",
            "gggGggggggGggggg",
            "ggGGgGgggggggggg",
            "ggggggggggggGggg",
            "ggggggggggggGgGg",
            "ggggggggggggGggg",
            "gggggggggggggggg",
            "gggggggggggggggg",
            "gggggggGGggggggg",
            "ggggggGggggggggg",
            "ggggggGggGgggggg",
            "ggGggggggggggggg",
            "gGGgGgggggggGGgg",
            "ggGGgggggggggGgg",
            "gggggggggggggggg",
        } },
        { "TPath", new[] {
            "sSssSSSSSssSesss",
            "SssssseSssssSSss",
            "esssssNNSssSSsse",
            "sSssSeeeeSSessss",
            "sSeSeSssssesssss",
            "sSssSssssssSssss",
            "esssSSsssseeeeSs",
            "eeSSeeeSSesssess",
            "ssSessseeesssSSs",
            "sssSsssSNNSSeess",
            "ssSssssSesssesss",
            "eeeessseSssssSSS",
            "sssseSeeSsssseee",
            "sssssNNeSssssSss",
            "sssseesssSSSesss",
            "sSeeessssseSsSss",
        } },
        { "TRoof", new[] {
            "rRrrrrrrrRxSSSxR",
            "rRrrrrrrrRxkekxR",
            "rRrrrRRrrRRxSSkR",
            "RRRRRRRRRRRkkkRR",
            "rrRRRrrRRRRRRRRR",
            "rrrRrrrRrrrRrrrR",
            "rrrRrrrRrrrrrrrR",
            "rrRRRrRRRrrRRrrR",
            "RRRrRRRrRRRrRRRr",
            "rRrrrRrrrrRrrRrr",
            "RrrrrRrrrrRrrrrr",
            "RRrrRRRrrRRrrRRr",
            "RRRRRrRRRRRRRRRR",
            "rrRRrrrRrrrrRrrR",
            "rrrRrrrRrrrrRrrR",
            "rrRRrrrRRrrRRrrR",
        } },
        { "TWall", new[] {
            "rrRrrrrrrrrrrrRr",
            "rRRrrRRrrRRrrRRr",
            "xxxxxxxxxxxxxxxx",
            "nnnnnnnnnnnnnnnn",
            "fffffffffffwffff",
            "ffNNNNffffNNNkff",
            "fNnkknNffNnkkNNf",
            "fnxbkknNknkNNkNk",
            "fnkkkknfkkNnnNnk",
            "fnkbkknkkkNnnnnk",
            "fNkkkkNfknNnnnnk",
            "fkNnNNkfNkYNNnnk",
            "ffkkkFffkkNnNnnk",
            "ffffffffknNnNNnk",
            "NNNNNNNNNxxxxkNN",
            "nNnnnNnnkSssssSN",
        } },
        { "TTree", new[] {
            "................",
            ".....xxxxx......",
            "....ggggggx.....",
            "...xggggggGx....",
            "..xGGggggggGx...",
            ".xGGgggggGGGGx..",
            ".xGggGGggGggGx..",
            ".xGGGggGGgGGGx..",
            ".xGGGGggGgGGGx..",
            ".xGGGGGGGGGGGx..",
            "..xxGGGGGGGkx...",
            "...xxkkkkkx.....",
            ".....xkkkx......",
            "....xxnnkx......",
            "...xxnNnNxx.....",
            ".....xxxxx......",
        } },
        { "TWater", new[] {
            "BwwBwbbwBBBBBBBB",
            "wwBBBwwBBBBBBBBB",
            "BBBBBBBBBbbbbbbB",
            "BBbbBBBBBbwwwBbw",
            "BBBBBBBBBBBBwwwB",
            "BBBBBBBBBBBBBBBB",
            "BBBBBBBBBBBBBBBB",
            "BbbbbbBBBBBBbbbB",
            "bbwwwbbbwBBBBBBB",
            "wwBBBwwwBBBBBBBB",
            "BBBBBBBBBBBBBbbb",
            "BBBBBBBBBBBBBBBB",
            "BBbbBBBBbbbbbBBB",
            "BBBBBBBBbwwbbbbb",
            "BBBBBBBBwBBwwwwB",
            "BbbBBbbBBBBBBBBB",
        } },
        { "TFence", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..x..x...x......",
            ".nn.xnx.xn..nx..",
            ".nn.xnx.xnxxnx..",
            "xnnxxnxxnnxxnx..",
            "xnnkNnkNnnkNnkx.",
            "xnnxxnxxxnxxnxx.",
            ".nn.xnx.xnxxnx..",
            "xnnxxnxxnnxxnnx.",
            "xnnkknkNnnkkNnx.",
            ".xxxxxxxxxxxxx..",
        } },
        { "TTallGrass", new[] {
            "................",
            "................",
            "................",
            "................",
            "..x.........x...",
            "..xx..xx.x.xx...",
            ".x.xx.x.xxxx.xx.",
            ".xxxgxGxxxgxxx..",
            ".xgkgxGxxxgkgx..",
            "xxgxgkxgkkgxGx..",
            "xxkxGgkgkgGkkxx.",
            ".gkGkgkgkgGgkg..",
            ".xGgkGkgkgkGkx..",
            "xxkkGGkGkkGkkxx.",
            ".kGGGGGkGGGkGG..",
            ".xxxxxxxxxxxxx..",
        } },
        { "TMud", new[] {
            "xNNxxxNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNkN",
            "NNNNNNNNNNNNNNkN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNnNNNNNNNNNNNNN",
            "NkkNkkNNNNNNNNNN",
            "NNkkNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNNNN",
            "NNNNNNNNNNNNNkNN",
            "NnNNNNNNNNNNkNNN",
            "NkkNNNNNNNNNNNNN",
            "NNNkxxNNNNNNNNNN",
        } },
        { "TMountain", new[] {
            "................",
            "......xx........",
            ".....xwsx.......",
            ".....xwws.......",
            "....xwswss......",
            "...xwswwwsx.....",
            "...xweweweex....",
            "..xSeSwSkeeex...",
            "..xSSsSSkeeSx...",
            "..SSssSSSkeeex..",
            ".xSeseSSSkseek..",
            "xsSsSkkseesekex.",
            "xSSSSkssekSSekx.",
            "xSSSksSSeekSekx.",
            "..xkkSSeeekex...",
            "....xxxxxxx.....",
        } },
        { "THill", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "......xxxx......",
            "....xgggggx.....",
            "...xggggGggx....",
            "...gggGGGGGGx...",
            "..xgGGGGGGGGG...",
            ".xgGGGGGGGGGGx..",
            ".xGGGGGGGGGGGGx.",
            "xgGGGGGGGGGGGGx.",
            ".kNnGGGGGGGGGkk.",
            "...kkxxxxxxxk...",
        } },
        { "TCave", new[] {
            "................",
            "................",
            "................",
            ".....xxxx.......",
            "...xxSsSSxx.....",
            "..xssSSseSSx....",
            "..xSSSSSSSekx...",
            "..xSSSxxSSeSSx..",
            ".xSSSxxxxeSsex..",
            ".xSSexxxxxsSex..",
            "xsSekxxxxxSSkx..",
            "xSesSxxxxxSesx..",
            "xGeSkxxxxxeSSex.",
            "xGGeexxkxxnSeex.",
            "xkGGkxSSekGGGGx.",
            "..xxxxxxxxxxxx..",
        } },
        { "TDoor", new[] {
            "................",
            "......xxxxx.....",
            "....xkSSSSsx....",
            "...kSSeSNeSSk...",
            "..xSSeNkkkeSSx..",
            "..xSSfk...kSSx..",
            "..xSNk....kNSx..",
            "..xSNrk...kNSx..",
            "..ksNk.kk..NSk..",
            "..eeNk.k.kkNex..",
            "..xSSk...krNSx..",
            "..xeNk..k.kNex..",
            "..kSerk..krNSsx.",
            ".xssksrkkrskssx.",
            ".xSSkessssekSSx.",
            ".xxxxxxxxxxxxxx.",
        } },
        { "THome", new[] {
            "................",
            "......x...xxx...",
            "....xrRxRxxSx...",
            "....xRrRRRxSx...",
            "..kRRRxxkRRex...",
            "..RRrxNnNxRRRx..",
            ".xRRRNFNfNxRRR..",
            ".RRRNffNffnkRR..",
            ".RRkFffffffNxR..",
            ".xxFkkkfNkkfNx..",
            "..NfkkkfkNNNNx..",
            ".xNfGGkfkNNnNxx.",
            "xxNfGNkfkNNnNxx.",
            "xGkfnkNfkkkNNxx.",
            "xGGkNNNkSseSkGx.",
            ".xxkxxxxxxxxxx..",
        } },
        { "TSpawn", new[] {
            "................",
            "................",
            ".......x........",
            ".......x........",
            "......xdx.......",
            ".....xBBBx......",
            "....xBBBBk......",
            "....kBBBBBk.....",
            "...kBBBdBBBk....",
            "xxkBBBBxdBBBxxx.",
            "xxBBBBdxdBBBBx..",
            "kxBBBdBxBdBBdkk.",
            "kxBBBBxxxBBddkk.",
            "xxBBBBxkxBBBxxx.",
            "xxdBBBkkxBBBxx..",
            ".xkkxxkkkxxkxx..",
        } },
        { "TSign", new[] {
            "................",
            "......xx........",
            "......nNx.......",
            "..xxxxNNxxxx....",
            "..xnnnnnnnnnx...",
            "..xnnnnnnnnnnx..",
            "..xNnnnnnnnnNx..",
            "..xnnnnnnnnnx...",
            "..xxxxxxxxxx....",
            "......nNx.......",
            "......nNx.......",
            "......nNx.......",
            ".....xnNxx......",
            ".....xnNxx......",
            "....xxGNkxx.....",
            ".....xxxxx......",
        } },
    };
    /// <summary>房间装饰（老宅）：墙上挂的（画 / 拱窗 / 挂旗 / 座钟 / 书架 / 蛛网 / 吊灯）和地上放的（烛台 / 盆栽 / 地毯 / 盔甲 / 木桶）。</summary>
    public static readonly Dictionary<string, string[]> Decor = new Dictionary<string, string[]>
    {
        { "DecoPainting", new[] {
            "................",
            ".xxx.......xxx..",
            ".xoNkkkkNkkYox..",
            ".xnxkkkxkkkxnx..",
            ".xnkkkkxxkkkk...",
            ".xnkkkkfxxkkk...",
            ".xYkkxxxfxkkk...",
            ".xnkkxFffxkkk...",
            ".xYkkxxffxxxk...",
            ".xnkkxkxxxxxk...",
            ".xnxxkxxfxxxk...",
            ".xnxkkkkfxkxk...",
            ".xnxkxkkRxxxk...",
            ".xnxxxxxxxxxnx..",
            ".xonkkkkkkknox..",
            ".xxx.......xxx..",
        } },
        { "DecoShelf", new[] {
            "................",
            "..xxxxxxxxxxx...",
            "..xnnkkkkkkkx...",
            "..xNxxxxxxxkx...",
            "...NxxxxxxRN....",
            "...NxxxxxxYN....",
            "...NNNNxNnnN....",
            "...NxxxxxxxN....",
            "...NxxRxGRxN....",
            "...NxxRxGxxN....",
            "...NxxxxxxxN....",
            "...NxxxxxxxN....",
            "...NxYxBxxPN....",
            "...NxYxBxxkN....",
            "..xNNkkkkkkkx...",
            "..xxxxxxxxxxx...",
        } },
        { "DecoCandle", new[] {
            "................",
            ".......xx.......",
            ".......xx.......",
            "...ox..xx..xo...",
            "...xx..xx..xx...",
            "...xx..xx..xw...",
            "...fx.xYYx.xf...",
            "..xfxx.xx..xNx..",
            "..xYxxxxxxxxxx..",
            "...xxxxnNxxxx...",
            "....xx.xN.xx....",
            ".......NN.......",
            ".......xx.......",
            ".......xn.......",
            ".....xxnYxx.....",
            ".....xxxxxx.....",
        } },
        { "DecoPlant", new[] {
            "................",
            ".....xx.........",
            ".....ggx........",
            ".....xGGxggx....",
            "...xxxGGxgx.....",
            "..xxgGxxGxxx....",
            "....xGxGxGGGx...",
            "...xGxxkGkx.....",
            "..xgggxxxGgx....",
            "..x.xxGkGxxgx...",
            "....xxxGxxx.....",
            "....kxxxxxx.....",
            "....xnnnnNx.....",
            "....xNkNkNx.....",
            "....xNnNNx......",
            ".....xxxxx......",
        } },
        { "DecoClock", new[] {
            "................",
            "......xxx.......",
            "....xxxxxxx.....",
            "....xxxNkxx.....",
            ".....kfffkx.....",
            ".....kfffxx.....",
            "....xkNfNkx.....",
            "....xkNkkxx.....",
            ".....xxxxx......",
            ".....xxxxx......",
            ".....xkxkx......",
            ".....xxYxx......",
            ".....xxxxx......",
            "....xxxxxxx.....",
            "....xNNNNNx.....",
            "....xxxxxxx.....",
        } },
        { "DecoWindow", new[] {
            "................",
            "......xxx.......",
            "....xSeekSx.....",
            "...xSkxxxxSx....",
            "...xkddxffxS....",
            "...SxddxffxS....",
            "..xSxddxdBdSx...",
            "..xkxxxxxxxkx...",
            "..xSxxxxxxxkx...",
            "..xexddxdddkx...",
            "..xSxddxdxxkx...",
            "..xSxxdxdxxkx...",
            "..xexxxxxxxkx...",
            "..xSxxxxxxxSx...",
            "..xSxxxxSxxSx...",
            "..xxxxxxxxxxx...",
        } },
        { "DecoRug", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".xxxxxxxxxxxxxx.",
            ".xYRYRRRRRRRRYx.",
            ".xYRRRRRRRRRRRx.",
            ".xYYRRRRRRRRYRx.",
            "xYRRRRRRRRRRRRYx",
            "xYRYRRRRRRRRYRYx",
            "xxxxxxxxxxxxxxxx",
        } },
        { "DecoBanner", new[] {
            "................",
            ".......x........",
            "......x.x.......",
            ".....x...x......",
            "..xxxxxxxxxxx...",
            "..xxYRRRRRYxx...",
            "....RRRRRRk.....",
            "....kRRRRRk.....",
            "....RRRRRRk.....",
            "....kRRRRRk.....",
            "....RRRRRRk.....",
            "....kRRRRRk.....",
            "....kRYxRRk.....",
            "....kYx.xRk.....",
            "....kx...xk.....",
            "....x.....x.....",
        } },
        { "DecoArmor", new[] {
            "................",
            ".......xx.......",
            "......xsSx......",
            "......xxxx......",
            "......xxxx......",
            ".....sxxxxS.....",
            "....xSxsSxSx....",
            "....xexSSxex....",
            "....xSsxxSSx....",
            ".....xxxxxx.....",
            ".....xSxxSx.....",
            ".....xxxxxx.....",
            "......Sxxk......",
            "......SxxS......",
            ".....xxxxxx.....",
            "....xxxxxxxx....",
        } },
        { "DecoBarrels", new[] {
            "................",
            ".....xxxxxx.....",
            "....xkSnnNxx....",
            "...xkkxknnxx....",
            "...xkkkknNxx....",
            "...xNkkkNNxx....",
            "...xNkkkkkkx....",
            "....xkxxkkxx....",
            "....xxxxxxxx....",
            "...xkkkxnnxxx...",
            "...xkkkSnnkkx...",
            "...xkkkknnkkx...",
            "...xkkxkNNkkx...",
            "...xkkkkkkkkx...",
            "...xxkkkkkkxx...",
            ".....xxkkxx.....",
        } },
        { "DecoChandelier", new[] {
            "................",
            "......xxxx......",
            ".......xx.......",
            ".......xx.......",
            ".......xx.......",
            ".......xx.......",
            ".......xx.......",
            ".......xx.......",
            "...o.kxNxxk.o...",
            "...x.oxxxxoxx...",
            "..xf.xxxxxx.fx..",
            "..xfxwxxxxfxfx..",
            "..xxxNxyNxYxxx..",
            "...xxxxnYxxxx...",
            "....xxxxxxxx....",
            ".......xx.......",
        } },
        { "DecoWeb", new[] {
            "................",
            ".xxxxxxxxxxxxxx.",
            "..xxxxxxxxwxxsx.",
            "...xx.xxxxxxxxx.",
            "...xx.xxxx.xxxx.",
            "...xwxxx.xwx.xx.",
            "....xxx.xxxxxxx.",
            "......xxxxx.xxx.",
            ".......wx.xxx.x.",
            ".......xxxxxxxx.",
            ".......x.xxx.xx.",
            ".......x...xxxx.",
            ".......x.....xx.",
            ".............kx.",
            "..............x.",
            "..............x.",
        } },
    };
    /// <summary>天气与技能：FxRain 雨丝（原图，第二版太细被弃用） / FxDrop 水滴 / FxSplash 水花 / FxAcid 酸雨 / FxLeaf 落叶（大风）/ FxFog 雾团 / FxSnow 雪花（备用：自定义天气）/ FxBomb 炸弹 / FxCannonball 炮弹 / FxTaunt 挑衅气泡 / FxMound 遁地土包 / FxHook 钩爪（摆荡锚点）/ FxStormCloud 雷云。</summary>
    public static readonly Dictionary<string, string[]> Fx = new Dictionary<string, string[]>
    {
        { "FxRain", new[] {
            "........xx......",
            "........xx......",
            "........xx......",
            "........xx......",
            ".......xbx......",
            ".......xb.......",
            ".......xb.......",
            ".......xb.......",
            "......xbbx......",
            "......xwbx......",
            "......xwbx......",
            ".....xwcbx......",
            ".....xwcbx......",
            ".....xbbbx......",
            "......xxk.......",
            "................",
        } },
        { "FxSplash", new[] {
            "................",
            "................",
            ".....xx.........",
            "xx...xx......xx.",
            "xcx......xx.xcx.",
            ".xx.....xcx.xx..",
            "....cx..xcx.....",
            ".xx.xc..xx.xxx..",
            ".xcxxcxxcxxwcx..",
            "..xcwccwccwcx...",
            "..xbcbbcbbbx....",
            ".xbbbBBbbbBBx...",
            ".xccBbBBBbbcx...",
            "...xxxxxxxx.....",
            "................",
            "................",
        } },
        { "FxAcid", new[] {
            "..........x.....",
            "..........x.....",
            ".........x......",
            "........x.x.....",
            "........x.x.....",
            ".......xxx......",
            ".......xxx......",
            ".......ggx......",
            "......xggx......",
            "......gggx......",
            ".....xgwgx......",
            ".....xggggx.....",
            ".....xggggx.....",
            ".....xgggx......",
            "......xxx.......",
            "................",
        } },
        { "FxLeaf", new[] {
            "................",
            "................",
            ".........xx.....",
            ".....xxxoYx.....",
            "...xxooooY......",
            "..xoRooYoRx...N.",
            "..xoooRoox..NNN.",
            ".xoooNoooY......",
            ".xooNNrRRxx.....",
            "..orRoooYY...NN.",
            "..xNoooorx...oN.",
            ".xNxYrrxx...NN..",
            "xxx.xxx.........",
            "xx..............",
            "................",
            "................",
        } },
        { "FxFog", new[] {
            "................",
            "......kkkk......",
            ".....essssk.....",
            ".....ksssssk....",
            "ek..ksssssskkk..",
            "kk.essssssk.esk.",
            "..kesssssssk.k..",
            ".ksssssssssse...",
            ".kssssssssssk...",
            "kssssssssssk....",
            "ksssssssSkkkk...",
            "ksssssssSk.ese..",
            ".ekeksssskk.k...",
            ".....ekek.......",
            "................",
            "................",
        } },
        { "FxSnow", new[] {
            "......xx........",
            ".....xxwxx......",
            "......www.......",
            "..xxx.xwx.xxx...",
            ".xwwxxxwx.xwwx..",
            ".xxwwwxwxwwwx...",
            "..xxxwxwxwxxx...",
            "....xxwwwxx.....",
            "..xxwwxwxwxxx...",
            ".xxwwwxwxwwwx...",
            ".xwwx.xwx.xwwx..",
            "..xxx.xwx.xxx...",
            "......www.......",
            ".....xxwxx......",
            "......xxx.......",
            "................",
        } },
        { "FxBomb", new[] {
            "..........k.....",
            "........kkrk....",
            ".........kyk....",
            ".......xkYxk....",
            "......xyxx......",
            ".....xxxx.......",
            "....xxeekx......",
            "...xeexxxkx.....",
            "..xeeweekkk.....",
            "..xeeSeekkkx....",
            "..xkeeeekkkx....",
            "..xkkeekkkkx....",
            "..xkkkkkkkk.....",
            "...xkkkkkkx.....",
            ".....xkkx.......",
            "................",
        } },
        { "FxCannonball", new[] {
            "................",
            ".....xxxxx......",
            "...xoxxxxxox....",
            "..xokeekxxxxR...",
            ".xoeeeeexxxxxx..",
            ".xkewweeexxxxx..",
            "xkeeweeeeexxxxx.",
            "xxeweeeeeexxxxx.",
            "xxeeeeeeeexxxxx.",
            "xxxeeeeeexxxxxx.",
            "xxxxeeexxxxxxxx.",
            ".xxxxxxxxxxxxx..",
            ".xoxxxxxxxxxxx..",
            "..rxxxxxxxxxr...",
            "...xxxxxxxox....",
            ".....xxxxx......",
        } },
        { "FxTaunt", new[] {
            "....xxxxxxxx....",
            "..xwwwwwwwwwwx..",
            "..wwwwwrrRwwww..",
            ".xwwwwwrrRwwwwx.",
            ".xwwwwwrrRwwwwx.",
            ".xwwwwwrrwwwwwx.",
            ".xwwwwwRrwwwwwx.",
            ".xwwwwwRRwwwwwx.",
            ".xwwwwwwwwwwwwx.",
            "..wwwwwrrwwwww..",
            "..xwwwwwwwwwwx..",
            "...xxwwwwwwxx...",
            "......xwwx......",
            "......xwx.......",
            "......xx........",
            "................",
        } },
        { "FxMound", new[] {
            "................",
            "................",
            "................",
            "......xxx.......",
            ".....xnnnx......",
            "....nnnnnNx.....",
            "...xnnnnnnNxxx..",
            "..xnnNNnnNNNxx..",
            ".xNnnnnnnNNnNx..",
            "xnxNNnnnNnNNxnx.",
            "xx..xNnnNxxxxxx.",
            "..xnx.xxx..x....",
            "...x.......x....",
            "................",
            "................",
            "................",
        } },
        { "FxHook", new[] {
            "...........xx...",
            ".....xkkkxxxx...",
            "....xkxxssssx...",
            "....x...xssSx...",
            "........kskksx..",
            ".......xsk..kx..",
            "......xsk...xx..",
            "......kxx...kx..",
            "...xxkk....xx...",
            "..xwkx..........",
            "..xk..xxxkkx....",
            "...kkxkkxxxk....",
            "....xx...xwk....",
            ".......kkxx.....",
            ".......xx.......",
            "................",
        } },
        { "FxStormCloud", new[] {
            "................",
            ".....xxxx.......",
            "....xPPPdx......",
            "....xPPPddx.....",
            "..xPkPdddePd....",
            ".xPPPdPPdPPdkx..",
            "xdddddPPdPddddx.",
            "xPddeeddedddddx.",
            ".xedekeykkdekx..",
            "...xxxyyxxxx....",
            ".....xyyxx......",
            ".....xxyy.......",
            "......xyx.......",
            "......xx........",
            "......x.........",
            "................",
        } },
        { "FxDrop", new[] {
            "................",
            "........x.......",
            ".......xcx......",
            "......xcbx......",
            ".....xcccbx.....",
            ".....xcccbx.....",
            ".....xcccbbx....",
            "....xcccccbx....",
            "...xxcccccbxx...",
            "...xccwccccbx...",
            "...xcwwwcccbx...",
            "...xccwccccbx...",
            "...xccccccbbx...",
            "...xxbcccbbxx...",
            "....xxbbbbxx....",
            "......xxxx......",
        } },
    };
    /// <summary>工坊元素（第 1 步默认房间不用，但自己搭关卡能放）：摆锤 / 锯片 / 队列机关 / 弹跳台 / 移动平台 / 传送带 / 可破坏方块 / 假墙 / 暗道入口 / 弹跳怪 / 飞行怪。</summary>
    public static readonly Dictionary<string, string[]> Elements = new Dictionary<string, string[]>
    {
        { "PendulumTrap", new[] {
            "................",
            "..xx............",
            "..xxx...........",
            "...xx...........",
            "...xxx..........",
            "....xx..........",
            "....xxx.........",
            ".....xx.........",
            ".....xxx........",
            "......xxxxwx....",
            ".....xxSeeex....",
            ".....xseexekx...",
            ".....xeexsxk....",
            ".....xeeeekkx...",
            ".....xxwkkkxx...",
            ".......xxxx.....",
        } },
        { "SawBlade", new[] {
            "................",
            ".....x.x........",
            "....xxxsxxkx....",
            "..x.kwwwwwsx....",
            ".xwxwssssssxkx..",
            "..kwsssssssssx..",
            "x.xssssxssssx...",
            "xkwsssssssssskx.",
            ".xsssxsxxxsssxx.",
            "xxsssxsxsxsssx..",
            "xsssssxxxsssssx.",
            ".xxsssssssssskx.",
            ".xxsssssssssx...",
            "..xxxsssssksx...",
            "....xskxsk.xx...",
            "....xx..xx......",
        } },
        { "StateQueueTrap", new[] {
            "................",
            "................",
            "................",
            ".xxxxxxxxxxxxx..",
            "xssssssssssssSx.",
            "xeSSSSSSSSSSSex.",
            "xSSSxSSSSSSSSSx.",
            "xSSxSxSSSxSgSSx.",
            "xSxrrrxSxgggxSx.",
            "xSxrrrSSxgggxSx.",
            "xSxrrRxSxGgGxSx.",
            "xSSxxxSSSexxSSx.",
            "xSSSSSSSSSSSSSx.",
            "xSSSSSSSSSSSSSx.",
            "xkkkkkkkkkkkkkx.",
            "xkkkkkkkkkkkkkx.",
        } },
        { "BouncyPlatform", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..xxxxxxxxxxx...",
            "xrrwwwrrrwwwrrx.",
            "xrwwwrrrrrwwwrx.",
            "xrrrrwwwwwrrrrx.",
            "xRRrrrrrrrrRRRx.",
            ".xxRRRRRRRRRxx..",
            ".xwxxxxxxxxxsx..",
            "xxxxxxeeexxxxxx.",
            "xxxxxxxxxxxxxxx.",
        } },
        { "MovingPlatform", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "...xxxxxxxxxx...",
            ".xxnnnnnnnnnnxx.",
            "xxxnnnnnnnnnnxsx",
            ".x.nNNnnnnnnn.x.",
            "...xxxxxxxxxx...",
            "...xxxkxxkxxx...",
        } },
        { "ConveyorBelt", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".xxxxxxxxxxxxxx.",
            "xSeeySSeyeSeyeex",
            "xSeyeSeeeSeeyeex",
            "xxxxxxxxxxxxxxxx",
            "xsskksxkksxkxsxx",
            "xxskksxkxxxkxssx",
            "xxxxxxxxxxxxxxxx",
        } },
        { "BreakableBlock", new[] {
            "................",
            ".xxxxxxxxxxxxx..",
            "xoYYrooYYrYYYYx.",
            "xrrrxrrrrrRrRRx.",
            "xRrrRrrrrrrRrRx.",
            "xrrrrxoooxrrrrx.",
            "xrrRxoooooxrrrx.",
            "xxxrxoxxxoxrrNx.",
            "xrRrxxxxooxrxrx.",
            "xrRYrRxooxxrrxx.",
            "xRxrrrxoxxrrRxx.",
            "xrrRrrxxxRrNrRx.",
            "xrrrRrxoxrRrrrx.",
            "xrrrRrxxxRRxrrx.",
            "xrrrxrRRrRNRNRx.",
            ".xxxxxxxxxxxxxx.",
        } },
        { "FakeWall", new[] {
            "................",
            ".kkkkkkkkkkkkkk.",
            ".kSSsSSSSSsSSSk.",
            ".kSksSSSSSsSSSk.",
            ".kSxSSSSSxSSSSk.",
            ".kssssxsssssksk.",
            ".kSSSSeSSSSSesk.",
            ".kSSSSeSsSSSSsk.",
            ".kSxkSxSSxkSkkk.",
            ".kSSsSSSSSssSSk.",
            ".kSSsSSSSSsSSSk.",
            ".kSkSSSSSkSSSSk.",
            ".ksxksxkskkkxsk.",
            ".kSSSSeSSSSSesk.",
            ".kkkkkkkkkkkkkk.",
            "................",
        } },
        { "HiddenPassage", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..xxxxxxxxxxx...",
            "..xxkkkkkkkxe...",
            "..Sxeexsxeexex..",
            ".xxeexxxxxeexx..",
            ".xxeexsxsxeexe..",
            ".exeeeexxeeexex.",
            "xexxxxxxxxxxxex.",
            "xxxxxxxxxxxxxxx.",
        } },
        { "BouncingEnemy", new[] {
            "................",
            "................",
            "................",
            "....xxxxxxx.....",
            "...xgggggggx....",
            "..xwwgggggggx...",
            ".xgwgggggggggx..",
            "xggggggggggggGx.",
            "xgggxgggggxgggx.",
            "xggwwxkgxxwwggx.",
            "xgggwxkggxwgggx.",
            "xgggggggggkgggx.",
            "xggggggggggggGx.",
            "xGGGgggggggGGGx.",
            "xGGGGGGGGGGGGGx.",
            ".xxxxxxxxxxxxx..",
        } },
        { "FlyingEnemy", new[] {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".xx.........xx..",
            "xRrrx.x.x..YRRx.",
            "xrrrrxYrrxrrrrx.",
            ".xrrrxwrwxrrrx..",
            "..xxrrrrRrrxx...",
            "....xxYrrxx.....",
            "......RrN.......",
            "......xxx.......",
        } },
    };
    /// <summary>远处视差长图：Far = 远山（蓝灰、雪顶）、Near = 近处的绿丘 + 小树 + 远处小村。每个字符 = 自己调色板的下标，. = 透明。宽 160 像素 = 10 格，横向循环平铺。</summary>
    public static readonly float[][] FarPalette =
    {
        new[] { 0.498f, 0.612f, 0.745f },
        new[] { 0.349f, 0.467f, 0.639f },
        new[] { 0.271f, 0.38f, 0.541f },
        new[] { 0.976f, 0.016f, 0.953f },
        new[] { 0.635f, 0.18f, 0.749f },
    };
    public static readonly string[] FarStrip =
    {
        "...............00......................................................................00.......................................................................",
        "..............000..............................00.....................................0000......................................................................",
        "..............0000............................0000....................................10000.............................................00......................",
        "............0000000..........................000000..................................0000000............0..............................0000.....................",
        "...........00000000..........0.............0000000000...............................0000000000..........00............................000000....................",
        "..........0000000000........00............000000000000..................0...........0000000000.........0000.............0............10000000........1..........",
        "....0....000000000100....1110000.........00000000000000................000.........000000000000.......000000...........000..........0000000000......111.........",
        "...000..00000000022200..111110000.......00200000000000000.1...........00100.......000011000000000...000000000.........00000........00000000001....111111........",
        "...00001001000002222100111111100000....012210000000000001111........000000000....1110011100000000000000000000011.....00000010.....0001111000000..11111111...1...",
        "..0000000111000222222211111111100000..012222220000001111111111.....00022000000001111111111000000000000000001111111.000000012000..10011111100000111111111111111..",
        ".00000011111102222222221111111111100000222222220000111111111111..0000222200000111111111111100000002200000011111111100000002220000001111111100011111111111111111.",
        "0000001111111222222222221111111111110222222222222011111111111111100222222200011111111111111100000222200011111111111110000222220001111111111111111222211111111111",
        "0000111111122222222222222222111111112222222222222221111111111111112222222220111111111111111110022222220111111111111111102222222111111111111111112222221111111111",
        "1111111112222222222222222222221111122222222222222222211111111111122222222222221111111111222112222222222211111111111122222222222222111111111112222222222211111111",
        "1111111222222222222222222222222111222222222222222222221111111222222222222222222111111112222212222222222222111111111222222222222222211221111112222222222221111111",
        "1112212222222222222222222222222222222222222222222222222221122222222222222222222222221122222222222222222222222112222222222222222222222222211222222222222222222111",
        "1122222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222221",
        "2222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222",
        "2222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222",
    };
    public static readonly float[][] NearPalette =
    {
        new[] { 0.627f, 0.847f, 0.373f },
        new[] { 0.471f, 0.761f, 0.282f },
        new[] { 0.271f, 0.545f, 0.196f },
        new[] { 0.192f, 0.478f, 0.157f },
        new[] { 0.886f, 0.078f, 0.867f },
    };
    public static readonly string[] NearStrip =
    {
        ".............................................00................................................................0................................................",
        ".........................................0000000000...........................................................00................................................",
        ".......................................000000000000000........................................................00................................................",
        "........000.........................000000000000000000000........................000.................0000..00.00................................................",
        "....0000000000............101110000000000000000000000000000.................0000000000000.........00.0000..00.00.000................000000000000................",
        "00000000000000000...011111111111111110000000000000000000000000............0000000000000000000....0111111111111110011............000000000000000000000...........",
        "00000000000000011111111111111111111111100000000000000000000000000......000000000110000000000111111111111111111111111.01......00000000000000000000000000.......00",
        "000000000001111111111111111111111111111111000000000000000011111110000000000001111111100001111111111111111111111111111111.000000000000000111110000000000000.00000",
        "0000000001111111111111111111111111111111111100000000000111111111111100000001111111111111111111111111111111111111111111111110000000001111111111110000000000000000",
        "0000001111111133111111111111111111111111111111100000111111111111111111111111111111111111133332111111111111111111111111111111110011111113311111111110000000000000",
        "0001111111111333311111111111111111111111111111111111111111111111111111111111311111111133333333333111111111111111111111111111111111111133331111111111110011111110",
        "1111111111111333311111111111111111111111133333111111111111111111111111111113331111133333333333333331111111111111111111111111111133311133331111111111111111111111",
        "1111111111111333311113111111111111133333333333333311111111111111111111311113331113333333333333333333311111111111111111111111111133311133331111111111111111111111",
        "3311123111111133111133211111111113333333333333333333311111111111111113331111311333333333333333333333333113113311111113333333331113111113311111131111111111111111",
        "3333333333111121111133111111133333333333333333333333333311331331111111331111333333333333333333333333333333333331113333333333333333211111311133333133211111112333",
        "3333333333333333332223223333333333333333333333333333333333333333111111312333333333333333333333333333333333333333333333333333333333333333333333333333332233333333",
        "3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333",
        "3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333",
        "3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333",
        "3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333",
        "3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333",
        "3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333",
        "3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333",
        "3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333",
    };

    // ═════════ 查表：哪个格子 / 元素 / 天气用哪张图（纯逻辑，sim 验证）═════════

    /// <summary>满格地面图（能无缝平铺，100% 填满）——OverworldArt.Audit 的"剪影太满"对它们不适用。</summary>
    public static readonly HashSet<string> FullTiles = new HashSet<string> { "TGrass", "TPath", "TRoof", "TWall", "TWater", "TMud" };

    /// <summary>小镇地面层：每格先铺什么（路 / 水 / 泥 / 草）。门、家、出生点底下铺路，其余铺草。</summary>
    public static string GroundKeyOf(char c)
    {
        if (c == '=' || c == 'M' || c == 'T' || OverworldCatalog.IsDoor(c)) return "TPath";
        if (c == 'w') return "TWater";
        if (c == 'g') return "TMud";
        return "TGrass";
    }

    /// <summary>小镇物件层：这一格上面立着什么图（null = 只有地面 / 用原来的图标）。房子最下面一排 = 墙面（带窗和门），上面 = 屋顶。</summary>
    public static string TownKeyOf(char c, bool wallFace)
    {
        switch (c)
        {
            case 'W': return wallFace ? "TWall" : "TRoof";
            case 't': return "TTree";
            case 'f': return "TFence";
            case '"': return "TTallGrass";
            case 'A': return "TMountain";
            case '^': return "THill";
            case 'h': return "TCave";
            case 'M': return "THome";
            case 'T': return "TSpawn";
        }
        return OverworldCatalog.IsDoor(c) ? "TDoor" : null;
    }

    /// <summary>天气粒子：用哪张图、一屏多少个、往哪飘（格/秒）、转不转。晴天 / 赶集 = 没有。</summary>
    public struct Weather { public string key; public int perScreen; public float vx, vy; public bool spin; public float alpha; }
    public static Weather WeatherOf(OverworldEvents.Kind k)
    {
        switch (k)
        {
            case OverworldEvents.Kind.Rain: return new Weather { key = "FxRain", perScreen = 70, vx = -2f, vy = -14f, alpha = 0.7f };
            case OverworldEvents.Kind.Storm: return new Weather { key = "FxRain", perScreen = 110, vx = -5f, vy = -18f, alpha = 0.8f };
            case OverworldEvents.Kind.Acid: return new Weather { key = "FxAcid", perScreen = 60, vx = -1f, vy = -10f, alpha = 0.8f };
            case OverworldEvents.Kind.Wind: return new Weather { key = "FxLeaf", perScreen = 26, vx = 7f, vy = -1.2f, spin = true, alpha = 0.95f };
            case OverworldEvents.Kind.Fog: return new Weather { key = "FxFog", perScreen = 18, vx = 0.6f, vy = 0f, alpha = 0.45f };
        }
        return new Weather { key = null };
    }

    /// <summary>技能 / 道具特效图（炸弹、炮弹、遁地土包、钩爪、挑衅气泡、雷云、水花）。</summary>
    public static readonly string[] SkillFx = { "FxBomb", "FxCannonball", "FxMound", "FxHook", "FxTaunt", "FxStormCloud", "FxSplash", "FxDrop" };

    /// <summary>视差：远景跟镜头走 factor（0 = 钉在天上，1 = 跟地面一起动）。返回远景该往右挪多少（格），保证横向循环不露缝。</summary>
    public static float ParallaxX(float camX, float factor, float stripWidth)
    {
        if (stripWidth <= 0f) return camX;
        float shift = camX * (1f - factor);
        float wrap = shift - stripWidth * (float)System.Math.Floor(shift / stripWidth);
        return wrap;
    }

    /// <summary>长图 → RGBA（左下角开始）。调色板下标字符 '0'..'9' 'a'..，'.' = 透明。</summary>
    public static float[] StripRgba(float[][] pal, string[] rows)
    {
        int h = rows.Length, w = rows[0].Length; var px = new float[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            char c = rows[h - 1 - y][x]; if (c == '.') continue;
            int idx = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'z' ? 10 + c - 'a' : -1;
            if (idx < 0 || idx >= pal.Length) continue;
            int i = (y * w + x) * 4; px[i] = pal[idx][0]; px[i + 1] = pal[idx][1]; px[i + 2] = pal[idx][2]; px[i + 3] = 1f;
        }
        return px;
    }

    /// <summary>任何一张图（房间图标 / 帧 / 图块 / 小镇 / 装饰 / 特效 / 工坊元素）→ 字符画。找不到 = null。</summary>
    public static string[] Rows(string key)
    {
        if (key == null) return null;
        if (Town.TryGetValue(key, out var r)) return r;
        if (Decor.TryGetValue(key, out r)) return r;
        if (Fx.TryGetValue(key, out r)) return r;
        if (Elements.TryGetValue(key, out r)) return r;
        return null;
    }

    /// <summary>所有新图的名字（自检 / 导出模板 / 换图台用）。</summary>
    public static IEnumerable<string> AllKeys()
    {
        foreach (var k in Town.Keys) yield return k;
        foreach (var k in Decor.Keys) yield return k;
        foreach (var k in Fx.Keys) yield return k;
        foreach (var k in Elements.Keys) yield return k;
    }

    // ═════════ 房间装饰（背景层：压暗、没有碰撞体、不挡机关——H6）═════════
    public static readonly string[] WallDecor = { "DecoPainting", "DecoWindow", "DecoBanner", "DecoClock", "DecoShelf", "DecoWeb", "DecoChandelier" };
    public static readonly string[] FloorDecor = { "DecoCandle", "DecoPlant", "DecoArmor", "DecoBarrels", "DecoRug" };
    public static readonly string[] OutdoorWallDecor = { "DecoWeb", "DecoBanner" };
    public static readonly string[] OutdoorFloorDecor = { "DecoPlant", "DecoBarrels" };

    public struct Dress { public int x, y; public string key; }

    static bool SolidCh(char c) => c == '#' || c == '=' || c == 'W' || c == 'v' || c == 'X' || c == 'F';
    static bool AirCh(char c) => c == '.' || c == ' ';
    static uint H(int x, int y, int seed) { unchecked { uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return h; } }

    /// <summary>
    /// 给房间挑装饰位置（纯函数：同一个房间 + 同一个种子 = 永远同样的摆法）。rows = 房间字符画（第 0 行 = 最上面），返回的 y 是世界坐标（0 = 最下面一行）。
    /// 规则：① 只放在空气格；② 3×3 范围内除了墙 / 地面以外什么都没有（不贴着机关、出生点、宝物、出口）；③ 地上的（烛台 / 盆栽 / 盔甲 / 木桶 / 地毯）脚下必须是地面，
    /// 墙上的（画 / 窗 / 挂旗 / 座钟 / 书架 / 蛛网 / 吊灯）下面至少空 2 格（不挡你看路）；④ 两件装饰至少隔 3 格；⑤ density = 每多少格空气放 1 件的倒数（0 = 不放）。
    /// outdoor = 户外主题：只放蛛网 / 挂旗 / 盆栽 / 木桶（不会在公园里挂吊灯）。
    /// </summary>
    public static List<Dress> Dressing(string[] rows, int seed, float density, bool outdoor)
    {
        var list = new List<Dress>();
        if (rows == null || rows.Length < 3 || density <= 0f) return list;
        int h = rows.Length, w = 0; foreach (var r in rows) w = System.Math.Max(w, r.Length);
        char At(int x, int yTop) => yTop < 0 || yTop >= h || x < 0 || x >= rows[yTop].Length ? 'W' : rows[yTop][x];
        var wall = outdoor ? OutdoorWallDecor : WallDecor; var floor = outdoor ? OutdoorFloorDecor : FloorDecor;
        int air = 0; for (int yt = 0; yt < h; yt++) for (int x = 0; x < w; x++) if (AirCh(At(x, yt))) air++;
        int want = (int)(air * System.Math.Min(1f, density));
        for (int yt = 1; yt < h - 1 && list.Count < want; yt++)
            for (int x = 1; x < w - 1 && list.Count < want; x++)
            {
                if (!AirCh(At(x, yt))) continue;
                bool clean = true;
                for (int dy = -1; dy <= 1 && clean; dy++) for (int dx = -1; dx <= 1; dx++) { char c = At(x + dx, yt + dy); if (!AirCh(c) && !SolidCh(c)) { clean = false; break; } }
                if (!clean) continue;
                bool onFloor = SolidCh(At(x, yt + 1));
                bool high = AirCh(At(x, yt + 1)) && AirCh(At(x, yt + 2));
                if (!onFloor && !high) continue;
                uint hv = H(x, yt, seed);
                if ((hv % 1000) / 1000f > density * 3f) continue; // 撒得开一点（不是从左上角挤满）
                bool near = false; foreach (var d in list) if (System.Math.Abs(d.x - x) < 3 && System.Math.Abs(d.y - (h - 1 - yt)) < 3) { near = true; break; }
                if (near) continue;
                var pool = onFloor ? floor : wall;
                string key = pool[(int)((hv >> 10) % (uint)pool.Length)];
                if (key == "DecoChandelier" && !SolidCh(At(x, yt - 1))) key = "DecoPainting"; // 吊灯要挂在天花板下
                if (key == "DecoWeb" && !(SolidCh(At(x, yt - 1)) && (SolidCh(At(x - 1, yt)) || SolidCh(At(x + 1, yt))))) key = outdoor ? "DecoBanner" : "DecoWindow"; // 蛛网在墙角
                list.Add(new Dress { x = x, y = h - 1 - yt, key = key });
            }
        return list;
    }


    // ═════════ 房间里怎么摆（纯逻辑）═════════
    /// <summary>长条机关：弹跳台 B（2×0.3）、移动平台 &gt;（3×0.4）、传送带 &lt;（1×0.3）——取图的底部一条横向平铺，不压成一团。</summary>
    public static readonly HashSet<string> WideElements = new HashSet<string> { "BouncyPlatform", "MovingPlatform", "ConveyorBelt" };

    /// <summary>换图后多大、往上挪多少（格）。工坊元素：摆锤 / 锯片 / 飞行怪 / 弹跳怪 原色块很小（0.3–0.8 格）→ 至少画 0.8 格，认得出；其余照 Step1Art.PlaceOf。</summary>
    public static float[] PlaceOf(string key, float sx, float sy)
    {
        if (key != null && Elements.ContainsKey(key) && !WideElements.Contains(key))
        {
            float s = Step1Art.FitScale(sx, sy, 0.8f); return new[] { s, Step1Art.FitLift(sy, s) };
        }
        return Step1Art.PlaceOf(key, sx, sy);
    }

    /// <summary>alpha（左下角开始，w×h）里有不透明像素的最低行 y0 和最高行 y1（全空 = y1 &lt; y0）。长条机关取这一段平铺。</summary>
    public static void OpaqueRows(float[] alpha, int w, int h, out int y0, out int y1)
    {
        y0 = h; y1 = -1;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (alpha[y * w + x] > 0.5f) { if (y < y0) y0 = y; if (y > y1) y1 = y; break; }
        // 只要底部那一段：从最低的不透明行往上，最多 8 行（16px 图 = 半格高的条）
        if (y1 >= y0 && y1 - y0 > 7) y1 = y0 + 7;
    }

    /// <summary>房间算不算户外：mode 1 = 老宅，2 = 户外，0 = 自动（公园类主题 + 最上面一排至少 30% 是空的 = 看得见天）。</summary>
    public static bool Outdoor(int mode, string theme, string[] rows)
    {
        if (mode == 1) return false; if (mode == 2) return true;
        bool park = theme == "AmusementPark" || theme == "CityPark" || theme == "MountainPark";
        if (!park || rows == null || rows.Length == 0) return false;
        string top = rows[0]; if (top.Length == 0) return false;
        int open = 0; foreach (char c in top) if (c == '.' || c == ' ') open++;
        return open >= top.Length * 0.3f;
    }

    // ═════════ 覆盖自检：每个东西都有图吗（sim + Unity 菜单都跑）═════════
    /// <summary>不需要图的房间元素：空气 / 空格（什么都不画）、出生点（只是位置）。</summary>
    public static readonly HashSet<string> NoArtElements = new HashSet<string> { "Air", "Space", "MarioSpawn", "TricksterSpawn" };
    static readonly HashSet<string> TerrainElements = new HashSet<string> { "Ground", "Platform", "Wall", "OneWayPlatform" };

    /// <summary>房间元素名（Registry elementName）→ 用哪张图。null = 没图（白盒）。</summary>
    public static string ElementArt(string elementName)
    {
        if (elementName == null) return null;
        if (TerrainElements.Contains(elementName)) return elementName == "OneWayPlatform" ? "Platform" : elementName == "Wall" ? "Wall" : "GroundTop";
        if (Step1Art.Icons.ContainsKey(elementName)) return elementName;
        if (Elements.ContainsKey(elementName)) return elementName;
        return null;
    }

    /// <summary>覆盖报告：缺什么（空 = 全覆盖）。elements = 房间里所有元素名；小镇格子从 OverworldCatalog 读；天气从 OverworldEvents.Kind 读。</summary>
    public static List<string> Coverage(IEnumerable<string> elements)
    {
        var miss = new List<string>();
        foreach (var e in elements)
        {
            if (NoArtElements.Contains(e)) continue;
            if (ArtKitRules.SlotIndex(e) >= 0) continue; // 素材槽：长相来自素材包（没配 = 默认图），不算缺
            string k = ElementArt(e); if (k == null) { miss.Add("房间元素 " + e); continue; }
            if (Step1Art.Icons.ContainsKey(k) || Step1Art.Tiles.ContainsKey(k) || Elements.ContainsKey(k)) continue;
            miss.Add("房间元素 " + e + " → " + k + "（图不存在）");
        }
        foreach (var t in OverworldCatalog.All)
        {
            string k = TownKeyOf(t.c, false) ?? OverworldArt.IconOf(t.c) ?? (t.c == '.' || t.c == '=' || t.c == 'w' || t.c == 'g' ? GroundKeyOf(t.c) : null);
            if (k == null) { miss.Add("小镇格子 " + t.c + " " + t.zh); continue; }
            if (!Town.ContainsKey(k) && !OverworldArt.Icons.ContainsKey(k)) miss.Add("小镇格子 " + t.c + " → " + k + "（图不存在）");
        }
        foreach (OverworldEvents.Kind wk in System.Enum.GetValues(typeof(OverworldEvents.Kind)))
        {
            if (wk == OverworldEvents.Kind.Clear || wk == OverworldEvents.Kind.Market) continue;
            var wf = WeatherOf(wk); if (wf.key == null || !Fx.ContainsKey(wf.key)) miss.Add("天气 " + wk);
        }
        foreach (var s in SkillFx) if (!Fx.ContainsKey(s)) miss.Add("特效 " + s);
        foreach (var d in WallDecor) if (!Decor.ContainsKey(d)) miss.Add("装饰 " + d);
        foreach (var d in FloorDecor) if (!Decor.ContainsKey(d)) miss.Add("装饰 " + d);
        if (FarStrip.Length == 0 || NearStrip.Length == 0) miss.Add("远山 / 近丘");
        return miss;
    }

    /// <summary>S245：小镇地面每格几个像素（16 = 和图块一样清楚）。贴图边长不超过 4096：超大地图自动降到 8 / 4 / 2。</summary>
    public static int GroundPixelsPerCell(int w, int h)
    {
        int n = Size; while (n > 1 && (w * n > 4096 || h * n > 4096)) n /= 2;
        return n;
    }

    // ═════════ S246：地面过渡边 + 影子（纯逻辑：游戏 / 网页 / sim 同一套规则）═════════
    // 参考：俯视 RPG 地块之间用"边缘抖动 / 过渡块"避免硬直线（Stardew Valley、RPG Maker 的 autotile 思路
    // https://www.rpgmakerweb.com/blog/autotiles ），物体脚下一块半透明椭圆影子让它"站在"地面上。
    /// <summary>地面层级：谁的边会"盖"到邻居上（草 > 泥 > 路 > 水）。层级高的一方在交界处往对方格子里长出 1–3 像素的毛边。</summary>
    public static int GroundRank(string key) => key == "TGrass" ? 3 : key == "TMud" ? 2 : key == "TPath" ? 1 : 0;

    /// <summary>
    /// 一格里某个像素该画哪张地面图（左下角为原点，n = 每格像素数）。邻居 = 北 / 东 / 南 / 西 的地面名（null = 地图外，当成自己）。
    /// 规则：邻居层级更高时，离那条边 d 像素以内按"格子坐标 + 像素坐标"的固定哈希决定要不要画成邻居（越靠边越容易），形成不规则毛边；
    /// 水和别的地面交界额外画一条浅色水边（返回 "Foam"）。同一张地图每次一样（没有随机数）。
    /// </summary>
    public static string GroundPixelKey(string self, string n, string e, string s, string w, int cx, int cy, int px, int py, int size)
    {
        // 圆角：两条相邻边都是"更高层级"的同一种地面（或水的两条岸）→ 外角切成圆弧（路口 / 池塘不再是直角）
        int rad = System.Math.Max(2, size * 6 / 16);
        string Corner(string a, string b, int dx, int dy)
        {
            if (a == null || b == null || a != b || a == self) return null;
            if (GroundRank(a) <= GroundRank(self) && self != "TWater") return null;
            if (dx >= rad || dy >= rad) return null;
            float fx = rad - dx - 0.5f, fy = rad - dy - 0.5f; float dist = (float)System.Math.Sqrt(fx * fx + fy * fy);
            if (dist > rad) return a;
            if (self == "TWater" && dist > rad - 2.2f) return "Foam";
            return self;
        }
        string cr = Corner(n, e, size - 1 - px, size - 1 - py) ?? Corner(n, w, px, size - 1 - py) ?? Corner(s, e, size - 1 - px, py) ?? Corner(s, w, px, py);
        if (cr != null) return cr;
        if (self == "TWater") // 水：岸边一道浅色水边（先于毛边——岸线要连贯）
        {
            int edge = int.MaxValue;
            if (n != null && n != "TWater") edge = System.Math.Min(edge, size - 1 - py);
            if (s != null && s != "TWater") edge = System.Math.Min(edge, py);
            if (e != null && e != "TWater") edge = System.Math.Min(edge, size - 1 - px);
            if (w != null && w != "TWater") edge = System.Math.Min(edge, px);
            if (edge <= System.Math.Max(1, size / 8)) return "Foam";
        }
        int depth = System.Math.Max(1, size * 4 / 16);
        string best = self; int bestD = int.MaxValue;
        void Try(string nb, int d)
        {
            if (nb == null || nb == self || d >= depth || d >= bestD) return;
            if (GroundRank(nb) <= GroundRank(self)) return;
            uint h = (uint)(cx * 73856093) ^ (uint)(cy * 19349663) ^ (uint)(px * 83492791) ^ (uint)(py * 2971215073);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            if ((h % (uint)depth) >= (uint)(depth - d)) return; // d=0 一定画，越往里越少
            best = nb; bestD = d;
        }
        Try(n, size - 1 - py); Try(s, py); Try(e, size - 1 - px); Try(w, px);
        return best;
    }

    /// <summary>哪些格子脚下画影子、影子多宽（格）。0 = 不画。人物另算（ShadowCharacter）。</summary>
    public static float ShadowWidth(char c, bool wallFace)
    {
        switch (c)
        {
            case 't': return 1.1f;
            case 'A': return 1.2f;
            case 'W': return wallFace ? 1.05f : 0f; // 房子只在墙脚那一排
            case 'f': return 0.9f;
            case 'M': case 'T': return 0.9f;
            case 'c': return 0.8f;
            case 'O': return 1.1f;
            case 'U': case 'B': case 'K': return 1.1f;
            case 'i': return 0.5f;
        }
        return OverworldCatalog.IsDoor(c) ? 0f : 0f;
    }
    public const float ShadowCharacter = 0.7f, ShadowAlpha = 0.38f;

    /// <summary>影子贴图（w×h 像素的椭圆，中间深、边缘淡）RGBA，左下角开始。</summary>
    public static float[] ShadowRgba(int w, int h)
    {
        var px = new float[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f, r = u * u + v * v;
            if (r > 1f) continue;
            px[(y * w + x) * 4 + 3] = ShadowAlpha * (r < 0.5f ? 1f : 0.65f);
        }
        return px;
    }
}

