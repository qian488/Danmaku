# -*- coding: utf-8 -*-
"""
批量生成弹幕 Resources/Demos/Danmaku/Img 下 Sprite Atlas（Unity Batch Mode + -executeMethod）。

Unity 可执行文件解析顺序：
  1. 环境变量 UNITY_EDITOR / UNITY_EDITOR_PATH（显式覆盖，指向 Unity.exe）。
  2. 根据 ProjectVersion.txt，在以下目录中找「版本文件夹/Editor/Unity(.exe)」：
     - Unity Hub 默认路径（Program Files 等）；
     - UNITY_EXTRA_EDITOR_ROOTS：分号分隔的**父目录**列表（适用于自定义安装，如 E:\\software\\unity）。
  3. Windows：若仍找不到，再使用命令行中带本工程 -projectPath 的已运行 Unity.exe。

重要：Batch Mode 会再启动一个 Unity 进程打开同一工程。若本工程已在 Editor 中打开，通常会失败。
      请先关闭该工程的 Unity 窗口，或使用菜单 Tools/Demos/Danmaku/Batch Img Sprite Atlases 在已打开的 Editor 内生成。

运行（在仓库根目录）:
  python Assets/Demos/Danmaku/Editor/BuildImgSpriteAtlases.py

参数：
  --force     跳过「本工程是否已有 Editor 打开」的预检（一般不建议）。
  第一个其余参数：Unity -logFile 路径；默认 “-” 表示输出到控制台。

预检：若存在 Library/EditorInstance.json 且其中 PID 在 Windows 上仍对应 Unity.exe，
      则直接退出（码 3），避免再启动 Batch 进程触发 Unity 崩溃式报错。
"""
import json
import os
import re
import subprocess
import sys


def _unity_project_root():
    d = os.path.dirname(os.path.abspath(__file__))
    d = os.path.dirname(d)  # Danmaku
    d = os.path.dirname(d)  # Demos
    d = os.path.dirname(d)  # Assets
    return os.path.dirname(d)


def _read_editor_version(project_root):
    path = os.path.join(project_root, "ProjectSettings", "ProjectVersion.txt")
    if not os.path.isfile(path):
        return None
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            for line in f:
                line = line.strip()
                if line.startswith("m_EditorVersion:"):
                    return line.split(":", 1)[1].strip()
    except OSError:
        return None
    return None


def _norm_path(p):
    return os.path.normcase(os.path.abspath(os.path.normpath(p)))


def _project_in_command_line(project_root, command_line):
    if not command_line:
        return False
    pr = _norm_path(project_root)
    # Unity: -projectPath "D:\a\b" 或 -projectPath D:\a\b
    low = command_line
    # 简单包含判断（大小写不敏感）
    pr_fs = pr.replace("\\", "/").lower()
    cl = low.replace("\\\\", "/").replace("\\", "/")
    if pr_fs in cl.lower():
        return True
    # 带引号片段
    pr_alt = pr.replace("\\", "/")
    if pr_alt.lower() in cl.lower():
        return True
    return False


def _unity_from_running_windows(project_root):
    if sys.platform != "win32":
        return None
    ps = (
        "Get-CimInstance Win32_Process -Filter \"Name = 'Unity.exe'\" "
        "| ForEach-Object { [PSCustomObject]@{ Exe = $_.ExecutablePath; Cmd = $_.CommandLine } } "
        "| ConvertTo-Json -Compress -Depth 3"
    )
    try:
        raw = subprocess.check_output(
            [
                "powershell",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                ps,
            ],
            stderr=subprocess.DEVNULL,
            text=True,
            encoding="utf-8",
            errors="replace",
        ).strip()
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        return None
    if not raw:
        return None
    try:
        data = json.loads(raw)
    except json.JSONDecodeError:
        return None
    if isinstance(data, dict):
        data = [data]
    for row in data:
        exe = row.get("Exe")
        cmd = row.get("Cmd") or ""
        if exe and os.path.isfile(exe) and _project_in_command_line(project_root, cmd):
            return exe
    return None


def _hub_editor_roots():
    roots = []
    if sys.platform == "win32":
        pf = os.environ.get("ProgramFiles", r"C:\Program Files")
        roots.append(os.path.join(pf, "Unity", "Hub", "Editor"))
        pf86 = os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")
        roots.append(os.path.join(pf86, "Unity", "Hub", "Editor"))
    elif sys.platform == "darwin":
        roots.append("/Applications/Unity/Hub/Editor")
    else:
        home = os.path.expanduser("~")
        roots.append(os.path.join(home, "Unity", "Hub", "Editor"))
    return roots


def _extra_editor_parent_roots():
    """UNITY_EXTRA_EDITOR_ROOTS：分号分隔，每项为包含「版本号子目录」的父路径（如 E:\\software\\unity）。"""
    raw = os.environ.get("UNITY_EXTRA_EDITOR_ROOTS", "").strip()
    if not raw:
        return []
    sep = ";" if sys.platform == "win32" else ":"
    out = []
    for part in raw.split(sep):
        p = part.strip().strip('"')
        if p and os.path.isdir(p):
            out.append(p)
    return out


def _all_version_install_roots():
    return _hub_editor_roots() + _extra_editor_parent_roots()


def _hub_unity_executable(hub_root, folder_name):
    if sys.platform == "darwin":
        return os.path.join(
            hub_root,
            folder_name,
            "Unity.app",
            "Contents",
            "MacOS",
            "Unity",
        )
    return os.path.join(hub_root, folder_name, "Editor", "Unity.exe")


