/// <summary>
/// S181：宪法第 3 层"每局填写"的游戏内问卷（纯逻辑，无 Unity 依赖，可直接测试）。
/// 顺序：算准了？→ 差点被发现？→（本局被抓过才问）服气吗/原因 → 还想再来 1–5 → 一句话备注（可跳过）。
/// 原因标签照抄宪法：没预兆 / 看不懂他 / 手滑 / 我露馅。
/// </summary>
public sealed class Step1RoundSurvey
{
    public enum Step { Calculated, NearMiss, CaughtVerdict, WantAgain, Note, Done }

    /// <summary>被抓判定选项：1 = 服气，2–5 = 不服气 + 宪法原因标签。</summary>
    public static readonly string[] CaughtVerdicts = { "fair", "unfair:no_warning", "unfair:couldnt_read_him", "unfair:slipped", "unfair:gave_myself_away" };

    private readonly bool wasCaught;

    public Step Current { get; private set; } = Step.Calculated;
    public bool? Calculated { get; private set; }
    public bool? NearMiss { get; private set; }
    public string CaughtVerdict { get; private set; } = "";
    public int WantAgain { get; private set; }
    public string Note { get; private set; } = "";
    public bool IsDone => Current == Step.Done;

    public Step1RoundSurvey(bool wasCaught) { this.wasCaught = wasCaught; }

    /// <summary>当前问题（中英对照，S182）。</summary>
    public string Prompt
    {
        get
        {
            switch (Current)
            {
                case Step.Calculated: return "这局有没有「我算准了他会走那里」的时刻？\nDid you have an \"I knew he'd go there\" moment?";
                case Step.NearMiss: return "这局有没有「差点被他发现」的时刻？\nDid he ALMOST spot you this round?";
                case Step.CaughtVerdict: return "你被抓了。服气吗？\nYou got caught. Was it fair?";
                case Step.WantAgain: return "还想再来一局吗？\nWant to play another round?";
                case Step.Note: return "一句话（可跳过）：马里奥最蠢或最聪明的一下？\nOptional: dumbest or smartest thing Mario did?";
                default: return "";
            }
        }
    }

    public int StepNumber => (int)Current + 1 - (!wasCaught && Current > Step.CaughtVerdict ? 1 : 0);
    public int StepCount => wasCaught ? 5 : 4;

    private static readonly string[] YesNoOptions = { "是 Yes  [Y]", "否 No  [N]" };
    private static readonly string[] VerdictOptions =
    {
        "服气，是我的错\nFair, my fault  [1]", "没预兆\nNo warning  [2]", "看不懂他\nCouldn't read him  [3]",
        "手滑\nMy hands slipped  [4]", "我自己露馅\nI gave myself away  [5]"
    };
    private static readonly string[] AgainOptions =
    {
        "1  不想\nNo", "2", "3  一般\nMaybe", "4", "5  马上再来！\nYes, now!"
    };
    private static readonly string[] NoOptions = new string[0];

    /// <summary>当前问题的可点按钮（Note 步没有按钮，用输入框）。</summary>
    public string[] Options
    {
        get
        {
            switch (Current)
            {
                case Step.Calculated:
                case Step.NearMiss: return YesNoOptions;
                case Step.CaughtVerdict: return VerdictOptions;
                case Step.WantAgain: return AgainOptions;
                default: return NoOptions;
            }
        }
    }

    /// <summary>点第 index 个按钮（0 起）。</summary>
    public bool Choose(int index)
    {
        if (Current == Step.Calculated || Current == Step.NearMiss)
            return index >= 0 && index <= 1 && AnswerYesNo(index == 0);
        return AnswerNumber(index + 1);
    }

    public bool AnswerYesNo(bool yes)
    {
        if (Current == Step.Calculated) { Calculated = yes; Current = Step.NearMiss; return true; }
        if (Current == Step.NearMiss) { NearMiss = yes; Current = wasCaught ? Step.CaughtVerdict : Step.WantAgain; return true; }
        return false;
    }

    public bool AnswerNumber(int n)
    {
        if (n < 1 || n > 5) return false;
        if (Current == Step.CaughtVerdict) { CaughtVerdict = CaughtVerdicts[n - 1]; Current = Step.WantAgain; return true; }
        if (Current == Step.WantAgain) { WantAgain = n; Current = Step.Note; return true; }
        return false;
    }

    public bool SubmitNote(string note)
    {
        if (Current != Step.Note) return false;
        Note = (note ?? "").Trim();
        Current = Step.Done;
        return true;
    }

    public static string YesNo(bool? v) => v.HasValue ? (v.Value ? "yes" : "no") : "";
}
