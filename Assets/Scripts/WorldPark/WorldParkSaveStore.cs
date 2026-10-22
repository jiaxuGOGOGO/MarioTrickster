using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>只写世界检查点专属目录，不使用旧PlayerPrefs或作者文件。文件hash是版本隔离，不是防作弊签名。</summary>
public static class WorldParkSaveStore
{
    public static string PathFor(WorldParkPack.Pack pack)
    {
        return Path.Combine(Application.persistentDataPath, "WorldPark", WorldParkProgress.Fingerprint(pack) + ".worldsave.json");
    }

    public static bool Exists(WorldParkPack.Pack pack)
    {
        try { return File.Exists(PathFor(pack)); }
        catch { return false; }
    }

    public static bool TryLoad(WorldParkPack.Pack pack, out WorldParkPack.Session restored, out string message)
    {
        restored = null;
        message = "";
        try
        {
            string path = PathFor(pack);
            if (!File.Exists(path)) { message = "此世界版本还没有检查点；请先开始新旅程。作者修改世界内容后，旧版本检查点仍保留但不会串用。"; return false; }
            if (new FileInfo(path).Length > WorldParkProgress.MaxSaveLength * 4L) { message = "检查点文件超过预算，未载入；原存档保留。"; return false; }
            restored = WorldParkProgress.Restore(pack, File.ReadAllText(path, Encoding.UTF8), out message);
            if (restored == null) { message = "检查点未通过校验：" + message + "。没有退回新游戏或覆盖原存档。"; return false; }
            message = "已恢复世界检查点：" + restored.Venue.name + " / " + restored.Current.name;
            return true;
        }
        catch (Exception ex) { message = "读取检查点失败：" + ex.Message + "；原文件未改动。"; return false; }
    }

    public static bool TrySave(WorldParkPack.Session session, out string message)
    {
        message = "";
        string temporary = "";
        try
        {
            string json = WorldParkProgress.Save(session, out message);
            if (json == null) return false;
            string path = PathFor(session.pack);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            temporary = path + ".writing";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
            // 新文件也重新解析，确认本次写盘不是截断或乱码；失败不触碰旧检查点。
            if (WorldParkProgress.Restore(session.pack, File.ReadAllText(temporary, Encoding.UTF8), out string error) == null)
                throw new IOException("写回校验失败：" + error);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".previous");
            else File.Move(temporary, path);
            temporary = "";
            message = "已保存检查点：" + session.Venue.name + " / " + session.Current.name;
            return true;
        }
        catch (Exception ex) { message = "检查点保存失败：" + ex.Message + "；上一个检查点保留，请不要依赖本次保存。"; return false; }
        finally
        {
            if (!string.IsNullOrEmpty(temporary)) try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }
}
