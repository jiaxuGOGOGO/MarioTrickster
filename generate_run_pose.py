"""
生成 8 帧跑步循环的 OpenPose 风格骨架图
用于 ComfyUI ControlNet OpenPose 输入

跑步循环 8 帧关键姿态（侧面视角，面朝右）：
  F1: 接触期（右脚着地，左腿后摆）
  F2: 承重期（右腿弯曲吸收冲击）
  F3: 中间支撑（右腿伸直，左腿前摆中）
  F4: 推蹬期（右脚蹬地，左腿前摆高点）
  F5: 接触期（左脚着地，右腿后摆）— 镜像 F1
  F6: 承重期（左腿弯曲）— 镜像 F2
  F7: 中间支撑（左腿伸直，右腿前摆中）— 镜像 F3
  F8: 推蹬期（左脚蹬地，右腿前摆高点）— 镜像 F4

骨架关节点（OpenPose 18 点简化为侧面关键点）：
  头、颈、右肩、右肘、右手、左肩、左肘、左手、
  右髋、右膝、右踝、左髋、左膝、左踝
"""

from PIL import Image, ImageDraw
import os
import math

# 画布尺寸（与 ComfyUI 出图尺寸匹配）
W, H = 512, 768
# 角色中心 x 偏移
CX = W // 2
# 地面 y
GROUND_Y = H - 60
# 身体比例基准
SCALE = 1.0

# 关节颜色（OpenPose 标准配色简化版）
COLORS = {
    'head': (255, 0, 0),
    'neck': (255, 85, 0),
    'torso': (255, 170, 0),
    'r_arm': (255, 255, 0),
    'l_arm': (170, 255, 0),
    'r_leg': (85, 255, 0),
    'l_leg': (0, 255, 85),
}

# 骨骼连接线颜色
BONE_COLORS = {
    'head_neck': (255, 0, 0),
    'neck_rshoulder': (255, 85, 0),
    'rshoulder_relbow': (255, 170, 0),
    'relbow_rhand': (255, 255, 0),
    'neck_lshoulder': (170, 255, 0),
    'lshoulder_lelbow': (85, 255, 0),
    'lelbow_lhand': (0, 255, 0),
    'neck_rhip': (0, 255, 85),
    'neck_lhip': (0, 255, 170),
    'rhip_rknee': (0, 255, 255),
    'rknee_rankle': (0, 170, 255),
    'lhip_lknee': (0, 85, 255),
    'lknee_lankle': (0, 0, 255),
}


def get_run_keypoints(frame_idx):
    """
    返回第 frame_idx 帧（0~7）的关节坐标字典。
    侧面跑步循环，面朝右。
    坐标系：(0,0) 在左上角，x 向右，y 向下。
    """
    # 身体核心位置（跑步时有轻微上下浮动）
    bounce = [0, 8, 4, -4, 0, 8, 4, -4]  # 上下浮动
    lean = [5, 8, 6, 3, 5, 8, 6, 3]  # 前倾

    by = GROUND_Y - 280 + bounce[frame_idx]  # 髋部 y
    bx = CX + lean[frame_idx]

    # 髋部
    hip_y = by
    r_hip = (bx + 5, hip_y)
    l_hip = (bx - 5, hip_y)

    # 颈部（躯干上方）
    neck = (bx + 12, hip_y - 160)

    # 头部
    head = (neck[0] + 5, neck[1] - 65)

    # 跑步手臂和腿部角度（8帧循环）
    # 角度以度为单位，0=向下，正=向前（右），负=向后（左）
    arm_angles = [
        # (右臂上臂角, 右臂前臂角, 左臂上臂角, 左臂前臂角)
        (30, 70, -40, -80),    # F1
        (15, 50, -25, -60),    # F2
        (-10, 40, 10, -40),    # F3
        (-40, -70, 30, 60),    # F4
        (-40, -80, 30, 70),    # F5 (mirror F1)
        (-25, -60, 15, 50),    # F6 (mirror F2)
        (10, -40, -10, 40),    # F7 (mirror F3)
        (30, 60, -40, -70),    # F8 (mirror F4)
    ]

    leg_angles = [
        # (右大腿角, 右小腿角, 左大腿角, 左小腿角)
        (-25, -10, 35, 50),    # F1: 右脚着地前方，左腿后摆
        (-15, -30, 25, 30),    # F2: 右腿弯曲承重
        (5, -5, 5, 40),       # F3: 右腿支撑，左腿前摆
        (20, 10, -25, -15),    # F4: 右脚蹬地，左腿前摆高
        (35, 50, -25, -10),    # F5: 镜像 F1
        (25, 30, -15, -30),    # F6: 镜像 F2
        (5, 40, 5, -5),       # F7: 镜像 F3
        (-25, -15, 20, 10),    # F8: 镜像 F4
    ]

    f = frame_idx
    r_upper_arm_a, r_forearm_a, l_upper_arm_a, l_forearm_a = arm_angles[f]
    r_thigh_a, r_shin_a, l_thigh_a, l_shin_a = leg_angles[f]

    # 肩膀
    r_shoulder = (neck[0] + 8, neck[1] + 15)
    l_shoulder = (neck[0] - 8, neck[1] + 15)

    # 手臂长度
    upper_arm_len = 70
    forearm_len = 65

    def angle_to_point(origin, angle_deg, length):
        """从 origin 出发，angle_deg 度方向（0=下，正=前/右），长度 length"""
        rad = math.radians(angle_deg)
        dx = math.sin(rad) * length
        dy = math.cos(rad) * length
        return (int(origin[0] + dx), int(origin[1] + dy))

    # 右臂
    r_elbow = angle_to_point(r_shoulder, r_upper_arm_a, upper_arm_len)
    r_hand = angle_to_point(r_elbow, r_forearm_a, forearm_len)

    # 左臂
    l_elbow = angle_to_point(l_shoulder, l_upper_arm_a, upper_arm_len)
    l_hand = angle_to_point(l_elbow, l_forearm_a, forearm_len)

    # 腿部长度
    thigh_len = 100
    shin_len = 95

    # 右腿
    r_knee = angle_to_point(r_hip, r_thigh_a, thigh_len)
    r_ankle = angle_to_point(r_knee, r_shin_a, shin_len)

    # 左腿
    l_knee = angle_to_point(l_hip, l_thigh_a, thigh_len)
    l_ankle = angle_to_point(l_knee, l_shin_a, shin_len)

    return {
        'head': head,
        'neck': neck,
        'r_shoulder': r_shoulder,
        'r_elbow': r_elbow,
        'r_hand': r_hand,
        'l_shoulder': l_shoulder,
        'l_elbow': l_elbow,
        'l_hand': l_hand,
        'r_hip': r_hip,
        'r_knee': r_knee,
        'r_ankle': r_ankle,
        'l_hip': l_hip,
        'l_knee': l_knee,
        'l_ankle': l_ankle,
    }


