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
| **成人头身比** | `head_unit=7.0~7.5`（写实基准）；**场景分治**：`manga_style=8_head`（松岡漫画基准），`realistic_style=7_7.5_head`（Hart 写实基准）。两者共存，按出图风格标签自动切换 | 角色蓝图基础值 |
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
| **角色崩坏防治三锚点法则** | `anti_off_model_3_anchors, head_size_locked_across_frames, face_cross_position_locked, eye_gap_locked, nose_mouth_gap_locked, line_weight_uniform_across_batch`；头部大小=全身比例基准单位，帧间/批次间不可变；脸部三锚点：十字线位置 + 眼距 + 鼻嘴距 = 帧间必须一致；线条粗细必须全 Cut/全批次统一。**场景分治（线宽）**：batch 间统一性维持；单一帧内前后层允许线宽差 `foreground_thicker_line_weight, front_detail_thick_line_back_thin_line` | Lineart 参考图 + sprite sheet QA |
| **前臂旋回法则（松岡）** | `forearm_twist_radius_ulna_cross, pronation_forearm_cross, supination_forearm_parallel`；手首回転时前臂尺骨/橈骨交叉 → 前臂轮廓变化；回内(pronation)=两骨交叉(前臂变粗)，回外(supination)=两骨平行(前臂变细) | DWPose / OpenPose 骨架校验 + Lineart 参考图 |
| **大腿骨外側接続法则（松岡）** | `femur_connects_pelvis_outer_side, legs_not_straight_down_from_hip, leg_gap_at_crotch`；大腿骨は骨盤の外側に結合（非真下）→ 脚を腰の真下から描くと不自然 | DWPose / OpenPose 骨架校验 |
| **関節可動域数値表（松岡）** | `ball_joint_shoulder_hip_multidirectional, hinge_joint_elbow_knee_finger_one_axis, elbow_flexion_forward_only, knee_flexion_backward_only, no_hyperextension`；球関節(肩/股)=多方向、蝶つがい関節(肘/膝/指)=一方向屈伸；肘=前方のみ屈曲、膝=後方のみ屈曲、逆方向は不可（AI 常见逆関節エラー防止） | DWPose / OpenPose 骨架校验 |
| **脊柱可動域不対称法則（松岡）** | `spine_flexion_gt_extension, back_arch_limited, forward_bend_double_backward`；前屈可動域 >> 後屈可動域 ≈ 前屈の半分；側屈は前屈の半分程度 | DWPose / OpenPose 骨架校验 |
| **関節角度数値（松岡）** | `leg_abduction_45deg, leg_adduction_20deg, knee_external_rotation_45deg, knee_internal_rotation_20deg, neck_rotation_60deg, neck_tilt_50deg, wrist_extension_70deg, wrist_flexion_90deg`；各関節の具体的可動域角度（ポーズ妥当性チェック用） | DWPose / OpenPose 骨架校验 |
| **女性筋肉不可見法則（松岡）** | `female_muscle_hidden_under_fat, female_smooth_body_contour, male_muscle_visible_surface`；女性キャラの体表に筋肉ディテールを描きすぎない（脂肪層で覆われる）；男性キャラは筋肉表面ライン可見 | Text Prompt + Lineart 参考图 |
| **男性10大筋肉群シルエット（松岡）** | `male_key_muscles_10, sternocleidomastoid_neck_turn, pectoralis_chest_plate, trapezius_shoulder_slope, deltoid_shoulder_cap, biceps_arm_bend, rectus_abdominis_sixpack, latissimus_dorsi_back_v, gluteus_maximus_hip_mass, quadriceps_thigh_front, gastrocnemius_calf_bulge`；男性角色限定：10大筋肉群の体表シルエット影響 | Text Prompt + Lineart 参考图 |
| **拮抗筋連動法則（松岡）** | `antagonist_muscle_pair, biceps_contract_triceps_stretch, arm_bend_biceps_bulge, arm_extend_triceps_visible, knee_bend_quad_stretch_hamstring_contract, calf_bulge_on_toe_point`；腕を曲げる→二頭筋収縮+三頭筋伸展；膝屈曲→前面四頭筋伸展+背面ハムストリング収縮。既有 `contract_stretch_rule` と互補：全身弯曲侧収縮 vs 局部拮抗筋対 | DWPose / OpenPose + Lineart 参考图 |
| **姿勢頭身比換算（松岡）** | `seiza_4_5_head, seated_chair_6_head, forward_bend_4_5_head, arm_span_equals_height`；正座≈4.5頭身、椅子座り≈6頭身、前屈≈4.5頭身、両腕広げ≈身長 | 角色蓝图基础值 + DWPose 校验 |
| **7頭身セグメント比率表（松岡）** | `7head_shoulder_width_1head, 7head_shoulder_to_elbow_1head, 7head_elbow_to_wrist_1head, 7head_throat_to_crotch_2head, 7head_hip_to_knee_2head, 7head_knee_to_ankle_2head`；7頭身キャラの各セグメント比率 | 角色蓝图基础值 + DWPose 校验 |
| **アタリ構築順序法則（松岡）** | `atari_build_order, head_circle_first, spine_line_second, shoulder_chest_hip_third, joint_circles_fourth, limbs_last, flesh_over_skeleton, muscle_fat_detail_last`；①頭の○→②背骨ライン→③肩/胸/腰ライン→④関節○→⑤手足→⑥肉付け | 草図底稿 / Lineart 参考图 |
| **男女シルエット線質差異（松岡）** | `male_angular_straight_lines, female_curved_soft_lines, male_inverted_triangle, female_hourglass, female_knee_inward, female_knock_knee_slight`；男性=角張った直線的ライン/逆三角形、女性=曲線的柔らかいライン/砂時計型/膝内向き | Text Prompt + Lineart 参考图 |
| **側面身体厚み法則（松岡）** | `side_body_depth_half_shoulder_width, female_chest_hip_protrude_beyond_half, side_view_no_center_align, side_parts_offset_for_balance, male_side_belly_thickness_front, male_shoulder_joint_below_clavicle, male_neck_head_toward_back`；横向き身体の厚み=正面肩幅の約1/2；女性は胸/ヒップが1/2より外側に張り出す；横向きパーツを中心に揃えない（重力バランスでずれる）。既有 `side_view_depth_enhancement` と互補（高价值重复） | Lineart 参考图 + DWPose |
| **年齢別頭身テーブル（松岡）** | `age_head_ratio_table, infant_3_4head, kindergarten_4_5head, elementary_5_6head, teen_6_7head, adult_7head, model_8head, sd_chibi_2_3head, hero_boss_9head`；乳児(1-3歳)=3-4頭身、幼稚園児(4-8歳)=4-5頭身、小学生(9-15歳)=5-6頭身、中高生(15-18歳)=6-7頭身、成人=7頭身、モデル=8頭身、SD/ミニキャラ=2-3頭身、ヒーロー/ボス=9頭身。既有 `child_large_head` を年齢段階で細分化拡張 | 角色蓝图年齢分支 |
| **年齢別体型変化ポイント（松岡）** | `infant_no_neck_short_limbs, kindergarten_round_body, elementary_shoulder_gt_head, teen_male_inverted_triangle_start, teen_female_lower_then_upper_develop, adult_female_waist_hip_contrast, adult_male_wide_shoulder_muscle`；各年齢段の体型変化キーポイント | 角色蓝图年齢分支 + Text Prompt |
| **身体の黄金比（松岡）** | `golden_ratio_navel_split, upper_lower_body_1_to_1.618`；へそ位置で上下分割 → 上:下 ≈ 1:1.618 | 角色蓝图基础值 |
| **体型描き分けシルエット法則（松岡）** | `thin_body_bone_visible_clavicle_ribs, muscular_inverted_triangle_sixpack, chubby_trapezoid_torso_short_neck_sloped_shoulder, fat_droop_gravity, body_type_personality_link, thin_nervous_shy, muscular_passionate_stubborn, chubby_warm_social, glamour_cheerful_proud, chubby_outward_stable_pose, thin_inward_closed_pose`；やせ型=骨格ライン露出、筋肉質=逆三角形+シックスパック、ぽっちゃり型=台形+短首+なで肩+脂肪垂れ。体型→性格連想テーブル付き | 角色蓝图体型分支 + Text Prompt |
| **下顎骨性別差異法則（松岡）** | `male_jaw_angular, female_jaw_rounded, jaw_only_movable_skull_bone`；男性下顎骨=角張り、女性=丸み | Text Prompt + Lineart 参考図 |
| **顎関節開閉連動法則（松岡）** | `jaw_rotation_pivot, wide_open_mouth_head_tilts_back, jaw_joint_near_ear`；大きく口を開ける→下顎骨回転+頭蓋骨後傾 | Text Prompt + DWPose |
| **表情筋6筋肉マッピング（松岡）** | `frontalis_eyebrow_raise_surprise, corrugator_frown_anger, orbicularis_oculi_eye_squint, orbicularis_oris_mouth_pucker, zygomaticus_smile_cheek_push, mentalis_chin_wrinkle_pout`；表情筋6筋肉→表情マッピング | Text Prompt |
| **8基本表情パーツ配置テーブル（松岡）** | `expression_8_pattern, smile_eyebrow_down_mouth_corner_up, laugh_mouth_wide_eyes_narrow_eyebrow_arch, sad_tears_eyebrow_inner_up, cry_upper_face_contract_lower_face_release, surprise_eyes_wide_eyebrow_high_arch, anger_eyebrow_close_to_eye_frown_line, fear_eyebrow_center_converge_mouth_corner_down, shy_blush_downcast_gaze_eyebrow_low`；8基本表情の各パーツ配置詳細 | Text Prompt |
| **正面顔パーツ比率法則（松岡）** | `face_thirds_hairline_brow_nose_chin, eye_level_at_face_half, eye_spacing_one_eye_width, nose_width_equals_eye_width, mouth_width_inner_iris_to_iris, ear_between_brow_and_nose_tip`；正面顔の三分割法+横幅基準。既有 `five_eye_width_head, one_eye_gap` を拡張統合 | Text Prompt + Lineart 参考図 |
| **顔輪郭6タイプ→性格連想テーブル（松岡）** | `face_shape_round_cheerful, face_shape_oval_intellectual, face_shape_square_leader, face_shape_inverted_triangle_sensitive_beauty, face_shape_triangle_wise_late_bloomer, face_shape_homebase_lone_wolf`；6種輪郭タイプ→性格連想 | 角色蓝图顔型分支 + Text Prompt |
| **目の男女描き分け7パラメータ（松岡）** | `female_eye_large_low_vertical_droopy_wide_spacing_big_iris_long_lash, male_eye_small_high_horizontal_upturned_narrow_spacing_small_iris_short_lash, female_eye_highlight_large_lower_eyeliner_thick`；目の大きさ/位置/縦横比/傾き/間隔/瞳サイズ/まつ毛 の7軸男女差 | Text Prompt + Lineart 参考図 |
| **顔パーツ男女差3テーブル統合（松岡）** | `female_brow_thin_light_arch_droopy_wide_gap, male_brow_thick_dark_straight_upturned_narrow_gap, female_nose_small_deformed_short_upward, male_nose_large_realistic_long_forward, female_mouth_small_narrow, male_mouth_large_wide, female_cheek_smooth_curved_jaw_slim_pointed, male_cheek_sharp_straight_jaw_angular_rugged`；眉/鼻/口/頬/あご の男女差統合テーブル | Text Prompt + Lineart 参考図 |
| **目の傾き→性格連想テーブル（松岡）** | `upturned_eye_fierce_confident, droopy_eye_gentle_healing, sanpaku_eye_cool_cruel_calculating, jitome_eye_suspicious_contempt, dead_fish_eye_no_highlight_despair, highlight_near_pupil_strong_gaze, highlight_far_from_pupil_vacant_look, no_highlight_dead_fish_eyes_despair`；つり目/たれ目/三白眼/ジト目/死んだ魚の目 + ハイライト位置→印象 | Text Prompt |
| **眉形→性格連想テーブル（松岡）** | `brow_normal_natural, brow_parallel_gentle, brow_upward_fierce, brow_downward_timid`；眉の4タイプ→性格連想 | Text Prompt |
| **鼻のデフォルメ段階法則（松岡）** | `nose_deform_level, nose_realistic_full_detail, nose_semi_ku_shape, nose_line_only_bridge, nose_omit_cute_style`；リアル→「く」の字型→鼻筋のみ直線→省略 の4段階 | Text Prompt（スタイル分岐） |
| **口の母音形状テーブル（松岡）** | `mouth_a_wide_open, mouth_e_horizontal_spread, mouth_i_horizontal_pull, mouth_o_round, mouth_u_pucker`；あ(a)/え(e)/い(i)/お(o)/う(u) の口形状（リップシンク参考） | Text Prompt |
| **耳のデフォルメ法則（松岡）** | `ear_deform_3_and_6_symbol, ear_realistic_helix_tragus_lobe, ear_position_brow_to_nose_tip, ear_accessory_earring_personality, animal_ears_fantasy_character`；デフォルメ：数字の3と6を組み合わせ。位置：眉から鼻先の間 | Text Prompt |
| **髪3束分割法則（松岡）** | `hair_3_sections_bangs_side_back, hair_flow_from_crown_whorl, hair_volume_outside_head_atari, head_roundness_under_hair, male_hair_upward_then_down_flow, male_stray_hair_flyaway`；髪を前髪/サイド/後ろ髪の3束に分割；つむじ起点；頭部の丸みを意識；男性髪は頭頂部で一度上に向かい下に流れる | Text Prompt + Lineart 参考図 |
| **髪型→性格連想テーブル（松岡）** | `female_short_hair_cheerful_active, female_medium_hair_calm_mature, female_long_hair_feminine_wave_soft, female_bun_hair_back_line_clear, male_short_hair_clean_fresh, male_medium_hair_ear_hidden_outward_tips, male_long_hair_wild_arrangement, female_hair_inward_gentle, male_hair_outward_active`；髪型/髪の流れ方向→性格連想 | Text Prompt |
| **年齢別顔変化法則（松岡）** | `infant_face_round_chin_small_eyes_below_center, teen_face_elongating_chin_sharpening, adult_face_oval_eyes_horizontal, elder_face_cheekbone_chin_prominent_wrinkles, wrinkle_20s_none, wrinkle_40s_forehead_nasolabial, wrinkle_60s_eye_corner_mouth, wrinkle_80s_deep_all_areas`；年齢別の顔変化+シワ段階法則 | 角色蓝图年齢分支 + Text Prompt |
| **球体意識法則（砂糖）** | `every_circle_is_a_sphere, sphere_cross_section_for_direction, head_as_squashed_sphere, direction_over_accuracy`；全ての丸を球体として意識して描く；球に断面線を入れて向きを明示；頭=上下から潰れた球体；正確さより方向感が重要 | 草図底稿 / Lineart 参考図 |
| **フラワーサック胴体法則（砂糖）** | `flour_sack_torso_metaphor, rubber_bag_stretch_squash, ribcage_pelvis_fixed_size_bag_stretches`；胴体=「中身の詰まった袋」として動勢を表現；胸郭球と骨盤球は固定サイズ、間のゴム袋が伸縮；海外アニメーター標準メタファー。既有 `contract_stretch_rule` と互補：全身弯曲法則 vs 胴体特化メカニズム（高价值重复） | DWPose / OpenPose + Lineart 参考図 |
| **胸骨盤独立回転法則（砂糖）** | `ribcage_pelvis_independent_rotation, torso_twist_creates_gesture, tilt_angle_diff_equals_gesture_intensity`；胸郭と骨盤は独立して動く→間の胴部が伸び縮み；胸郭の向き≠骨盤の向き→ねじれが動勢を生む；傾き角度差=動勢の強さ。既有 `torso_3block_twist` と互補：3分割ひねり=激しい動作、本条=基本原理（高价值重复） | DWPose / OpenPose + Lineart 参考図 |
| **円柱四肢法則（砂糖）** | `limbs_as_flexible_cylinders, wrapping_line_shows_direction, organic_curves_for_limbs, leg_cylinder_taper_to_ankle, arm_as_one_or_two_cylinders_impression_based, hand_as_mitten`；腕脚=伸び縮みする円柱；ラッピングラインで方向を示す；人体は有機的→曲線で描く；脚は足首に向かって細くなる；腕は印象に応じて1本or2本の円柱；手はミトン程度にざっくり | Lineart 参考図 + DWPose |
| **Loomis簡略化頭部法則（砂糖）** | `loomis_simplified, cranial_sphere_plus_facial_plane_cut, neck_as_cylinder, face_cross_line_for_direction`；頭を脳頭蓋（球体）+顔面頭蓋（平面カット）に分割；側頭部をナイフで切り落とした断面=側面；首=筒；十字線で顔の方向を示す | 草図底稿 / Lineart 参考図 |
| **パンツ法骨盤方向確認法則（砂糖）** | `pants_visualization_for_pelvis_direction, waist_circle_plus_leg_root_circles`；骨盤の向きが不明な時、パンツを履かせたイメージで確認；ウエスト部分の円+2本の脚付け根の円→骨盤の向き・傾きが明確に | 草図底稿 / DWPose |
| **全身骨格ランドマーク5区分法則（みにまる）** | `skeleton_landmark_5_zones, front_5_zones = {skull / ribcage / pelvis / arm / leg}, back_4_landmarks = {occipital_bump / C7_vertebra / scapula / sacrum}, skull_side_wider_than_front, eye_level_at_skull_rectangle_center, zygomatic_arch_below_eye_socket, external_ear_canal_at_side_center, inner_ankle_higher_than_outer_ankle`；前面5区域(頭蓋骨/胸郭/骨盤/腕/脚)+背面4ランドマーク(後頭部凸部/第7頸椎/肩甲骨/仙骨)；頭蓋骨は正面より側面が幅広い；目の位置=長方形中央；くるぶし内側(脛骨)>外側(腓骨)。松岡 `skeleton_landmark` 系規則の解剖学的根拠を補強（高价值重复） | Lineart 参考図 + DWPose / OpenPose |
| **胸郭箱化ルール（みにまる）** | `ribcage_box_rule, ribcage_height_1_5_head, ribcage_width_2_head, ribcage_depth_1_head, ribcage_egg_shape_from_box, chin_to_clavicle_half_head, clavicle_bicycle_handle_curve, sternum_necktie_shape_midline, rib_bottom_landmark_for_waist`；胸郭の箱：縦=頭1.5個/横=頭2倍弱/奥行き=頭1個→卵型に削り出す；アゴ→鎖骨=頭半分；鎖骨=自転車ハンドルのカーブ；胸骨=ネクタイ形状で正中線基準；肋骨下部凸部=腰くびれ/骨盤距離の目安。既有 `ribcage_pelvis_fixed_size_bag_stretches`（砂糖）の解剖学的寸法を補完（高价值重复） | Lineart 参考図 + 草図底稿 |
| **肩甲骨連動ルール（みにまる）** | `scapula_clavicle_pair, scapula_moves_with_arm, scapula_elevation_arm_raise, scapula_abduction_arm_forward, scapula_adduction_chest_out_angel_wings, acromion_shoulder_joint`；肩甲骨=鎖骨と対で肩峰で接続；腕の動きに連動：挙上(肩上げ)/外転(腕前出し→肩甲骨外開き)/内転(胸張り→天使の羽)。**AI出図ルール**：腕前出しポーズ→背面で肩甲骨外開き；胸張り→肩甲骨内寄せ | Lineart 参考図 + DWPose / OpenPose |
| **首の骨のど仏ルール（みにまる）** | `adam_apple_thyroid_cartilage, cricoid_cartilage, hyoid_bone_chin_triangle, measuring_cup_shape_thyroid, chin_ear_hyoid_triangle_from_below, male_adam_apple_sexy`；のど仏=甲状軟骨(計量カップ形)+輪状軟骨；舌骨=アゴ下筋肉集中点；下から見た顔：アゴ先+エラ+舌骨=三角形；男性キャラ→のど仏描写でセクシーな印象 | Text Prompt + Lineart 参考図 |
| **全身筋肉5区分詳細体系（みにまる）** | `muscle_5_zones_detailed, zone1_neck_shoulder_chest = {sternocleidomastoid / trapezius / deltoid_3part / pectoralis_major_fish_shape}, zone2_abdomen = {rectus_abdominis_8pack_navel_at_bottom / obliques_connect_ribcage_pelvis / serratus_anterior_fan_shape_ribs5to9}, zone3_back = {trapezius_diamond / latissimus_dorsi_largest / teres_major / erector_spinae_valley}, zone4_arm = {biceps_ellipse_toward_hand / brachialis_under_biceps / triceps_long_tendon / brachioradialis_thumb_side / forearm_flexor_extensor_split_by_ulna}, zone5_leg = {quadriceps_cylinder / adductors_triangle / sartorius_s_curve / tibialis_anterior / peroneus}`；全身筋肉の起始停止+フォルム+描画手順。松岡 `male_key_muscles_10`（シルエットレベル）の解剖学的深化（高价值重复）。**描画手順**：①固いランドマーク(胸郭+骨盤)→②柔らかい腹筋で繋ぐ；腕：①ポーズ→②尺骨設定(小指→ヒジ)→③屈筋/伸筋分割 | Lineart 参考図 + Text Prompt |
| **骨盤箱化ルール（みにまる）** | `pelvis_box_rule, pelvis_3_landmarks = {anterior_superior_iliac_spine / pubic_symphysis / sacrum}, pelvis_height_half_head, pelvis_width_eq_ribcage, pelvis_trapezoid_tapers_down, pelvis_side_tilts_forward, sacrum_v_shape_back, male_pelvis_tall_narrow, female_pelvis_wide_short, male_pubis_forward, female_pubis_backward_bigger_buttocks`；骨盤3ランドマーク→箱化：縦=頭半分/横幅≈胸郭/下すぼまり台形/側面前傾；**男女差**：男性=縦長狭い+恥骨前/女性=横広短い+恥骨後→お尻大きい。松岡 `male_shoulder_gt_hip, female_hip_eq_shoulder` の骨格根拠（高价值重复） | Lineart 参考図 + 角色蓝图基础值 |
| **腕の骨格回内ルール（みにまる）** | `arm_bone_pronation_rule, ulna_pinky_side, radius_thumb_side, pronation_radius_crosses_ulna, natural_standing_slight_pronation, forearm_as_flat_board_simplified`；尺骨(小指側)+橈骨(親指側)；回内→橈骨が尺骨上に交差；自然立ち=手のひら側面向き=軽い回内=骨交差状態；前腕の単純化=2本合わせて板状フォルム。松岡 `forearm_twist_radius_ulna_cross` と互補：松岡=体表変化、みにまる=骨格メカニズム（高价值重复） | Lineart 参考図 + DWPose |
| **脚の骨格ルール（みにまる）** | `leg_bone_rule, greater_trochanter_lateral_protrusion, femur_not_straight_down_lateral_first, patella_closer_to_femur, tibia_inner_fibula_outer, inner_ankle_higher_than_outer`；大転子=股関節から横に飛び出す；大腿骨=一度横に出てからヒザへ斜め；膝蓋骨=大腿骨寄り；脛骨(内)/腓骨(外)；くるぶし内側>外側。松岡 `femur_connects_pelvis_outer_side` と互補：松岡=描画ルール、みにまる=骨格構造（高价值重复） | Lineart 参考図 + DWPose |
| **横顔比率体系（みにまる）** | `profile_face_ratio, profile_atari_vertical_box_with_cranial_ball, eye_at_box_quarter_width, brow_eye_distance_eq_eye_height, nose_bridge_at_brow_chin_midpoint, mouth_at_nose_chin_midpoint, E_line_brow_nose_lip_protrude, ear_at_profile_center_eye_height, hairline_equal_spacing_chin_nose_brow, occiput_peak_at_brow_height, neck_angled_not_vertical`；横顔アタリ：縦長箱+脳頭蓋ボール+アゴ；Eライン=鼻とアゴを結ぶ線(眉間/鼻/上唇がはみ出す=美しい)；後頭部最凸部=眉の高さ；首は垂直でなく斜め。**バリエーション**：青年男性=目細く+アゴしっかり；子ども=目大きく+アゴ小さく上位置；10代女性=アゴ小さめ | Text Prompt + Lineart 参考図 |
| **斜め顔描画体系（みにまる）** | `three_quarter_face_rules, box_centerline_ne_eye_centerline, nose_higher_than_eye_line, brow_bridge_trapezoid_atari, eye_level_changes_with_camera_angle, face_triangle_balance = {brow_eye_nose_triangle_for_balance}`；斜め顔=立体感で間違いやすい；**鉄則**：箱の中心線≠両目の中心線（鼻は目より高い）；眉間アタリ=台形；EL変化でアオリ/俯瞰の目角度が変わる；眉・目・鼻を三角形シルエット内に配置→バランス安定。松岡 `face_low_angle / face_high_angle` の基礎原理（高价值重复） | Text Prompt + Lineart 参考図 |
| **アオリ顔裏技3解決策（みにまる）** | `low_angle_face_3_solutions, solution1_shadow_only_no_jawline = soft_impression, solution2_diagonal_low_angle = near_jaw_only_plus_cheek_softline, solution3_avoid_chin_above_horizontal, profile_rotate_to_low_angle_trick, grid_lines_5 = {crown / brow / nose / chin / hyoid}`；アオリ描画裏技：横顔を描いてから回転；**アゴ問題3解決策**：①影だけで表現(線なし)→優しい印象；②斜めアオリ→手前側下アゴ+ほっぺた柔ライン；③アゴ先と下アゴが水平以上の角度を避ける。**AI出図ルール**：`low angle face`は斜めアオリ推奨、正面アオリはアゴ崩れリスク高 | Text Prompt + Lineart 参考図 |
| **手のひら9ステップ描画法（みにまる）** | `hand_palm_9step, step1_palm_square_half_split, step2_emotion_line_crease_split, step3_thumb_position_MP_joint_outside_palm, thumb_tip_reaches_index_2nd_joint, step4_four_fingers_middle_longest, step5_2nd_joint_arc_1st_joint_arc, step6_fingers_as_cylinders_or_boxes, step7_round_fingertips, step8_3_pads_thenar_hypothenar_finger_base, wrist_W_shape, step9_outer_contour_thick_inner_thin, dorsal_view_fingers_appear_longer, fist_box_plus_thumb_on_2nd_joint`；手のひら側9ステップ：①手のひら四角(指=全体の半分)→②シワで分割→③親指位置(先端=人差し指第二関節)→④4指(中指最長)→⑤関節弧→⑥円柱/四角化→⑦指先丸く→⑧3膨らみ+手首W形→⑨外輪郭太/内細。手の甲=指が長く見える；拳=箱+親指。松岡 `hand_3block` の詳細展開（高价值重复） | Lineart 参考図 + Text Prompt |
| **足の描画5ステップ体系（みにまる）** | `foot_drawing_5step, foot_simplify_4parts = {ankle_ball / heel_pudding / instep_arc / toes}, side_pinky_flat_big_toe_arch, instep_top_bump_landmark, toe_3groups = {big_toe_upward / middle_3_ball_crush / pinky_oval_curl}, front_5step = {step1_silhouette_ankle_triangle / step2_3part_split / step3_outer_contour_wrapping / step4_bump_big_toe_base_pinky_metatarsal / step5_instep_triangle_shading}`；足の図形化4パーツ(くるぶしボール/かかとプリン/甲弓形/つま先)；側面：小指側=接地/親指側=アーチ；指3グループ(親指上向き/中3本ボール潰れ/小指楕円巻き込み)；正面5ステップ。松岡 `foot_3block` の詳細展開（高价值重复） | Lineart 参考図 + Text Prompt |
| **頭蓋骨+顎骨2パーツ構造法則（OCHABI）** | `skull_jaw_2part_structure, child_skull_large_round, adult_jaw_developed_elongated, age_expression_by_skull_jaw_ratio`；顔=頭蓋骨+顎骨の2パーツ構造；子ども=頭蓋骨大きくまん丸→顎骨未発達；大人=顎骨発達→縦長。頭蓋骨と顎骨の比率変更だけで年齢表現可能。既有 `loomis_simplified`（砂糖）の年齢応用拡張 | Lineart 参考図 + 角色蓝图年齢分支 |
| **顔の6等分比率法則（OCHABI）** | `face_6_division_grid, eye_at_half_face, nose_at_one_third_face, mouth_at_one_sixth_face, midline_at_face_width_center, ratio_shift_changes_age_impression`；顔を縦6等分→目(1/2位置)/鼻(1/3位置)/口(1/6位置)で配置；比率を微調整→大人っぽい(目と鼻が中心から遠い)/子どもっぽい(近い)。既有 `face_thirds_hairline_brow_nose_chin`（松岡3分割）の細分化。**場景分治**：精密設定稿/キャラデザ=6等分、ゲームスプライト=3分割で十分 | Text Prompt + Lineart 参考図 |
| **オノマトペ線質法則（OCHABI）** | `onomatopoeia_line_quality, gatagata_angular_short_lines, chikuchiku_sharp_radial_lines, fuwafuwa_soft_curves, gasagasa_rough_overlapping, mosamosa_dense_curves, gushagusha_chaotic_cross`；線の速度/強さ/太さで質感を表現：がたがた=角張った短線、ちくちく=鋭い放射線、ふわふわ=柔らかい曲線、がさがさ=粗い重ね線。既有 `CSI_line_vocabulary`（砂糖）の下位具体化：C/S/I=印象レベル、オノマトペ=質感レベル（高价值重复） | Text Prompt + Lineart 参考図 |
| **Rule of 1/3 動物通用3部分法則（Peter Han）** | `animal_rule_of_thirds, head_3part = {muzzle / cranium / neck}, torso_3part = {chest / belly / hip}, limb_3part = {shoulder_hip_joint / elbow_knee / ankle}, tail_extra_string, applies_to_all_land_animals`；全ての陸上動物のHead/Torso/Limbsは各3パーツに分割可能；尾=追加のString。Balloon on a String法と組み合わせて使用。**AI出図ルール**：モンスター/動物キャラデザイン時、3パーツ分割でアタリを取る | Lineart 参考図 + 草図底稿 |
| **Blending Forms + Segmentation 形体融合法則（Peter Han）** | `blending_forms_wrinkle_fat_muscle_fur, segmentation_volume_communication, hair_fur_crest_of_wave_pattern, criss_cross_hatch_curves_arches`；簡単な形体をwrinkles/fat/muscle/furで融合→統一感；Segmentation=分節による体積伝達（昆虫の外骨格/爬虫類の鱗）；毛髪/毛皮=波パターンの波頂に標示。既有 `flour_sack_torso_metaphor`（砂糖）を全身+動物+昆虫に拡張（高价值重复） | Lineart 参考図 + Text Prompt |
| **Exaggeration for Iconic Sketch 法則（Peter Han）** | `exaggeration_for_iconic_sketch, find_unique_form_color_action, slight_exaggeration_communicates, movement_pose_elevates_sketch`；動物/キャラの独特な形態/色/動作を見つけ→軽い誇張→アイコニックなスケッチに昇華；動きとポーズが比例/形状を超えた生命感を与える。既有 `exaggerate_LoA_curve`（砂糖）/`gesture_exaggeration_principle`（みにまる）と互補：砂糖=動的誇張、みにまる=ジェスチャー誇張、Peter Han=デザイン伝達目的の誇張（高价值重复） | Text Prompt + Lineart 参考図 |
| **線質材質表現法則（Telecom2）** | `line_expresses_material_texture, grass_line_soft_irregular, skin_line_smooth_flowing, metal_line_sharp_uniform, cloth_line_soft_wavy, stone_line_rough_angular`；動画のクリーンアップ線は対象の材質を表現しなければならない：草=柔らかく不規則、肌=滑らかに流れる、金属=鋭く均一、布=柔らかく波状、石=粗く角張る。既有 `CSI_line_vocabulary`（砂糖）=印象レベルC/S/I比率、本条=材質レベルの具体線質指定（下位具体化） | Text Prompt + Lineart 参考図 |
| **重叠深度エラーパターン法則（Telecom2）** | `overlap_depth_error_pattern, elbow_forward_of_upper_arm_correct, elbow_behind_upper_arm_flat_error, forearm_overlap_upper_arm_shows_depth`；前腕が上腕を重ねて遮る=正しい奥行き表現；上腕が前腕の前に出る=奥行きエラー(扁平化)。**AI出図ルール**：腕の重なり方向を確認→前腕が手前=OK、上腕が手前=NG（ControlNet線画で矯正） | Lineart 参考図 + DWPose / OpenPose |
| **転頭時顔幅透視変化法則（Telecom2）** | `head_turn_face_width_foreshortening, oblique_angle_narrow_face, frontal_full_width_face, head_is_3d_sphere, shoulder_width_also_changes`；転頭アニメーション中、顔幅は球体透視に従い変化：斜め=顔幅狭い、正面=フル幅。肩幅も同様に変化。既有 `head_turn_hierarchy` =視線>頭>体の層級、本条=転頭時の幅変化（補充関係） | DWPose / OpenPose + Lineart 参考図 |
| **キャラモデル一貫性4指標チェックリスト（Telecom2）** | `character_model_consistency_4check, check_chin_shape, check_eyebrow_angle_thickness, check_hair_volume_silhouette, check_eye_size_ratio`；同一キャラを複数回AI生成する際の崩壊防止4指標：①アゴ形状②眉角度/太さ③髪ボリューム/シルエット④目サイズ比率。**場景分治**：sprite sheet内(帧間)=既有 `anti_off_model_3_anchors`（頭サイズ+十字線+眼距+鼻口距+線幅）；batch間(複数回生成)=本条4指標。IPAdapter weight一貫性と併用 | IPAdapter + sprite sheet QA |
| **立ちポーズ片足重心強制法則（Telecom2）** | `standing_pose_contrapposto_mandatory, weight_on_one_leg, no_symmetric_attention_pose, relaxed_standing_hip_tilt`；立ちキャラは必ず片足に重心を置く(コントラポスト)。左右対称の「気をつけ」ポーズ=硬直/不自然→禁止(意図的な軍人/ロボット表現を除く)。既有 `opposing_forces`（コントラポスト原理）の具体適用約束。**AI出図ルール**：`relaxed standing pose, weight on one leg, slight hip tilt` | DWPose / OpenPose + Text Prompt |
| **服シワ放射法則（室井）** | `wrinkle_radial_joint_origin, wrinkles_radiate_from_joints, shoulder_elbow_knee_hip_as_wrinkle_origin, pants_line_crotch_cross_origin, wrinkle_lines_NOT_converge_single_point, sleeve_wrinkle_follows_arm_cross_section`；服のシワは関節(肩/肘/膝/股)から放射状に発生。パンツライン=股の交差線がシワ起点。シワ線が一点に集中するのは×→意識的にずらす。袖のシワ=腕の断面に沿って巻き付く。既有 `wrinkle_5_types`（みにまる）=5分類体系、本条=「どの関節から発生するか」の起点定位法（補完関係）。**AI出図ルール**：`wrinkles radiating from joints, cloth tension lines from shoulder/elbow/knee` | Text Prompt + Lineart 参考図 |
| **スカート円錐モデル法則（室井）** | `skirt_cone_model, skirt_as_cone_from_waist, pleats_follow_cone_surface, mini_skirt_short_cone, long_skirt_tall_cone, leg_cross_section_for_long_skirt_wrinkles`；スカートは腰から下がる円錐として描く。プリーツ=円錐表面の溝。ミニスカート=短い円錐、ロングスカート=長い円錐。足の断面線を意識してシワを入れる。**AI出図ルール**：`skirt as cone shape from waist, pleats following cone surface` | Text Prompt + Lineart 参考図 |
| **頭首肩一体法則（室井）** | `head_neck_shoulder_unit, head_neck_shoulder_as_single_block, arm_raise_shoulder_rises_neck_shortens, opposite_shoulder_drops_head_tilts, never_draw_head_neck_shoulder_separately`；頭/首/肩は分離して描かない→一つのブロック。腕を上げると肩も上がり首が短くなり、反対側の肩が下がり頭も傾く。分離描画=不自然な動き。**AI QA**：DWPose で肩/首/頭の連動が正しいかチェック | DWPose / OpenPose + Text Prompt |
| **顔立体3問題修正法則（室井）** | `face_3d_correction_3rules, face_wrap_around_sphere, neck_diagonal_rear_attachment, jaw_line_partial_omission, NOT_flat_face, NOT_vertical_neck, NOT_connected_jaw_line`；顔立体感3問題：①顔面は球体→目/鼻は球面上に配置(wrap)②首は斜め後ろから接続(垂直接続×)③顎ラインの一部を省略すると立体的。既有 `head_turn_face_width_foreshortening`（Telecom2）=回転時の幅変化、本条=静止時の立体感修正（補完関係）。**AI negative prompt**：`flat face, vertical neck, connected jaw line` | Text Prompt + Lineart 参考図 |
| **指軸ずれ扇状法則（室井）** | `finger_axis_offset_fan, fingers_NOT_parallel, fan_shaped_finger_spread, wrist_palm_axis_offset, nail_midline_shows_3d, finger_upper_bony_convex_lower_fleshy_concave, all_fingertips_converge_to_palm_center_when_bent, foot_triangle_rectangle_2parts`；指は平行に並ばない→扇状に広がる。手首と掌の軸がずれる。爪と指の中線で立体感。指上面=骨ばって反り(凸)、下面=柔らかく肉付き(凹)。指を曲げると全指先が掌中心に収束。足=前(三角)+後(矩形)の2パーツ。既有 `hand_3block_thumb_palm_fingers`（松岡）=手の3ブロック分割、本条=指の詳細配置法（補完関係）。**AI negative prompt**：`parallel fingers, stiff hand` | Text Prompt + Lineart 参考図 |
| **デフォルメ5段階関節ロック法則（室井）** | `deform_5stage_joint_lock, stage1_photo, stage2_lower_head_ratio_keep_joints, stage3_stick_figure_trace, stage4_add_volume_expression, stage5_further_deform_shrink_body, joint_ratio_preserved_across_deform, deform_level_maps_to_img2img_denoise`；実写→キャラ化5段階：①元写真→②頭身を下げる(関節位置は維持)→③棒人間トレース→④立体感+表情追加→⑤さらにデフォルメ(体を小さく)。関節位置の維持が鍵。**AI出図WF**：img2img denoising段階的に上げる + DWPose関節位置固定。★ゲームキャラデフォルメに直結 | DWPose / OpenPose + img2img denoise設定 |

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
| **侧面视角深度增强** | `side_view_depth_enhancement, side_center_line_inward, head_slight_3q_in_side, feet_angle_outward_in_side`；严格侧面=扁平；深度增强方法：①Center Line从边缘稍微后移→露出远侧一小条身体；②头部从纯侧面转向微3/4；③脚微微外展。**场景分治**：`gameplay_sprite` 继续遵守 pure side view（但允许脚微外展作为微调）；`concept_art / character_sheet / promo_art` 使用全套深度增强。松岡互補：`side_view_no_center_align, side_parts_offset_for_balance`（横向きパーツを中心に揃えない=同一法則の別表現） | Lineart 参考图 + DWPose + Text Prompt |
| **関節ライン収束法則（松岡）** | `joint_lines_converge_to_vp, front_joint_lines_parallel, angled_joint_lines_tilted, rear_angle_joint_lines_converge`；斜め向きポーズでは肩/肘/手首/膝の対になる関節を結ぶ線がパースの消失点に向かう。正面=平行、斜め=傾斜、斜め後ろ=消失点に収束。既有 `five_alignment_lines` の具体化（高价值重复） | DWPose / OpenPose + Lineart 参考图 |
| **アオリ/フカン身体パース変形法則（松岡）** | `low_angle_feet_large_head_small, high_angle_head_large_feet_small, angle_joint_line_tilt_increases, box_method_perspective_check, cylinder_cross_section_for_body_depth, low_angle_elongate_lower_legs, high_angle_enlarge_upper_body`；アオリ(見上げ)=足>腰>肩>頭（下→上縮小）、フカン(見下ろし)=頭>肩>腰>足（上→下縮小）；BOX法で校验可。**场景分治**：`gameplay_sprite`=使用禁止；`promo_art / concept_art / character_sheet`=使用可 | Text Prompt + DWPose / OpenPose + Lineart 参考図 |
| **顔アオリ/フカンパーツ変形テーブル（松岡）** | `face_low_angle_forehead_narrow_eye_brow_gap_wide_droopy_eye_chin_area_large_jaw_round, face_high_angle_eye_brow_gap_narrow_eye_upturned_nose_long_chin_area_compressed, face_low_angle_horizontal_curve_up, face_high_angle_horizontal_curve_down, face_midline_curves_toward_viewer, cylinder_face_guide`；顔のアオリ/フカン時のパーツ変形詳細+アタリ曲線方向。**场景分治**：`gameplay_sprite`=不使用；`concept_art / promo_art / character_sheet`=使用可 | Text Prompt + Lineart 参考図 |
| **後頭部つむじ位置法則（松岡）** | `crown_whorl_slightly_behind_top, low_angle_back_head_slim_cheek, high_angle_back_head_full_cheek, neck_starts_from_inner_chin`；つむじ=頭頂部の少し後ろ；アオリ後頭部=頬ほっそり、フカン後頭部=頬ふっくら | Text Prompt + Lineart 参考図 |
| **魚眼パース法則（松岡）** | `fisheye_perspective, circular_distortion_line, body_fits_in_circle, extreme_foreshortening`；魚眼=円形ラインの効いたパース表現。**场景分治**：`gameplay_sprite`=使用禁止；`promo_art / key_visual / boss_intro`=使用可 | Text Prompt + Lineart 参考図 |
| **前面太線パース強調法則（松岡）** | `foreground_thicker_line_weight, 3_depth_layers_front_mid_back, front_detail_thick_line_back_thin_line`；前面/中面/後面の3部分に分けて遠近感；最前面を太い線で描くとパース強調。**場景分治（線幅）**：batch間統一性は維持しつつ、単一フレーム内の前後レイヤーでは線太さに差をつけてOK | Lineart 参考図 + Text Prompt |
| **前后肢体透视缩放** | `forward_limb_larger, backward_limb_smaller, perspective_exaggeration_on_limbs`；前伸手臂/腿画大（近大远小）；后伸手臂/腿画小；远处的手可轻微夸张缩小 | Text Prompt + Lineart 参考图 |
| **円柱圧縮法則（砂糖）** | `cylinder_foreshortening, shorter_length_wider_cross_section, foreshortened_cylinder_ellipse_larger`；円柱が手前に向かってくる→圧縮発生：長さが短くなり+断面の楕円が大きく見える。既有 `forward_limb_larger` のメカニズム解説（高价值重复） | Lineart 参考図 + Text Prompt |
| **関節オーバーラップ深度法則（砂糖）** | `overlap_at_joints, depth_without_wrapping_lines, curve_direction_at_joint_shows_depth, knee_as_gap_between_two_cylinders`；関節端にオーバーラップラインを引くだけで円柱の向きを示せる（ラッピングライン不要）；カーブの向きで円柱の向きが変わる→奥行き表現；膝=2本の円柱の間のギャップ。既有 `overlap_creates_depth` の具体テクニック（高价值重复） | Lineart 参考図 + DWPose |
| **Cカーブ方向=円柱方向法則（砂糖）** | `C_curve_direction_per_limb_segment, commit_to_direction_even_if_uncertain`；各パーツ（腕/脛/足首/脚つけ根）のCカーブの向き=そのセグメントの3D方向；微妙な角度でも「真っすぐにしておこう」はNG→方向をコミットする；間違えたら逆にすればいい | Lineart 参考図 + DWPose |
| **ラッピングライン立体感法則（みにまる）** | `wrapping_line_3d_effect, wrapping_line_around_cylinder_shows_volume, T_intersection_overlap_depth, controlnet_wrapping_line_improves_3d`；ラッピングライン=円柱をぐるりと回る線→立体感を示す；腕や脚の節目に描く→向きと奥行き感；オーバーラップ=T字交差で前後関係明確化。既有 `wrapping_line_shows_direction`（砂糖）のAI出図ルール追加：**ControlNetの線画でラッピングラインを明示すると立体感向上**（高价值重复） | Lineart 参考図 + ControlNet |
| **逆パース禁止ルール（みにまる）** | `reverse_perspective_forbidden, near_edge_larger_far_edge_smaller, box_draw_3step = {step1_find_nearest_point / step2_near_large_far_small / step3_parallel_lines_to_VP}`；近い辺が大きく、遠い辺が小さい。逆になると「逆パース」；箱の描画3ステップ：①一番手前はどこ？→②近いものは大きく遠いものは小さく→③平行線は同じ消失点へ。既有 `box_method_perspective_check`（松岡）の具体的エラー防止ルール（高价值重复）。**AI出図ルール**：箱型パーツ(胸郭/骨盤)のパースが逆転していたらControlNetで矯正必須 | Lineart 参考図 + ControlNet |
| **円柱EL-楕円関係ルール（みにまる）** | `cylinder_EL_ellipse_rule, near_EL_flat_ellipse, far_from_EL_round_ellipse, cylinder_draw_order = {centerline_first / 90deg_cross / ellipse_on_cross}, limb_wrapping_line_ellipse_matches_EL`；円柱のEL(アイレベル)と楕円の関係：ELに近い円=薄く(平たく)、ELから離れるほど正円に近づく；腕・脚のラッピングラインに直結：ヒザがEL近くなら楕円薄く、足元は正円に近い；描画手順：中心線→90度クロス→楕円。既有 `cylinder_foreshortening`（砂糖）のELメカニズム補足（高价值重复） | Lineart 参考図 + Text Prompt |
| **単純化図形化体系（みにまる）** | `body_simplification_system, step1_ribcage_box_pelvis_box_fixed_landmarks, step2_limbs_as_cylinders, step3_abdomen_soft_connection, ratio_based_placement_head_units, grid_division_for_part_placement`；人体の単純化手順：①胸郭箱(卵型)+骨盤箱(台形)→固いランドマーク先置き→②腕・脚=円柱→③腹部=柔らかい接続部；比率で描く(頭基準で何個分)；グリッド分割でパーツ位置決定。既有 `atari_build_order`（松岡）と互補：松岡=順序、みにまる=図形化方法（高价值重复） | Lineart 参考図 + 草図底稿 |
| **○△□→3Dベーシックシェイプ2段階図形化法則（OCHABI）** | `2stage_shape_simplification, stage1_flat_circle_triangle_square, stage2_3d_cuboid_cylinder_cone_sphere, any_object_decomposable, vase_eq_cylinder_plus_cone, house_eq_triangular_prism_plus_cuboid, cup_eq_cylinder, lemon_eq_cone_plus_sphere`；全ての物体を2段階で図形化：①平面○△□でシルエット把握→②立体ベーシックシェイプ(直方体/円柱/円すい/球体)に置換。既有 `body_simplification_system`（みにまる）=人体限定、本条=万物対象の上位原理。**AI出図ルール**：新規アセットのコンセプトアート段階でまずベーシックシェイプ分解を行い、ControlNet用線画を作成 | Lineart 参考図 + 草図底稿 |
| **最適描画角度選択法則（OCHABI）** | `optimal_drawing_angle_selection, choose_angle_for_simplest_shape, rotate_object_mentally_for_basic_shape, depth_conveying_angle_priority`；複雑な物体も角度を変えると基本図形に置き換えやすくなる；「いちばん奥行きが伝わりそうな視点を探す」ことが重要。**AI出図ルール**：アセットのコンセプト出図時、最も形が伝わる角度を優先選択 | Text Prompt + Lineart 参考図 |
| **1点透視描画ワークフロー（OCHABI）** | `1point_perspective_workflow, step1_EL_line_VP_point, step2_side_face_parallel_to_EL, step3_edges_to_VP_guideline, step4_far_side_face, step5_far_legs, guideline_value_10pct`；①EL補助線+消失点→②消失点の左下に物体側面(横線=ELと平行)→③側面端からVPへ補助線→④奥側面→⑤奥の脚。補助線はバリュー10%で描く。室内情景拡張：壁→消失点→天井/床境界線→家具→人物(頭頂=EL上)→窓→壁下暗く | Lineart 参考図 + ControlNet |
| **2点透視描画ワークフロー（OCHABI）** | `2point_perspective_workflow, step1_two_VPs_on_EL, step2_nearest_corner_vertical, step3_edges_to_left_VP_right_VP, step4_doors_windows_upper_lower_to_VP_verticals_straight, step5_repeat_for_buildings`；①EL上に左右消失点A/B→②中心に建物の角(最近点)→③左面→VP-A、右面→VP-B→④ドア/窓(上下辺=VP収束、縦辺=垂直)→⑤建物や道を追加。街並み描画に最適 | Lineart 参考図 + ControlNet |
| **アイレベル全円形変化法則（OCHABI）** | `EL_circle_deformation_universal, on_EL_circle_becomes_line, below_EL_ellipse_widens_downward, above_EL_ellipse_widens_upward, applies_to_all_circular_objects`；EL上=円が潰れて線になる、ELから上下に離れるほど楕円が縦長→正円に近づく。既有 `cylinder_EL_ellipse_rule`（みにまる）=円柱限定、本条=全円形対象の上位原理。コーヒーカップ/花瓶/タイヤ等の円形パーツ全てに適用 | Lineart 参考図 + Text Prompt |
| **円柱描画5ステップ（OCHABI）** | `cylinder_drawing_5step, step1_vertical_rectangle_guideline_10pct, step2_centerline_vertical_horizontal, step3_top_bottom_ellipses_bottom_rounder, step4_diagonal_30_40deg_lines, step5_connect_diagonals`；①縦長長方形補助線(バリュー10%)→②中心線(垂直・水平)→③上下に楕円(下の楕円は上より縦長)→④中心線に対して30-40度の斜め線→⑤斜め線同士をつなぐ。既有 `cylinder_draw_order`（みにまる）の完全版ワークフロー（高价值重复） | Lineart 参考図 + 草図底稿 |
| **風景分解描画法則（OCHABI）** | `landscape_decomposition_drawing, decompose_complex_scene_to_elements, basic_shapes_plus_texture_first, add_characters_second, clothing_conveys_occupation, window_size_conveys_depth`；複雑な風景も要素ごとに分解：①○△□+質感→②自転車+人→③服で職業→④窓の大きさで奥行き(手前=大/奥=小)。背景アセットのコンセプトアートに有用 | Lineart 参考図 + Text Prompt |
| **5 Core Shapes 万物構築法則（Peter Han）【核心規則】** | `5_core_shapes = {sphere / cube / cone / pyramid / cylinder}, frustum_as_6th, form_language_organic_shape, pinch_pull_push_twist_deformation, core_forms_as_clay_blocks, bean_shape_2d_only_no_volume`；全ての物体を5種の核心形体で構築；Frustum(截錐体)=6番目；Form Language=核心形体をpinch/pull/push/twistで有機的に変形；Bean Shape=2Dのみで体積伝達不可→Contour追加必須。既有 `2stage_shape_simplification`（OCHABI）の具体形体リスト+変形法拡張（高价值重复）。**AI出図ルール**：新規アセットのコンセプト段階で5 Core Shapes分解を行い、ControlNet用線画を作成 | Lineart 参考図 + 草図底稿 + ControlNet |
| **Center Line → Contour Line 体積伝達ワークフロー（Peter Han）【核心規則】** | `centerline_first_contour_second, contour_wraps_like_ribs_of_spine, minimal_contours, contour_as_section_separator, branching_centerlines, primary_centerline_to_branch, dissecting_form_internal_volume, line_weight_individualize_forms`；①Center Lineを先に描く→②Contour Lineが脊椎の肋骨のように巻きつく→③最小限の本数→④輪郭線を断面として形体分離→⑤分岐中心線で複雑形体対応→⑥線幅差で形体個別化。既有 `wrapping_line_shows_direction`（砂糖）/`wrapping_line_3d_effect`（みにまる）の完全版ワークフロー+branching概念追加（高价值重复） | Lineart 参考図 + 草図底稿 + ControlNet |
| **Side View First → 3/4 View 通用物件設計ワークフロー（Peter Han）** | `side_view_first_lock_proportion, then_3q_view_add_volume, side_study_for_all_complex_objects, side_view_shape_language_first, information_from_side_to_3q`；全ての複雑物体(車両/飛行機/武器/動物)はまずSide Viewで比例と形状言語をロック→その後3/4 Viewで体積を追加。**場景分治**：`gameplay_sprite`=既存pure side viewルールを維持；`concept_art / asset_design / character_sheet`=本ワークフローを適用。既有 `side_view_depth_enhancement` と互補：既存=人体側面深度強化、本条=万物通用設計フロー | Lineart 参考図 + 草図底稿 |
| **Draw Through Construction 穿透式構築法（Peter Han）** | `draw_through_construction, draw_hidden_parts, construction_lines_visible_in_sketch, contact_point_draw_through, find_contact_point_first`；被遮蔽部分も描く→構造精度保証；構築線は草図中に可視であるべき；Contact Pointを先に見つけ→形体を貫通して描く。**AI出図ルール**：ControlNet用線画作成時、被遮蔽部分も薄線で描くと構造精度向上 | Lineart 参考図 + ControlNet |
| **Form Relationships 5種形体関係法則（Peter Han）** | `form_relationships_5types = {overlap / interlock / separation / addition / subtraction}, overlap_near_covers_far, interlock_forms_mesh_together, separation_gap_between_forms, addition_forms_combine, subtraction_cut_away_from_form`；5種の形体関係：Overlap(重なり=近が遠を覆う)+Interlock(噂み合い)+Separation(分離)+Addition(加算)+Subtraction(減算)。既有 `overlap_creates_depth` はOverlapのみ、本条で残り4種を追加。**AI出図ルール**：アセットデザイン時、5種の関係を意識してパーツ配置 | Lineart 参考図 + 草図底稿 |
| **Cut Away Method 切割構築法（Peter Han）** | `cut_away_method, start_larger_geometric_form, cut_target_shape_from_block, box_to_cut_to_round_off, maintain_accuracy_and_simplicity`；大きな幾何形体から目標形状を切り出す：①Box→②Cut(形状切り出し)→③Round off(エッジ丸め)。精度と簡潔さを両立。車両/タイヤ/タンク/飛行機等の硬表面物件に特に有効。**AI出図ルール**：硬表面アセットのControlNet線画はまずBoxアタリ→切削→丸めの順序で作成 | Lineart 参考図 + ControlNet |
| **Balloon on a String 生物構築法（Peter Han）** | `balloon_on_string_method, balloon_eq_cranium, string_eq_spine, simplest_gesture_capture, applies_to_all_animals_birds_marine, movement_captured_by_string_gesture`；Balloon=頭蓋骨(cranium)、String=背骨(spine)→全ての動物/鳥類/海洋生物に適用；Stringが動物のジェスチャーを与える；動きのある対象を最簡潔に捕捉。**AI出図ルール**：モンスター/動物キャラのジェスチャーアタリに使用；Balloon+String→Rule of 1/3→ディテールの順序 | Lineart 参考図 + 草図底稿 + DWPose |
| **Reference Points 通用比例測定システム（Peter Han）** | `reference_points_along_centerline, measure_length_between_objects, wheel_base_for_vehicles, no_wheels_use_reference_points, nose_to_tail_sequential_measurement`；中心線沿いに参照点を設定→物体各部分の長さ比率を測定；車両=ホイールベースで比例測定；飛行機等車輪なし物体=参照点システムで前から後ろへ順次測定。既有 `7head_segment_ratio`（松岡）=人体限定、本条=万物通用比例測定法 | Lineart 参考図 + 草図底稿 |
| **投影図法/アイソメトリック使い分け法則（吉田誠治）** | `axonometric_projection, isometric_120deg_axes, cabinet_oblique_half_depth, cavalier_oblique_full_depth, no_vanishing_point_no_horizon, size_constant_everywhere`；投影図法=消失点なし/アイレベルなし/画面どこでもサイズ不変；アイソメトリック=3軸120°交差→比較的自然；キャビネット図=奥行き半分；キャバリエ図=奥行き等倍。**場景分治**：`gameplay_sprite`=既存negative `isometric` 維持；`tilemap_design / asset_reference_sheet / level_design_doc`=アイソメトリック許可 | Text Prompt + 草図底稿 |
| **消失点配置安全法則（吉田誠治）** | `VP_outside_frame_safe, VP_overlap_subject_kills_depth, 2point_VP_too_close_distorts, 2point_VP_same_height_required, 3point_VP_too_close_use_fisheye, 1point_VP_center_unified`；消失点がモチーフと重なると立体感消失；2点透視=2VP近すぎ→歪み→画面外に逃がす；2VP高さ違い→不自然；3点VP近すぎ→魚眼パース採用；1点透視=同じ向きの物体は全て同一VPを共有。既有 `1point_perspective_workflow`（OCHABI）に消失点安全距離ルール追加（高价值重复） | Text Prompt + ControlNet |
| **画角印象選択テーブル（吉田誠治）** | `wide_angle_strong_perspective_center_focus, telephoto_flat_scale_emphasis_even_focus, standard_angle_neutral_anime_default, 2VP_distance_wide_close_tele_far, no_intent_use_standard_slightly_tele`；広角=パースきつい+遠近感強調+中央集中；望遠=パース緩やか+スケール感強調+均等注視；標準(やや望遠)=意図なし時のデフォルト；アニメ構成=望遠寄り多い。**AI出図ルール**：演出意図から画角を先に決定→VP距離を連動設定 | Text Prompt |
| **斜面パース消失点法則（吉田誠治）** | `slope_VP_above_below_eye_level, uphill_VP_above_EL, downhill_VP_below_EL, steeper_slope_farther_VP_from_EL, slope_angle_check_square_at_VP, compressed_depth_telephoto_for_steep_slope`；坂道VP=アイレベル上にできない→水平道VPの垂直上下に発生；登り坂=上方向/下り坂=下方向；急坂ほどELとVPが離れる；角度確認=VP位置に正方形描画；急坂の段差過大=望遠パースで圧縮+建物密集。**AI出図ルール**：坂道シーンでは水平VPと斜面VPを分離指定 | Text Prompt + ControlNet |
| **人物パース接地法則（吉田誠治）** | `character_perspective_grounding, foot_rectangle_on_ground_plane, shoulder_knee_guideline_follows_perspective, body_cube_proportion_check, cross_section_volume_check, multiple_characters_align_at_eye_level, characters_first_then_EL_then_background`；人体も立体→パース影響を受ける；接地面に四角形→足の裏を意識；左右の肩/膝の補助線がパースに乗る；人体キューブで比率確認；複数人物=アイレベル高さに該当部分を重ねる；人物配置先→補助線でEL→背景の順序。既有 `low_angle_feet_large_head_small`（松岡）=変形ルール、本条=接地面構築法 | Text Prompt + ControlNet + DWPose |
| **タンジェント回避法則（吉田誠治）** | `avoid_tangent_lines, tangent_kills_depth_silhouette, solution_separate_or_overlap, separate_reduces_density_shows_context, overlap_shows_depth_hides_context, never_let_edges_just_touch, frame_edge_tangent_also_dangerous`；タンジェント=2本以上の線が接触→前後関係不明+シルエット認識困難；解決=「離す」か「重ねる」の2択（接触は禁止）；モチーフ同士だけでなくフレーム端との接線も危険。既有 `overlap_creates_depth` は重畳のみ、本条=タンジェント回避の完全ルール | Text Prompt + Lineart 参考図 |
| **構図6タイプ選択法則（吉田誠治）** | `composition_6types = {center_hinomaru / rule_of_thirds / diagonal / s_curve / frame_within_frame / triangle}, center_stable_direct_theme, thirds_stable_dynamic_balance, diagonal_movement_depth, s_curve_eye_flow_whole_screen, frame_depth_eye_guide, triangle_stable_heavy`；6構図タイプ：①中心(日の丸)=安定+テーマ直球；②三分割=安定+動きバランス；③対角線=動き+奥行き；④S字=視線を画面全体に誘導；⑤フレーム=手前枠+奥行き+視線誘導；⑥三角=安定+重厚感。**AI出図ルール**：`composition_type:` タグで構図を明示指定 | Text Prompt |
| **奥行き間距圧縮法則（Telecom2）** | `depth_spacing_compression, receding_objects_closer_together, equal_spacing_kills_depth, compress_tile_spacing_for_depth_layers`；奥に向かう物体はサイズが小さくなるだけでなく、間隔も圧縮される。等間隔配置=奥行き感消失。公式はなく絵ごとに判断。既有透視規則=消失点収束、本条=アニメーションフレーム間の間距圧縮。**AI出図ルール**：奥行きのあるスプライトシートではタイル間隔も圧縮 | Lineart 参考図 + sprite sheet QA |
| **視差背景フォローカメラ法則（Telecom2）** | `parallax_bg_follow_camera, bg_seamless_tileable_horizontal, character_stays_centered_bg_scrolls, depth_layers_different_scroll_speed, far_layer_slow_near_layer_fast`；横スクロールゲームの背景は水平方向にシームレスタイル可能であること。キャラは画面中央に固定、背景がスクロール。異なる深度レイヤーは異なるスクロール速度(視差効果)：遠景=遅い、近景=速い。**AI出図ルール**：`seamless tileable horizontal background` で背景生成、レイヤー別に分離生成 | Text Prompt + Unity Parallax設定 |
| **フカン/アオリ全身圧縮法則（室井）** | `fukan_aori_body_compression, from_above_large_head_small_feet_wider_upper_body, from_below_large_feet_small_head_wider_lower_body, trapezoid_silhouette_fukan_wide_top_aori_wide_bottom, near_body_part_enlarged_far_reduced`；フカン(上から)=頭大きく足小さく+胸幅広く足幅狭い(上底広い台形)。アオリ(下から)=足大きく頭小さく+胸幅狭く足幅広い(下底広い台形)。近い部位が大きく、遠い部位が小さくなる。既有 `foreshortening_overlap_depth`（Peter Han）=一般原理、本条=全身レベルの具体圧縮パターン。**AI出図ルール**：`from above, large head small feet` / `from below, large feet small head` | Text Prompt + DWPose / OpenPose |
| **物件ボックス化法則（室井）【核心規則】** | `object_box_construction, all_objects_as_box_combination, chair_2boxes_seat_back, sofa_boxes_side_by_side, near_box_steep_perspective_far_box_gentle, car_box_cut_detail, headlights_as_eyes_bumper_as_mouth, compressed_inner_tire_side_compression`；全ての物件(椅子/ソファ/机/車)は立方体の組み合わせで描く。近くの立方体=パース線が急、遠く=緩い。車=ボックス→カット→ディテール。既有 `depth_spacing_compression`（Telecom2）=圧縮量、本条=構築法。**AI出図ルール**：`furniture as box construction, perspective grid` | Text Prompt + ControlNet |
| **広角/望遠レンズ効果法則（室井）【核心規則】** | `wide_tele_lens_effect, wide_angle_large_size_diff_distortion_wide_bg, telephoto_small_size_diff_compressed_bg, subjective_wide_angle_immersive, objective_telephoto_observational, smartphone_view_wide_boxing_wide, family_gathering_telephoto_road_telephoto`；広角=近い→サイズ差大+パース歪み大+背景広い。望遠=遠い→サイズ差小+パース歪み小+背景圧縮。主観(広角)=臨場感/没入感→ゲーム1人称。客観(望遠)=俯瞰/観察→ゲーム3人称。既有 `画角印象選択テーブル`（吉田）=画角印象、本条=レンズ物理効果（補完関係）。**AI出図ルール**：`wide angle lens, exaggerated perspective` / `telephoto lens, compressed perspective` | Text Prompt + ControlNet |
| **目線誘導レイアウト法則（室井）【核心規則】** | `eye_guidance_layout, main_subject_large_centered, secondary_subject_smaller_peripheral, VP_above_main_subject, size_hierarchy_guides_gaze, contrast_guidance_bright_in_dark, sandwich_guidance_dark_walls_bright_center, mob_contrast_guidance_dark_crowd_bright_hero, height_expression_compressed_lower_building`；レイアウト4ステップ：①見せたいもの=大きく中央寄り②次に見せたいもの=小さく周辺③パース線設定(VPは主要オブジェクトの上)④ディテール追加。明暗目線誘導：暗い中の明るいキャラ/左右の暗い壁で明るい中央/モブの中の明るいヒーロー。高さ表現=建物下部圧縮。既有 `focal_point_contrast_boost`（吉田）=光影焦点、本条=構図焦点（補完関係）。**AI出図ルール**：`layout composition, main subject large and centered, vanishing point above main subject` | Text Prompt + ControlNet |
| **人体スケール基準法則（室井）【核心規則】** | `human_scale_reference, door_height_180_200cm, window_eye_level_90cm, foundation_30_40cm, building_height_400_450cm, room_ceiling_240cm, desk_70cm, bed_200x90cm, tree_emotion_round_warm_willow_spooky_spiky_cold_dead_desolate, tree_asymmetric_3d_branches`；全ての建物/家具/乗り物は人体サイズを基準に設計。ドア高さ180-200cm、窓=目の高さ90cm、基礎30-40cm、建物高さ400-450cm、天井高240cm、机70cm、ベッド200×90cm。樹木形態感情：丸い=暖かい/柳=不気味/尖った=冷たい/枯れ木=荒涼。建物底辺=地面に食い込む直線(丸み禁止)。**AI出図ルール**：`building with human scale reference, door height 180cm` / `rounded tree warm atmosphere` / `dead tree desolate lonely` | Text Prompt + ControlNet |
| **空間ビート構成法則（室井）【核心規則】** | `space_beat_composition, L_section_floor_wall_person, copy_section_smaller_toward_back, connect_section_edges_creates_perspective, equal_object_diagonal_split_creates_1point, curve_line_density_shows_curvature, person_first_then_perspective_not_reverse, space_baton_relay_grass_to_tree_scale_switch`；パースなしで空間を描く方法=「空間ビート」。3ステップ：①L字断面(床+壁)に人を配置②断面を奥に向かって小さくコピー③断面の端を線で繋ぐ→自然にパースが生まれる。等間隔物体の対角線分割→1点透視。曲面=線の間隔で曲率表現(密=急カーブ、疎=緩カーブ)。人物先行→パース後付け(逆は退屈)。空間バトンリレー=草が小さくなったら木にスケール基準切替。既有 `depth_spacing_compression`（Telecom2）=圧縮量、本条=空間構築手法（補完関係）。★★ゲームステージレイアウトに直結 | Text Prompt + ControlNet |

