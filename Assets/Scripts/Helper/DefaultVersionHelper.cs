//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using DemoFrameWork;
using UnityEngine;

namespace DemoFrameWork.Runtime
{
    /// <summary>
    /// 默认版本号辅助器。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class DefaultVersionHelper : Version.IVersionHelper
    {
        /// <summary>
        /// 获取游戏版本号（来自 GameConfig，未初始化时回退 Application.version）。
        /// </summary>
        public string GameVersion
        {
            get { return DemoGameEntry.Config != null ? DemoGameEntry.Config.Version : Application.version; }
        }
    }
}