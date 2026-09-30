using UnityEngine;

/// <summary>
/// S200：绊线（ASCII 'R'）—— 地上一根细线（看得见，H3）。马里奥踩过去：绊一下（tripStunSeconds，默认 0.4 秒），
/// 并且**启动你布置好的连锁**（ChainPlan）。每回合一次，回合重置复原。你自己踩不会触发（你装的线）。
/// 让"以身入局"成立：你跑过绊线，他追过来就会踩到，连锁自动开始，你不用停下按 L。
/// 规则：H4——只判断碰到的是不是马里奥；H9——只绊一下，不会卡住。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Tripwire : LevelElementBase
{
    [SerializeField] private float stumbleSeconds = 0.4f;
    private bool used;
    private Transform visual;
    public bool Used => used;
    public static event System.Action<Vector2> Tripped;

    public void Configure(float seconds) { stumbleSeconds = Mathf.Max(0.05f, seconds); }

    private void Awake()
    {
        elementName = "绊线";
        category = ElementCategory.Misc;
        tags = ElementTag.Interactive | ElementTag.OneShot | ElementTag.Resettable;
        description = "马里奥踩到绊一下，并启动你布置好的连锁";
        GetComponent<BoxCollider2D>().isTrigger = true;
        visual = transform.Find("Visual");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used || other == null) return;
        var mario = other.GetComponentInParent<MarioController>();
        if (mario == null) return;
        used = true;
        mario.ApplyKnockbackStun(stumbleSeconds);
        var rb = mario.GetComponent<Rigidbody2D>(); if (rb != null) rb.velocity = new Vector2(rb.velocity.x * 0.3f, rb.velocity.y);
        if (visual != null) visual.gameObject.SetActive(false);
        Step1Fx.Burst(transform.position, 4, new Color(0.9f, 0.9f, 0.8f, 1f), 3f, Vector2.up, 150f, 14f, 0.1f, 0.3f); // S216：线崩断
        Tripped?.Invoke(transform.position);
        Step1Hint.Show(Step1Text.TripwireHit, 1.5f);
    }

    public override void OnLevelReset() { used = false; if (visual != null) visual.gameObject.SetActive(true); }
}
