using System;
using DemoFrameWork.UI;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 弹幕暂停窗口属性。
    /// 显示时作为弹出层（IsPopup=true），后面有半透明遮罩。
    /// 继续/返回 Home 操作通过 EventCenter 广播，不需要在属性里传回调。
    /// <para><b>与 Prefab 的关系</b>：<c>OpenWindow(..., new DanmakuPauseWindowProperties())</c> 只影响<strong>本次 Show</strong> 的逻辑；
    /// <see cref="DemoFrameWork.UI.WindowUILayer.ReparentScreen"/> 在<strong>注册</strong>时读的是 Prefab 实例上序列化的 <c>properties</c>（与 Open 传入是两套数据，后者带 <c>SuppressPrefabProperties</c> 不会写回 Prefab）。
    /// 弹窗须在 Prefab 根节点 Inspector 里展开 <b>Screen properties / Window Properties</b>，勾选 <b>Is Popup</b> 并保存；勿仅手写 YAML，否则 Unity 可能无法反序列化到泛型 Controller 的嵌套字段，注册时仍视为非 Popup。</para>
    /// </summary>
    [Serializable]
    public class DanmakuPauseWindowProperties : WindowProperties
    {
        public DanmakuPauseWindowProperties() : base(suppressPrefabProperties: true)
        {
            IsPopup               = true;
            HideOnForegroundLost  = false;
            WindowQueuePriority   = WindowPriority.ForceForeground;
        }
    }
}