### 🗄️ 抽屉 3：🏃 [动画与物理]

*(记录：关键帧数、运动模糊、防滑步约束)*

- **通用防滑步原则**：角色/敌人重心锁死在 **Bottom Center (0.5, 0)**。地形/陷阱重心通常为 **Center (0.5, 0.5)**。
- **动画结构模具（ControlNet）**：所有带连续动作的生物关节角色（跑、跳、受击）**强制启用 `DWPose` / `OpenPose` 预处理器**。
- **动作发力顺序（Pose 约束）**：`action_order_crush_to_stretch`, `show_preload_before_release`, `jump_point_full_body_stretch`
- **重心与运动线（Pose 约束）**：`trace_center_of_gravity_shift`, `movement_reference_line_lock`, `waist_rises_first_head_detours`
- **接触受压校验（Pose / Lineart）**：`sitting_contact_compression`, `buttocks_sink_on_seat`, `knee_to_heel_not_overextended`, `seated_thigh_flatten_on_surface`（Hart 补充：大腿与椅面接触时被压扁）
- **复杂姿势回切校验（Pose / Lineart）【核心规则】**：`check_front_and_side_when_lost`；当 `pose_complexity=high`、`limb_overlap=high` 或 `foreshortening_confusion=true` 时，允许先回切 `front_view_structure_check` / `side_view_structure_check` 验证骨架，再返回目标姿势。
- **与纯侧视规则的场景分治**：当场景标签包含 `gameplay_sprite` 时，继续遵守 `pure side view`，但允许在侧视骨架内执行 `crush_to_stretch` 与 `center_of_gravity_shift`；当场景标签包含 `pose_study`、`concept_motion_sheet`、`animation_keypose_sheet` 时，可放开为多视角动作分析。
- **基准帧数（修订版）**：行走 6–8 帧/步（循环 8 帧 = 2 步），跑步 4–7 帧/步（循环 8–14 帧），跳跃 5–8 帧，攻击 6 帧，待机/飞行 4 帧。旧值（行走 4 帧/跳跃 3 帧）降级为“超简版备注，仅限极端省帧场景”。
  > **帧数修订原因**：Telecom Animation Bible 确认行走最小可用 5–6 帧/步（含重心转移中间帧），跳跃最小 5 帧（含蓄力 + 着地冲击），攻击最小 6 帧（Anticipation + Strike + Recovery）。

