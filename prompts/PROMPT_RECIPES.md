# PROMPT_RECIPES

> **⚠️ 工业化管线 V4.1 专属核心知识库（双轨架构）**
> 本文档是 Manus TA 的主配方库。**上半部（技法库）**只回答 How，负责把教程蒸馏后的结构、动作、透视、材质与参数规则沉淀为可分发到节点的稳定约束；**下半部（实体库）**只回答 What，负责给角色、地形、陷阱与交互物提供纯视觉蓝图。出图时，必须执行 **“技法抽屉 × 实体蓝图”十字交叉**，并在 ComfyUI 中做**节点物理隔离分发**。
>
> **冲突优先级铁律**：`物理防滑步 > 空间透视正确 > 光影材质表现`。

---

## 0. 蒸馏落库增强协议（整合增强版）

### 0.1 不可改写的主骨架

| 项目 | 硬约束 | 直接执行要求 |
| :--- | :--- | :--- |
| **双轨分治** | 技法库只存 How，实体库只存 What | 禁止把实体外观词直接塞进技法抽屉，也禁止把透视/动作/参数规则写进实体蓝图 |
| **十字交叉出图** | 出图必须由“实体蓝图基础词 × 技法词”交叉生成 | Prompt、ControlNet、采样参数按节点分轨，不得混成一团长串 |
| **节点物理隔离** | 结构约束、动作约束、风格材质、采样参数必须分节点下发 | 角色动作优先走 `DWPose / OpenPose`，硬表面优先走 `Lineart / Canny` |
| **Unity 适配优先** | 一切新规则都必须服从横版、Bottom Center、防滑步、32x32 基准 | 与最终落地不兼容的教程表述只能降级为备注，不得升格为正文主规则 |

### 0.2 蒸馏完成强制入库核查清单

> **未勾完，不得宣称“蒸馏完成”。未落库、未记录、未提交、未推送，不算闭环。**

- [ ] 已按**章节 / 主题 / 技法簇**完成分块抽取，禁止直接把整本教程压缩成一版最终配方。
- [ ] 每个分块都已转换成**可执行规则**，并明确其归属：`解剖 / 透视 / 动画 / 光影 / 参数 / 角色蓝图 / 地形蓝图 / 陷阱蓝图 / 交互物蓝图`。
- [ ] 所有**高风险规则**已完成二次回查，尤其是：`动作纠偏`、`防滑步`、`纯侧视限制`、`3/4 结构校验边界`、`材质例外`、`风格漂移抑制`。
- [ ] 每条新增或修订规则都已写明**节点落点**，避免只有语义、没有 ComfyUI 落点。
- [ ] 所有重复内容都已经过**区分式去冗余**判断，而不是机械去重。
- [ ] 本次蒸馏中，凡是**同一规则在多个教程中出现 ≥2 次且表述一致**者，已标记为 **核心规则（core rule）**，不得因“去冗余”而删除。
- [ ] 仅当两条规则**完全等价且不新增任何约束、边界条件、失败模式或落地差异**时，才允许合并；合并后必须保留**信息最完整版本**。
- [ ] 已生成本次蒸馏 recap，至少交代：`新增规则 / 高价值重复 / 已剔除冗余 / 待确认疑点`。
- [ ] 已更新 `SESSION_TRACKER.md`，记录本次协议增强或规则入库结果。
- [ ] 提交说明已附带**本次蒸馏入库清单**，且推送后已确认远端分支前进。

### 0.3 高价值重复内容识别标准

| 判定情形 | 结论 | 入库动作 |
| :--- | :--- | :--- |
| **同一规则在 ≥2 个教程中重复出现，且表述一致** | **核心规则** | 升级为正文主规则或验收项，禁止删除 |
| **规则重复出现，但新增了失败模式、边界条件或适用场景** | **高价值重复** | 保留更完整版本，并把新增信息并入正文或校验分支 |
| **规则重复出现，只是换了说法但含义、限制、落点完全一致** | **纯重复** | 可合并，但必须保留表述最完整的一版 |
| **单一来源出现，但一旦遗漏会显著破坏最终效果** | **高风险单点规则** | 不因“只出现一次”降级，必须二次回查后决定是否入库 |