def _unity_from_unity_hub(project_root):
    version = _read_editor_version(project_root)
    if not version:
        return None

    all_pairs = []
    for hub in _all_version_install_roots():
        if not os.path.isdir(hub):
            continue
        try:
            names = os.listdir(hub)
        except OSError:
            continue
        for name in names:
            exe = _hub_unity_executable(hub, name)
            if os.path.isfile(exe):
                all_pairs.append((name, exe))

    if not all_pairs:
        return None

    for name, exe in all_pairs:
        if name == version:
            return exe

    m = re.match(r"^(\d+\.\d+\.\d+f\d+)", version)
    if m:
        prefix = m.group(1)
        for name, exe in all_pairs:
            if name.startswith(prefix):
                return exe

    return None


def _resolve_unity_executable(project_root):
    override = os.environ.get("UNITY_EDITOR") or os.environ.get("UNITY_EDITOR_PATH")
    if override and os.path.isfile(override):
        return override, "环境变量 UNITY_EDITOR / UNITY_EDITOR_PATH（显式覆盖）"

    exe = _unity_from_unity_hub(project_root)
    if exe:
        src = "Unity Hub 或 UNITY_EXTRA_EDITOR_ROOTS + ProjectVersion.txt"
        return exe, src

    if sys.platform == "win32":
        exe = _unity_from_running_windows(project_root)
        if exe:
            return exe, "当前已打开本工程的 Unity 进程（Windows，仅作兜底）"

    return None, None


def _parse_cli_args():
    force = False
    rest = []
    for a in sys.argv[1:]:
        if a == "--force":
            force = True
        elif a:
            rest.append(a)
    log_file = rest[0] if rest else "-"
    return force, log_file


def _unity_editor_process_likely_running(project_root):
    """通过 Library/EditorInstance.json + tasklist 判断本工程是否仍被 Editor 占用。"""
    path = os.path.join(project_root, "Library", "EditorInstance.json")
    if not os.path.isfile(path):
        return False
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            data = json.load(f)
        pid = data.get("process_id")
        if pid is None:
            return False
    except (OSError, json.JSONDecodeError, TypeError, ValueError):
        return False

    if sys.platform != "win32":
        return True

    try:
        raw = subprocess.check_output(
            ["tasklist", "/FI", "PID eq %s" % int(pid), "/FO", "CSV", "/NH"],
            text=True,
            stderr=subprocess.DEVNULL,
            timeout=20,
        )
    except (
        subprocess.CalledProcessError,
        FileNotFoundError,
        subprocess.TimeoutExpired,
        OSError,
        ValueError,
    ):
        return False
    return "Unity.exe" in raw


def _log_shows_project_locked_by_another_instance(log_path):
    if not log_path or log_path == "-" or not os.path.isfile(log_path):
        return False
    try:
        with open(log_path, "rb") as f:
            f.seek(0, 2)
            sz = f.tell()
            f.seek(max(0, sz - 400000))
            chunk = f.read().decode("utf-8", errors="replace")
        return "another Unity instance is running with this project open" in chunk
    except OSError:
        return False


def main():
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
            sys.stderr.reconfigure(encoding="utf-8")
        except (OSError, ValueError):
            pass

    root = _unity_project_root()
    force_batch, log_file = _parse_cli_args()

    if not force_batch and _unity_editor_process_likely_running(root):
        print(
            "预检：本工程似乎仍由 Unity Editor 打开（Library/EditorInstance.json 中的进程仍在运行）。\n"
            "Unity 不允许第二个实例以 Batch Mode 打开同一工程，会继续失败或崩溃。\n\n"
            "可选操作：\n"
            "  1) 关闭该工程的所有 Unity 窗口后，再运行本脚本；或\n"
            "  2) 在已打开的 Editor 中执行菜单：Tools / Demos / Danmaku / Batch Img Sprite Atlases。\n\n"
            "若你确认没有 Editor 占用（例如残留 json），可加参数 --force 跳过本预检。\n",
            file=sys.stderr,
        )
        sys.exit(3)

    unity, source = _resolve_unity_executable(root)
    if not unity:
        ver = _read_editor_version(root) or "(无法读取)"
        print(
            "未找到 Unity 可执行文件。\n"
            "  - 确认已安装与 ProjectSettings 一致的版本："
            + ver
            + "\n"
            "  - 自定义安装目录可设置 UNITY_EXTRA_EDITOR_ROOTS（分号分隔父路径，如 E:\\\\software\\\\unity）。\n"
            "  - 或设置 UNITY_EDITOR 指向 Unity.exe；Windows 上也可先打开本工程再运行（用运行中进程兜底）。",
            file=sys.stderr,
        )
        sys.exit(2)

    cmd = [
        unity,
        "-batchmode",
        "-quit",
        "-nographics",
        "-projectPath",
        root,
        "-executeMethod",
        "DemoFrameWork.Demos.Danmaku.Editor.DanmakuSpriteAtlasCli.BuildImgSpriteAtlasesFromCli",
        "-logFile",
        log_file,
    ]
    print("Using Unity:", unity)
    print("Resolved by:", source)
    print("Running:", " ".join(cmd))
    r = subprocess.call(cmd)
    if r != 0:
        print(
            "\nUnity 进程退出码: %s。\n"
            "若本工程已在 Unity Editor 中打开，Batch Mode 通常会失败：请先关闭该工程的所有 Editor 窗口，再重新运行本脚本。\n"
            "或在已打开的 Editor 中使用菜单：Tools / Demos / Danmaku / Batch Img Sprite Atlases。\n"
            % (r,),
            file=sys.stderr,
        )
        if _log_shows_project_locked_by_another_instance(log_file):
            print(
                "日志中已出现 “another Unity instance is running with this project open”，与上述原因一致。\n",
                file=sys.stderr,
            )
    sys.exit(r if r is not None else 1)


if __name__ == "__main__":
    main()