| 规则簇 | 可执行 Tag / 数值 / 约束 | 节点落点 |
| :--- | :--- | :--- |
| **对立力量法则（Contrapposto）【核心规则】** | `opposing_forces, upper_lower_body_counter_direction, contrapposto, major_force_structural, minor_force_aesthetic`；上半身和下半身力量方向相反；力量从脚底向上传递，每个关节是方向改变节点；Major Forces 影响躯干核心（结构性），Minor Forces 影响四肢（装饰性）。松岡補強：`contrapposto_weight_side_torso_shorter, weight_hip_protrude_diagonally_up`（軸足側の胴体の線は短くなる/腰に斜め上に突き出す）；`seichusen_body_surface_midline, center_line_ground_to_cog`（正中線と中心線を明確に区別） | DWPose / OpenPose 骨架校验 + Text Prompt |
| **自然站姿 S 曲线法则** | `natural_pose_s_curve, torso_outward_arc_legs_inward_arc, counterbalancing_curves, no_stiff_straight_pose`；自然站姿用曲线（躯干向外弧+腿向内弧=反向平衡）；直线站姿=僵硬/像要倒；Line of Action = S曲线或反S曲线。松岡補強：`s_curve_confident_character, no_s_curve_shy_slouch_character, chest_out_enhances_s_curve`（猫背キャラにはS字を避ける=例外条件追加） | DWPose / OpenPose 骨架校验 + Text Prompt |
| **脊柱曲线法则** | `spine_curved_not_straight, spine_determines_upper_lower_relation, back_view_spine_s_curve`；直脊柱=僵硬，弯曲脊柱=自然；Center Line 沿背部是曲线而非直线；脊柱决定上下半身位置关系 | DWPose / OpenPose 骨架校验 + Lineart 参考图 |
| **坐姿重心与接触压缩（补充）** | `seated_gravity_on_hips, seated_upper_body_free`；坐姿重心在臀部（非脚）；坐姿中上半身更自由（不需要平衡）；躯干仍遵循对立力量规则 | DWPose / OpenPose + Lineart 参考图 |
| **步行躯干反转法则** | `walking_torso_counter_rotation, leg_forward_torso_rotates_away, abdomen_line_shows_rotation`；腿前迨时躯干向反方向旋转；腹部线条表达躯干转动；后腿被前腿重叠+轻微阴影 | DWPose / OpenPose 骨架校验 + Text Prompt |
| **运动曲线法则（Arc of Motion）【核心规则】** | `arc_of_motion, all_body_parts_follow_arcs, no_linear_interpolation, head_arc_walk, hand_arc_swing`；所有身体部位运动必须沿弧线轨迹；中间帧不能是两关键帧的线性混合；头/手/脚的帧间轨迹必须是平滑弧线；偏离弧线 = 视觉卡顿 | DWPose / OpenPose 骨架校验 + sprite sheet QA |
| **帧间距（Spacing）速度控制法则** | `spacing_controls_speed, accel_sparse_decel_dense, loop_equal_spacing, heavy_object_abrupt_accel, light_object_gradual_accel`；加速 = 帧间距从小到大（密→疏）；减速 = 帧间距从大到小（疏→密）；循环动画必须等间距；重物加减速短促，轻物加减速绵长 | sprite sheet 帧位置规划 + AnimationClip timing |
| **循环动画闭合法则** | `loop_last_frame_seamless, loop_equal_spacing_mandatory, walk_loop_8f_standard, run_loop_8_14f`；循环 sprite sheet 最后帧必须无缝接回第一帧；循环段必须等间距；行走循环标准 8 帧（2步）；跑步循环 8–14 帧 | sprite sheet QA + AnimationClip WrapMode |
| **行走四要素法则** | `walk_4_elements, walk_vertical_bounce, heel_first_landing, arm_opposite_leg, center_of_gravity_over_support_foot`；(1)上下动：中间帧体最高，交叉帧体最低；(2)脚运：脚跟先着地 = 正常走，脚尖先着地 = 潜行；(3)手臂反向：右脚前→左手前（同侧手脚同向 = AI 常见错误）；(4)重心平衡：每帧重心必须在支撑脚上方 | DWPose / OpenPose 骨架校验 + sprite sheet QA |
| **走跑区分法则** | `walk_has_double_support_frame, run_has_airborne_frame, walk_stable_run_unstable`；走路 = 至少 1 帧双脚着地（安定）；跑步 = 至少 1 帧双脚离地（不安定/连续跳跃）；跑步 = Squash（着地下沉）+ Stretch（伸展上升）交替 | DWPose / OpenPose + sprite sheet QA |
| **情绪行走参数表** | `emotion_walk_params, stride_vertical_bounce_arm_swing_lean`；自信走：上下动大 + 脚高抬 + 后仰；氮丧走：步幅小 + 手臂不摆 + 脚不离地；愤怒走：前倾 + 大步 + 手臂大摆；潜行：脚尖着地 + 8 帧/步。体型：胖 = 上下动大 + 步幅小；年龄：幼儿 = 步幅极小 + 手臂前伸，老人 = 前倾 + 不伸展 | DWPose / OpenPose + Text Prompt 情绪标签 |
| **Follow-Through（跟随延迟）法则** | `follow_through_delay, soft_appendage_lag_2_3f, cloth_wave_propagation, hair_tip_lags_head`；所有柔软附属物（头发/斗篷/尾巴/裙摆）必须有 Follow-Through 延迟；末端延迟 2–3 帧追随主体运动；布料飘动 = 波峰沿布面方向传递（非随机抖动） | sprite sheet QA + Text Prompt |
| **转头动画层级法则** | `head_turn_hierarchy, gaze_leads_head_leads_body, nose_arc_not_linear`；转头 = 视线先行 > 头先行 > 体跟随；鼻尖沿弧线旋转（非直线平移）；头/体/颈同速旋转 = 机器人感 | DWPose / OpenPose + sprite sheet QA |
| **Anticipation（预备动作）法则** | `anticipation_before_fast_action, attack_rhythm_antic_strike_recovery, antic_dense_strike_sparse_recovery_dense`；所有快速动作前必须有预备帧；攻击节奏：Anticipation（蓄力 2–3 帧，慢）→ Strike（打击 1–2 帧，极快）→ Recovery（收势 1 帧，慢）；帧间距：蓄力密→打击疏→收势密 | sprite sheet 帧数规划 + DWPose / OpenPose |
| **VFX 循环法则（烟/火/水）【升级】** | `smoke_loop_ball_upward, smoke_sphere_decomposition, fire_loop_6f_silhouette_stable_interior_random, smoke_dissipate_expand_not_shrink, water_splash_crown_sequence`；烟循环：球状山部分向上传递 + 均等间距 + 轮廓一致；**烟球体分解法(Telecom2追加)**：大规模烟/爆発=重なり合う球体の集合体として構成、各球体の山を上方へシフトしてアニメート、AI prompt=`billowing smoke, spherical cloud forms`；火焰循环：6 帧标准 + 整体轮廓不变 + 内部随机变化 + 火焰块向上送；烟消散：扩散→粒子化→消失（不是缩小）；**水花序列(Telecom2追加)**：水影响=V型飛沫→水柱上升→王冠状拡散→水滴分離→同心円波紋收束。AI単フレームではどのフェーズを描くか指定 | sprite sheet VFX + Text Prompt |
| **S&S 适度使用约束（补充）** | `squash_stretch_moderation, no_overuse_ss, micro_ss_for_expression`；Squash & Stretch 不能过度使用，适度使用 = 有节奏感；微表情级 S&S（惊讶缩伸/握拳皱拉）也能增加生动感。与现有 `action_order_crush_to_stretch` 互补，不替代 | Text Prompt + sprite sheet QA |
| **走速度と身体傾斜の連動（松岡）** | `run_speed_body_lean_correlation, jog_slight_lean, sprint_heavy_lean, speed_up_wider_stride_bigger_arm_swing`；スピードが上がるほど身体が傾き、歩幅が広がり、腕の振りも大きくなる | DWPose / OpenPose + Text Prompt |
| **男女の動きベクトル差異（松岡）** | `male_motion_vector_outward_upward, female_motion_vector_inward_downward`；男性的動作ベクトル=外向き/上向き、女性的動作ベクトル=内向き/下向き | Text Prompt + DWPose / OpenPose |
| **体幹3分割ひねり法則（松岡）** | `torso_3block_twist, chest_abdomen_hip_separate_rotation, twist_adds_dynamism`；激しい動作（蹴り/投げ/パンチ）では体幹を胸/腹/腰の3ブロックに分けてアタリ；各ブロックが異なる角度でひねれる。既有 `walking_torso_counter_rotation` と互補：歩行=自然な反転、本条=激しい動作の意図的3分割 | DWPose / OpenPose + Lineart 参考图 |
| **アクションライン傾斜＝視線誘導法則（松岡）** | `action_line_tilt_guides_eye, static_horizontal_vertical_boring, dynamic_tilted_action_line`；静的なアクションライン（水平垂直）=面白みに欠ける；動的なアクションライン=少し傾ける→視線誘導が自然にできる | DWPose / OpenPose + Text Prompt |
| **武器/小物視線誘導法則（松岡）** | `weapon_direction_matches_gaze, weapon_curve_guides_eye_to_face, prop_as_eye_guide`；キャラクターの視線と刀の向きは同方向にする→視線誘導；刀のそりの曲線が見る人の視線をキャラクターの顔に誘導 | Text Prompt |
| **手のサイズ基準法則（松岡）** | `hand_length_equals_chin_to_eyebrow, hand_3block_thumb_palm_fingers, thumb_base_thickest, female_hand_slim_sharp_curved_round_tip, male_hand_thick_square_joint_visible_angular_tip, finger_arc_middle_peak, finger_curve_not_straight`；手の大きさ=顔の長さ（あご→眉毛）；手を3ブロック（親指/手の甲/指）に分割；男女の手の描き分け | Text Prompt + Lineart 参考図 |
| **足の構造3ブロック法則（松岡）** | `foot_3block_toes_instep_heel, inner_ankle_higher_than_outer, foot_arch_structure, female_foot_cone_toes_smooth, male_foot_square_toes_bony`；足を「指」「甲」「かかと」の3ブロックに分割；内くるぶし>外くるぶし；男女の足の描き分け | Text Prompt + Lineart 参考図 |
| **4基本感情の筋肉方向法則（松岡）** | `joy_muscles_outward_relax, anger_muscles_center_converge, sadness_brow_inner_up_outer_down_eyes_vacant, surprise_brow_up_eyes_wide_mouth_round`；笑い=筋肉外側にゆるむ、怒り=中心に寄る、悲しみ=眉頭上反り眉尻下がり、驚き=眉上がり目見開き | Text Prompt |
| **アングル×感情相性テーブル（松岡）** | `low_angle_anger_confidence_cool, high_angle_sadness_shyness_cute, male_expression_subtle_restrained, female_expression_dynamic_exaggerated, high_angle_gaze_up_tension, high_angle_gaze_down_calm`；アオリ=威圧感/怒りと相性良、フカン=弱さ/悲しみと相性良；男性表情=控えめ、女性表情=ダイナミック | Text Prompt |
| **LoA（Line of Action）印象駆動法則（砂糖）【核心規則】** | `LoA_first_detail_last, single_dominant_LoA_curve, LoA_curve_type = {C_back_arch / S_dynamic / reverse_C_lean_back / C_crouch}, exaggerate_LoA_curve, LoA_iteration_allowed, verb_driven_LoA = {arching / bending / stretching / twisting / crouching}`；LoA=全身を貫く1本の印象線→最初に決定；実際のポーズより印象を誇張したカーブを採用してOK；何度でも描き直し可；動詞（反る/伸びる/曲がる）でLoAの性格を決定。**場景分治**：`gesture_study / animation_keypose / concept_sketch` = 本法則（印象駆動LoA先行）；`character_sheet / model_sheet / turnaround` = 松岡アタリ法則（解剖精度ベース） | DWPose / OpenPose backbone curve + Text Prompt |
| **CSI線質理論（砂糖）【核心規則】** | `CSI_line_vocabulary = {C: curve / S: s_curve / I: straight}, C_S_softness_organic_feminine_relaxed, I_hardness_tension_power_masculine_artificial, CSI_mix_ratio_controls_impression`；人体のあらゆるラインをC/S/Iの3種類に分類；C/S=やわらかさ/有機的/女性性、I=硬さ/緊張/男性性；キャラの性格/シーンの雰囲気でCSI比率を調整。松岡 `male_angular_straight_lines, female_curved_soft_lines` の上位メカニズム（高价值重复） | Text Prompt + Lineart 参考図 |
| **CSI性別/世界観比率テーブル（砂糖）** | `female_char_C_S_70pct_I_30pct, male_char_I_60pct_C_S_40pct, power_scene_I_ratio_up, relax_scene_C_S_ratio_up, world_CSI_ratio_unified_across_project`；女性キャラ=C/S多め(70%)+I少なめ(30%)；男性キャラ=I多め(60%)+C/S少なめ(40%)；力強いシーン=I比率UP；リラックスシーン=C/S比率UP；プロジェクト全体でCSI比率を統一→世界観のトーン統一。MarioTrickster=カートゥーン→C/S主体+I(陷阱/人工物) | Text Prompt |
| **ジェスチャー3段階ワークフロー法則（砂糖）** | `gesture_to_volume_pipeline, three_pass_workflow = {pass1_LoA_mass / pass2_cylinder_volume / pass3_flow_detail}, additive_drawing_workflow = LoA → +head → +ribcage → +pelvis → +limbs → +ground_contact, full_workflow = LoA(CSI) → spheres → rubber_bag → cylinders → overlap → flesh`；Pass1=LoA+球体(頭/胸/骨盤)+棒線→Pass2=円柱(腕/脚)+ラッピングライン→Pass3=フロー意識+ディテール；1分ジェスチャー=Pass1のみ、2分=+Pass2 | DWPose / OpenPose + Lineart 参考図 |
| **印象誇張法則（砂糖）** | `exaggerate_LoA_curve, self_impression_as_ground_truth, gesture_impression_gt_photographic_accuracy, impression_changes_each_time_OK`；実際のポーズより受けた印象を強調した曲線を採用；自分の印象が基準（写真精度ではない）；同じポーズでも見るたびに印象が変わってOK。**場景分治**：動的ポーズ/ジェスチャー=本法則；設定稿/モデルシート=解剖精度優先 | Text Prompt + DWPose / OpenPose |
| **フロー意識法則（砂糖）** | `draw_verbs_not_nouns, flow_over_parts, continuous_rhythm_line, LoA_before_contour, contour_supports_gesture`；名詞(パーツ名)に意識が行くとフローが止まる→動詞(動き)を描く；全身を貫くリズム=フロー；輪郭はLoAの後→輪郭は印象を支えるもの | DWPose / OpenPose + Text Prompt |
| **エンターテインメントゾーン法則（砂糖）** | `entertainment_zone, rule_compliance_plus_creative_intent, two_pass_workflow = pass1_gesture_creative → pass2_anatomy_check_rule`；ルール(解剖学/パース)とクリエイティブ(印象/感情)のバランスが取れた場所=エンターテインメントゾーン；Pass1=クリエイティブに印象を描く→Pass2=ルールで解剖チェック | Text Prompt + DWPose / OpenPose |
| **かたまりの印象法則（砂糖）** | `silhouette_as_mass_impression, CSI_applied_to_clothing_folds, CSI_ratio_matches_character_impression`；シルエット全体を1つの塊として見る→直線と曲線の組み合わせで印象を表現；服のシワにもCSI適用；硬いキャラ=I多め、柔らかいキャラ=C/S多め | Text Prompt + Lineart 参考図 |
| **服の3段階ワークフロー法則（砂糖）** | `clothing_workflow = gesture_mannequin → nude_anatomy → clothed_character`；服を着せると立体感が消える問題の解決策：①素体(球体/円柱)→②裸体(解剖)→③着衣；②-A=服の中の裸を描く / ②-B=オーバーラップと円柱を利用して服を描く | Lineart 参考図 + Text Prompt |
| **シルエットドローイング法則（みにまる）** | `silhouette_drawing_rule, squint_for_overall_shape, large_silhouette_to_small_silhouette, negative_silhouette_between_limbs, silhouette_readability_equals_good_pose`；薄目で見て全体の大まかな形を描く→全体のシルエットから内側の小さいシルエットへ；ネガティブシルエット=脚と脚の間/胴体と腕の間の「階間の形」に注目。**AI出図ルール**：シルエットが読み取れるポーズ=良いポーズ。ポーズ検証時にシルエット化して確認 | DWPose / OpenPose + sprite sheet QA |
| **ジェスチャー誇張の原則（みにまる）** | `gesture_exaggeration_principle, exaggerate_beyond_reality, proud_pose_chest_out_elbow_up, run_pose_big_arm_swing_big_leg_bend, dynamic_pose_exaggerated_gesture`；現実よりオーバーに描く→意図が明確に伝わる；例：威張るポーズ→胸をグッと張りヒジを上げる；走るポーズ→腕を大きく振り脚を大きく曲げる。既有 `exaggerate_LoA_curve`（砂糖）の具体例追加（高价值重复）。**AI出図ルール**：`dynamic pose, exaggerated gesture` をプロンプトに入れると誇張表現強化 | Text Prompt + DWPose / OpenPose |
| **背骨傾斜＝動作法則（OCHABI）** | `spine_tilt_action_rule, standing_spine_90deg, walking_spine_70deg, running_spine_40deg, spine_angle_plus_leg_bend_determines_motion`；直立=背骨垂直(90度)、歩く=背骨70度傾斜、走る=背骨40度傾斜。背骨の傾きと足の曲がり方で動きを描き分ける。既有 `run_speed_body_lean_correlation`（松岡）に具体角度数値を追加（高价值重复） | DWPose / OpenPose + Text Prompt |
| **感情関節距離法則（OCHABI）** | `emotion_joint_distance_rule, joy_joints_away_from_axis_bowleg_active, calm_joints_close_to_axis, spine_as_thick_hose_flexible`；喜び=関節を身体の軸から離す(ガニ股等、活発なしぐさ)；おとなしさ=関節を軸に近づける；背骨=小さい骨の積み重ね→太いホースのように動く。既有 `emotion_walk_params` の静的ポーズ版拡張 | DWPose / OpenPose + Text Prompt |
| **服のランドマーク描画法則（OCHABI）** | `clothing_landmark_drawing, collar_neck_boundary_curve, waist_center_horizontal, hem_ankle_position, sleeve_shoulder_to_mid_forearm_diagonal, collar_shape_changes_design, waist_position_changes_design`；服のランドマーク：襲(首の境目を曲線でつなぐ)/ウエスト(腰の中心に横線)/裾(足首の位置)/袖(肩と腕から肘の間まで斜め下方向の直線)。既有 `clothing_workflow`（砂糖）の具体ランドマーク補強（高价值重复） | Lineart 参考図 + Text Prompt |
| **部位別間距差分法則（Telecom2）** | `per_body_part_spacing_variation, legs_fast_arms_slow_ok, different_parts_different_timing, complex_motion_from_timing_offset`；同一アクション内でも身体部位ごとに異なる間距/タイミングを持たせられる（例：足=速いストライド+腕=遅いフォロースルー）。既有 `spacing_controls_speed` =全体的間距制御、本条=部位別差分。sprite sheetでは各部位のタイミングオフセットを意識 | sprite sheet QA + AnimationClip |
| **弾み材質分化法則（Telecom2）** | `bounce_material_differentiation, light_hard_high_many_bounces, heavy_soft_low_few_bounces, ping_pong_high_bouncy, bowling_ball_tiny_quick_stop`；同じ形状でも材質/重量が異なれば弾みパターンが完全に異なる。軽い+硬い=高く多く弾む；重い+柔らかい=低く少なく弾む。既有 `heavy_object_abrupt_accel` の弾み特化補完 | sprite sheet VFX + Text Prompt |
| **歩き踵キックバック弧法則（Telecom2）【核心規則】** | `walk_heel_kickback_arc, trailing_heel_kicks_up_backward_first, sole_briefly_visible, then_swings_forward_in_arc, NOT_straight_slide_forward`；歩行中、後ろ足の踵はまず上後方に踴り上げ(足裏が一瞬見える)→その後弧を描いて前方へ振り出す。直線的に前へスライドしない。これはAI歩行アニメーションの最も多いエラー。既有 `walk_4_elements` =heel_first_landingのみ、本条=キックバック弧追加 | DWPose / OpenPose + sprite sheet QA |
| **波伝播フォロースルー法則（Telecom2）** | `wave_propagation_follow_through, force_travels_root_to_tip, each_segment_delays_1_2_frames, rope_whip_cape_chain_wave_pattern, NOT_simultaneous_movement`；長い柔軟物体（ロープ/ムチ/マント/チェーン）は力が根元から先端へ波として伝播。各セグメントは1-2フレーム遅延。同時移動は禁止。既有 `follow_through_delay` =基本ラグ、本条=波伝播パターン追加 | sprite sheet VFX + Text Prompt |
| **旗振幅グラディエント法則（Telecom2）** | `flag_amplitude_gradient, fixed_edge_tight_ripples, free_edge_large_waves, amplitude_increases_pole_to_tip, cape_same_principle_shoulder_to_hem`；旗/マント/バナー：固定端(ポール/肩)から自由端へ振幅が増大。固定端=小さな波、自由端=大きな波。既有 `follow_through_delay` の振幅グラディエント補完。**AI出図ルール**：`flag waving, increasing wave amplitude from pole to edge` | Text Prompt + sprite sheet VFX |
| **四足歩行二人モデル法則（Telecom2）** | `quadruped_two_humans_model, front_legs_hind_legs_independent_biped_rules, digitigrade_heel_elevated, 4_legs_staggered_NOT_simultaneous, quadruped_run_mostly_airborne`；四足歩行=二人の人間が縦列で歩くモデル。前足と後足はそれぞれ独立に二足歩行ルールに従う。足は趾行(踵が浮いている)。4足はスタッガードタイミング(同時着地禁止)。走り=ほぼ空中。ゲーム敵キャラ(Goomba/Chain Chomp等)に適用 | DWPose(animal) + sprite sheet QA |
| **魚泳ぎ4タイプ法則（Telecom2）** | `fish_swimming_4types, type1_basic_tail_rear_swing, type2_torpedo_tail_only_body_rigid, type3_ray_pectoral_fin_wave, type4_serpentine_whole_body_undulate, fish_S_curve_body_not_straight, tail_lags_behind_head_follow_through`；魚の泳ぎ4タイプ：①基本(尾+後体左右)②トルペード(尾のみ/体剛直)③エイ(胸びれ波動)④蛇行(全身波動)。体はSカーブで波打ち、尾は頭に対してフォロースルー。ゲーム水中敵(Cheep Cheep)にはType1適用 | sprite sheet VFX + Text Prompt |
| **鳥羽ばたき翼端フォロースルー法則（Telecom2）** | `bird_wing_tip_follow_through, wing_tips_lag_behind_root, S_curve_wing_silhouette, large_bird_slow_wide, small_bird_fast_narrow, downstroke_cups_air, upstroke_root_leads`；翼端は上下ストロークともに翼根に対してラグ(フォロースルー)。Sカーブの翼シルエットを形成。大型鳥=遅く広く、小型鳥=速く狭く。ゲーム飛行敵(Paratroopa等)に適用 | DWPose(animal) + sprite sheet VFX |
| **走りスタイル体型マッピング法則（Telecom2）** | `run_style_body_type_mapping, fat_wide_bouncy_arms_out, toddler_short_stride_no_cushion, elderly_forward_lean_short_stride, athlete_high_knee_long_stride, bowlegged_comedic_tough, pigeon_toed_feminine_shy`；走りアニメーションは体型に合わせる。太った人=広い+弾む；幼児=短いストライド+膝クッションなし；老人=前傾+短いストライド；アスリート=高い膝+長いストライド。既有走り規則=汎用、本条=体型別バリエーション追加 | DWPose / OpenPose + Text Prompt |
| **ジャンプ予備動作蓲み必須法則（Telecom2）【核心規則】** | `jump_anticipation_crouch_mandatory, forward_lean_before_crouch, crouch_before_takeoff, landing_knee_bend_cushion, without_anticipation_looks_floating`；ジャンプには予備動作(前傾+蓲み)が必須。予備動作なし=キャラが浮いて見える。着地は膝曲げクッション必須。既有 `action_order_crush_to_stretch` =汎用S&S順序、本条=ジャンプ特化の蓲みシーケンス。**Marioプラットフォーマーの核心動作** | DWPose / OpenPose + sprite sheet QA |
| **剣斜り予備動作弧法則（Telecom2）** | `sword_slash_anticipation_arc, windup_body_twist_back, fast_slash_arc_downward, follow_through_past_target, sword_tip_traces_wide_arc`；剣攻撃は①蓄力(体ねじり+剣上げ)→②高速斜り弧→③フォロースルー(剣先が目標を越える)。剣先は広い弧を描く。既有 `kinetic_chain_hip_leads` =汎用原理、本条=武器攻撃特化シーケンス。ゲーム敵攻撃アニメーションに適用 | DWPose / OpenPose + sprite sheet VFX |
| **起立前傾必須法則（Telecom2）** | `stand_up_forward_lean_mandatory, torso_lean_forward_before_hip_lift, center_of_gravity_over_feet_first, without_lean_falls_backward`；座位からの起立は上体前傾が必須(重心を足の上に移動)→その後腰を上げる。前傾なし=物理的に立てない(後ろに倒れる)。ゲーム：被ダメージ後の起き上がりアニメーションに適用 | DWPose / OpenPose + sprite sheet QA |
| **重心軸傾斜ダイナミズム法則（室井）【核心規則】** | `gravity_axis_tilt_dynamism, perfect_balance_static_dead, slight_tilt_about_to_move, large_tilt_intense_motion, walk_forward_lean, one_leg_weight_over_support, heavy_object_lean_opposite`；全動作共通の重心軸原理：完全バランス=静止=死んで見える。少し崩す=動き出す直前。大きく崩す=激しい動き。歩き=前傾、片足立ち=支持脚上に重心、重い荷物=荷物と反対側に傾く。既有 `walk_unbalanced_pose`（Telecom2）=歩行特化、本条=全動作共通原理（上位規則）。**AI出図ルール**：`dynamic pose, tilted center of gravity, off-balance stance` | DWPose / OpenPose + Text Prompt |
| **歩き7要素チェックリスト法則（室井）【核心規則】** | `walk_7element_checklist, gaze_forward, hair_follow_through, shoulder_micro_rise, torso_twist_shoulder_hip_counter, clothes_follow_through, kicking_foot_behind, center_of_gravity_forward_lean, front_walk_limb_cross_section_change, foot_gap_at_crossing`；歩きアニメーションQAチェックリスト7要素：①視線=進行方向②髪=フォロースルー③肩=腕の動きで微上昇④胴体=ひねり(肩/腰逆回転)⑤服=フォロースルー⑥蹴り足=後方に残る⑦重心=前傾。正面歩き：四肢の断面変化(圧縮/伸長)を意識。足交差時は雙脚に隙間を作る。既有 `walk_heel_kickback_arc`（Telecom2）=踵キックバック特化、本条=全身7要素チェックリスト（補完関係）。**AI出図ルール**：`walking pose, forward lean, kicking foot behind, hair follow-through, torso twist` | DWPose / OpenPose + sprite sheet QA |
| **走り vs 歩き空中フェーズ差分法則（室井）【核心規則】** | `run_vs_walk_aerial_phase, run_has_aerial_phase_both_feet_off_ground, walk_always_one_foot_grounded, run_more_forward_lean_than_walk, run_more_follow_through_hair_clothes, run_more_torso_twist, run_neck_hidden_in_shoulders, run_bow_shaped_body, run_center_of_gravity_ahead_of_toes, backward_lean_looks_braking`；走りと歩きの決定的差分：走り=空中フェーズあり(両足が地面から離れる)、歩き=常に片足接地。走りの特徴：①前傾角度が歩きより大きい②髪/服のフォロースルーが大きい③肩/腰のひねりが大きい④首が肩に埋まる⑤弓なり形(bow shape)で進行方向表現⑥重心はつま先より前。重心が後ろ=ブレーキに見える。既有 `run_style_body_type_mapping`（Telecom2）=体型別、本条=走り基本構造（補完関係）。**AI出図ルール**：`running pose, aerial phase, bow-shaped body, forward lean, hair flowing back, shoulders raised hiding neck` | DWPose / OpenPose + sprite sheet QA |
| **片足バランス全身統一法則（室井）【核心規則】** | `one_leg_balance_unified_body, straight_support_leg, arms_spread_for_balance, unified_body_mass_over_support_foot, bent_support_leg_unstable_error, bent_arms_no_balance_error, whole_body_as_single_mass, handstand_same_principle`；片足立ち=支持脚の上に全身の重心を統一。×支持脚を曲げる(不安定)、×腕を曲げる(バランス取れない)。○支持脚を伸ばし、両腕を広げてバランス。全身を一つの塊として描く。逆立ちも同じ原理。既有 `standing_pose_contrapposto_mandatory`（Telecom2）=片足重心位置、本条=全身統一+腕バランス（補完関係）。**AI出図ルール**：`one leg standing, straight support leg, arms spread for balance, unified body mass` | DWPose / OpenPose + Text Prompt |
| **重量表現1.5倍法則（室井）【核心規則】** | `weight_expression_1_5x, heavy_object_draw_1_5x_size, body_object_no_gap_contact, bent_knees_lowered_center, without_object_would_fall_pose, rope_pull_8stage_force_expression, stronger_force_more_unified_body_line`；重い荷物を持つ表現=荷物を実際の1.5倍の大きさで描く。荷物と体の間に隙間=軽く見える、密着=重く見える。「荷物がなかったら倒れるポーズ」で描くと重さが出る。紐引き8段階力表現：力が強くなるほど全身が一直線に統一。★★Marioアイテム持ち上げ/運搬に直結 | DWPose / OpenPose + Text Prompt |
| **風力表現法則（室井）【核心規則】** | `wind_force_expression, front_clothes_cling_to_body, back_clothes_hair_flow_downwind, center_of_gravity_lean_into_wind, all_flow_direction_unified, strong_wind_extreme_lean_toes_forward, wind_direction_consistency_mandatory`；風(見えない力)は人体の反応で表現。①前面の服が体に密着②背面の服/髪が風下になびく③重心を風上に前傾。風方向統一法則：髪/服/紐のなびき方向を全て統一。バラバラ=×。強風=倒れるほど前傾+つま先を前に。既有 `follow_through_delay`（Telecom）=一般フォロースルー、本条=風力特化の外力フォロースルー（補完関係）。**AI出図ルール**：`walking against strong wind, forward lean, front clothes clinging, hair flowing in wind direction`。**AI negative prompt**：`inconsistent wind direction, hair blowing different directions` | Text Prompt + sprite sheet VFX |
| **80%伸展最大力法則（室井）** | `80_percent_extension_max_power, arm_80_percent_extended_max_impact, 100_percent_extended_power_released, throw_sequence_windup_twist_80pct_release, punch_80pct_max_impact_not_full_extension`；投げる/殖る動作は腕が80%伸びた時が最大パワー。100%伸展=力が抜けている。80%=まだ力が残っている。投げ：①構え→②体ひねり(80%力)→③リリース(全身伸展)。既有 `anticipation_squash_required`（Telecom2）=予備動作、本条=最大力の瞬間（補完関係）。**AI出図ルール**：`throwing pose, 80 percent arm extension, torso twist, maximum power moment` | DWPose / OpenPose + Text Prompt |
| **弓なり進行方向表現法則（室井）** | `bow_shape_direction_expression, bow_shaped_body_shows_movement_direction, 5_stages_upright_to_max_deform, car_hood_bend_exaggeration_allowed, stronger_motion_more_bow_curve`；進行方向を表現するには体全体を弓なりに曲げる。5段階：①直立→②少し曲げ→③弓なり→④さらに曲げ+スピード線→⑤最大変形。車にも適用可能。既有 `motion_arc_global_continuity`（Telecom）=全カット連続性、本条=単一ポーズの弓なり。**AI出図ルール**：`bow-shaped body expressing direction of movement, dynamic forward lean` | DWPose / OpenPose + Text Prompt |

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
| **服のシワ5分類体系（みにまる）** | `wrinkle_5_types = {pulled / twisted / bent / pinched / sagging}, pulled_wrinkle_cylinder_wrap_reverse_direction, twisted_wrinkle_diagonal_cylinder_wrap, bent_wrinkle_excess_fabric_overlap, pinched_wrinkle_accordion_fold, sagging_wrinkle_diamond_cross_gravity, thin_fabric_more_wrinkles, thick_fabric_fewer_wrinkles, gravity_drapes_excess_fabric, main_wrinkles_only_no_overdraw, cloth_layering_thickness_expression_muroi, inner_layer_tight_outer_layer_loose, thick_cloth_round_folds_thin_cloth_sharp_folds, collar_cuff_hem_show_fabric_thickness`；服のシワ5分類：①引っ張られシワ(肩・胸凸部が布を引く→円柱に巻きつく紐イメージ)→②ねじられシワ(胴体ねじり→円柱に斜め巻き)→③折り曲げシワ(腕脚内側→布余りはみ出し)→④挟まれシワ(わき等→アコーディオン状)→⑤たるみシワ(重力→ひし形交差)；布の厚さでシワ量変化；メインの大きなシワだけ描けばOK。**AI出図ルール**：シワタイプをプロンプトで指定(`pulled fabric wrinkles` / `twisted fabric folds` / `bent elbow fabric folds` / `pinched fabric folds` / `sagging fabric folds`)+布の厚さ(`thin fabric` / `thick fabric`)で精度向上 | Text Prompt + Lineart 参考図 |
| **バリュー10段階グレースケール体系（OCHABI）** | `value_10_step_grayscale, value_0pct_white, value_100pct_black, darker_value_closer, lighter_value_farther, aerial_perspective_near_90pct_mid_50pct_far_20pct`；白から黒までの階調を0%(=白)【10%【20%…100%(=黒)の10段階で管理；濃いバリューほど手前、薄いほど奥(空気遠近法)；山並みの例: 手前=90%、中間=50%、奥=20%。**AI出図ルール**：背景レイヤー分割時にバリュー段階で前後関係を制御 | Text Prompt + 後処理 |
| **バリュー白黒印象法則（OCHABI）** | `value_impression_rule, white_light_airy_far_weak_impact, black_dark_heavy_close_strong_impact, dark_suit_heavy_white_tshirt_light`；白=明るい/軽やか/遠くに感じる/インパクト弱い；黒=暗い/重い/手前に近く感じる/インパクト強い。キャラクターの服色で印象を制御 | Text Prompt |
| **球体バリュー地球モデル（OCHABI）** | `sphere_value_earth_model, apex_0pct_nearest_light, edge_10pct, daytime_zone_10_30pct, evening_zone_30_60pct, night_zone_60_70pct, front_edge_90pct, contact_shadow_100pct, eraser_highlight_0_10pct`；光源からの距離でバリューを決定：頂点(=光源に最も近い)=0%、端=10%、昼の地域=10-30%、夕方の地域=30-60%、夜の地域=60-70%、手前のエッジ=90%。光源側を練り消しゴムで0-10%に白く消す。**AI出図ルール**：球体状パーツ(頭/肩/膝)のライティング参考値 | Text Prompt + 後処理 |
| **エッジ強調法則（OCHABI）** | `edge_emphasis_rule, front_edge_value_90pct, back_edge_lighter, contact_point_100pct, floor_shadow_gradient_70pct_to_10pct, floor_base_value_10pct`；手前のエッジ(物体の最も手前の輪郭線)にバリュー90%を乗せる；奥のエッジは薄く；接地部分=100%；床の影は接地部分に近いほど濃く(70%)、離れるほど薄く(10%)；床自体のバリュー=10%。**AI出図ルール**：接地影のグラデーションを意識してプロンプト指定(`contact shadow, gradient floor shadow`) | Text Prompt + 後処理 |
| **カラヴァッジョ光源配置法則（OCHABI）** | `caravaggio_light_placement_rule, light_hits_story_point, light_on_head_and_hands_for_narrative, window_light_from_upper_right_classic, light_placement_equals_storytelling`；カラヴァッジョ『聖マタイの召命』分析：光源=右上の窓；光が当たる人物の頭と手の動作=物語のポイント。**AI出図ルール**：「光が当たる場所=ストーリーのポイント」を意識して配置；`dramatic lighting, window light from upper right, spotlight on character` | Text Prompt + 後処理 |
| **3種表面材質システム（Peter Han）** | `surface_material_3types = {shiny / matte / weathered}, shiny_high_contrast_form_shadow_as_horizon_reflection, matte_low_contrast_soft_shadow_edge_white_pencil_highlight, weathered_assorted_crosshatch_rust_dents_scratches_holes_paint_wear, practice_on_simple_forms_first, center_line_contour_form_shadow_then_focal_point`；3種表面材質：①Shiny/Glossy=高コントラスト+形体影線=反射地平線→②Matte/Dull=低コントラスト+影境界柔化→③Weathered=多種排線+錆/凹み/傷/穴/塗装剥がれ。全ての表面に共通：Center Line→Contour→Form Shadow→Focal Pointの順序。**AI出図ルール**：`shiny metallic surface, high contrast reflection` / `matte surface, soft shadow edge` / `weathered surface, rust, scratches, paint wear` で材質指定 | Text Prompt + Lineart 参考図 |
| **Focal Point → Core Shadow → Detail Application ワークフロー（Peter Han）** | `focal_point_first_then_shadow_then_detail, detail_in_shadows_not_highlights, crosshatch_follows_contour_direction, no_flatten_by_full_texture_coverage, contrast_in_value_scale_pattern_complexity`；①Focal Point決定→②光源からCore Shadow特定→③排線は輪郭に沿う→④細部は影の中に置く→⑤全体をテクスチャで覆うと扁平化(禁止)。対比はvalue/scale/pattern/complexityの4軸。既有 `caravaggio_light_placement_rule`（OCHABI）=光源叙事、本条=細部適用ワークフロー+失敗モード（高价值重复） | Text Prompt + 後処理 |
| **Shadow Grouping 陰影分組法（Peter Han）** | `shadow_grouping, squint_to_separate_light_shadow, group_shadows_block_in, push_darks_in_shadows, balance_dark_and_light, lower_half_heavier_weight`；Squint法(薄目で見る)→光と影を分離→影をグループ化してブロック塗り→より速い可読性+細部の印象。下半分に重みを与える。既有 `silhouette_drawing_rule`（みにまる）=squintでシルエット、本条=squintで陰影分組（目的が異なるため共存）。**AI出図ルール**：`grouped shadows, high contrast light and shadow, dark lower half` | Text Prompt + 後処理 |
| **Pinch & Peak + Ribbon 布料褶皺定位法（Peter Han）** | `pinch_peak_cloth_system, pinch_at_body_underside_joints, peak_at_shoulders_elbows_protrusions, ribbon_flow_first, vertical_lines_at_turn_peak, creases_from_edge_center_open, condense_to_edges, soft_fold_vs_hard_fold, arrows_show_fold_path, geometric_shapes_for_clothing_sections`；布料褶皺の定位：Pinch(挨まれ)=身体下側/関節内側、Peak(突出)=肩/肘等；リボンの流れから始める；転彎頂点に垂直線；褶皺は端から始まり中心は空ける；柔褶皺 vs 硬褶皺。既有 `wrinkle_5_types`（みにまる）=5分類体系、本条=「どこから探すか」の直感的定位法（高价值重复） | Text Prompt + Lineart 参考図 |
| **直接光2色塗り分け法則（吉田誠治）【核心規則】** | `direct_light_2color_split, light_face_eq_showcase, shadow_face_single_fill, sharp_boundary_strong_impression, soft_boundary_weak_impression, decide_main_face_first`；直接光=その絵で一番強い光源；「光が当たっているかどうか」でざ2色に塗り分け；明暗境界くっきり=印象強、ぼやけ=印象弱；見せたい面に光を当て、見せたくない面は単色塗りつぶし。**AI出図WF**：Step1=明るい面(見せ場)を最初に決定→promptで `light direction` を明示 | Text Prompt + 後処理 |
| **反射光影色法則（吉田誠治）** | `reflected_light_blue_tint_in_shadow, sky_reflection_makes_shadow_blue, high_saturation_color_reflects_strongly, highlight_eq_light_source_reflection_visible, material_reflection_rate = {glass_metal_strong / wood_paper_weak}, reflected_light_band_shape_on_floor_wall`；影の中の微妙な明暗=反射光の影響；空や影からの反射光は青くなる→影も青色の影響；彩度の高い色はあちこちに反射；ハイライト=光源が反射して直接見えている状態；材質別反射率差。**AI出図ルール**：`blue_tinted_shadow, ambient_occlusion, reflected_light_band` | Text Prompt + 後処理 |
| **透過光SSS応用法則（吉田誠治）** | `SSS_skin_orange_pink_terminator, SSS_backlit_leaves_vivid_green, SSS_translucent_objects, SSS_occurs_backlight_not_cast_shadow, skin_terminator_warm_orange_pink, plant_leaf_backlit_bright_green, milk_juice_wax_SSS`；肌の明暗境界=明るいオレンジ/ピンクで塗る→SSS再現；植物の葉=裏側から光→緑が鮮やか；SSS=逆光寄りで発生、落ち影境界では発生しにくい。**AI出図ルール**：`subsurface_scattering, translucent_skin, backlit_foliage` | Text Prompt + 後処理 |
| **3光源ライティングWF（吉田誠治）【核心規則】** | `3light_source_WF, step1_direct_light_2color_split, step2_reflected_light_blue_tint, step3_SSS_translucent, key_light_main_fill_light_shadow_back_light_silhouette, indoor_very_dark_window_bright`；実際の風景=直接光+反射光+透過光の3つが同時存在；Step1=直接光で2色塗り分け→Step2=反射光(blue_tint)追加→Step3=透過光(SSS)追加；3点照明=キーライト(順光)+フィルライト(影潰さない)+バックライト(シルエット)；室内=とにかく暗く、窓の外=明るく。既有 `caravaggio_light_placement_rule`（OCHABI）=叙事光源、本条=3光源実装WF | Text Prompt + 後処理 |
| **順光逆光印象テーブル（吉田誠治）** | `front_light_vivid_color_detail_visible_depth_weak, back_light_silhouette_emphasis_atmospheric_perspective_strong, backlight_indoor_default_window_scene, 3point_lighting_key_fill_back`；順光=色彩鮮やか+細部見える+立体感分かりやすい；逆光=トーン抑えめ+シルエット強調+遠近感分かりやすい；逆光時は空気遠近法が顕著；昼間の室内背景=ほぼ逆光。**AI出図ルール**：`backlight` → `silhouette_emphasis + atmospheric_perspective` / `front_light` → `vivid_color + detail_visible` | Text Prompt |
| **見せ場集中描写法則（吉田誠治）** | `focal_point_contrast_boost, secondary_elements_lower_contrast, light_gradient_face_to_feet, showcase_eq_highlight_plus_high_saturation_plus_silhouette, non_showcase_underpainting_only_ok, focal_point_detail_density_high_periphery_low`；見せ場=光が当たっている+彩度高い+特徴的シルエット；見せ場にハイライト/影を一段丁寧に；見せ場以外=下塗りのままでOK；主役以外に影を作ってコントラスト下げ→視線誘導。既有 `focal_point_first_then_shadow_then_detail`（Peter Han）=影→細部WF、本条=見せ場密度集中（高价值重复） | Text Prompt + 後処理 |
| **6種空気遠近法テクニック（吉田誠治）【核心規則】** | `aerial_perspective_6techniques = {density_reduction / screen_overlay / contrast_gradient / compare_bright_shadow_crush / blur_DOF / combined}, density_far_less_detail, screen_sky_color_overlay_far, contrast_near_high_far_low, compare_bright_crush_shadow_only, blur_far_DOF, combine_multiple_for_best`；①密度=遠景の描き込み密度を落とす；②スクリーン=遠景に空の色を被せる；③コントラスト=近景高/遠景低；④比較(明)=影色だけ潰す→クリアな空気感；⑤ぼかし=被写界深度；⑥併用が最効果的。既有 `aerial_perspective_near_90pct`（OCHABI）=バリュー比率のみ、本条=6具体技法 | Text Prompt + 後処理(ComfyUIノード) |
| **レイリー/ミー散乱使い分け法則（吉田誠治）** | `rayleigh_scattering_blue_distance_dry_europe, mie_scattering_white_haze_humid_japan, cloud_white_mie, crepuscular_rays_mie, no_atmosphere_no_scattering_space`；レイリー散乱=遠くほど青く/湿度低い欧米風景；ミー散乱=遠くほど白く霎む/湿度高い日本風景；雲の白=ミー散乱；薄明光線=ミー散乱主因；宇宙=大気なし→散乱なし。**AI出図ルール**：日本風景=`mie_scattering, white_haze` / 欧州風景=`rayleigh_scattering, blue_distance` | Text Prompt |
| **色決定法則（吉田誠治）** | `color_decision_2color_contrast_first, sunlit_warm_yellow_shadow_cool_blue, impasto_high_contrast_many_colors, anime_cel_low_contrast_sharp_edge, watercolor_narrow_hue_bright, metal_reflects_surrounding_colors, avoid_muddy_use_2_primaries_only, CMY_min_to_zero_clears_mud`；まず2色置く→色の印象は対比で決まる；太陽光下=明るい面黄色寄り/暗い面青；厚塗り=コントラスト高+色数多；アニメ塗り=コントラスト低でもメリハリ；水彩=色相狭+明るめ；金属=周囲色を反射；濁り回避=CMY最小値を0に。**AI出図ルール**：`sunlit_face=warm_yellow, shadow_face=cool_blue` | Text Prompt |
| **配色比率法則（吉田誠治）** | `base_color_70pct, main_assort_color_25pct, accent_color_5pct, limit_palette_3_colors, high_saturation_small_area, change_only_1_of_HSB_for_harmony`；使う色を3色以下に限定；ベースカラー70%/メインカラー25%/アクセントカラー5%；彩度の高い色=面積を減らす；色相/彩度/明度のうち1つだけ変える→まとまりやすい。**AI出図ルール**：`limited_palette, base_70_accent_5` | Text Prompt |
| **彩度メリハリ法則（吉田誠治）** | `midtone_low_saturation_first, showcase_high_saturation_only, highlight_shadow_desaturated_realistic, wood_highlight_nearly_gray, metal_highlight_shadow_high_saturation, skin_ear_plant_SSS_keeps_saturation_in_shadow`；中間色の彩度を高く塗ると不自然→まず低彩度で着色、見せたいところだけ彩度UP；ハイライト/影=彩度かなり落とす→写実的；木=ほぼ灰色；金属=ハイライト/影の彩度高；肌/植物=SSSで影でも彩度維持 | Text Prompt |
| **地面明度法則（吉田誠治）** | `bright_ground_plane, sunlit_surface_near_white, asphalt_sunlit_nearly_white, plant_sunlit_yellow_green, indoor_window_overexpose, ground_brighter_than_you_think`；地面に置く色=想像以上に明るい；アスファルト=太陽光下でほぼ白；植物=太陽光下でほぼ黄色に近い緑；屋内=窓からの光は白飛びさせるくらい。既有 `floor_base_value_10pct`（OCHABI）=バリュー値のみ、本条=地面全般明度法則（高价值重复） | Text Prompt |
| **木漏れ日ピンホール法則（吉田誠治）** | `dappled_sunlight_pinhole_principle, round_light_spots_on_ground, low_leaf_sharp_shadow, high_leaf_round_blurred_shadow, shadow_blur_by_height_from_ground`；木漏れ日の光=丸いシルエット→ピンホールカメラの原理；低い葉の影=シルエットくっきり；高い葉の影=丸く削られた形；地面からの高さに比例して影がぼやける | Text Prompt + 後処理 |
| **透明感3要素法則（吉田誠治）** | `transparency_effect_3elements = {large_gradient + sharp_highlight + hue_shift}, gradient_light_to_dark_or_reverse, highlight_must_be_sharp_edged, hue_shift_avoid_muddy_combination`；透明感=①大きなグラデーション+②エッジの効いたハイライト+③色相の変化；グラデーション=光源方向ほど暗くても明るくてもOK；ハイライト=とにかくエッジ明確；色相変化=濁らない組み合わせ。既有 `shiny_high_contrast`（Peter Han）=部分カバー、本条=透明感専用法則 | Text Prompt |
| **抜け感空間法則（吉田誠治）** | `negative_space_behind_character, open_sky_gap, arch_framing, reduce_bg_density_around_subject, showcase_elsewhere_not_behind_subject`；人物周辺の背景密度を減らし、それ以外に背景の見せ場を用意→抜け感；人物の後ろに空をつくる；アーチ/窓/建物の高さ差で抜け感を演出。**AI出図ルール**：`negative_space, open_sky, arch_framing` | Text Prompt |
| **時間天候ライティング変化テーブル（吉田誠治）【核心規則】** | `time_weather_lighting_table, sunny_hard_shadow_high_contrast, cloudy_soft_shadow_low_contrast, evening_long_shadow_warm_haze, night_artificial_light_soft_shadow, base_layer_no_shadow_plus_shadow_layer_time_variant_plus_effect_layer_atmosphere`；晴れ=硬い影+高コントラスト；曇り=柔らかい影+低コントラスト；夕方=長い影+暖色霎；夜=人工光+ソフトシャドウ。**AI出図WF**：base_layer(影なし)+shadow_layer(時間別)+effect_layer(大気)の3層分離 | Text Prompt + 後処理(ComfyUIノード) |
| **室内照明リアリズム法則（吉田誠治）** | `night_interior_dark, light_falloff_realistic, colored_light_source, avoid_uniform_brightness, light_diffraction_saturated_edge, foreground_contrast_up_for_depth, near_black_slight_brightness_centers_eye`；夜景の室内=想像以上に暗い；照明の近くほど明るく、壁は照明から遠いので暗い；照明周辺を一律で明るくするのは不自然；光の回折=彩度の高い色を乗せる；照明自体にも色を乗せる；真黒にせず少し明るい色で中央に視線集中 | Text Prompt + 後処理 |
| **厚塗りWF法則（吉田誠治）** | `impasto_WF_6step, step1_shadow_silhouette_70pct_brush, step2_direct_light_reflected_light_50pct_brush, step3_eyedropper_blend_sideways, step4_refine_silhouette, step5_texture_color_variation_terminator, step6_highlight_finish, shadow_color_blue_tint, direct_light_yellow_tint, highlight_alone_raises_quality`；厚塗りWF：①影色(不透明度70%)でシルエット塗りつぶし→②直接光+反射光(不透明度50%)で軽い色重ね→③スポイト→横へ塗り広げ→④シルエット整え→⑤テクスチャ/色ムラ/明暗境界加筆→⑥ハイライト仕上げ；影=青み/直接光=黄色寄り/反射光=青寄り | Text Prompt + 後処理 |
| **雲描写法則（吉田誠治）** | `cloud_shadow_blue_top, cloud_yellow_bounce_light, cloud_dark_bottom_less_transmitted, cloud_silhouette_sharp_not_blurred, cloud_flow_horizontal_near_horizon, cloud_density_horizon_high, near_cloud_torn_detail_far_cloud_round`；雲=半透明な氷の集合体；上の影面=空を反射して青く明るい；光が当たった面からの反射で影の面が黄色く明るい；下ほど透過光届きにくく暗い；シルエットはぼかさない；地平線近くほど水平に流れる；手前の雲=細かく千切れ、遠景=丸い | Text Prompt |
| **水3要素描写法則（吉田誠治）【核心規則】** | `water_3elements = {water_color + bottom_transparency + surface_reflection}, fresnel_effect_near_transparent_far_reflective, deep_water_dark_color, shallow_water_bottom_visible, tropical_emerald_white_sand_mix, front_light_vivid_reflection_back_light_bright_surface, reflection_stretches_vertical`；水の描き方3要素：①水の色(青/深いほど濃い)②水底の透過(浅いほど見える)③水面の反射(フレネル効果=手前透明/奥反射)；南国の海=エメラルドグリーン(透明度高+白い砂浜)；反射=縦に伸びる | Text Prompt |
| **森描写レイヤー法則（吉田誠治）** | `forest_depth_layers, dark_foreground_bright_midground, random_tree_size_spacing, ground_slope_variation, midground_shadow_tunnel_depth, grass_silhouette_overlap_trees, foreground_darkest_shadow_heavy, dappled_light_bright_contrast`；森=奥行き方向に明暗を層状に重ねる；前景=一番暗い影を重く；木漏れ日=ガツンと明るく；中景に影のトンネル→空間感；木の太さ/配置=ランダムに変化；地面は傾斜；草のシルエットを木と重ねて密度感 | Text Prompt |
| **岩石材質描写法則（吉田誠治）** | `rock_painting_WF = {rough_silhouette / edge_contrast_boost / crack_detail / moss_highlight_contact_shadow}, rock_angular_edges_even_on_round, edge_terminator_contrast_up, large_rock_add_cracks, surface_stain_subtle, contact_shadow_darkest`；岩=丸いシルエットでもところどころ角がある；明暗境界のエッジコントラストUP；大きい岩=割れ目も入れる；表面の微妙な汚れ+苔+接地影。既有 `weathered_assorted_crosshatch`（Peter Han）=風化表面全般、本条=岩石専用WF（高价值重复） | Text Prompt + 後処理 |
| **夜景描写法則（吉田誠治）** | `night_rain_WF = {low_contrast_low_saturation / wet_road_reflection / detail_preserve_values / artificial_light_colored}, night_overall_low_contrast_low_saturation, wet_surface_light_spread, preserve_initial_big_values, artificial_light_not_pure_white_add_color, convenience_store_light_bright_not_white`；夜=全体コントラスト低+彩度低；濡れた路面=光が反射して広がる；最初に決めた大きな明暗をいじらない；人工光=真っ白ではなく色を乗せる；コンビニの光=明るいが真っ白ではない | Text Prompt + 後処理 |

