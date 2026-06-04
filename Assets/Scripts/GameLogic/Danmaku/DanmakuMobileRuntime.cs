using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DemoFrameWork.GameLogic.Danmaku
{
    public static class DanmakuMobileRuntime
    {
        public static bool IsMobileLike
        {
            get
            {
                if (Application.isMobilePlatform)
                    return true;

#if UNITY_EDITOR
                return EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
                       || EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
#else
                return false;
#endif
            }
        }
    }
}