### 0.4 冗余判定标准

| 可否合并 | 判定标准 | 保留要求 |
| :--- | :--- | :--- |
| **可以合并** | 两条规则在**语义、适用条件、风险提示、节点落点、输出影响**上完全等价 | 保留信息最完整、最可执行的一版 |
| **不可合并** | 任一条规则多出了**边界条件**、**失败症状**、**例外场景**、**数值范围**、**节点差异** | 视为高价值重复或补充说明，必须保留 |
| **不可粗暴删减** | 重复出现本身构成“高频验证信号” | 允许压缩文字，不允许删除约束 |

### 0.5 四段验收节点（蒸馏 → 落库 → 出图 → Unity）

| 阶段 | 必须交付的可检验输出物 | 验收焦点 |
| :--- | :--- | :--- |
| **蒸馏** | 分块抽取记录、遗漏风险清单、蒸馏 recap | 是否存在“已读未入库”的高风险规则 |
| **落库** | `PROMPT_RECIPES.md` 或协议文档的明确 diff、规则归类结果、节点落点说明 | 规则是否进入双轨体系，而非停留在临时笔记 |
| **出图** | 可直接复制的 ComfyUI 节点配置图纸、关键 ControlNet/采样参数、抽样验证图或锚点对照 | 蒸馏语义能否稳定传到出图层 |
| **Unity** | 导入设置与切片约束确认结果、Pivot/PPU/FitMode 对齐记录、资产落地路径 | 最终资产是否仍满足防滑步、像素完美与横版适配 |

### 0.6 提交流程追加要求

> 每次与蒸馏落库相关的 commit / PR，都必须附带**本次蒸馏入库清单**，至少包含以下四类字段：`新增规则`、`高价值重复`、`已剔除冗余`、`待确认疑点`。如果缺失该清单，则视为提交说明不完整。

---

## 📚 上半部：通用技法库（5 个知识抽屉）

### 🗄️ 抽屉 1：🧍‍♂️ [解剖与形态]

*(记录：头身比、防断肢、多角度姿势)*

- **通用防畸变负面（Text Prompt）**：`deformed, extra limbs, bad anatomy, missing fingers, text, watermark`