### 🗄️ 抽屉 5：⚙️ [AI 硬核参数]

*(记录：CFG 甜区、Denoising 比例、采样器建议)*

- **探索期垫图抽卡（IPAdapter）**：权重建议 `0.6 - 0.8`。
- **标准 SDXL 节点配置（KSampler）**：
  - 大模型：`sd_xl_base_1.0.safetensors`
  - 步数（Steps）：`30 - 40`
  - 提示词引导系数（CFG）：`5.0 - 7.0`
  - 采样器：`euler_ancestral`，调度器：`normal`
- **批量一致性保护**：连续批量出图时，建议在批次之间执行 `Purge Cache`；当需要冻结风格时，优先锁 `seed + LoRA + concept anchor` 组合，而不是只盯 Prompt 文案。

| 规则簇 | 可执行 Tag / 数值 / 约束 | 节点落点 |
| :--- | :--- | :--- |
| **コマ打ち帧数换算表** | `koma_uchi_system, 1koma_24fps_full, 2koma_12fps_tv_standard, 3koma_8fps_tv_economy`；1コマ打ち = 24 画/秒（Full Animation）；2コマ打ち = 12 画/秒（TV 标准）；3コマ打ち = 8 画/秒（TV 省帧）。游戏 60fps 换算：2コマ = 每 5 帧换 1 张 = 12fps；3コマ = 每 7–8 帧换 1 张 = 8fps。Unity AnimationClip SampleRate 应匹配コマ打ち设定 | AnimationClip 参数 + sprite sheet 帧数规划 |
| **安全フレームオーバーサイズ生成法則（Telecom2）** | `safe_frame_oversize_for_camera, generate_1_2x_to_1_5x_display_resolution, prevent_edge_exposure_during_pan, prevent_pixelation_during_zoom`；Unityでパン/ズームされる背景やスプライトは、最終表示解像度の1.2～1.5倍で生成。パン時の端露出とズーム時のピクセル化を防止。カメラワークがない場合は等倍でOK | KSampler解像度設定 + Unity Camera設定 |
| **セルレイヤー分離生成法則（Telecom2）** | `cel_layer_separation_for_unity, generate_BG_midground_character_FG_effects_separately, each_layer_separate_AI_pass, consistent_perspective_across_layers, maps_to_unity_sorting_layers`；ゲームアセットを透明レイヤー別に生成(BG/中景/キャラ/前景/エフェクト)。UnityのSorting Layerシステムに直接マッピング。各レイヤーは別々のAI生成パスで、一貫した透視を維持 | ComfyUIワークフロー設計 + Unity Sorting Layer |

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

