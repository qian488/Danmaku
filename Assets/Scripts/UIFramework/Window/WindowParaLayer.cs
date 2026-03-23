using UnityEngine;
using System.Collections.Generic;

namespace DemoFrameWork.UI {
    /// <summary>
    /// 这是一个“辅助”层级，以便显示优先级更高的窗口。
    /// 默认情况下，它包含任何标记为弹出窗口的窗口。它由 WindowUILayer 控制
    /// </summary>
    public class WindowParaLayer : MonoBehaviour {
        [SerializeField] 
        private GameObject darkenBgObject = null;

        private List<GameObject> containedScreens = new List<GameObject>();
        
        public void AddScreen(Transform screenRectTransform) {
            screenRectTransform.SetParent(transform, false);
            containedScreens.Add(screenRectTransform.gameObject);
        }

        /// <summary>
        /// 弹出窗必须与 Darken 在同一父节点下，且显示时窗口要在蒙版之上（由 <see cref="WindowController.HierarchyFixOnShow"/> 最后 sibling 保证）。<br/>
        /// <see cref="WindowUILayer.ReparentScreen"/> 只看注册时 Prefab 上的 <c>Properties.IsPopup</c>，与 <c>OpenWindow(..., new XxxProperties())</c> 传入的值可能不一致，
        /// 会导致窗口仍在 WindowLayer、蒙版在 PriorityWindowLayer 而挡住全部点击。
        /// </summary>
        public void EnsureScreenForPopup(Transform screenRectTransform) {
            if (screenRectTransform == null) return;

            if (screenRectTransform.parent == transform) {
                GameObject go = screenRectTransform.gameObject;
                if (!containedScreens.Contains(go))
                    containedScreens.Add(go);
                return;
            }

            Debug.LogWarning(
                "[WindowParaLayer] 弹出窗「" + screenRectTransform.name +
                "」不在 PriorityWindowLayer 下，已运行时 SetParent 纠正（与 DarkenBG 同父节点）。\n" +
                "原因通常是：<b>注册时</b> Controller 上序列化的 IsPopup 未生效（与 OpenWindow 传入的 Properties 无关）。\n" +
                "处理：在 Unity 中打开该窗口 Prefab，根节点 Inspector 展开 <b>Screen properties</b>，勾选 <b>Is Popup</b> 并 Apply/Save；勿仅手写 .prefab YAML。\n" +
                "若已手写 isPopup 仍出现本警告，说明嵌套字段未被 Unity 反序列化，必须以 Inspector 保存一次以写入正确 typetree。",
                screenRectTransform.gameObject);

            screenRectTransform.SetParent(transform, false);
            containedScreens.Add(screenRectTransform.gameObject);
        }

        public void RefreshDarken() {
            for (int i = 0; i < containedScreens.Count; i++) {
                if (containedScreens[i] != null) {
                    if (containedScreens[i].activeSelf) {
                        darkenBgObject.SetActive(true);
                        return;
                    }
                }
            }

            darkenBgObject.SetActive(false);
        }

        public void DarkenBG() {
            darkenBgObject.SetActive(true);
            darkenBgObject.transform.SetAsLastSibling();
        }
    }
}