| 规则簇 | 可执行 Tag / 数值 / 约束 | 节点落点 |
| :--- | :--- | :--- |
| **头部外轮廓** | `skull_widest_point, double_taper_head, cheek_to_jaw_taper, chin_lock` | Text Prompt / Lineart 参考图 |
| **面部基准线** | `center_line, eye_line, facial_T_zone` | Text Prompt / 草图底稿 |
| **头部中线定位** | `eyes_mid_head, nose_mid_eye_chin, lips_mid_nose_chin, ear_near_head_center_profile, hairline_mid_head_eye` | Text Prompt / 草图底稿 / Lineart 参考图 |
| **五官硬比例** | `five_eye_width_head, one_eye_gap, eyebrow_to_ear_top_align, eye_to_nose_bridge_align, ear_canal_mid_head_profile, nose_base_to_ear_base_align` | Text Prompt |
| **细节叠加顺序** | `lock_part_positions_before_details`；先锁 `eyes/ears/nose/mouth` 的位置与比例，再叠加 `hair/eyelashes/expression details`，禁止先堆局部细节再回推大形 | Prompt 条件分支 / 草图底稿 |
| **头倾角修正** | `head_tilt_up_feature_compression, head_tilt_down_feature_compression`；当 `head_tilt != 0` 时，**禁止**沿用无倾角五官平铺比例 | Prompt 条件分支 / Pose 参考图 |
| **成人头身比** | `head_unit=7.0~7.5` | 角色蓝图基础值 |
| **性别身高差** | `female_offset=-0.5_head` | 角色蓝图基础值 |
| **风格化高度** | `heroic=8_head`, `fashion=9_12_head`, `stylized_floor>=7.5_head` | 角色蓝图风格分支 |
| **上肢定位** | `elbow_at_rib_bottom, wrist_at_crotch, fingertips_mid_thigh` | DWPose / OpenPose 骨架校验 |
| **儿童体型** | `child_large_head, child_narrow_shoulders, child_neck_narrower_than_jaw, child_chest_lt_waist, child_short_fingers, child_hips_eq_shoulders, child_slight_knock_knee` | 角色年龄分支 |
| **头颈联动单元** | `head_neck_unit, head_tilt_neck_crimp, head_turn_neck_asymmetry`；头向左倾→左颈褶皱；头前倾→后颈与上背融合(显长)；头后仰→后颈褶皱(显短)；头左转→左颈短右颈长 | DWPose / OpenPose 骨架校验 + Text Prompt |
| **骨架弧线法则** | `limb_subtle_curves, no_straight_limbs, shoulder_extends_beyond_torso`；四肢必须用微弧线画，禁止直线（否则僵硬）；肩膀延伸超出躯干框架 | DWPose / OpenPose 骨架校验 + Lineart 参考图 |
| **性别体型差异（补充）** | `male_shoulder_gt_hip, female_hip_eq_shoulder, male_brow_ridge_prominent`；男性肩宽>臀宽，女性臀宽≈肩宽；男性眉骨突出；女性下颌线柔化 | 角色蓝图基础值 + Text Prompt |
| **收缩拉伸法则【核心规则】** | `contract_stretch_rule, bend_side_compress, opposite_side_stretch, line_of_action_toward_contract`；身体弯向一侧→弯曲侧收缩(褶皱/堆叠/距离短)，对侧拉伸(光滑/拉长/距离大)；Line of Action 弯向收缩侧。**场景分治**：动画帧用 `action_order_crush_to_stretch`，静态姿势/设定稿用本规则 | DWPose / OpenPose + Lineart 参考图 + Text Prompt |
| **肩臀对齐与角度差** | `shoulder_hip_angle_offset, weight_leg_hip_up_shoulder_down, five_alignment_lines`；自然站姿中肩线和臀线必须有角度差（平行=僵硬）；重心腿侧臀部抬高→同侧肩膀下沉；5 条对齐线即使不均匀也大部分保持对齐 | DWPose / OpenPose 骨架校验 + Text Prompt |
| **肩膀随手臂联动** | `shoulder_follows_arm, arm_raise_shoulder_raise, arm_raise_same_side_contract`；手臂抬起→同侧肩膀跟着抬起→同侧躯干收缩→对侧拉伸 | DWPose / OpenPose 骨架校验 |
| **膝关节偏移与腿部深度** | `thigh_past_shin_at_knee, near_knee_past_far_knee, foundation_leg_straight`；侧面视角腿锁定时大腿在膝盖处突出超过小腿；一个膝盖在另一个前面=深度感；支撑腿保持直立 | DWPose / OpenPose + Lineart 参考图 |

### 🗄️ 抽屉 2：📐 [透视与物件]

*(记录：等距视角、静物结构、平台拼接规律)*

- **横版跳跃全局侧视锁定（Text Prompt）【已废弃】**：`side-scrolling platformer, side view`  
  **废弃原因**：新教程确认头部与角色存在 `front / side / back / 3/4` 结构校验需求；若把 `side view` 当成全场景唯一视角，会误杀设定稿、头像卡、宣传图中的结构校正流程。现改为 **场景分治**。
- **Gameplay 纯侧视锁定（Text Prompt）**：`side-scrolling platformer, pure side view, orthographic feeling, no camera yaw`
- **Gameplay 防视角偏移负面（Text Prompt）**：`isometric, top-down view, 3d render, perspective, three-quarter view, front view`
- **设定稿 / 宣传图 3/4 结构校验（Text Prompt）**：`three-quarter head study, front faceplate with inward side planes, limited far side visibility`
- **头部空间面规则**：`front_faceplate, inward_side_planes`；3/4 视角下远侧面默认 `hidden_or_minimal`，仅在“朝镜头略转”标签下允许 `slightly_visible_far_plane`。
- **地形无缝拼接规则**：基础地面必须为 `32x32`，平台必须水平方向无缝拼接。
- **物件结构模具（ControlNet）**：静态地形、陷阱、交互物 **强制启用 `Lineart` 或 `Canny` 预处理器**，锁定边缘轮廓。
- **角色头部结构校验（ControlNet）**：当场景标签包含 `head_study`, `portrait_sheet`, `promo_art`, `3_4_validation` 时，允许启用 `Lineart` 锁头部大形；当场景标签包含 `gameplay_sprite` 时，**禁止**用 3/4 头部参考覆盖纯侧视蓝图。

