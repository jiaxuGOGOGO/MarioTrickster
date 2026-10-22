using UnityEngine;

/// <summary>
/// S245：小镇存档读写（PlayerPrefs）——自动档 = OverworldGame.SaveKey，手动档 1–3 = SaveKey + ".Slot1..3"（Step1Flow.SlotKey）。
/// 纯逻辑（格式、校验、排序、摘要）全在 Step1Flow（sim 验证）；这里只管写盘 + 盖上"什么时候存的"。坏档 = 读成 null（当空位），不报错。
/// </summary>
public static class TownSaveStore
{
    /// <summary>房间刚打完（OverworldRoomLink 设）→ 回到小镇第一次存档记成"打完房间"进度点。</summary>
    public static bool RoomJustDone;
    private static double counter;

    public static Step1Flow.TownSave Read(int slot)
    {
        string k = Step1Flow.SlotKey(OverworldGame.SaveKey, slot);
        return PlayerPrefs.HasKey(k) ? Step1Flow.FromJson(PlayerPrefs.GetString(k, "")) : null;
    }

    public static void Write(int slot, Step1Flow.TownSave s)
    {
        if (s == null) return;
        s.when = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        counter = System.Math.Max(counter + 1, System.DateTime.Now.Ticks / 1e7); // 秒；同一秒存两次也分得出先后
        s.stamp = counter;
        PlayerPrefs.SetString(Step1Flow.SlotKey(OverworldGame.SaveKey, slot), Step1Flow.ToJson(s)); PlayerPrefs.Save();
    }

    public static void Clear(int slot) { PlayerPrefs.DeleteKey(Step1Flow.SlotKey(OverworldGame.SaveKey, slot)); PlayerPrefs.Save(); }
    public static void ClearAll() { for (int i = 0; i <= Step1Flow.ManualSlots; i++) PlayerPrefs.DeleteKey(Step1Flow.SlotKey(OverworldGame.SaveKey, i)); PlayerPrefs.Save(); }
}
