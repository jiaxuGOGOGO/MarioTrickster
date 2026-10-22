using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S187：运行时的受控随机布局。构建器在每个"随机槽位"上把所有候选元素都摆好（箱子、草丛、火……），
/// 本组件每回合开始按种子只激活其中一个（或都不激活 = 留空）。
///   - 选择算法与 Step1Layout.Resolve 完全一致（行优先、同一个 System.Random），所以测试里验证过可达性的组合就是游戏里会出现的组合；
///   - 不重建关卡，不影响任何引用（出生点、宝物、出口、附身锚点都不动）；
///   - 种子写入试玩记录（LastSeed），任何一局都能复现。
/// </summary>
public class Step1LayoutVariants : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public Vector2Int cell;
        public string options = "";           // 与 Step1Layout.Slots 同序，'.' = 留空
        public GameObject[] objects = new GameObject[0]; // 与 options 一一对应，'.' 对应 null
    }

    [SerializeField] private MarioMindTuningSO tuning;
    [SerializeField] private List<Slot> slots = new List<Slot>();
    [Tooltip("固定种子（-1 = 每回合随机）。调试/复现用")]
    [SerializeField] private int fixedSeed = -1;

    private GameManager manager;
    private int roundCounter;

    public int LastSeed { get; private set; }
    public IReadOnlyList<Slot> Slots => slots;

    public void AddSlot(Vector2Int cell, string options, GameObject[] objects)
    {
        slots.Add(new Slot { cell = cell, options = options, objects = objects });
    }

    public void SetTuning(MarioMindTuningSO t) => tuning = t;

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += Reroll;
        Reroll();
    }

    private void OnDestroy()
    {
        if (manager != null) manager.OnRoundStart -= Reroll;
    }

    private void Reroll()
    {
        int seed = fixedSeed >= 0 ? fixedSeed : unchecked(Environment.TickCount * 17 + (++roundCounter) * 104729);
        Apply(seed);
    }

    /// <summary>按种子激活对应元素；返回每个槽位选中的字符（行优先顺序）。</summary>
    public string Apply(int seed)
    {
        LastSeed = seed;
        SceneryProp.RestoreBlasted(); // S198：先复原被炸掉的摆件，再按种子开关（顺序无关，列表复原后清空）
        var options = new List<string>();
        foreach (var slot in slots) options.Add(slot.options ?? "");
        int[] picks = Step1Layout.Pick(seed, options);
        var picked = new char[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            int choice = picks[i];
            picked[i] = s.options.Length > 0 ? s.options[choice] : '.';
            for (int k = 0; k < s.objects.Length; k++)
            {
                var go = s.objects[k];
                if (go == null) continue;
                bool on = k == choice;
                if (go.activeSelf == on) continue;
                go.SetActive(on);
                // 刚被激活的机关从干净状态开始（不带上一局残留的喷火/冷却）
                if (on) foreach (var element in go.GetComponents<ControllableLevelElement>()) element.OnLevelReset();
            }
        }
        foreach (var bush in FindObjectsOfType<RustleOnPass>())
            bush.ConfigureWind(tuning.windMinSeconds, tuning.windMaxSeconds, unchecked(seed + bush.GetInstanceID()));
        return new string(picked);
    }
}
