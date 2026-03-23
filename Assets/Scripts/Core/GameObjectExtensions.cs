// DemoFrameWork 扩展方法

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoFrameWork
{
    public static class GameObjectExtensions
    {
        /// <summary>判断 GameObject 是否在场景中（非 prefab 实例）。</summary>
        public static bool InScene(this GameObject go)
        {
            return go != null && go.scene.IsValid();
        }
    }
}
