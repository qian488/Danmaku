# -*- coding: utf-8 -*-
"""
生成 Stage01~04 关卡资源（与主菜单难度一一对应：简单/普通/困难/无尽）。

- 弹幕任务直接写入 Tasks，不在运行时做周期 Boss 合并。
- 每 BOSS_INTERVAL 秒一条 Boss（SpawnEnemy + IsBossEnemy + BossFolderName）。
- 难度：Easy 略疏略慢；Normal 为基准；Hard / Expand 更密、更快、ways 更多。

运行（在 Demos/Danmaku 目录或任意 cwd）:
  python Assets/Demos/Danmaku/Editor/GenerateExpandedStages.py
"""
import os

SCRIPT_GUID = "e341e7ffd12dfca4a93f1d8c7d3d342a"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 与 Resources/Demos/Danmaku/Img/boss/ 下文件夹名一致，顺序即出场顺序
BOSS_FOLDER_NAMES = [
    "Alice",
    "AwanLjan",
    "Cirno",
    "Daiyousei",
    "DaiyouseiB",
    "Doremi",
    "Handan",
    "Reisen",
    "RMarisa",
    "RReimu",
    "Tewi",
]

BOSS_INTERVAL = 30.0
# Boss TriggerTime 相对整点略偏后，避免与同秒弹幕任务顺序纠缠
BOSS_TIME_EPS = 0.06


def _boss_arena_bullet_loop_yaml():
    """Boss 战场地弹幕循环；与 StageData / 工程内 Stage01.asset 中 BossArenaBulletLoop 块一致。"""
    return (
        "  BossArenaBulletLoopFirstDelaySeconds: 0.5\n"
        "  BossArenaBulletLoopIntervalSeconds: 2\n"
        "  BossArenaBulletLoop:\n"
        "  - BulletOrigin: {x: 6.4, y: 2.2}\n"
        "    BulletWaveRandomXBetweenPlayRightAndOffscreen: 0\n"
        "    BulletSpawnExtraRightMin: -1\n"
        "    BulletSpawnExtraRightMax: -1\n"
        "    Shooter:\n"
        "      Ways: 14\n"
        "      Angle: 180\n"
        "      VecAngle: 180\n"
        "      Range: 140\n"
        "      Radius: 0.18\n"
        "      Spy: 0\n"
        "    BulletSpeed: 2.65\n"
        "    BulletHitRadius: 0.06\n"
        "    BulletLayer: 0\n"
        "    BulletStyleFolderIndex: -1\n"
        "    BulletSpriteVariantIndex: -1\n"
        "  - BulletOrigin: {x: 6.35, y: 0}\n"
        "    BulletWaveRandomXBetweenPlayRightAndOffscreen: 0\n"
        "    BulletSpawnExtraRightMin: -1\n"
        "    BulletSpawnExtraRightMax: -1\n"
        "    Shooter:\n"
        "      Ways: 20\n"
        "      Angle: 180\n"
        "      VecAngle: 180\n"
        "      Range: 360\n"
        "      Radius: 0.35\n"
        "      Spy: 0\n"
        "    BulletSpeed: 2.9\n"
        "    BulletHitRadius: 0.06\n"
        "    BulletLayer: 0\n"
        "    BulletStyleFolderIndex: -1\n"
        "    BulletSpriteVariantIndex: -1\n"
        "  - BulletOrigin: {x: 6.35, y: -2.4}\n"
        "    BulletWaveRandomXBetweenPlayRightAndOffscreen: 0\n"
        "    BulletSpawnExtraRightMin: -1\n"
        "    BulletSpawnExtraRightMax: -1\n"
        "    Shooter:\n"
        "      Ways: 18\n"
        "      Angle: 180\n"
        "      VecAngle: 180\n"
        "      Range: 360\n"
        "      Radius: 0.28\n"
        "      Spy: 0\n"
        "    BulletSpeed: 2.66\n"
        "    BulletHitRadius: 0.06\n"
        "    BulletLayer: 0\n"
        "    BulletStyleFolderIndex: -1\n"
        "    BulletSpriteVariantIndex: -1\n"
    )


