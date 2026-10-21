// ── S253 网页试玩：在浏览器里真正“玩”一遍世界（不是文字彩排）──
// 规则全部来自已与 C# 逐步对比的纯逻辑：WorldPark 会话（worldpark.js）、BorderlandGame（borderland.js）、
// WPExplore（explore.js，现场探索与鬼）、WPPlayModel（play-model.js，演出 / 字幕卡 / 博弈桌）。
// 这里只负责画面、输入、音效与存档；不允许在这里写任何规则。
const WPPlay = (() => {
  'use strict';
  const M = WPPlayModel, C = M.Cues, X = WPExplore, BG = BorderlandGame, W = WorldPark;
  const esc = s => String(s == null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const now = () => performance.now() / 1000;
  const SAVE = 'wpPlay.save.v1:';
  const KIND = { story: '剧情', reveal: '揭示', ending: '结局', room: '房间', explore: '现场', game: '博弈' };
  const MODE = { beauty: '数字猜心', mark: '看不见的标记', split: '分还是偷' };
  let P = null;

  // ───────────────────────── 样式 ─────────────────────────
  const CSS = `
.wpp{position:fixed;inset:0;z-index:9000;background:#05060b;color:#e9edf5;font-family:"Microsoft YaHei","PingFang SC","Noto Sans SC",system-ui,sans-serif;overflow:hidden;user-select:none;-webkit-user-select:none}
.wpp *{box-sizing:border-box}
.wpp .red{color:#ff4d6d;text-shadow:0 0 12px #ff4d6d99}.wpp .cold{color:#7fd8ff;text-shadow:0 0 12px #7fd8ff99}
.wpp-top{position:absolute;left:0;right:0;top:0;height:48px;display:flex;align-items:center;gap:10px;padding:0 14px;background:linear-gradient(#000d,#0000);z-index:40}
.wpp-chip{font-weight:900;padding:1px 9px;border:1px solid currentColor;border-radius:6px;letter-spacing:1px;min-width:40px;text-align:center}
.wpp-vn{font-weight:700;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;min-width:0}.wpp-top .wpp-btn{white-space:nowrap;padding:5px 10px;flex:none}
.wpp[data-mode=title] .wpp-top [data-k=notes],.wpp[data-mode=title] .wpp-top [data-k=pause],.wpp[data-mode=summary] .wpp-top [data-k=notes],.wpp[data-mode=summary] .wpp-top [data-k=pause]{display:none}
@media(max-width:620px){.wpp-nn{display:none}}.wpp-nn{color:#9aa3b5;font-size:13px;white-space:nowrap}
.wpp-sp{flex:1}
.wpp-btn{background:#ffffff12;border:1px solid #ffffff33;color:#e9edf5;border-radius:8px;padding:6px 12px;font:inherit;font-size:13px;cursor:pointer;transition:background .15s,transform .15s}
.wpp-btn:hover{background:#ffffff24}.wpp-btn:disabled{opacity:.35;cursor:not-allowed}
.wpp-btn.main{background:#e9edf5;color:#05060b;font-weight:800;border-color:#e9edf5;padding:10px 26px;font-size:15px}
.wpp-btn.main:hover{transform:translateY(-1px);background:#fff}
.wpp-btn.danger{border-color:#ff4d6d;color:#ffb3c0}
.wpp-stage{position:absolute;inset:0}
.wpp-scene{position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;padding:9vh 22px calc(7vh + 76px);background:radial-gradient(ellipse at 50% 28%,#141a2b 0%,#05060b 72%)}
.wpp-scene.red{background:radial-gradient(ellipse at 50% 28%,#2a0f18 0%,#05060b 72%)}
.wpp-scene::before,.wpp-scene::after{content:"";position:absolute;left:0;right:0;height:7vh;background:#000;z-index:1}.wpp-scene::before{top:0}.wpp-scene::after{bottom:0}
.wpp-col{position:relative;z-index:2;width:min(800px,100%);max-height:100%;overflow:auto;padding:4px 4px 10px;scrollbar-width:thin}
.wpp-cardbig{font-size:56px;font-weight:900;line-height:1;letter-spacing:2px}
.wpp-kind{letter-spacing:4px;font-size:12px;color:#9aa3b5;margin-top:10px}
.wpp-title{font-size:28px;font-weight:800;margin:6px 0 14px}
.wpp-text{font-size:18px;line-height:1.95;white-space:pre-wrap;min-height:3.5em;cursor:pointer}
.wpp-caret{display:inline-block;width:.5em;height:1.05em;background:#e9edf5;vertical-align:-3px;margin-left:2px;animation:wppBlink 1s steps(1) infinite}
.wpp-rules{margin-top:16px;border:1px solid #ffffff2a;border-left:3px solid #ffd166;background:#ffd16610;border-radius:8px;padding:10px 14px;font-size:14.5px;line-height:1.8;color:#e6dcc0}
.wpp-rules b{color:#ffd166;letter-spacing:3px;font-size:12px;display:block}
.wpp-twists{display:grid;gap:10px;margin-top:18px}
.wpp-twist{display:grid;grid-template-columns:1fr 30px 1fr;gap:8px;opacity:0;transform:translateY(12px) scale(.98);transition:all .5s}
.wpp-twist.on{opacity:1;transform:none}
.wpp-twist .b{background:#ffffff0c;border:1px dashed #ffffff44;border-radius:10px;padding:10px 12px;color:#b9c0cf;line-height:1.7}
.wpp-twist .a{background:#ff4d6d1c;border:1px solid #ff4d6d;border-radius:10px;padding:10px 12px;box-shadow:0 0 22px #ff4d6d44;line-height:1.7}
.wpp-twist .ar{display:flex;align-items:center;justify-content:center;color:#ff4d6d;font-size:22px;font-weight:900}
.wpp-twist small{display:block;font-size:11px;letter-spacing:2px;color:#9aa3b5;margin-bottom:3px}
.wpp-missed{margin-top:14px;color:#ffb347;font-size:14px;opacity:0;transition:opacity .5s}.wpp-missed.on{opacity:1}
.wpp-stamp{display:inline-block;margin:14px 0 4px;padding:6px 22px;border:5px solid currentColor;border-radius:10px;font-size:42px;font-weight:900;letter-spacing:5px;transform:rotate(-5deg);animation:wppStamp .55s cubic-bezier(.2,1.6,.4,1)}
.wpp-stamp.clear{color:#5dffb0;text-shadow:0 0 16px #5dffb0}.wpp-stamp.gameover{color:#ff4d6d;text-shadow:0 0 16px #ff4d6d}.wpp-stamp.end{color:#ffd166;text-shadow:0 0 16px #ffd166}
.wpp-stampsub{color:#c6cddc;margin:12px 0 6px}
.wpp-doors{display:flex;flex-wrap:wrap;gap:12px;margin-top:20px}
.wpp-door{width:158px;min-height:124px;border-radius:12px;border:1px solid #ffffff30;background:#ffffff08;padding:12px;cursor:pointer;color:inherit;font:inherit;text-align:left;transition:transform .2s,box-shadow .2s}
.wpp-door.open{border-color:#7fd8ff;box-shadow:0 0 18px #7fd8ff44}.wpp-door.open:hover{transform:translateY(-4px);box-shadow:0 0 28px #7fd8ff88}
.wpp-door.shut{opacity:.42;cursor:not-allowed}.wpp-door b{display:block;font-size:30px;line-height:1.1;margin-bottom:6px}.wpp-door small{display:block;color:#9aa3b5;margin-top:6px;font-size:12px}
.wpp-bar{position:absolute;left:0;right:0;bottom:7vh;z-index:3;display:flex;gap:10px;justify-content:center;align-items:center;padding:14px}
.wpp-drawer{position:absolute;right:0;top:48px;bottom:0;width:min(400px,92vw);background:#0a0d17f4;border-left:1px solid #ffffff22;padding:14px 16px 20px;overflow:auto;z-index:45;transform:translateX(102%);transition:transform .25s;line-height:1.7}
.wpp-drawer.on{transform:none}.wpp-tabs{display:flex;gap:6px;margin-bottom:12px;flex-wrap:wrap}.wpp-tabs .wpp-btn[aria-pressed=true]{background:#e9edf5;color:#05060b}
.wpp-note{border:1px solid #ffffff22;border-radius:10px;padding:9px 12px;margin-bottom:9px;font-size:14px}.wpp-note b{color:#ffd166}.wpp-note .t{color:#ff8fa3;margin-top:4px}
.wpp-log{font-size:13px;color:#c6cddc;white-space:pre-wrap}
.wpp-cue{position:absolute;inset:0;z-index:60;display:flex;flex-direction:column;align-items:center;justify-content:center;background:#000e;cursor:pointer;overflow:hidden}
.wpp-cue .big{font-size:min(150px,26vw);font-weight:900;line-height:1;letter-spacing:4px}
.wpp-cue .big,.wpp-cue .t,.wpp-cue .s{will-change:opacity}
.wpp-cue .t{font-size:32px;font-weight:800;margin-top:14px}
.wpp-cue .s{color:#9aa3b5;margin-top:10px;letter-spacing:2px;text-align:center;padding:0 20px}
.wpp-cue .k{position:absolute;bottom:24px;color:#ffffff55;font-size:12px}
.wpp-cue.laser{background:#22040bdd}.wpp-cue.laser .big{animation:wppShake .4s .35s both}
.wpp-cue.pass .big{color:#5dffb0;text-shadow:0 0 30px #5dffb0}
.wpp-beam{position:absolute;top:0;bottom:0;width:8px;left:50%;margin-left:-4px;background:#ff2244;box-shadow:0 0 40px 14px #ff2244;transform-origin:top;animation:wppBeam 1s ease-out both}
.wpp-flash{position:absolute;inset:0;background:#fff;opacity:0;animation:wppFlash .55s .28s both}
.wpp-canvas{position:absolute;inset:0;width:100%;height:100%;display:block}
.wpp-timer{position:absolute;top:56px;left:50%;transform:translateX(-50%);font:800 34px/1 Consolas,"Courier New",monospace;letter-spacing:2px;text-shadow:0 0 10px #000,0 0 2px #000;z-index:20}
.wpp-timer.urgent{color:#ff4d6d;animation:wppPulse .5s infinite alternate}
.wpp-bigcount{position:absolute;top:42%;left:50%;transform:translate(-50%,-50%);font:900 150px/1 Consolas,monospace;color:#ff4d6d;opacity:.55;pointer-events:none;z-index:19;text-shadow:0 0 34px #ff4d6d}
.wpp-prompt{position:absolute;bottom:30px;left:50%;transform:translateX(-50%);background:#000c;border:1px solid #ffffff44;border-radius:999px;padding:8px 18px;font-size:15px;z-index:20;white-space:nowrap}
.wpp-prompt kbd{background:#e9edf5;color:#05060b;border-radius:4px;padding:0 7px;font-weight:900;margin-right:8px;font-family:inherit}
.wpp-toasts{position:absolute;top:100px;left:50%;transform:translateX(-50%);display:flex;flex-direction:column;gap:6px;align-items:center;z-index:21;pointer-events:none}
.wpp-toast{background:#000d;border:1px solid #ffffff33;border-radius:8px;padding:6px 14px;font-size:14px;animation:wppIn .25s;white-space:nowrap}
.wpp-toast.alarm{border-color:#ff4d6d;color:#ffc2cd}.wpp-toast.good{border-color:#5dffb0;color:#c9ffe6}
.wpp-cluecard{position:absolute;left:18px;bottom:18px;width:min(380px,80vw);background:#0b0e18ee;border:1px solid #ffd166;border-radius:12px;padding:12px 14px;z-index:20;animation:wppIn .3s;line-height:1.7}
.wpp-cluecard small{color:#ffd166;letter-spacing:3px;display:block}
.wpp-hunted{position:absolute;inset:0;pointer-events:none;z-index:18;box-shadow:inset 0 0 140px 36px #ff224477;animation:wppPulse .55s infinite alternate}
.wpp-help{position:absolute;right:14px;bottom:14px;color:#9aa3b5;font-size:12px;z-index:20;text-align:right;line-height:1.6;background:#0007;padding:6px 10px;border-radius:8px}
.wpp-modal{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;z-index:50;background:#000b;animation:wppIn .25s}
.wpp-panel{background:#0b0e18;border:1px solid #ffffff33;border-radius:16px;padding:24px 28px;width:min(560px,92vw);text-align:center;line-height:1.8;max-height:88vh;overflow:auto}
.wpp-panel h2{margin:6px 0 8px;font-size:30px}.wpp-panel .row{display:flex;gap:10px;justify-content:center;flex-wrap:wrap;margin-top:16px}
.wpp-table{position:absolute;inset:48px 0 150px 0}
.wpp-felt{position:absolute;left:7%;right:7%;top:12%;bottom:24%;border-radius:50%;background:radial-gradient(ellipse at 50% 40%,#183d31,#0a1b16 72%);border:12px solid #2a1d12;box-shadow:0 0 70px #000 inset,0 24px 70px #000a}
.wpp-felt.red{background:radial-gradient(ellipse at 50% 40%,#3d1820,#1b0a0e 72%)}
.wpp-seat{position:absolute;transform:translate(-50%,-31px);width:140px;text-align:center;transition:opacity .35s,filter .35s}
.wpp-seat.me .wpp-pick{position:absolute;left:calc(50% + 42px);top:-4px;margin:0}
.wpp-seat.me .wpp-truth,.wpp-seat.me .wpp-delta{position:absolute;left:calc(50% + 102px);width:120px;text-align:left}.wpp-seat.me .wpp-truth{top:14px}.wpp-seat.me .wpp-delta{top:34px}
.wpp-seat .av{position:relative;width:62px;height:62px;margin:0 auto;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:25px;font-weight:800;background:#1d2335;border:2px solid #ffffff44}
.wpp-seat.me .av{background:#e9edf5;color:#05060b}.wpp-seat.spot .av{border-color:#ffd166;box-shadow:0 0 20px #ffd166}
.wpp-seat.idle{opacity:.55}.wpp-seat.dead{opacity:.32;filter:grayscale(1)}
.wpp-seat.dead .av::after{content:"✕";position:absolute;color:#ff4d6d;font-size:54px;filter:none}
.wpp-seat .nm{font-weight:700;margin-top:4px;font-size:14px}
.wpp-lives{font-size:12px;letter-spacing:3px;color:#ff4d6d}.wpp-lives i{font-style:normal;color:#ffffff2a}
.wpp-pts{font-size:12px;color:#ffd166}
.wpp-bub{position:absolute;bottom:calc(100% + 4px);left:50%;transform:translateX(-50%);background:#e9edf5;color:#05060b;border-radius:10px;padding:5px 11px;font-size:13px;white-space:nowrap;max-width:260px;overflow:hidden;text-overflow:ellipsis;animation:wppIn .3s;z-index:3}
.wpp-bub::after{content:"";position:absolute;left:50%;top:100%;margin-left:-6px;border:6px solid transparent;border-top-color:#e9edf5}
.wpp-pick{width:52px;height:70px;margin:6px auto 0;perspective:500px;visibility:hidden}.wpp-pick.show{visibility:visible}
.wpp-pick .in{position:relative;width:100%;height:100%;transition:transform .45s;transform-style:preserve-3d}
.wpp-pick.flip .in{transform:rotateY(180deg)}
.wpp-pick .f,.wpp-pick .k{position:absolute;inset:0;border-radius:8px;backface-visibility:hidden;-webkit-backface-visibility:hidden;display:flex;align-items:center;justify-content:center;font-weight:900;font-size:22px}
.wpp-pick .k{background:repeating-linear-gradient(45deg,#3b1f6b,#3b1f6b 6px,#2a1450 6px,#2a1450 12px);border:2px solid #ffffff33}
.wpp-pick .f{background:#f5f2ea;color:#111;transform:rotateY(180deg);border:2px solid #fff}
.wpp-seat.win .f{box-shadow:0 0 18px #5dffb0;border-color:#5dffb0}.wpp-seat.wrong .f{box-shadow:0 0 18px #ff4d6d;border-color:#ff4d6d}
.wpp-truth{font-size:12px;color:#ffd166;height:16px}
.wpp-delta{font-size:13px;font-weight:800;color:#ff4d6d;height:17px}.wpp-delta.up{color:#5dffb0}
.wpp-seatbeam{position:absolute;left:50%;bottom:40%;height:100vh;width:6px;margin-left:-3px;background:#ff2244;box-shadow:0 0 26px 8px #ff2244;transform-origin:bottom;animation:wppBeamUp 1.1s ease-out both;pointer-events:none}
.wpp-center{position:absolute;left:50%;top:43%;transform:translate(-50%,-50%);text-align:center;width:min(480px,62%);pointer-events:none}
.wpp-center .cb{font-size:34px;font-weight:900;animation:wppIn .35s}.wpp-center .cs{color:#d4dae6;margin-top:6px}
.wpp-center .md{font-size:13px;letter-spacing:4px;color:#9aa3b5}.wpp-center .mt{font-size:30px;font-weight:900;margin-top:4px}
.wpp-newrule{margin-top:12px;color:#ffd166;font-weight:800;animation:wppIn .4s}
.wpp-round{position:absolute;top:10px;left:14px;font-weight:800;letter-spacing:2px;white-space:nowrap;text-shadow:0 0 6px #000;z-index:4}
.wpp-ctl{position:absolute;left:50%;bottom:-136px;transform:translateX(-50%);display:flex;flex-direction:column;align-items:center;gap:8px;width:min(720px,58vw);z-index:5}
.wpp-prm{color:#d4dae6;font-size:14px;text-align:center;background:#0009;padding:4px 12px;border-radius:8px}
.wpp-rowc{display:flex;gap:10px;align-items:center;flex-wrap:wrap;justify-content:center}
.wpp-num{font:900 42px/1 Consolas,monospace;width:96px;text-align:center;background:#0008;border:1px solid #ffffff44;border-radius:10px;color:#e9edf5;padding:4px 0}
.wpp-range{width:min(420px,64vw);accent-color:#ff4d6d}
.wpp-suit{width:66px;height:66px;font-size:34px;border-radius:12px;padding:0}
.wpp-move{min-width:110px;font-size:22px;font-weight:900;padding:10px 18px}
.wpp-info{position:absolute;left:14px;bottom:-136px;width:min(240px,19vw);font-size:13px;color:#d4dae6;line-height:1.6;background:#0009;border-radius:10px;padding:10px 12px;z-index:4}
.wpp-info:empty{display:none}
.wpp-titlescr{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;text-align:center;padding:24px;background:radial-gradient(ellipse at 50% 18%,#1b2340,#05060b 72%)}
.wpp-logo{font-size:min(64px,12vw);font-weight:900;letter-spacing:10px;text-shadow:0 0 26px #7fd8ff;animation:wppFlicker 2.2s linear}
.wpp-suits{font-size:26px;letter-spacing:14px;margin-top:6px}.wpp-suits span:nth-child(2),.wpp-suits span:nth-child(3){color:#ff4d6d}
.wpp-prem{max-width:660px;line-height:1.95;color:#c6cddc;margin:18px auto;font-size:16px}
.wpp-fine{color:#7d8597;font-size:12px;margin-top:18px;line-height:1.7}
@keyframes wppFlicker{0%{opacity:0}8%{opacity:1}11%{opacity:.15}18%{opacity:1}21%{opacity:.25}27%,100%{opacity:1}}
@keyframes wppIn{from{opacity:0;transform:translateY(12px)}to{opacity:1;transform:none}}
@keyframes wppBeam{0%{transform:scaleY(0)}35%{transform:scaleY(1)}100%{transform:scaleY(1);opacity:0}}
@keyframes wppBeamUp{0%{transform:scaleY(0);opacity:1}30%{transform:scaleY(1);opacity:1}100%{transform:scaleY(1);opacity:0}}
@keyframes wppFlash{0%{opacity:0}18%{opacity:.85}100%{opacity:0}}
@keyframes wppStamp{0%{transform:scale(2.6) rotate(-5deg);opacity:0}100%{transform:scale(1) rotate(-5deg);opacity:1}}
@keyframes wppBlink{50%{opacity:0}}@keyframes wppPulse{from{opacity:.5}to{opacity:1}}
@keyframes wppShake{0%,100%{transform:none}20%{transform:translateX(-10px)}40%{transform:translateX(9px)}60%{transform:translateX(-6px)}80%{transform:translateX(4px)}}
@media(max-width:900px){.wpp-ctl{width:94vw}.wpp-info{display:none}}
@media(max-width:760px){.wpp-twist{grid-template-columns:1fr}.wpp-twist .ar{transform:rotate(90deg)}.wpp-info{position:static;width:auto;margin:6px}}
`;

  // ───────────────────────── 合成音效（无外部文件）─────────────────────────
  const Sfx = (() => {
    let ctx = null, master = null, muted = false;
    const ensure = () => {
      if (!ctx) { const AC = window.AudioContext || window.webkitAudioContext; if (!AC) return null; ctx = new AC(); master = ctx.createGain(); master.gain.value = 0.32; master.connect(ctx.destination); }
      if (ctx.state === 'suspended') ctx.resume();
      return ctx;
    };
    const tone = (f, t0, dur, type, vol, f2) => {
      const o = ctx.createOscillator(), g = ctx.createGain(); o.type = type || 'sine'; o.frequency.setValueAtTime(f, t0);
      if (f2) o.frequency.exponentialRampToValueAtTime(f2, t0 + dur);
      g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(vol || 0.2, t0 + 0.012); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
      o.connect(g); g.connect(master); o.start(t0); o.stop(t0 + dur + 0.05);
    };
    const noise = (t0, dur, vol, hp) => {
      const len = Math.max(1, Math.floor(ctx.sampleRate * dur)), b = ctx.createBuffer(1, len, ctx.sampleRate), d = b.getChannelData(0);
      for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
      const s = ctx.createBufferSource(); s.buffer = b; const f = ctx.createBiquadFilter(); f.type = 'highpass'; f.frequency.value = hp || 1000;
      const g = ctx.createGain(); g.gain.setValueAtTime(vol || 0.2, t0); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
      s.connect(f); f.connect(g); g.connect(master); s.start(t0); s.stop(t0 + dur + 0.02);
    };
    const play = name => {
      if (muted || !ensure()) return;
      const t = ctx.currentTime;
      switch (name) {
        case 'neon': tone(98, t, 1.6, 'sawtooth', 0.05); for (const k of [0, 0.1, 0.32, 0.36, 0.74]) noise(t + k, 0.05, 0.22, 2600); tone(880, t + 0.95, 0.3, 'sine', 0.1); tone(1320, t + 1.05, 0.4, 'sine', 0.07); break;
        case 'flip': noise(t, 0.06, 0.22, 2800); tone(420, t, 0.05, 'triangle', 0.06); break;
        case 'beep': tone(880, t, 0.09, 'square', 0.1); break;
        case 'beepUrgent': tone(1320, t, 0.13, 'square', 0.14); break;
        case 'laser': tone(1900, t, 0.6, 'sawtooth', 0.2, 110); noise(t + 0.05, 0.45, 0.28, 400); tone(55, t + 0.3, 0.6, 'sine', 0.3); break;
        case 'pass': tone(660, t, 0.16, 'triangle', 0.22); tone(990, t + 0.12, 0.3, 'triangle', 0.22); break;
        case 'clear': [523, 659, 784, 1047].forEach((f, i) => tone(f, t + i * 0.11, 0.7, 'triangle', 0.18)); break;
        case 'gameover': [392, 330, 262, 196].forEach((f, i) => tone(f, t + i * 0.19, 0.55, 'sawtooth', 0.1)); break;
        case 'end': tone(587, t, 1.4, 'sine', 0.18); tone(880, t + 0.22, 1.3, 'sine', 0.1); break;
        case 'alarm': for (let i = 0; i < 3; i++) { tone(740, t + i * 0.2, 0.1, 'square', 0.13); tone(988, t + i * 0.2 + 0.1, 0.1, 'square', 0.13); } break;
        case 'rustle': noise(t, 0.22, 0.16, 1700); break;
        case 'clue': tone(1175, t, 0.12, 'sine', 0.18); tone(1568, t + 0.08, 0.3, 'sine', 0.16); break;
        case 'click': noise(t, 0.03, 0.14, 4200); break;
        case 'type': noise(t, 0.014, 0.04, 5200); break;
        case 'deny': tone(170, t, 0.16, 'square', 0.1); break;
        case 'door': tone(220, t, 0.25, 'triangle', 0.15, 330); noise(t, 0.2, 0.08, 900); break;
      }
    };
    return { play, ensure, isMuted: () => muted, setMuted: v => { muted = !!v; } };
  })();

  // ───────────────────────── 流程 ─────────────────────────
  function applyAction(s, a) {
    if (a === 'next') return s.Continue();
    if (a === 'win' || a === 'lose') return s.ResolveRoom(a === 'win');
    if (a === 'explore-win' || a === 'explore-lose') return s.ResolveExplore(a === 'explore-win');
    if (a.startsWith('observe:')) return s.ObserveClue(a.slice(8));
    if (a.startsWith('game:')) return s.ResolveGame(a.slice(5).split('|'));
    if (a.startsWith('travel:')) return s.Travel(a.slice(7));
    throw Error('未知动作：' + a);
  }
  function hash(str) { let h = 2166136261; for (let i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619); } return (h >>> 0).toString(36); }
  const $ = sel => P.root.querySelector(sel);

  function open(json, opts) {
    close();
    let pack;
    try { pack = W.parse(typeof json === 'string' ? json : JSON.stringify(json)); } catch (e) { alert('世界包无法读取：' + e.message); return false; }
    const errors = W.validate(pack).filter(i => i.severity === 'error');
    if (errors.length) { alert('这个世界还有 ' + errors.length + ' 个结构错误，先在“世界工坊”修好再试玩：\n\n' + errors.slice(0, 6).map(i => '· ' + i.text).join('\n')); return false; }
    try { W.start(pack); } catch (e) { alert('世界无法开始：' + e.message); return false; }
    if (!document.getElementById('wppStyle')) { const st = document.createElement('style'); st.id = 'wppStyle'; st.textContent = CSS; document.head.appendChild(st); }
    const root = document.createElement('div'); root.className = 'wpp'; root.tabIndex = -1;
    root.innerHTML = `<div class="wpp-stage"></div><div class="wpp-top"><span class="wpp-chip cold" hidden></span><span class="wpp-vn"></span><span class="wpp-nn"></span><span class="wpp-sp"></span>
      <button class="wpp-btn" data-k="notes" title="Tab">线索本</button><button class="wpp-btn" data-k="sound">声音：开</button><button class="wpp-btn" data-k="pause" title="Esc">暂停</button><button class="wpp-btn" data-k="quit">退出试玩</button></div>
      <div class="wpp-drawer"></div>`;
    document.body.appendChild(root);
    P = { json: JSON.stringify(pack), pack, key: SAVE + hash(JSON.stringify(pack)), root, stage: root.querySelector('.wpp-stage'), s: null, actions: [], mode: '', announced: null, cues: [], cue: null, keys: new Set(), paused: false, last: now(), raf: 0, drawer: '', label: (opts && opts.label) || '', onClose: opts && opts.onClose };
    root.querySelector('[data-k=notes]').onclick = () => toggleDrawer('notes');
    root.querySelector('[data-k=sound]').onclick = e => { Sfx.setMuted(!Sfx.isMuted()); e.target.textContent = '声音：' + (Sfx.isMuted() ? '关' : '开'); };
    root.querySelector('[data-k=pause]').onclick = () => pause(true);
    root.querySelector('[data-k=quit]').onclick = () => close();
    window.addEventListener('keydown', onKey, true); window.addEventListener('keyup', onKeyUp, true); window.addEventListener('blur', onBlur);
    root.addEventListener('pointerdown', () => Sfx.ensure(), { capture: true });
    showTitle();
    P.raf = requestAnimationFrame(frame);
    root.focus();
    return true;
  }
  function close() {
    if (!P) return;
    cancelAnimationFrame(P.raf);
    window.removeEventListener('keydown', onKey, true); window.removeEventListener('keyup', onKeyUp, true); window.removeEventListener('blur', onBlur);
    const cb = P.onClose; P.root.remove(); P = null;
    if (cb) cb();
  }
  function loadSave() { try { const j = JSON.parse(localStorage.getItem(P.key) || 'null'); return j && Array.isArray(j.actions) ? j : null; } catch (e) { return null; } }
  function save() {
    const n = P.s.Current; if (!n || !['story', 'reveal', 'ending', 'room'].includes(n.kind)) return;
    try { localStorage.setItem(P.key, JSON.stringify({ actions: P.actions, venue: venueName(P.s.Venue), node: n.name || n.id, at: Date.now() })); } catch (e) { /* 存储满或被禁用：试玩照常 */ }
  }
  function rebuild(actions) {
    const s = W.start(P.pack);
    for (const a of actions) applyAction(s, a);
    P.s = s; P.actions = actions.slice();
  }
  const venueName = v => !v ? '' : (v.name || v.id);

  function startJourney(actions) {
    try { rebuild(actions || []); } catch (e) { toastTop('存档无法恢复（世界被修改过？），从头开始'); rebuild([]); }
    P.announced = null; P.cues = []; P.cue = null; removeCue();
    afterChange();
  }
  function act(a) {
    try { applyAction(P.s, a); } catch (e) { toastTop('无法执行：' + e.message); Sfx.play('deny'); return false; }
    P.actions.push(a);
    afterChange();
    return true;
  }
  function afterChange() {
    const s = P.s, v = s.Venue, n = s.Current;
    if (P.announced !== s.venueId) { P.announced = s.venueId; queueCue(C.Entrance(v)); }
    save();
    updateTop();
    closeDrawer();
    if (['story', 'reveal', 'ending', 'room'].includes(n.kind)) showScene();
    else if (n.kind === 'explore') showExplore();
    else if (n.kind === 'game') showGame();
  }
  function retryVenue() {
    const id = P.s.venueId; let cut = -1;
    for (let i = P.actions.length - 1; i >= 0; i--) if (P.actions[i] === 'travel:' + id) { cut = i; break; }
    startJourney(P.actions.slice(0, cut + 1));
  }
  function updateTop() {
    const chip = $('.wpp-chip'), vn = $('.wpp-vn'), nn = $('.wpp-nn');
    if (!P.s) { chip.hidden = true; vn.textContent = P.pack.name || ''; nn.textContent = '网页试玩'; return; }
    const v = P.s.Venue, n = P.s.Current;
    const card = C.Card(v) || '?'; chip.hidden = venueName(v).startsWith(card); chip.textContent = card; chip.className = 'wpp-chip ' + C.Tone(v);
    vn.textContent = venueName(v); nn.textContent = (KIND[n.kind] || n.kind) + ' · ' + (n.name || n.id);
  }

  // ───────────────────────── 标题 / 结束 ─────────────────────────
  function showTitle() {
    P.mode = 'title'; P.s = null; updateTop();
    const sv = loadSave();
    P.stage.innerHTML = `<div class="wpp-titlescr"><div>
      <div class="wpp-logo">${esc(P.pack.name || '无名的世界')}</div>
      <div class="wpp-suits"><span>♠</span><span>♥</span><span>♦</span><span>♣</span></div>
      <p class="wpp-prem">${esc(P.pack.premise || '')}</p>
      <div class="row" style="display:flex;gap:12px;justify-content:center;flex-wrap:wrap">
        ${sv ? `<button class="wpp-btn main" data-a="cont">继续旅程（${esc(sv.venue)} · ${esc(sv.node)}）</button>` : ''}
        <button class="wpp-btn ${sv ? '' : 'main'}" data-a="new">${sv ? '从头开始' : '开始旅程'}</button>
        <button class="wpp-btn" data-a="quit">返回设计台</button>
      </div>
      <p class="wpp-fine">WASD / 方向键 移动 · E 或 空格 调查 / 继续 · Tab 线索本 · Esc 暂停<br>
      网页试玩与 Unity 用同一套规则（探索、鬼、博弈、演出逐步跨端对比 0 差异）；画面是网页版，横版房间阶段只能手选结果。<br>
      进度自动存在这个浏览器里（只存在剧情 / 揭示 / 结局这类安全点）。${P.label ? '<br>' + esc(P.label) : ''}</p>
    </div></div>`;
    P.stage.querySelector('[data-a=new]').onclick = () => { Sfx.ensure(); Sfx.play('click'); try { localStorage.removeItem(P.key); } catch (e) { } startJourney([]); };
    const c = P.stage.querySelector('[data-a=cont]'); if (c) c.onclick = () => { Sfx.ensure(); Sfx.play('click'); startJourney(sv.actions); };
    P.stage.querySelector('[data-a=quit]').onclick = () => close();
  }
  function showSummary() {
    P.mode = 'summary';
    const sc = M.Scene.Build(P.s, null);
    let cleared = 0, visa = 0, found = 0, total = 0;
    for (const v of P.pack.venues) { if (P.s.results[v.id] === 'clear') { cleared++; visa += C.VisaDays(v); } for (const c of v.clues || []) { total++; if (P.s.found.has(v.id + '/' + c.id)) found++; } }
    P.stage.innerHTML = `<div class="wpp-titlescr"><div class="wpp-panel" style="width:min(640px,94vw)">
      <div class="wpp-kind">JOURNEY</div><h2>旅程结束</h2>
      <p>通过 ${cleared} 个会场 · 签证累计 +${visa} 天 · 找到线索 ${found}/${total}</p>
      <div style="text-align:left;margin:12px auto;max-width:440px">${sc.journey.map(j => `<div>${esc(j)}</div>`).join('')}</div>
      <div class="row"><button class="wpp-btn main" data-a="again">再走一次</button><button class="wpp-btn" data-a="title">回到标题</button><button class="wpp-btn" data-a="quit">返回设计台</button></div>
    </div></div>`;
    P.stage.querySelector('[data-a=again]').onclick = () => { try { localStorage.removeItem(P.key); } catch (e) { } startJourney([]); };
    P.stage.querySelector('[data-a=title]').onclick = () => showTitle();
    P.stage.querySelector('[data-a=quit]').onclick = () => close();
  }

  // ───────────────────────── 演出覆盖层 ─────────────────────────
  function queueCue(c, after) { if (!c) { if (after) after(); return; } P.cues.push({ c, after }); }
  function showCue(q) {
    const c = q.c; q.t0 = now();
    const el = document.createElement('div'); el.className = 'wpp-cue ' + c.kind;
    if (c.kind === 'entrance') el.innerHTML = `<div class="big ${c.tone}">${esc(c.card || '?')}</div><div class="t">${esc(c.title)}</div><div class="s">${esc(c.sub)}</div><div class="k">点击跳过</div>`;
    else if (c.kind === 'laser') el.innerHTML = `<div class="wpp-beam"></div><div class="wpp-flash"></div><div class="big red">${esc(c.title)}</div><div class="s">${esc(c.sub)}</div><div class="k">点击继续</div>`;
    else el.innerHTML = `<div class="big">${esc(c.title)}</div><div class="s">${esc(c.sub)}</div><div class="k">点击继续</div>`;
    el.onclick = () => endCue();
    P.root.appendChild(el); q.el = el; P.cue = q; paintCue(q, 0);
    Sfx.play(c.sound);
  }
  const clamp01 = x => x < 0 ? 0 : x > 1 ? 1 : x;
  function flicker(k) { if (k < 0.12) return k / 0.12; if (k < 0.18) return 0.15; if (k < 0.3) return 1; if (k < 0.36) return 0.25; return 1; }
  function paintCue(q, k) {
    const el = q.el; if (!el) return; const big = el.querySelector('.big'), tt = el.querySelector('.t'), ss = el.querySelector('.s');
    const kind = q.c.kind;
    if (big) big.style.opacity = String(kind === 'entrance' ? flicker(k) : kind === 'pass' ? clamp01(k / 0.25) : 1);
    if (tt) tt.style.opacity = String(kind === 'entrance' ? clamp01((k - 0.9) / 0.5) : 1);
    if (ss) ss.style.opacity = String(kind === 'entrance' ? clamp01((k - 1.3) / 0.5) : clamp01((k - 0.35) / 0.4));
  }
  function endCue() { if (!P || !P.cue) return; const q = P.cue; P.cue = null; if (q.el) q.el.remove(); if (q.after) q.after(); }
  function removeCue() { P.root.querySelectorAll('.wpp-cue').forEach(e => e.remove()); }

  // ───────────────────────── 抽屉：线索本 / 规则 / 旅程 / 记录 ─────────────────────────
  function toggleDrawer(tab) { if (!P.s) return; if (P.drawer === tab) closeDrawer(); else { P.drawer = tab; renderDrawer(); } }
  function closeDrawer() { if (!P) return; P.drawer = ''; const d = $('.wpp-drawer'); if (d) d.classList.remove('on'); }
  function renderDrawer() {
    const d = $('.wpp-drawer'), sc = M.Scene.Build(P.s, null), v = P.s.Venue;
    const tabs = [['notes', '线索本'], ['rules', '规则'], ['journey', '旅程']].concat(P.mode === 'game' && P.g ? [['log', '博弈记录']] : []);
    let body = '';
    if (P.drawer === 'notes') body = sc.notes.length ? sc.notes.map(n => `<div class="wpp-note"><b>${esc(n.name)}</b><div>${esc(n.before)}</div>${n.after ? `<div class="t">其实：${esc(n.after)}</div>` : ''}</div>`).join('') : '<p style="color:#9aa3b5">本会场还没有发现线索。去现场靠近发光的“？”按 E 调查。</p>';
    else if (P.drawer === 'rules') body = `<div class="wpp-note"><b>${esc(C.Card(v))} ${esc(venueName(v))}</b><div style="white-space:pre-wrap">${esc(v.rules || '（这个会场没有写规则）')}</div></div>${v.setting ? `<div class="wpp-note"><b>场地</b><div>${esc(v.setting)}</div></div>` : ''}`;
    else if (P.drawer === 'journey') body = sc.journey.map(j => `<div class="wpp-note">${esc(j)}</div>`).join('');
    else if (P.drawer === 'log' && P.g) { const st = P.g.view && P.g.view.phase === 'reveal' && P.g.before ? P.g.before : P.g.st; body = `<div class="wpp-log">${esc(st.transcript.join('\n'))}</div>`; }
    d.innerHTML = `<div class="wpp-tabs">${tabs.map(([k, l]) => `<button class="wpp-btn" data-t="${k}" aria-pressed="${P.drawer === k}">${l}</button>`).join('')}<span class="wpp-sp"></span><button class="wpp-btn" data-t="x">关闭</button></div>${body}`;
    d.querySelectorAll('[data-t]').forEach(b => b.onclick = () => { if (b.dataset.t === 'x') closeDrawer(); else { P.drawer = b.dataset.t; renderDrawer(); } });
    d.classList.add('on');
  }

  // ───────────────────────── 暂停 ─────────────────────────
  function pause(on) {
    if (!P || !P.s || P.mode === 'title' || P.mode === 'summary') return;
    const old = P.root.querySelector('.wpp-pause'); if (old) old.remove();
    P.paused = on; P.keys.clear();
    if (!on) return;
    const el = document.createElement('div'); el.className = 'wpp-modal wpp-pause';
    el.innerHTML = `<div class="wpp-panel"><div class="wpp-kind">PAUSE</div><h2>暂停</h2><p style="color:#9aa3b5">进度已存到最近的剧情点。现场探索和博弈中途离开，回来会从那个阶段的开头重新开始。</p>
      <div class="row"><button class="wpp-btn main" data-a="go">继续</button><button class="wpp-btn" data-a="retry">重来本会场</button><button class="wpp-btn" data-a="title">回到标题</button><button class="wpp-btn danger" data-a="quit">退出试玩</button></div></div>`;
    el.querySelector('[data-a=go]').onclick = () => pause(false);
    el.querySelector('[data-a=retry]').onclick = () => { pause(false); retryVenue(); };
    el.querySelector('[data-a=title]').onclick = () => { pause(false); showTitle(); };
    el.querySelector('[data-a=quit]').onclick = () => close();
    P.root.appendChild(el);
  }

  // ───────────────────────── 字幕卡（剧情 / 揭示 / 结局 / 房间前）─────────────────────────
  function showScene() {
    const s = P.s, v = s.Venue, n = s.Current, sc = M.Scene.Build(s, null);
    const cue = sc.kind === 'ending' ? C.Ending(v, n) : null;
    P.mode = 'scene';
    P.sc = { sc, t: 0, typed: -1, twistsOn: 0, missedOn: false, cue, sounded: false };
    const isArrival = sc.kind === 'story' && n.id === v.entry && sc.rules;
    P.stage.innerHTML = `<div class="wpp-scene ${sc.tone === 'red' ? 'red' : ''}"><div class="wpp-col">
      <div class="wpp-cardbig ${sc.tone}">${esc(sc.card || '?')}</div>
      <div class="wpp-kind">${esc(KIND[sc.kind] || sc.kind)} · ${esc(sc.venueName)}</div>
      ${cue ? `<div class="wpp-stamp ${cue.kind}" style="visibility:hidden">${esc(sc.stamp)}</div><div class="wpp-stampsub" style="visibility:hidden">${esc(sc.stampSub)}</div>` : ''}
      <div class="wpp-title">${esc(sc.title)}</div>
      <div class="wpp-text"><span class="tx"></span><span class="wpp-caret"></span></div>
      ${isArrival ? `<div class="wpp-rules" style="display:none"><b>规则</b>${esc(sc.rules)}</div>` : ''}
      <div class="wpp-twists">${sc.twists.map(t => `<div class="wpp-twist"><div class="b"><small>当时以为 · ${esc(t.name)}</small>${esc(t.before)}</div><div class="ar">→</div><div class="a"><small>其实</small>${esc(t.after)}</div></div>`).join('')}</div>
      ${sc.missed.length ? `<div class="wpp-missed">你错过了：${sc.missed.map(esc).join('、')}——真相不会在这里揭开。</div>` : ''}
      ${sc.kind === 'ending' ? `<div class="wpp-doors" style="display:none">${sc.doors.map(d => `<button class="wpp-door ${d.open ? 'open' : 'shut'}" data-v="${esc(d.venue)}" ${d.open ? '' : 'disabled'}><b class="${d.card.includes('♥') || d.card.includes('♦') ? 'red' : 'cold'}">${esc(d.card)}</b>${esc(d.name)}<small>${d.open ? '前往这个会场' : esc(d.reason)}</small></button>`).join('')}</div>` : ''}
    </div>
    <div class="wpp-bar"><button class="wpp-btn" data-a="notes">线索本（${sc.notes.length}）</button>${sc.kind === 'ending' ? `${cue && cue.kind === 'gameover' ? '<button class="wpp-btn" data-a="retry">重来本会场</button>' : ''}${sc.doors.some(d => d.open) ? '<button class="wpp-btn" data-a="finish">结束旅程</button>' : ''}` : ''}<button class="wpp-btn main" data-a="main">显示全部</button></div></div>`;
    P.stage.querySelector('.wpp-text').onclick = () => sceneMain(true);
    P.stage.querySelector('[data-a=notes]').onclick = () => toggleDrawer('notes');
    P.stage.querySelector('[data-a=main]').onclick = () => sceneMain(false);
    const fin = P.stage.querySelector('[data-a=finish]'); if (fin) fin.onclick = () => showSummary();
    const rt = P.stage.querySelector('[data-a=retry]'); if (rt) rt.onclick = () => retryVenue();
    P.stage.querySelectorAll('.wpp-door.open').forEach(b => b.onclick = () => { Sfx.play('door'); act('travel:' + b.dataset.v); });
  }
  function sceneReady() { return P.sc.t >= M.Scene.ReadyAt(P.sc.sc); }
  function sceneMain(textClick) {
    if (P.paused || P.cue) return;
    const S = P.sc, sc = S.sc;
    if (!sceneReady()) { S.t = M.Scene.ReadyAt(sc) + 0.01; return; }
    if (textClick) return;
    Sfx.play('click');
    if (sc.kind === 'story' || sc.kind === 'reveal') act('next');
    else if (sc.kind === 'room') showRoomChoice();
    else if (sc.kind === 'ending') { const open = sc.doors.filter(d => d.open); if (open.length === 1) { Sfx.play('door'); act('travel:' + open[0].venue); } else if (!open.length) showSummary(); }
  }
  function tickScene(dt) {
    const S = P.sc, sc = S.sc;
    if (!P.paused && !P.cue) S.t += dt;
    const k = M.Scene.Visible(sc.text, S.t);
    if (k !== S.typed) { if (k > S.typed && S.typed >= 0 && k % 3 === 0) Sfx.play('type'); S.typed = k; P.stage.querySelector('.tx').textContent = sc.text.slice(0, k); }
    const done = S.t >= M.Scene.TextDone(sc);
    const caret = P.stage.querySelector('.wpp-caret'); if (caret) caret.style.display = done ? 'none' : '';
    if (done) { const r = P.stage.querySelector('.wpp-rules'); if (r) r.style.display = ''; }
    const tw = P.stage.querySelectorAll('.wpp-twist');
    while (S.twistsOn < tw.length && S.t >= M.Scene.TwistAt(sc, S.twistsOn)) { tw[S.twistsOn].classList.add('on'); S.twistsOn++; Sfx.play('flip'); }
    if (!S.missedOn && done) { const ms = P.stage.querySelector('.wpp-missed'); if (ms) ms.classList.add('on'); S.missedOn = true; }
    const ready = sceneReady();
    if (ready && S.cue && !S.sounded) { S.sounded = true; P.stage.querySelectorAll('.wpp-stamp,.wpp-stampsub').forEach(e => e.style.visibility = 'visible'); const d = P.stage.querySelector('.wpp-doors'); if (d) d.style.display = ''; Sfx.play(S.cue.sound); }
    const btn = P.stage.querySelector('[data-a=main]');
    let label = '显示全部';
    if (ready) {
      if (sc.kind === 'ending') { const open = sc.doors.filter(d => d.open); label = open.length === 1 ? '前往 ' + open[0].card + ' ' + open[0].name : open.length ? '选择一扇门' : '结束旅程'; btn.disabled = open.length > 1; }
      else label = sc.action || '继续';
    }
    if (btn.textContent !== label) btn.textContent = label;
  }
  function showRoomChoice() {
    const n = P.s.Current;
    modal(`<div class="wpp-kind">ROOM</div><h2>横版房间：${esc(n.room || n.name || n.id)}</h2>
      <p style="color:#c6cddc">这个阶段在 Unity 的横版房间里进行。网页试玩不运行横版物理——请按你设想的结果选择：</p>
      <div class="row"><button class="wpp-btn main" data-a="win">房间通过</button><button class="wpp-btn danger" data-a="lose">房间失败</button></div>`, {
      win: () => act('win'), lose: () => act('lose')
    });
  }
  function modal(html, handlers) {
    const el = document.createElement('div'); el.className = 'wpp-modal'; el.innerHTML = `<div class="wpp-panel">${html}</div>`;
    for (const [k, fn] of Object.entries(handlers)) { const b = el.querySelector(`[data-a=${k}]`); if (b) b.onclick = () => { el.remove(); fn(); }; }
    P.root.appendChild(el); return el;
  }

  // ───────────────────────── 现场探索（俯视）─────────────────────────
  function showExplore() {
    const made = X.create(P.s);
    if (!made.run) { P.stage.innerHTML = `<div class="wpp-titlescr"><div class="wpp-panel"><h2>现场无法开始</h2><p>${esc(made.error)}</p></div></div>`; return; }
    P.mode = 'explore';
    P.ex = { run: made.run, ev: 0, endQueued: false, toastKeep: [], card: null, cardUntil: 0, face: { x: 0, y: -1 }, vis: null, visAt: null, lastMsg: '', clueTotal: (P.s.Current.points || []).filter(q => q && q.kind === 'clue').length };
    P.stage.innerHTML = `<canvas class="wpp-canvas"></canvas><div class="wpp-hunted" hidden></div><div class="wpp-timer"></div><div class="wpp-bigcount" hidden></div><div class="wpp-toasts"></div><div class="wpp-prompt" hidden></div><div class="wpp-cluecard" hidden></div>
      <div class="wpp-help">WASD / 方向键 移动 · E 调查 / 开门<br>Tab 线索本 · Esc 暂停 · 规则在线索本旁</div>`;
    P.ex.canvas = P.stage.querySelector('canvas');
  }
  function tickExplore(dt) {
    const E = P.ex, r = E.run, n = r.node;
    if (!P.paused && !P.cue && !r.Ended) {
      let h = 0, v = 0; const k = P.keys;
      if (k.has('KeyA') || k.has('ArrowLeft')) h -= 1; if (k.has('KeyD') || k.has('ArrowRight')) h += 1;
      if (k.has('KeyW') || k.has('ArrowUp')) v += 1; if (k.has('KeyS') || k.has('ArrowDown')) v -= 1;
      if (h || v) { const l = Math.hypot(h, v); E.face = { x: h / l, y: v / l }; }
      const before = r.Remaining;
      r.Tick(Math.min(dt, 0.1), h, v);
      if (n.timeout > 0) { const b = C.CountdownBeep(before, r.Remaining); if (b) Sfx.play(C.Urgent(r.Remaining) ? 'beepUrgent' : 'beep'); }
      while (E.ev < r.Events.length) { const e = r.Events[E.ev++]; const line = C.ChaseLine(e); if (line && e.type !== 'caught') toast(line, e.type === 'seen' || e.type === 'rustle' ? 'alarm' : 'good'); const snd = C.ChaseSound(e); if (snd) Sfx.play(snd); }
    }
    if (r.Ended && !E.endQueued) {
      E.endQueued = true;
      queueCue(C.Activity(P.s.Venue, r.Won, r.LastMessage), () => exploreResult());
    }
    drawExplore();
    // HUD
    const tm = P.stage.querySelector('.wpp-timer');
    if (n.timeout > 0) { const s = Math.max(0, r.Remaining); const txt = Math.floor(s / 60) + ':' + String(Math.floor(s % 60)).padStart(2, '0') + '.' + Math.floor((s * 10) % 10); if (tm.textContent !== txt) tm.textContent = txt; tm.classList.toggle('urgent', C.Urgent(r.Remaining)); }
    else if (tm.textContent !== '不限时') tm.textContent = '不限时';
    const big = P.stage.querySelector('.wpp-bigcount'), showBig = n.timeout > 0 && r.Remaining > 0 && r.Remaining <= C.CountdownFrom && !r.Ended;
    big.hidden = !showBig; if (showBig) { const d = String(Math.ceil(r.Remaining - 1e-9)); if (big.textContent !== d) big.textContent = d; }
    P.stage.querySelector('.wpp-hunted').hidden = !(r.Hunted && !r.Ended);
    const pr = P.stage.querySelector('.wpp-prompt'), np = r.NearbyPoint;
    if (np && !r.Ended) {
      const label = np.label || np.id; let txt;
      if (np.kind === 'clue') { const found = P.s.found.has(P.s.venueId + '/' + np.clue); txt = `<kbd>E</kbd>${found ? '再看一眼' : '调查'}「${esc(label)}」`; }
      else txt = `<kbd>E</kbd>打开「${esc(label)}」`;
      if (pr.innerHTML !== txt) pr.innerHTML = txt; pr.hidden = false;
    } else pr.hidden = true;
    const cc = P.stage.querySelector('.wpp-cluecard'); if (E.card && now() > E.cardUntil && !np) { E.card = null; cc.hidden = true; }
  }
  function exploreInteract() {
    const E = P.ex; if (!E || P.paused || P.cue) return; const r = E.run; if (r.Ended) return;
    const np = r.NearbyPoint; if (!np) { Sfx.play('deny'); return; }
    const wasFound = np.kind === 'clue' && P.s.found.has(P.s.venueId + '/' + np.clue);
    const res = r.Interact(np.id);
    if (!res.ok) { toast(res.message, 'alarm'); Sfx.play('deny'); return; }
    if (np.kind === 'clue') {
      if (!wasFound) P.actions.push('observe:' + np.clue);
      const c = (P.s.Venue.clues || []).find(x => x.id === np.clue);
      const cc = P.stage.querySelector('.wpp-cluecard');
      cc.innerHTML = `<small>${wasFound ? '已记下的线索' : '新线索 · 已记入线索本'}</small><b style="font-size:16px">${esc(c ? c.name : np.label)}</b><div>${esc(c ? c.hint : '')}</div>`;
      cc.hidden = false; cc.style.animation = 'none'; void cc.offsetWidth; cc.style.animation = '';
      E.card = np.id; E.cardUntil = now() + 6;
      Sfx.play(wasFound ? 'click' : 'clue');
    } else { E.lastExit = np; Sfx.play('door'); }
  }
  function exploreEndLine(r) {
    if (r.CaughtBy) { const c = (r.node.points || []).find(q => q && q.id === r.CaughtBy); return '你被「' + ((c && (c.label || c.id)) || r.CaughtBy) + '」抓住了。'; }
    const ex = P.ex && P.ex.lastExit; if (ex) return '你打开了「' + (ex.label || ex.id) + '」。';
    return r.Won ? '你离开了现场。' : '时间到了。';
  }
  function exploreResult() {
    const r = P.ex.run, v = P.s.Venue, found = (v.clues || []).filter(c => c && c.foundAt === r.node.id && P.s.found.has(v.id + '/' + c.id)).length;
    const total = (v.clues || []).filter(c => c && c.foundAt === r.node.id && c.trigger === 'interact').length;
    modal(`<div class="wpp-cardbig ${C.Tone(v)}" style="font-size:40px">${esc(C.Card(v))}</div><h2 style="color:${r.Won ? '#5dffb0' : '#ff4d6d'}">${r.Won ? '通过' : '出局'}</h2>
      <p>${esc(exploreEndLine(r))}</p><p style="color:#9aa3b5">本场调查了 ${found}/${total} 处现场 · 用时 ${r.Elapsed.toFixed(1)} 秒</p>
      <div class="row"><button class="wpp-btn main" data-a="ok">确认结果</button></div>`, { ok: () => act(r.Won ? 'explore-win' : 'explore-lose') });
  }
  function toast(text, cls) {
    const box = P.stage.querySelector('.wpp-toasts'); if (!box) { toastTop(text); return; }
    const el = document.createElement('div'); el.className = 'wpp-toast ' + (cls || ''); el.textContent = text; box.appendChild(el);
    while (box.children.length > 4) box.firstChild.remove();
    setTimeout(() => el.remove(), 2600);
  }
  function toastTop(text) {
    if (!P) return; let box = P.root.querySelector('.wpp-toasts.top');
    if (!box) { box = document.createElement('div'); box.className = 'wpp-toasts top'; box.style.zIndex = 70; P.root.appendChild(box); }
    const el = document.createElement('div'); el.className = 'wpp-toast alarm'; el.textContent = text; box.appendChild(el); setTimeout(() => el.remove(), 3200);
  }

  // —— 画面 ——
  const rgb = (t, k) => { const c = t && t.rgb ? t.rgb : [0.3, 0.3, 0.3]; return `rgb(${Math.round(c[0] * 255 * k)},${Math.round(c[1] * 255 * k)},${Math.round(c[2] * 255 * k)})`; };
  const cellHash = (x, y) => { let h = (x * 374761393 + y * 668265263) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
  function drawExplore() {
    const E = P.ex, r = E.run, m = r.map, cv = E.canvas, ctx = cv.getContext('2d');
    const dpr = Math.min(2, window.devicePixelRatio || 1), Wc = Math.max(1, Math.floor(cv.clientWidth * dpr)), Hc = Math.max(1, Math.floor(cv.clientHeight * dpr));
    if (cv.width !== Wc || cv.height !== Hc) { cv.width = Wc; cv.height = Hc; }
    const ts = Math.max(22 * dpr, Math.min(72 * dpr, Math.floor(Math.min(Hc / 12.5, Wc / 19))));
    const vw = Wc / ts, vh = Hc / ts;
    const camX = m.W <= vw ? m.W / 2 : Math.min(Math.max(r.x, vw / 2), m.W - vw / 2);
    const camY = m.H <= vh ? m.H / 2 : Math.min(Math.max(r.y, vh / 2), m.H - vh / 2);
    const sx = x => (x - camX) * ts + Wc / 2, sy = y => (camY - y) * ts + Hc / 2;
    const t = now(), R = X.K.PlayerViewRadius;
    // 视野（与内核 PlayerSees 同口径：8 格 + 视线）
    if (!E.visAt || Math.abs(E.visAt.x - r.x) > 0.05 || Math.abs(E.visAt.y - r.y) > 0.05) {
      E.vis = new Uint8Array(m.W * m.H);
      for (let y = 0; y < m.H; y++) for (let x = 0; x < m.W; x++) { const dx = x + 0.5 - r.x, dy = y + 0.5 - r.y; if (dx * dx + dy * dy <= R * R && X.lineOfSight(m, r.x, r.y, x + 0.5, y + 0.5)) E.vis[y * m.W + x] = 1; }
      E.visAt = { x: r.x, y: r.y };
    }
    ctx.fillStyle = '#020309'; ctx.fillRect(0, 0, Wc, Hc);
    const x0 = Math.max(0, Math.floor(camX - vw / 2) - 1), x1 = Math.min(m.W - 1, Math.ceil(camX + vw / 2) + 1);
    const y0 = Math.max(0, Math.floor(camY - vh / 2) - 2), y1 = Math.min(m.H - 1, Math.ceil(camY + vh / 2) + 1);
    // 地面
    for (let y = y1; y >= y0; y--) for (let x = x0; x <= x1; x++) {
      const c = X.at(m, x, y), tl = X.tile(c), px = sx(x), py = sy(y + 1);
      const raised = X.solid(c) && X.blocksSight(c);
      ctx.fillStyle = raised ? '#1a1d26' : rgb(tl, 0.62 + cellHash(x, y) * 0.08); ctx.fillRect(px, py, ts + 1, ts + 1);
      if (c === '"') { ctx.strokeStyle = 'rgba(160,230,120,.55)'; ctx.lineWidth = Math.max(1, ts / 22); for (let i = 0; i < 4; i++) { const gx = px + ts * (0.15 + 0.22 * i + cellHash(x + i, y) * 0.08), sway = Math.sin(t * 2 + x + i) * ts * 0.04; ctx.beginPath(); ctx.moveTo(gx, py + ts * 0.9); ctx.lineTo(gx + sway, py + ts * 0.35); ctx.stroke(); } }
      else if (c === 'w') { ctx.strokeStyle = 'rgba(200,230,255,.25)'; ctx.lineWidth = Math.max(1, ts / 30); const o = Math.sin(t * 1.5 + x * 0.7 + y) * ts * 0.1; ctx.beginPath(); ctx.moveTo(px + ts * 0.15, py + ts * 0.5 + o); ctx.lineTo(px + ts * 0.85, py + ts * 0.5 + o); ctx.stroke(); }
      else if (c === '=') { ctx.fillStyle = 'rgba(255,255,255,.05)'; ctx.fillRect(px + ts * 0.1, py + ts * 0.1, ts * 0.8, ts * 0.8); }
    }
    // 按行从上到下：墙/树 + 物体（下方的东西挡住上方）
    const bh = ts * 0.42;
    const ents = [];
    for (const q of r.node.points || []) if (q && q.kind !== 'chaser') ents.push({ y: q.y + 0.5, draw: () => drawPoint(ctx, q, sx, sy, ts, t, E) });
    for (const c of r.Chasers) if (r.PlayerSees(c)) { ents.push({ y: c.y, draw: () => drawChaser(ctx, c, sx, sy, ts, t, m, true) }); }
    ents.push({ y: r.y, draw: () => drawPlayer(ctx, r, sx, sy, ts, t, E) });
    ents.sort((a, b) => b.y - a.y);
    // 视锥画在最底层的物体之下，避免盖住墙
    for (const c of r.Chasers) if (r.PlayerSees(c)) drawCone(ctx, c, sx, sy, ts, m);
    let ei = 0;
    for (let y = y1; y >= y0; y--) {
      for (let x = x0; x <= x1; x++) {
        const c = X.at(m, x, y); if (!(X.solid(c) && X.blocksSight(c))) continue;
        const tl = X.tile(c), px = sx(x), py = sy(y + 1);
        if (c === 't') {
          ctx.fillStyle = '#3a2614'; ctx.fillRect(px + ts * 0.42, py + ts * 0.45, ts * 0.16, ts * 0.45);
          ctx.fillStyle = rgb(tl, 0.9); ctx.beginPath(); ctx.arc(px + ts / 2, py + ts * 0.25, ts * 0.52, 0, Math.PI * 2); ctx.fill();
          ctx.fillStyle = 'rgba(255,255,255,.08)'; ctx.beginPath(); ctx.arc(px + ts * 0.38, py + ts * 0.1, ts * 0.22, 0, Math.PI * 2); ctx.fill();
        } else {
          ctx.fillStyle = rgb(tl, 0.55); ctx.fillRect(px, py + ts - bh, ts + 1, bh + 1);
          ctx.fillStyle = rgb(tl, 1.0); ctx.fillRect(px, py - bh, ts + 1, ts + 1);
          ctx.fillStyle = 'rgba(255,255,255,.07)'; ctx.fillRect(px, py - bh, ts + 1, Math.max(1, ts * 0.06));
        }
      }
      while (ei < ents.length && ents[ei].y >= y) { ents[ei].draw(); ei++; }
    }
    while (ei < ents.length) { ents[ei].draw(); ei++; }
    // 夜色：看不到的格子压暗
    for (let y = y1; y >= y0; y--) for (let x = x0; x <= x1; x++) if (!E.vis[y * m.W + x]) { ctx.fillStyle = 'rgba(2,3,9,.74)'; ctx.fillRect(sx(x), sy(y + 1) - bh, ts + 1, ts + 1 + (y === y0 ? bh : 0)); }
    const g = ctx.createRadialGradient(sx(r.x), sy(r.y), ts * 2, sx(r.x), sy(r.y), ts * (R + 1));
    g.addColorStop(0, 'rgba(0,0,0,0)'); g.addColorStop(1, 'rgba(0,0,0,.55)'); ctx.fillStyle = g; ctx.fillRect(0, 0, Wc, Hc);
  }
  function drawCone(ctx, c, sx, sy, ts, m) {
    const sight = c.Sight, half = X.K.ChaserHalfAngle * Math.PI / 180, base = Math.atan2(c.fy, c.fx), pts = [[sx(c.x), sy(c.y)]];
    const own = X.at(m, Math.floor(c.x), Math.floor(c.y));
    for (let a = -half; a <= half + 1e-6; a += half / 12) {
      const ang = base + a, dx = Math.cos(ang), dy = Math.sin(ang); let d = 0;
      while (d < sight) { d += 0.2; const cx = Math.floor(c.x + dx * d), cy = Math.floor(c.y + dy * d), ch = X.at(m, cx, cy); if ((cx !== Math.floor(c.x) || cy !== Math.floor(c.y)) && (X.blocksSight(ch) || X.height(ch) > X.height(own))) break; }
      pts.push([sx(c.x + dx * Math.min(d, sight)), sy(c.y + dy * Math.min(d, sight))]);
    }
    const col = c.mode === 'chase' ? 'rgba(255,40,70,.30)' : c.mode === 'search' ? 'rgba(255,170,60,.22)' : 'rgba(255,240,170,.14)';
    ctx.fillStyle = col; ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]); for (const p of pts) ctx.lineTo(p[0], p[1]); ctx.closePath(); ctx.fill();
    ctx.strokeStyle = col.replace(/[\d.]+\)$/, '.6)'); ctx.lineWidth = 1; ctx.stroke();
  }
  function drawChaser(ctx, c, sx, sy, ts, t) {
    const x = sx(c.x), y = sy(c.y), rr = ts * 0.34;
    ctx.fillStyle = 'rgba(0,0,0,.4)'; ctx.beginPath(); ctx.ellipse(x, y + rr * 0.8, rr, rr * 0.35, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#15151c'; ctx.beginPath(); ctx.moveTo(x - rr, y + rr * 0.8); ctx.quadraticCurveTo(x - rr, y - rr * 1.2, x, y - rr * 1.3); ctx.quadraticCurveTo(x + rr, y - rr * 1.2, x + rr, y + rr * 0.8); ctx.closePath(); ctx.fill();
    ctx.fillStyle = '#f2efe6'; ctx.beginPath(); ctx.ellipse(x, y - rr * 0.6, rr * 0.55, rr * 0.62, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = c.mode === 'chase' ? '#ff2244' : '#111'; const ex = c.fx * rr * 0.18, ey = -c.fy * rr * 0.12;
    for (const s of [-1, 1]) { ctx.beginPath(); ctx.ellipse(x + s * rr * 0.2 + ex, y - rr * 0.65 + ey, rr * 0.09, rr * 0.14, 0, 0, Math.PI * 2); ctx.fill(); }
    if (c.mode !== 'patrol') { ctx.font = `900 ${Math.round(ts * 0.55)}px sans-serif`; ctx.textAlign = 'center'; ctx.fillStyle = c.mode === 'chase' ? '#ff4d6d' : '#ffb347'; ctx.fillText(c.mode === 'chase' ? '!' : '?', x, y - rr * 1.5 - Math.abs(Math.sin(t * 6)) * ts * 0.1); }
    ctx.font = `700 ${Math.round(ts * 0.26)}px sans-serif`; ctx.textAlign = 'center'; ctx.fillStyle = 'rgba(255,255,255,.75)'; ctx.fillText(c.Name, x, y + rr * 1.55);
  }
  function drawPoint(ctx, q, sx, sy, ts, t, E) {
    const r = E.run, m = r.map; if (!E.vis[q.y * m.W + q.x]) return;
    const x = sx(q.x + 0.5), y = sy(q.y + 0.5), label = q.label || q.id;
    const near = Math.hypot(q.x + 0.5 - r.x, q.y + 0.5 - r.y) < 3.2;
    if (q.kind === 'clue') {
      const found = P.s.found.has(P.s.venueId + '/' + q.clue);
      const pulse = found ? 0.4 : 0.65 + 0.35 * Math.sin(t * 4 + q.x);
      ctx.save(); ctx.translate(x, y - ts * 0.1); ctx.rotate(Math.PI / 4);
      ctx.shadowColor = '#ffd166'; ctx.shadowBlur = found ? 0 : ts * 0.5 * pulse; ctx.fillStyle = found ? 'rgba(255,209,102,.35)' : `rgba(255,209,102,${pulse})`;
      const s = ts * 0.32; ctx.fillRect(-s / 2, -s / 2, s, s); ctx.restore();
      ctx.font = `900 ${Math.round(ts * 0.3)}px sans-serif`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillStyle = '#1b1300'; ctx.fillText(found ? '✓' : '?', x, y - ts * 0.08); ctx.textBaseline = 'alphabetic';
      if (near || !found) { ctx.font = `700 ${Math.round(ts * 0.27)}px sans-serif`; ctx.fillStyle = found ? 'rgba(255,209,102,.6)' : '#ffd166'; ctx.fillText(label, x, y + ts * 0.5); }
    } else if (q.kind === 'exit') {
      ctx.fillStyle = '#0e2a3a'; ctx.strokeStyle = '#7fd8ff'; ctx.lineWidth = Math.max(1.5, ts / 18); ctx.shadowColor = '#7fd8ff'; ctx.shadowBlur = ts * 0.3;
      const w = ts * 0.56, h = ts * 0.74; ctx.beginPath(); ctx.moveTo(x - w / 2, y + h / 2); ctx.lineTo(x - w / 2, y - h / 4); ctx.arc(x, y - h / 4, w / 2, Math.PI, 0); ctx.lineTo(x + w / 2, y + h / 2); ctx.closePath(); ctx.fill(); ctx.stroke(); ctx.shadowBlur = 0;
      ctx.font = `700 ${Math.round(ts * 0.27)}px sans-serif`; ctx.textAlign = 'center'; ctx.fillStyle = '#bfefff'; ctx.fillText(label, x, y + h / 2 + ts * 0.3);
    }
  }
  function drawPlayer(ctx, r, sx, sy, ts, t, E) {
    const x = sx(r.x), y = sy(r.y), rr = ts * 0.3, hid = r.PlayerHidden;
    ctx.globalAlpha = hid ? 0.55 : 1;
    ctx.fillStyle = 'rgba(0,0,0,.45)'; ctx.beginPath(); ctx.ellipse(x, y + rr * 0.85, rr, rr * 0.35, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#e9edf5'; ctx.strokeStyle = '#7fd8ff'; ctx.lineWidth = Math.max(2, ts / 16); ctx.beginPath(); ctx.arc(x, y - rr * 0.2, rr, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    ctx.fillStyle = '#05060b'; ctx.beginPath(); ctx.arc(x + E.face.x * rr * 0.45, y - rr * 0.2 - E.face.y * rr * 0.45, rr * 0.2, 0, Math.PI * 2); ctx.fill();
    ctx.globalAlpha = 1;
    if (hid) { ctx.font = `800 ${Math.round(ts * 0.26)}px sans-serif`; ctx.textAlign = 'center'; ctx.fillStyle = '#a6f08a'; ctx.fillText('躲在草里', x, y - rr * 1.6); }
  }

  // ───────────────────────── 博弈桌 ─────────────────────────
  function showGame() {
    const n = P.s.Current, def = n.game;
    P.mode = 'game';
    P.g = { def, choices: [], st: BG.run(def, []), before: null, revealStart: -1, view: null, flipped: 0, laserRound: -1, endQueued: false, num: 50, ctlKey: '' };
    P.stage.innerHTML = `<div class="wpp-table"><div class="wpp-felt ${C.Tone(P.s.Venue)}"></div><div class="wpp-round"></div><div class="wpp-seats"></div><div class="wpp-center"></div><div class="wpp-info"></div><div class="wpp-ctl"></div>
      <div class="wpp-help" style="bottom:auto;top:12px">空格 跳过亮牌 · Tab 线索本 / 记录 · Esc 暂停</div></div>`;
    P.stage.querySelector('.wpp-table').onclick = e => { if (e.target.closest('.wpp-ctl')) return; skipReveal(); };
    const seats = P.stage.querySelector('.wpp-seats');
    seats.innerHTML = def.seats.map((_, i) => `<div class="wpp-seat" data-i="${i}"><div class="wpp-bub" hidden></div><div class="av"></div><div class="nm"></div><div class="wpp-lives"></div><div class="wpp-pts"></div><div class="wpp-pick"><div class="in"><div class="k"></div><div class="f"></div></div></div><div class="wpp-truth"></div><div class="wpp-delta"></div></div>`).join('');
  }
  function skipReveal() { const G = P.g; if (!G || !G.view || !G.view.revealing) return; G.revealStart = now() - G.view.revealLength - 0.01; }
  function submitChoice(choice) {
    const G = P.g; if (!G || P.paused || P.cue || !G.view || !G.view.inputOpen) return;
    const trial = G.choices.concat([String(choice)]), st = BG.run(G.def, trial);
    if (st.error) { toast('这个选择无效：' + st.error, 'alarm'); Sfx.play('deny'); return; }
    G.before = G.st; G.st = st; G.choices = trial; G.revealStart = now(); G.flipped = 0; G.ctlKey = '';
    Sfx.play('click');
  }
  function tickGame() {
    const G = P.g;
    if (P.paused && G.revealStart >= 0) G.revealStart += 1 / 60; // 暂停时冻结亮牌
    const revealT = G.revealStart < 0 ? -1 : now() - G.revealStart;
    const v = M.Table.Build(G.def, G.before, G.st, revealT); G.view = v;
    if (v.phase === 'error') { P.stage.querySelector('.wpp-center').innerHTML = `<div class="cb">博弈规则有误</div><div class="cs">${esc(G.st.error)}</div>`; return; }
    if (v.flipped > G.flipped) { Sfx.play('flip'); G.flipped = v.flipped; }
    P.stage.querySelector('.wpp-round').textContent = v.roundLine;
    // 座位
    const seatEls = P.stage.querySelectorAll('.wpp-seat');
    for (const s of v.seats) {
      const el = seatEls[s.index]; if (!el) continue;
      el.style.left = (s.x * 100) + '%'; el.style.top = (s.y * 100) + '%';
      el.classList.toggle('me', s.me); el.classList.toggle('dead', !s.alive); el.classList.toggle('idle', s.alive && !s.active && !s.me);
      el.classList.toggle('spot', s.spotlight); el.classList.toggle('win', s.winner); el.classList.toggle('wrong', s.wrong);
      setText(el.querySelector('.av'), s.initial); setText(el.querySelector('.nm'), s.name);
      const lives = '●'.repeat(Math.max(0, s.lives)) + '<i>' + '●'.repeat(Math.max(0, s.maxLives - s.lives)) + '</i>';
      setHtml(el.querySelector('.wpp-lives'), lives);
      setText(el.querySelector('.wpp-pts'), v.mode === 'split' ? s.points + ' 分' : '');
      const bub = el.querySelector('.wpp-bub'); bub.hidden = !s.bubble; setText(bub, s.bubble);
      const pk = el.querySelector('.wpp-pick'); pk.classList.toggle('show', !!s.pick); pk.classList.toggle('flip', s.revealed);
      setText(pk.querySelector('.f'), s.pick);
      setText(el.querySelector('.wpp-truth'), s.truth ? '真实标记 ' + s.truth : '');
      const dl = el.querySelector('.wpp-delta'), dtxt = v.resultShown ? (s.lost > 0 ? '−' + s.lost + ' 命' : '') + (s.gain > 0 ? ' +' + s.gain + ' 分' : '') : '';
      setText(dl, dtxt.trim()); dl.classList.toggle('up', s.lost === 0 && s.gain > 0);
    }
    // 出局激光（每轮一次）
    if (v.resultShown && G.laserRound !== G.st.records.length) {
      G.laserRound = G.st.records.length;
      let any = false;
      for (const s of v.seats) if (s.eliminatedNow) { const el = seatEls[s.index]; const b = document.createElement('div'); b.className = 'wpp-seatbeam'; el.appendChild(b); setTimeout(() => b.remove(), 1200); any = true; if (!s.me) toast(C.Eliminated(s.name).title + ' · GAME OVER', 'alarm'); }
      if (any) Sfx.play('laser');
    }
    // 中央
    const center = P.stage.querySelector('.wpp-center');
    let ch;
    if (v.phase === 'choose') ch = `<div class="md">${esc(C.Card(P.s.Venue))} · ${esc(MODE[v.mode] || v.mode)}</div><div class="mt">${v.mode === 'beauty' ? '全体平均 × ' + esc(G.def.ratio) + '%' : v.mode === 'mark' ? '你的标记是？' : '分，还是偷？'}</div>`;
    else if (v.phase === 'reveal') ch = `<div class="md">亮牌</div>`;
    else ch = `<div class="cb">${esc(v.centerBig)}</div><div class="cs">${esc(v.centerSmall)}</div>${v.newRule ? `<div class="wpp-newrule">${esc(v.newRule)}</div>` : ''}${v.finished ? `<div class="cb" style="margin-top:12px;color:${v.won ? '#5dffb0' : '#ff4d6d'}">${esc(v.finishLine)}</div>` : ''}`;
    setHtml(center, ch);
    setHtml(P.stage.querySelector('.wpp-info'), v.phase === 'choose' ? v.info.map(l => `<div>${esc(l)}</div>`).join('') : '');
    // 操作区
    const key = v.phase + '|' + G.st.round;
    if (key !== G.ctlKey) { G.ctlKey = key; renderControls(v); }
    if (v.finished && !G.endQueued) {
      G.endQueued = true;
      setTimeout(() => { if (!P || P.g !== G) return; queueCue(C.Activity(P.s.Venue, G.st.won, G.st.won ? '博弈通过' : '你输掉了这场博弈'), () => gameResult()); }, 700);
    }
    if (P.drawer === 'log') { const d = $('.wpp-drawer .wpp-log'); const st = v.phase === 'reveal' && G.before ? G.before : G.st; if (d && d.textContent.length !== st.transcript.join('\n').length) d.textContent = st.transcript.join('\n'); }
  }
  function renderControls(v) {
    const G = P.g, box = P.stage.querySelector('.wpp-ctl');
    if (v.phase !== 'choose') { box.innerHTML = v.revealing ? '<div class="wpp-prm">点击桌面或按空格跳过</div>' : ''; return; }
    let html = `<div class="wpp-prm">${esc(v.prompt)}</div>`;
    if (v.mode === 'beauty') html += `<div class="wpp-rowc"><input class="wpp-range" type="range" min="0" max="100" step="1" value="${G.num}"><input class="wpp-num" type="number" min="0" max="100" value="${G.num}"><button class="wpp-btn main" data-a="go">亮出数字</button></div>`;
    else if (v.mode === 'mark') html += `<div class="wpp-rowc">${['♠', '♥', '♦', '♣'].map(s => `<button class="wpp-btn wpp-suit ${s === '♥' || s === '♦' ? 'red' : ''}" data-s="${s}">${s}</button>`).join('')}</div>`;
    else html += `<div class="wpp-rowc"><button class="wpp-btn wpp-move" data-s="分" style="border-color:#5dffb0">分</button><button class="wpp-btn wpp-move" data-s="偷" style="border-color:#ff4d6d">偷</button></div>`;
    box.innerHTML = html;
    const rg = box.querySelector('.wpp-range'), nm = box.querySelector('.wpp-num');
    if (rg) { const sync = val => { G.num = Math.max(0, Math.min(100, Math.round(+val || 0))); rg.value = G.num; nm.value = G.num; }; rg.oninput = () => sync(rg.value); nm.oninput = () => sync(nm.value); nm.onkeydown = e => { if (e.key === 'Enter') submitChoice(String(G.num)); }; box.querySelector('[data-a=go]').onclick = () => submitChoice(String(G.num)); }
    box.querySelectorAll('[data-s]').forEach(b => b.onclick = () => submitChoice(b.dataset.s));
  }
  function gameResult() {
    const G = P.g, v = P.s.Venue;
    modal(`<div class="wpp-cardbig ${C.Tone(v)}" style="font-size:40px">${esc(C.Card(v))}</div><h2 style="color:${G.st.won ? '#5dffb0' : '#ff4d6d'}">${G.st.won ? '通过' : '出局'}</h2>
      <p>${esc(G.view ? G.view.finishLine : '')}</p><p style="color:#9aa3b5">共 ${G.st.round} 轮 · 你的选择：${esc(G.choices.join(' / '))}</p>
      <div class="row"><button class="wpp-btn main" data-a="ok">确认博弈结果</button></div>`, { ok: () => act('game:' + G.choices.join('|')) });
  }
  const setText = (el, s) => { if (el && el.textContent !== s) el.textContent = s; };
  const setHtml = (el, s) => { if (el && el._h !== s) { el._h = s; el.innerHTML = s; } };

  // ───────────────────────── 主循环与输入 ─────────────────────────
  function frame() {
    if (!P) return;
    if (P.root.dataset.mode !== P.mode) P.root.dataset.mode = P.mode;
    const t = now(), dt = Math.min(0.25, Math.max(0, t - P.last)); P.last = t;
    try {
      if (!P.cue && P.cues.length && !P.paused) showCue(P.cues.shift());
      if (P.cue && !P.paused && t - P.cue.t0 >= P.cue.c.seconds) endCue();
      if (P.cue) paintCue(P.cue, t - P.cue.t0);
      if (P.mode === 'scene' && P.sc) tickScene(dt);
      else if (P.mode === 'explore' && P.ex) tickExplore(dt);
      else if (P.mode === 'game' && P.g) tickGame();
    } catch (e) { console.error(e); toastTop('试玩出错：' + e.message); }
    if (P) P.raf = requestAnimationFrame(frame);
  }
  const MOVE = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);
  function onKey(e) {
    if (!P) return;
    const typing = e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA');
    e.stopPropagation();
    if (e.code === 'Escape') { e.preventDefault(); if (P.drawer) closeDrawer(); else pause(!P.paused); return; }
    if (e.code === 'Tab') { e.preventDefault(); toggleDrawer(P.drawer ? P.drawer : 'notes'); return; }
    if (typing) return;
    if (MOVE.has(e.code)) { e.preventDefault(); P.keys.add(e.code); }
    if (P.paused || e.repeat) return;
    if (e.code === 'Space' || e.code === 'Enter' || e.code === 'KeyE') {
      e.preventDefault(); Sfx.ensure();
      if (P.cue) { endCue(); return; }
      if (P.root.querySelector('.wpp-modal')) { const b = P.root.querySelector('.wpp-modal .wpp-btn.main'); if (b) b.click(); return; }
      if (P.mode === 'scene') sceneMain(false);
      else if (P.mode === 'explore') exploreInteract();
      else if (P.mode === 'game') { if (P.g.view && P.g.view.revealing) skipReveal(); else if (P.g.view && P.g.view.inputOpen && P.g.def.mode === 'beauty' && e.code === 'Enter') submitChoice(String(P.g.num)); }
    }
  }
  function onKeyUp(e) { if (!P) return; e.stopPropagation(); P.keys.delete(e.code); }
  function onBlur() { if (P) P.keys.clear(); }

  return { open, close, isOpen: () => !!P, _state: () => P };
})();
if (typeof module !== 'undefined') module.exports = WPPlay;
