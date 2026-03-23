namespace DemoFrameWork.UI
{
    using UnityEngine;
    
    /// <summary>
    /// 红点 UI 绑定组件。
    /// 挂在任意 UI 节点上，Inspector 中填写红点路径（如 "Main/Shop"）并指定红点显示节点，
    /// 无需手动调用 AddListener，组件启用/禁用时自动订阅和取消。
    /// 也可在运行时调用 SetPath 动态绑定路径，适用于由代码实例化红点预制体的场景。
    /// </summary>
    public class ReddotNode : MonoBehaviour
    {
        [Tooltip("红点路径，使用 / 分隔层级，例如 \"Main/Shop\"")]
        [SerializeField] private string path;
    
        [Tooltip("红点显示节点（通常为红点 Image 或数字 Text 的父节点）；为空时使用当前 GameObject")]
        [SerializeField] private GameObject reddotDisplay;
    
        private void OnEnable()
        {
            if (string.IsNullOrEmpty(path)) return;
    
            ReddotManager.Instance.AddListener(path, OnValueChanged);
            OnValueChanged(ReddotManager.Instance.GetValue(path));
        }
    
        private void OnDisable()
        {
            if (string.IsNullOrEmpty(path)) return;
    
            ReddotManager.Instance.RemoveListener(path, OnValueChanged);
        }
    
        /// <summary>
        /// 运行时动态绑定红点路径。适用于代码实例化红点预制体后，由外部传入 path 的场景。
        /// 若组件当前处于启用状态，会自动取消旧 path 的订阅并订阅新 path。
        /// </summary>
        public void SetPath(string newPath)
        {
            if (isActiveAndEnabled && !string.IsNullOrEmpty(path))
            {
                ReddotManager.Instance.RemoveListener(path, OnValueChanged);
            }
    
            path = newPath;
    
            if (isActiveAndEnabled && !string.IsNullOrEmpty(path))
            {
                ReddotManager.Instance.AddListener(path, OnValueChanged);
                OnValueChanged(ReddotManager.Instance.GetValue(path));
            }
        }
    
        private void OnValueChanged(int value)
        {
            var target = reddotDisplay != null ? reddotDisplay : gameObject;
            target.SetActive(value > 0);
        }
    }
}
