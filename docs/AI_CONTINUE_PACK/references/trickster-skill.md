# 分册：捣蛋者技能 / 按键

## 现有按键（S200）
← → 移动｜↑ 跳｜P 伪装｜L 触发机关（伪装时）｜Shift+方向 切换目标/瞄准｜B 炸弹（现形，3 个）｜Z 缩小（2 次）｜G 诱饵（现形，1 个）｜F 连锁编号 / Shift+F 一键布置｜T 挑衅（现形，3 次）｜↓ 通风管｜V 标签｜H 帮助｜M/Tab 图例｜C 镜头｜R 重开（回合结束）｜Esc 暂停。
**已被占用、别再用**：A D W S Q O I N Space（旧输入/马里奥键位）、P L V H M Tab C R B Z G F T、方向键、Esc、F1–F12。可用：J K U X Y（Step1Keys 已支持）、E、1–9（需在 Step1Keys 加）。

## 模板（参考 `TauntAbility.cs` / `DecoyAbility.cs`，挂在捣蛋者身上）
```csharp
public class XxxAbility : MonoBehaviour {
  [SerializeField] MarioMindTuningSO tuning; public void SetTuning(MarioMindTuningSO t){tuning=t;}
  public static bool CanXxx(bool disguised, int left, float cd) => …;   // 纯逻辑，测试用
  void Start(){ … GameManager.Instance.OnRoundStart += ResetRound; }    // 每回合复原次数
  void Update(){
    if (self==null || Step1HandsOffCheck.IsRunning || Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen || Time.timeScale<=0f) return;
    if (!Step1Keys.Down(KeyCode.X)) return;
    if (!CanXxx(...)) { Step1Hint.Show(原因); return; }
    …; Step1Hint.Show(成功提示);
  }
}
```
接线：`Step1PrankRoomBuilder` 里 `trickster.gameObject.AddComponent<XxxAbility>().SetTuning(tuning);`（BuilderVersion+1）。

## 设计要求
- 每个技能写清：**代价**（现形才能用 / 次数 / 冷却 / 暴露位置 / 自己也会中）+ **马里奥反制**（能看见/听见/识破）。
- 技能产生的马里奥信息只走 MarioEyes 的公开通道（看见机关动、听见位置）。
- 连锁编排 `ChainPlan`（S200）是"以身入局"核心：编号 → L/绊线启动 → 预判落点自动 `OnTricksterActivate`（保留预警）。扩展连锁时复用 `ChainPlan.ShouldFire` 系列纯函数，别另起一套。
- 一定要有画面反馈（头顶字 / 屏幕字幕 / 闪烁），H6。