def _fmt(f, nd=3):
    return round(float(f), nd)


def bullet_wave(t, ox, oy, ways, bs, angle=180, rng=360, radius=0.2, spy=0):
    ox, oy, bs, radius = _fmt(ox), _fmt(oy), _fmt(bs, 2), _fmt(radius, 3)
    return (
        f"  - TriggerTime: {t}\n"
        "    TaskType: 1\n"
        "    EnemyPrefabPath: \n"
        "    SpawnPosition: {x: 0, y: 0}\n"
        f"    BulletOrigin: {{x: {ox}, y: {oy}}}\n"
        "    Shooter:\n"
        f"      Ways: {ways}\n"
        f"      Angle: {angle}\n"
        f"      VecAngle: {angle}\n"
        f"      Range: {rng}\n"
        f"      Radius: {radius}\n"
        f"      Spy: {spy}\n"
        f"    BulletSpeed: {bs}\n"
        "    BulletHitRadius: 0.06\n"
        "    BulletLayer: 0\n"
        "    BGMName: \n"
        "    PhaseName: \n"
        "    MessageText: \n"
    )


def enemy_spawn(t, y):
    y = _fmt(y)
    return (
        f"  - TriggerTime: {t}\n"
        "    TaskType: 0\n"
        "    EnemyPrefabPath: Demos/Danmaku/Prefabs/Enemy\n"
        f"    SpawnPosition: {{x: 5.5, y: {y}}}\n"
        "    BulletOrigin: {x: 0, y: 0}\n"
        "    Shooter:\n"
        "      Ways: 1\n"
        "      Angle: 0\n"
        "      VecAngle: 0\n"
        "      Range: 360\n"
        "      Radius: 0\n"
        "      Spy: 0\n"
        "    BulletSpeed: 3\n"
        "    BulletHitRadius: 0.06\n"
        "    BulletLayer: 0\n"
        "    BGMName: \n"
        "    PhaseName: \n"
        "    MessageText: \n"
    )


def boss_spawn(t, boss_folder: str):
    name = boss_folder.strip()
    t = _fmt(t)
    return (
        f"  - TriggerTime: {t}\n"
        "    TaskType: 0\n"
        "    EnemyPrefabPath: Demos/Danmaku/Prefabs/Enemy\n"
        "    SpawnPosition: {x: 5.5, y: 0.4}\n"
        "    SpawnEnemyUseManualPosition: 0\n"
        "    IsBossEnemy: 1\n"
        f"    BossFolderName: {name}\n"
        "    BlockStageTimelineUntilBossDefeated: 1\n"
        "    BossHpStageMultiplier: 1\n"
        "    BulletOrigin: {x: 0, y: 0}\n"
        "    BulletWaveRandomXBetweenPlayRightAndOffscreen: 0\n"
        "    BulletSpawnExtraRightMin: -1\n"
        "    BulletSpawnExtraRightMax: -1\n"
        "    Shooter:\n"
        "      Ways: 1\n"
        "      Angle: 0\n"
        "      VecAngle: 0\n"
        "      Range: 360\n"
        "      Radius: 0\n"
        "      Spy: 0\n"
        "    BulletSpeed: 3\n"
        "    BulletHitRadius: 0.06\n"
        "    BulletLayer: 0\n"
        "    BGMName: \n"
        "    PhaseName: \n"
        "    MessageText: \n"
    )


def phase_change(t, name):
    return (
        f"  - TriggerTime: {t}\n"
        "    TaskType: 3\n"
        "    EnemyPrefabPath: \n"
        "    SpawnPosition: {x: 0, y: 0}\n"
        "    BulletOrigin: {x: 0, y: 0}\n"
        "    Shooter:\n"
        "      Ways: 1\n"
        "      Angle: 0\n"
        "      VecAngle: 0\n"
        "      Range: 360\n"
        "      Radius: 0\n"
        "      Spy: 0\n"
        "    BulletSpeed: 3\n"
        "    BulletHitRadius: 0.06\n"
        "    BulletLayer: 0\n"
        "    BGMName: \n"
        f"    PhaseName: {name}\n"
        "    MessageText: \n"
    )


