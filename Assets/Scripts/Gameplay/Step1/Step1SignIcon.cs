using UnityEngine;

/// <summary>
/// S247：路牌图标（出口 / 宝物）。构建器只存图标名，运行时用 Step1PropIcons 现做贴图（程序生成的贴图不能存进场景）。
/// 纯显示：不加碰撞体、不影响玩法。Resources/Step1Icons/&lt;名字&gt;.png 同名覆盖 = 换美术。
/// </summary>
public class Step1SignIcon : MonoBehaviour
{
    public string iconKey;
    public float offsetX;

    private void Start()
    {
        var sp = Step1PropIcons.IconSprite(iconKey);
        if (sp == null) return;
        var go = new GameObject("Icon");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(offsetX, 0f, 0f);
        go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.sortingOrder = 40;
    }
}
