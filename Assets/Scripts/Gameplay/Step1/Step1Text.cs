/// <summary>
/// S182：第 1 步所有玩家可见文字（中英对照）集中在这里，便于统一修改；纯逻辑，可测试。
/// </summary>
public static class Step1Text
{
    public enum Outcome { MarioEscaped, TricksterCaughtOut, TimeUp, MarioKnockedOut, HandsOffTimeout, Other }

    public const string HandsOffTimeoutReason = "HandsOffTimeout";

    /// <summary>把 GameManager 的胜者+原因翻成玩家能懂的结局。</summary>
    public static Outcome Classify(string winner, string reason)
    {
        reason = reason ?? "";
        if (reason == HandsOffTimeoutReason) return Outcome.HandsOffTimeout;
        if (winner == "Mario") return reason.StartsWith("Trickster caught") ? Outcome.TricksterCaughtOut : Outcome.MarioEscaped;
        if (winner == "Trickster")
        {
            if (reason.StartsWith("Time ran out")) return Outcome.TimeUp;
            if (reason.StartsWith("Health depleted")) return Outcome.MarioKnockedOut;
        }
        return Outcome.Other;
    }

    public static bool PlayerWon(Outcome o) => o == Outcome.TimeUp || o == Outcome.MarioKnockedOut;

    public static string Headline(Outcome o)
    {
        switch (o)
        {
            case Outcome.MarioEscaped: return "马里奥带着宝物逃走了\nMario escaped with the loot";
            case Outcome.TricksterCaughtOut: return "你被抓满 3 次\nYou were caught 3 times";
            case Outcome.TimeUp: return "时间到，马里奥没逃出去 —— 你赢了！\nTime's up, Mario didn't escape — YOU WIN!";
            case Outcome.MarioKnockedOut: return "马里奥被机关打倒 —— 你赢了！\nMario was knocked out — YOU WIN!";
            case Outcome.HandsOffTimeout: return "马里奥卡住了（超时）\nMario got stuck (timeout)";
            default: return "本局结束\nRound over";
        }
    }

    public static string MarioStateText(MarioMindState state, bool waiting, bool carryingLoot, float waitSeconds = 0f, bool stunned = false)
    {
        int secs = (int)System.Math.Ceiling(waitSeconds);
        if (waiting) return secs > 0 ? $"{secs} 秒后出发，快去埋伏！ Starts in {secs}s — get ready!" : "准备中 Getting ready";
        if (stunned) return "被坑晕了！ Dizzy!";
        switch (state)
        {
            case MarioMindState.Curious: return "? 起疑了 Suspicious";
            case MarioMindState.Investigating: return "! 过去查看 Checking it out";
            case MarioMindState.Chasing: return "!! 看见你了，在追你！ Chasing you!";
            case MarioMindState.Searching: return "?! 追丢了，在找 Lost you, searching";
            default: return carryingLoot ? "拿到宝了，跑回出口 Running home" : "去右边拿宝 Going for the loot";
        }
    }

    /// <summary>马里奥头顶的短句（两行，中文在上）。</summary>
    public static string HeadIntent(MarioMindState state, string intent)
    {
        if (intent == "READY...") return "准备\nREADY";
        if (intent == "DIZZY") return "晕了\nDIZZY";
        switch (state)
        {
            case MarioMindState.Curious: return "嗯？\nHUH?";
            case MarioMindState.Investigating: return "去看看\nCHECK";
            case MarioMindState.Chasing: return "站住！\nSTOP!";
            case MarioMindState.Searching: return "人呢？\nWHERE?";
            default: return intent == "RUN HOME" ? "回出口\nHOME" : "拿宝\nLOOT";
        }
    }

    public const string ControlsBar = "← → 移动 Move    ↑ 跳 Jump    P 伪装 Disguise    L 触发机关 Trigger    |    H 帮助 Help    Esc 暂停 Pause";

    public const string Help =
        "<b>怎么玩  HOW TO PLAY</b>\n\n" +
        "你 = <color=#6FA8FF><b>蓝色方块</b></color>（捣蛋鬼）   马里奥 = <color=#FF6B6B><b>红色方块</b></color>\n" +
        "You = the <color=#6FA8FF>BLUE</color> block.   Mario = the <color=#FF6B6B>RED</color> block.\n\n" +
        "马里奥会自己去<b>右边拿宝</b>，再<b>跑回左边出口</b>。\n" +
        "Mario grabs the loot on the RIGHT, then runs back to the exit on the LEFT.\n\n" +
        "<b>你的目标：</b>别让他带宝逃走。用机关坑他，别被他抓到（3 条命）。\n" +
        "<b>Your goal:</b> stop him escaping. Prank him with traps. Don't get caught (3 lives).\n\n" +
        "1. 走到机关旁 → 按 <b>P</b> 伪装，站着别动一会儿    Go next to a trap → <b>P</b> to disguise, stand still\n" +
        "2. 他走近时 → 按 <b>L</b> 触发（火 / 封路墙 / 塌桥）    When he's close → <b>L</b> to trigger\n" +
        "   被火烧到他会晕一下，封路墙能拦住他。   Fire makes him dizzy; the wall blocks him.\n" +
        "   <b>4 秒内连着坑他 = 连招</b>，晕得更久！   Chain traps within 4s = COMBO, longer dizzy!\n\n" +
        "躲在<b>箱子后面</b>或<b>高墙另一边</b>，他就看不见你。   Hide behind a crate or a tall wall — he can't see you.\n" +
        "马里奥头顶  Above Mario:   <b>?</b> 起疑   <b>!</b> 来查看   <b>!!</b> 看见你在追   <b>?!</b> 追丢了\n" +
        "白色扇形 = 他的视野，墙会挡住。   White cone = his view (walls block it).\n\n" +
        "<color=#BBBBBB>H 关闭帮助 close help    C 换镜头 camera</color>";

    public const string Paused = "已暂停  Paused\n<size=22>Esc 继续 Resume</size>";
    public const string AfterSurvey = "✓ 已保存 Saved\n\n<b>N</b> = 下一局 Next round        <b>R</b> = 从头开始 Restart";
}
