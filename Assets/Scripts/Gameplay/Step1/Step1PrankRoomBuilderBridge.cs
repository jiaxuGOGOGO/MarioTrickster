using UnityEngine;

/// <summary>
/// S202：运行时拿到"这个场景是用哪张 ASCII 图建的"（构建器写入）。用于自动检查轨迹与工坊画布对上号、陷阱试探读路线。
/// 只存地形文本，不含任何捣蛋者信息。
/// </summary>
public class Step1PrankRoomBuilderBridge : MonoBehaviour
{
    [SerializeField, TextArea(2, 20)] private string room = "";
    public void SetRoom(string[] rows) { room = rows != null ? string.Join("\n", rows) : ""; }
    public static string[] CurrentRoom
    {
        get
        {
            var b = FindObjectOfType<Step1PrankRoomBuilderBridge>();
            return b == null || string.IsNullOrEmpty(b.room) ? null : b.room.Replace("\r", "").Split('\n');
        }
    }
}
