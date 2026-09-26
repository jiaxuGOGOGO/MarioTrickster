using UnityEngine;

/// <summary>马里奥头顶的意图标记（? / ! / !! / ?! / OUCH! / GOTCHA!）+ 一行意图 + 起疑条。纯表现，无玩法影响。</summary>
public class MarioMindLabel : MonoBehaviour
{
    [SerializeField] private MarioMindDriver driver;
    [SerializeField] private float height = 1.3f;

    private TextMesh mark;
    private TextMesh intent;
    private LineRenderer bar;
    private LineRenderer barBack;

    private void Start()
    {
        if (driver == null) driver = GetComponentInParent<MarioMindDriver>();
        mark = MakeText("MindMark", 90, 0.07f, new Vector3(0f, height + 0.75f, 0f));
        intent = MakeText("MindIntent", 40, 0.045f, new Vector3(0f, height, 0f));
        barBack = MakeBar("MindBarBack", new Color(0f, 0f, 0f, 0.5f), 10);
        bar = MakeBar("MindBar", Color.yellow, 11);
    }

    private TextMesh MakeText(string name, int fontSize, float charSize, Vector3 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        var text = go.AddComponent<TextMesh>();
        text.fontSize = fontSize; text.characterSize = charSize;
        Step1Gui.ApplyFont(text); // S182：中英双语
        text.anchor = TextAnchor.LowerCenter; text.alignment = TextAlignment.Center;
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sortingOrder = 200;
        return text;
    }

    private LineRenderer MakeBar(string name, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true; line.positionCount = 2;
        line.startWidth = line.endWidth = 0.1f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = color;
        line.sortingOrder = 200 + order;
        return line;
    }

    private void LateUpdate()
    {
        if (driver == null || driver.Mind == null || mark == null) return;
        MarioOrder order = driver.LastOrder;
        mark.text = order.mark ?? "";
        mark.color = ColorFor(order.state, order.mark);
        intent.text = Step1Text.HeadIntent(order.state, order.intent ?? "");
        intent.color = new Color(1f, 1f, 1f, 0.85f);

        float n = driver.Mind.Meter.Normalized;
        Vector3 left = transform.position + new Vector3(-0.5f, height - 0.15f, 0f);
        barBack.SetPosition(0, left); barBack.SetPosition(1, left + Vector3.right);
        bar.SetPosition(0, left); bar.SetPosition(1, left + Vector3.right * Mathf.Max(0.001f, n));
        Color c = driver.Mind.Meter.Level == SuspicionLevel.Alert ? Color.red : driver.Mind.Meter.Level == SuspicionLevel.Curious ? Color.yellow : Color.white;
        bar.startColor = bar.endColor = c;
    }

    private static Color ColorFor(MarioMindState state, string markText)
    {
        if (markText == "OUCH!") return new Color(1f, 0.5f, 0.2f);
        if (markText == "GOTCHA!") return new Color(0.3f, 1f, 0.4f);
        switch (state)
        {
            case MarioMindState.Curious: return Color.yellow;
            case MarioMindState.Investigating: return new Color(1f, 0.6f, 0f);
            case MarioMindState.Chasing: return Color.red;
            case MarioMindState.Searching: return new Color(1f, 0.8f, 0.3f);
            default: return Color.white;
        }
    }
}