| 规则簇 | 可执行 Tag / 数值 / 约束 | 节点落点 |
| :--- | :--- | :--- |
| **中线偏移深度法则【核心规则】** | `center_line_shift_depth, center_line_off_center_equals_rotation, front_view_center_straight_3q_center_curved`；正面中线居中=扁平；中线偏移=暗示身体转动=深度；中线越偏离中心→转动越大→深度越强；正面中线笔直，3/4视角中线变成凹凸曲线 | Lineart 参考图 + Text Prompt |
| **重叠深度法则【核心规则】** | `overlap_creates_depth, near_overlaps_far, overlap_invasion_rule, more_overlap_more_depth`；重叠=最重要的深度暗示；近处身体部分遮挡远处部分；重叠线“入侵”相邻区域(肩线入侵上臂/上臂线入侵前臂/胸线重叠远侧手臂)；越多重叠→越强深度感 | Lineart 参考图 + Text Prompt + DWPose |
| **四面不等大法则** | `four_sides_unequal, show_multiple_sides_for_depth, primary_surface_largest`；人物有4个面(front/left/back/right)；3/4视角时各面大小不等；主要面最大，远离观众的面更窄；展示越多不同面→深度越强 | Text Prompt + Lineart 参考图 |
| **隐藏半身深度法则** | `hidden_half_depth, near_side_full_far_side_partial, concealment_suggests_depth`；隐藏人物远侧=创造深度错觉；近侧手臂/腿完全可见，远侧只部分可见 | Lineart 参考图 + Text Prompt |
| **侧面视角深度增强** | `side_view_depth_enhancement, side_center_line_inward, head_slight_3q_in_side, feet_angle_outward_in_side`；严格侧面=扁平；深度增强方法：①Center Line从边缘稍微后移→露出远侧一小条身体；②头部从纯侧面转向微3/4；③脚微微外展。**场景分治**：`gameplay_sprite` 继续遵守 pure side view（但允许脚微外展作为微调）；`concept_art / character_sheet / promo_art` 使用全套深度增强 | Lineart 参考图 + DWPose + Text Prompt |
| **前后肢体透视缩放** | `forward_limb_larger, backward_limb_smaller, perspective_exaggeration_on_limbs`；前伸手臂/腿画大（近大远小）；后伸手臂/腿画小；远处的手可轻微夸张缩小 | Text Prompt + Lineart 参考图 |

### 🗄️ 抽屉 3：🏃 [动画与物理]

*(记录：关键帧数、运动模糊、防滑步约束)*

- **通用防滑步原则**：角色/敌人重心锁死在 **Bottom Center (0.5, 0)**。地形/陷阱重心通常为 **Center (0.5, 0.5)**。
- **动画结构模具（ControlNet）**：所有带连续动作的生物关节角色（跑、跳、受击）**强制启用 `DWPose` / `OpenPose` 预处理器**。
- **动作发力顺序（Pose 约束）**：`action_order_crush_to_stretch`, `show_preload_before_release`, `jump_point_full_body_stretch`
- **重心与运动线（Pose 约束）**：`trace_center_of_gravity_shift`, `movement_reference_line_lock`, `waist_rises_first_head_detours`
- **接触受压校验（Pose / Lineart）**：`sitting_contact_compression`, `buttocks_sink_on_seat`, `knee_to_heel_not_overextended`, `seated_thigh_flatten_on_surface`（Hart 补充：大腿与椅面接触时被压扁）
- **复杂姿势回切校验（Pose / Lineart）【核心规则】**：`check_front_and_side_when_lost`；当 `pose_complexity=high`、`limb_overlap=high` 或 `foreshortening_confusion=true` 时，允许先回切 `front_view_structure_check` / `side_view_structure_check` 验证骨架，再返回目标姿势。
- **与纯侧视规则的场景分治**：当场景标签包含 `gameplay_sprite` 时，继续遵守 `pure side view`，但允许在侧视骨架内执行 `crush_to_stretch` 与 `center_of_gravity_shift`；当场景标签包含 `pose_study`、`concept_motion_sheet`、`animation_keypose_sheet` 时，可放开为多视角动作分析。
- **基准帧数**：跑步 8 帧，待机/行走/飞行 4 帧，跳跃 3 帧。