def draw_skeleton(draw, kp):
    """在 ImageDraw 上绘制 OpenPose 风格骨架"""
    line_width = 8
    joint_radius = 10

    # 骨骼连接
    bones = [
        ('head', 'neck', BONE_COLORS['head_neck']),
        ('neck', 'r_shoulder', BONE_COLORS['neck_rshoulder']),
        ('r_shoulder', 'r_elbow', BONE_COLORS['rshoulder_relbow']),
        ('r_elbow', 'r_hand', BONE_COLORS['relbow_rhand']),
        ('neck', 'l_shoulder', BONE_COLORS['neck_lshoulder']),
        ('l_shoulder', 'l_elbow', BONE_COLORS['lshoulder_lelbow']),
        ('l_elbow', 'l_hand', BONE_COLORS['lelbow_lhand']),
        ('neck', 'r_hip', BONE_COLORS['neck_rhip']),
        ('neck', 'l_hip', BONE_COLORS['neck_lhip']),
        ('r_hip', 'r_knee', BONE_COLORS['rhip_rknee']),
        ('r_knee', 'r_ankle', BONE_COLORS['rknee_rankle']),
        ('l_hip', 'l_knee', BONE_COLORS['lhip_lknee']),
        ('l_knee', 'l_ankle', BONE_COLORS['lknee_lankle']),
    ]

    for start_name, end_name, color in bones:
        start = kp[start_name]
        end = kp[end_name]
        draw.line([start, end], fill=color, width=line_width)

    # 关节点
    joint_colors = {
        'head': COLORS['head'],
        'neck': COLORS['neck'],
        'r_shoulder': COLORS['torso'],
        'r_elbow': COLORS['r_arm'],
        'r_hand': COLORS['r_arm'],
        'l_shoulder': COLORS['torso'],
        'l_elbow': COLORS['l_arm'],
        'l_hand': COLORS['l_arm'],
        'r_hip': COLORS['torso'],
        'r_knee': COLORS['r_leg'],
        'r_ankle': COLORS['r_leg'],
        'l_hip': COLORS['torso'],
        'l_knee': COLORS['l_leg'],
        'l_ankle': COLORS['l_leg'],
    }

    for name, pos in kp.items():
        color = joint_colors.get(name, (255, 255, 255))
        r = joint_radius
        draw.ellipse([pos[0]-r, pos[1]-r, pos[0]+r, pos[1]+r], fill=color)


def main():
    output_dir = '/home/ubuntu/MarioTrickster/pose_references'
    os.makedirs(output_dir, exist_ok=True)

    frames = []
    for i in range(8):
        img = Image.new('RGB', (W, H), (0, 0, 0))
        draw = ImageDraw.Draw(img)
        kp = get_run_keypoints(i)
        draw_skeleton(draw, kp)

        # 保存单帧
        frame_path = os.path.join(output_dir, f'run_pose_f{i+1:02d}.png')
        img.save(frame_path)
        frames.append(img)
        print(f'Saved: {frame_path}')

    # 拼接成一张总览图（2行4列）
    overview = Image.new('RGB', (W * 4, H * 2), (30, 30, 30))
    for i, frame in enumerate(frames):
        col = i % 4
        row = i // 4
        overview.paste(frame, (col * W, row * H))

    overview_path = os.path.join(output_dir, 'run_pose_overview.png')
    overview.save(overview_path)
    print(f'Saved overview: {overview_path}')


if __name__ == '__main__':
    main()
