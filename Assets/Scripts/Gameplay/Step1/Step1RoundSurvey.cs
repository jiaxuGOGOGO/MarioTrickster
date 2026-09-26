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

    public string Prompt
    {
        get
        {
            switch (Current)
            {
                case Step.Calculated: return "Did you have a \"I KNEW he'd go there\" moment?   Y = yes   N = no";
                case Step.NearMiss: return "Did you have a \"he ALMOST spotted me\" moment?   Y = yes   N = no";
                case Step.CaughtVerdict: return "You got caught. Was it fair?\n1 fair (my fault)   2 no warning   3 couldn't read him   4 slipped   5 gave myself away";
                case Step.WantAgain: return "Want to play another round?   1 (no) ... 5 (yes, right now!)";
                case Step.Note: return "Optional: dumbest / smartest thing Mario did (type, Enter = done)";
                default: return "Saved. Press N for next round.";
            }
        }
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