| 规则簇 | 可执行 Tag / 数值 / 约束 | 节点落点 |
| :--- | :--- | :--- |
| **对立力量法则（Contrapposto）【核心规则】** | `opposing_forces, upper_lower_body_counter_direction, contrapposto, major_force_structural, minor_force_aesthetic`；上半身和下半身力量方向相反；力量从脚底向上传递，每个关节是方向改变节点；Major Forces 影响躯干核心（结构性），Minor Forces 影响四肢（装饰性） | DWPose / OpenPose 骨架校验 + Text Prompt |
| **自然站姿 S 曲线法则** | `natural_pose_s_curve, torso_outward_arc_legs_inward_arc, counterbalancing_curves, no_stiff_straight_pose`；自然站姿用曲线（躯干向外弧+腿向内弧=反向平衡）；直线站姿=僵硬/像要倒；Line of Action = S曲线或反S曲线 | DWPose / OpenPose 骨架校验 + Text Prompt |
| **脊柱曲线法则** | `spine_curved_not_straight, spine_determines_upper_lower_relation, back_view_spine_s_curve`；直脊柱=僵硬，弯曲脊柱=自然；Center Line 沿背部是曲线而非直线；脊柱决定上下半身位置关系 | DWPose / OpenPose 骨架校验 + Lineart 参考图 |
| **坐姿重心与接触压缩（补充）** | `seated_gravity_on_hips, seated_upper_body_free`；坐姿重心在臀部（非脚）；坐姿中上半身更自由（不需要平衡）；躯干仍遵循对立力量规则 | DWPose / OpenPose + Lineart 参考图 |
| **步行躯干反转法则** | `walking_torso_counter_rotation, leg_forward_torso_rotates_away, abdomen_line_shows_rotation`；腿前迨时躯干向反方向旋转；腹部线条表达躯干转动；后腿被前腿重叠+轻微阴影 | DWPose / OpenPose 骨架校验 + Text Prompt |

### 🗄️ 抽屉 4：🎨 [光影与材质]

*(记录：特定材质画法、边缘光、全局色调、专属 LoRA 触发词)*

- **探索期基准画风**：
  - 正向：`(high definition pixel art:1.3), (hand-drawn dark outlines:1.2), (warm pastoral atmosphere:1.1), crisp edges, no anti-aliasing`
  - 负向：`blur, gradient, realistic, UI`
- **默认像素风后处理**：生成后必须使用 `nearest-neighbor` 算法缩放至目标尺寸。
- **风格漂移抑制**：批量资产生产时，每连续生成 `3-5` 个资产必须回看概念锚点；结构锁定优先发生在前 `30%-40%` 步数；图生图或局部重绘时 `denoising` 建议保持在 `0.20-0.40`，高于 `0.50` 视为高风险漂移区。

| 规则簇 | 可执行 Tag / 数值 / 约束 | 节点落点 |
| :--- | :--- | :--- |
| **战略性阴影深度法则** | `strategic_shadow_for_depth, shadow_implies_3d, light_shadow_not_heavy_handed`；阴影让物体看起来立体（只有立体物体才能遮挡光源）；避免重手法，几处战略性阴影即可；后腿/远侧肢体加轻微阴影暗示深度 | Text Prompt + 后处理/手动修图 |

### 🗄️ 抽屉 5：⚙️ [AI 硬核参数]