*Last Updated: 2026-04-11 by Manus TA*
*Peter Han Distillation: 2026-04-10 — Peter Han《Dynamic Bible》14 条新增（透视8 / 光影4 / 解剖3）+ 5 条高价值重复 + 2 条纯重复剔除 + 0 条冲突仲裁（全规則兼容）*
*Hart Distillation: 2026-04-10 — Christopher Hart《Figure It Out! Drawing Essential Poses》16 条新增 + 2 条高价值重复合并*
*Telecom Distillation: 2026-04-10 — Telecom Animation Film《アニメーション・バイブル》13 条新增 + 2 条高价值重复 + 1 条帧数修订*
*Matsuoka Distillation: 2026-04-10 — 松岡伸治《イラスト・漫画のためのキャラクター描画教室》45 条新增（解剖30 / 透视8 / 动画7）+ 20 条高价值重复合并 + 2 条场景分治仲裁（头身比 / 线宽）*
*Satou Distillation: 2026-04-10 — 砂糖ふくろう《10パーセントの力で描くはじめてのジェスチャードローイング》21 条新增（动画10 / 解剖7 / 透视4）+ 5 条高价值重复 + 1 条场景分治仲裁（印象駆動 vs 解剖精度）*
*Minimaru Distillation: 2026-04-10 — みにまる《ちょこっと人体解剖学で圧倒的にうまく描けるキャラクターデッサン》18 条新增（解剖28 / 透视5 / 动画2 / 光影1）+ 11 条高价值重复 + 0 条冲突仲裁（全规則兼容）*
*OCHABI Distillation: 2026-04-10 — OCHABI Institute《線一本からはじめる伝わる絵の描き方》20 条新增（透视8 / 光影5 / 解剖3 / 动画4）+ 6 条高价值重复 + 1 条场景分治仲裁（題比率6等分 vs 3分割）*
*Yoshida Distillation: 2026-04-10 — 吉田誠治《TIPS! 絵が描きたくなるヒント集》26 条新增（光影19 / 透视7）+ 4 条高价值重复 + 3 条純重复剔除 + 1 条场景分治仲裁（アイソメトリック gameplay_sprite禁止 / tilemap_design許可）*
*Telecom2 Distillation: 2026-04-11 — Telecom Animation Film《アニメーション・バイブル》第二輪深度蒸留 — 22 条新増（動画11 / 解剖6 / 透视2 / 光影1 / AI参数2）+ 1 条既有升级(VFX循环法则) + 2 条高价值重复強化 + 1 条純重复剔除(smoke_dissipation) + 1 条场景分治仲裁(キャラモデル一貫性: 帧間=3アンカー / batch間=4指標) + 0 条A/B実測工単*
*Muroi Distillation: 2026-04-11 — 室井康雄《アニメ私塾流 最速でなんでも描けるようになるキャラ作画の技術》19 条新増（動画7 / 解剖6 / 透视6）+ 1 条既有升级(wrinkle_5_types 布レイヤリング追加) + 3 条高価値重複(hand_mitten / walk_heel_kickback / run_style_body_type) + 2 条純重複剔除(腕振り振り子 / 立ち幅跳びS&S) + 0 条冲突仲裁 + 0 条A/B実測工単*