def message_task(t, text):
    return (
        f"  - TriggerTime: {t}\n"
        "    TaskType: 4\n"
        "    EnemyPrefabPath: \n"
        "    SpawnPosition: {x: 0, y: 0}\n"
        "    BulletOrigin: {x: 0, y: 0}\n"
        "    Shooter:\n"
        "      Ways: 1\n"
        "      Angle: 0\n"
        "      VecAngle: 0\n"
        "      Range: 360\n"
        "      Radius: 0\n"
        "      Spy: 0\n"
        "    BulletSpeed: 3\n"
        "    BulletHitRadius: 0.06\n"
        "    BulletLayer: 0\n"
        "    BGMName: \n"
        "    PhaseName: \n"
        f"    MessageText: {text}\n"
    )


def build_tasks(start_msg, clear_msg, wm, bsm, dt_mul):
    """
    StageRunner 要求 Tasks 按 TriggerTime 非递减；同秒顺序由 Unity 内 TieBreak 处理。
    wm: ways 倍率；bsm: 弹速倍率；dt_mul: 波次时间步倍率（越大越疏，适合简单难度）。
    """
    chunks = []
    seq = 0

    def add(t, text):
        nonlocal seq
        chunks.append((float(t), seq, text))
        seq += 1

    n_boss = len(BOSS_FOLDER_NAMES)
    total_span = BOSS_INTERVAL * n_boss
    # 道中弹幕铺到「最后一个 Boss 稍前」
    wave_end = total_span - 2.0

    add(0, message_task(0, start_msg))

    y_cycle = [2.4, 1.2, 0, -1.2, -2.4, 1.8, -1.8, 0.8]
    ox_cycle = [6.5, 6.2, 6.0, 5.8, 6.4, 6.1]

    # 三阶段分界（按总时长比例，使长关仍有「前/中/后」感）
    p1_end = wave_end * 0.29
    p2_end = wave_end * 0.53

    # —— 前半 ——
    t = 2.0
    idx = 0
    while t < p1_end:
        ox = ox_cycle[idx % len(ox_cycle)]
        oy = y_cycle[(idx // 2) % len(y_cycle)]
        ways = int(min(40, max(6, round((9 + (idx % 8) * 2) * wm))))
        bs = round((2.55 + (idx % 6) * 0.14) * bsm, 2)
        rng = 110 + (idx * 19) % 250
        radius = 0.12 + (idx % 5) * 0.11
        spy = 1 if idx % 9 == 0 else 0
        add(round(t, 3), bullet_wave(round(t, 3), ox, oy, ways, bs, rng=rng, radius=radius, spy=spy))
        if idx % 2 == 0:
            add(round(t + 0.35, 3), enemy_spawn(round(t + 0.35, 3), y_cycle[idx % len(y_cycle)]))
        if idx % 5 == 0:
            add(round(t + 0.65, 3), enemy_spawn(round(t + 0.65, 3), y_cycle[(idx + 4) % len(y_cycle)]))
        t += (1.85 + (idx % 4) * 0.25) * dt_mul
        idx += 1

    add(_fmt(p1_end + 0.3), phase_change(_fmt(p1_end + 0.3), "Phase Mid"))

    # —— 中段 ——
    t = max(100.0, p1_end + 2.0)
    idx = 0
    while t < p2_end:
        for off in (0.0, 0.65):
            ox = 6.35 - (idx % 6) * 0.12
            oy = -3.3 + ((idx + int(off * 3)) % 8) * 0.92
            ways = int(min(44, max(11, round((15 + idx % 9) * wm))))
            bs = round((2.75 + (idx % 7) * 0.18) * bsm, 2)
            tt = round(t + off, 3)
            add(
                tt,
                bullet_wave(
                    tt,
                    ox,
                    oy,
                    ways,
                    bs,
                    rng=150 + idx % 210,
                    radius=0.18 + idx % 4 * 0.14,
                ),
            )
        if idx % 2 == 0:
            te1 = round(t + 0.15, 3)
            te2 = round(t + 0.5, 3)
            add(te1, enemy_spawn(te1, y_cycle[idx % len(y_cycle)]))
            add(te2, enemy_spawn(te2, y_cycle[(idx + 2) % len(y_cycle)]))
        t += (2.2 + (idx % 5) * 0.35) * dt_mul
        idx += 1

    add(_fmt(p2_end + 0.3), phase_change(_fmt(p2_end + 0.3), "Phase Final"))

    # —— 终盘（直到接近最后一 Boss）——
    t = max(176.2, p2_end + 2.0)
    idx = 0
    while t < wave_end:
        ox = 6.35 + (idx % 3) * 0.08
        oy = -3.6 + (idx % 9) * 0.88
        ways = int(min(48, max(14, round((20 + idx % 12) * wm))))
        bs = round((2.95 + (idx % 6) * 0.2) * bsm, 2)
        tt = round(t, 3)
        add(tt, bullet_wave(tt, ox, oy, ways, bs, rng=360, radius=0.28 + idx % 6 * 0.12))
        if idx % 3 == 0:
            te1 = round(t + 0.25, 3)
            te2 = round(t + 0.55, 3)
            add(te1, enemy_spawn(te1, y_cycle[idx % len(y_cycle)]))
            add(te2, enemy_spawn(te2, y_cycle[(idx + 5) % len(y_cycle)]))
        t += (1.55 + (idx % 6) * 0.12) * dt_mul
        idx += 1

    # —— Boss：30s、60s、…（略偏 epsilon 避免与同秒波次冲突）——
    for i, folder in enumerate(BOSS_FOLDER_NAMES):
        bt = (i + 1) * BOSS_INTERVAL + BOSS_TIME_EPS
        add(_fmt(bt, 2), boss_spawn(bt, folder))

    add(_fmt(total_span + 1.0, 2), message_task(total_span + 1.0, clear_msg))

    chunks.sort(key=lambda x: (x[0], x[1]))
    return "".join(c[2] for c in chunks)


def asset_header(name, stage_id, bullet_layers):
    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}\n"
        f"  m_Name: {name}\n"
        "  m_EditorClassIdentifier: \n"
        f"  StageId: {stage_id}\n"
        "  StageBGM: Demos/Danmaku/Audio/BGM/game\n"
        "  BackgroundPrefabPath: \n"
        f"  BulletLayerCount: {bullet_layers}\n"
        "  SpawnEnemyFromRightEdgeOffscreen: 1\n"
        "  EnemySpawnExtraRightWorld: 1.2\n"
        "  BulletWaveRandomXBetweenPlayRightAndOffscreen: 0\n"
        "  BulletSpawnExtraRightMin: 0\n"
        "  BulletSpawnExtraRightMax: 5\n"
        + _boss_arena_bullet_loop_yaml()
        + "  EnablePeriodicBossSpawns: 0\n"
        + "  PeriodicBossIntervalSeconds: 30\n"
        + "  PeriodicBossEnemyPrefabPath: \n"
        + "  PeriodicBossFolderNames: []\n"
        + "  Tasks:\n"
    )


def main():
    # (文件名, StageId, 开局 Message, 通关 Message, ways 倍率, 弹速倍率, 波次间隔倍率[大=更疏])
    specs = [
        ("Stage01.asset", "Stage01", "Stage 01 — Easy（简单）", "Stage 01 Clear", 0.88, 0.9, 1.14),
        ("Stage02.asset", "Stage02", "Stage 02 — Normal（普通）", "Stage 02 Clear", 1.0, 1.0, 1.0),
        ("Stage03.asset", "Stage03", "Stage 03 — Hard（困难）", "Stage 03 Clear", 1.2, 1.08, 0.9),
        ("Stage04.asset", "Stage04", "Stage 04 — Expand（无尽用关）", "Stage 04 Clear", 1.32, 1.12, 0.85),
    ]
    for fname, sid, start_m, clear_m, wm, bsm, dt_mul in specs:
        body = asset_header(sid, sid, 1) + build_tasks(start_m, clear_m, wm, bsm, dt_mul)
        path = os.path.join(ROOT, fname)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(body)
        print("Wrote", path, "chars", len(body))


if __name__ == "__main__":
    main()