*(记录：CFG 甜区、Denoising 比例、采样器建议)*

- **探索期垫图抽卡（IPAdapter）**：权重建议 `0.6 - 0.8`。
- **标准 SDXL 节点配置（KSampler）**：
  - 大模型：`sd_xl_base_1.0.safetensors`
  - 步数（Steps）：`30 - 40`
  - 提示词引导系数（CFG）：`5.0 - 7.0`
  - 采样器：`euler_ancestral`，调度器：`normal`
- **批量一致性保护**：连续批量出图时，建议在批次之间执行 `Purge Cache`；当需要冻结风格时，优先锁 `seed + LoRA + concept anchor` 组合，而不是只盯 Prompt 文案。

---

## 👾 下半部：实体蓝图库（What）

> 这里只记录每个实体的**纯视觉特征词（长什么样）**。出图时，系统必须自动叠加上半部技法约束；**实体描述不得替代动作、透视、参数与验收规则**。

### 🏃 角色与敌人（Entities）

| 实体名称 | 视觉特征词（Visual Tags） |
| :--- | :--- |
| **Mario**（主角） | `Mario-like character, red cap, blue overalls` |
| **Trickster**（幽灵形态） | `Ghostly trickster character, purple ethereal glow, semi-transparent body` |
| **SimpleEnemy**（基础巡逻怪） | `Cute slime monster, green color` |
| **BouncingEnemy**（弹跳怪） | `Spring-loaded robot enemy, metallic texture` |
| **FlyingEnemy**（飞行怪） | `Bat creature, flapping wings, dark purple` |

### 🧱 地形与平台（Environment & Platforms）

| 实体名称 | 视觉特征词（Visual Tags） |
| :--- | :--- |
| **Ground / Wall**（基础地形） | `stone texture, mossy` |
| **Platform**（普通平台） | `wooden planks texture` |
| **OneWayPlatform**（单向平台） | `thin platform, metal grating texture` |
| **BouncyPlatform**（弹跳平台） | `bouncy mushroom platform, rubbery texture` |
| **CollapsingPlatform**（崩塌平台） | `cracked stone platform, crumbling texture` |
| **MovingPlatform**（移动平台） | `mechanical moving platform, metallic texture with yellow caution stripes` |
| **ConveyorBelt**（传送带） | `conveyor belt segment, industrial mechanical texture` |
| **BreakableBlock**（可破坏方块） | `fragile brick block, cracked terracotta texture` |
| **FakeWall**（伪装墙） | `illusion wall block, slightly faded stone texture` |

### ⚠️ 陷阱与机关（Hazards & Mechanics）

| 实体名称 | 视觉特征词（Visual Tags） |
| :--- | :--- |
| **SpikeTrap**（地刺） | `Sharp metal spikes, rusty metal, pointing upwards` |
| **FireTrap**（火焰陷阱） | `Roaring campfire flame, bright orange and yellow` |
| **PendulumTrap**（摆锤） | `Heavy spiked iron ball, dark metal texture` |
| **SawBlade**（旋转锯片） | `Circular mechanical saw blade, spinning motion blur effect, silver metal` |

### ✨ 交互物与 UI（Interactables & UI）

| 实体名称 | 视觉特征词（Visual Tags） |
| :--- | :--- |
| **Collectible**（收集物 / 金币） | `Shiny gold coin, bright yellow` |
| **Checkpoint**（检查点 / 旗帜） | `Red checkpoint flag on a pole` |
| **GoalZone**（终点门） | `Ornate wooden door, glowing magical aura` |
| **HiddenPassage**（隐藏通道入口） | `Dark mysterious cave entrance, stone archway` |

---

## 终端提交口径（用于 commit / PR）

```text
[Distillation Landing Checklist]
新增规则:
- ...

高价值重复:
- ...

已剔除冗余:
- ...

待确认疑点:
- ...
```

*Last Updated: 2026-04-10 by Manus TA*
*Hart Distillation: 2026-04-10 — Christopher Hart《Figure It Out! Drawing Essential Poses》16 条新增 + 2 条高价值重复合并*
