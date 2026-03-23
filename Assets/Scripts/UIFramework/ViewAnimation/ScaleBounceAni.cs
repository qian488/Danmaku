using System;
using UnityEngine;
using DG.Tweening;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// 窗口缩放回弹动画：从小变大，略微超过正常尺寸再回弹到「目标原本的缩放」。
    /// 回弹终点为动画开始时 target 的 localScale（如预制体里设的 0.8），不会强制回弹到 1。依赖 DOTween。
    /// </summary>
    public class ScaleBounceAni : AniComponent
    {
        [Tooltip("起始缩放系数（0=从无到有，1=从当前尺寸开始；与 restScale 相乘得到起始 scale）")]
        [SerializeField] private float startScale = 0f;

        [Tooltip("峰值相对静止 scale 的倍数（如 1.08 表示先放大到 restScale 的 1.08 倍再回弹）")]
        [SerializeField] private float peakScale = 1.08f;

        [Tooltip("从小到峰值的时间")]
        [SerializeField] private float durationToPeak = 0.2f;

        [Tooltip("从峰值回弹到静止 scale 的时间")]
        [SerializeField] private float durationToNormal = 0.15f;

        public override void Animate(Transform target, Action callWhenFinished)
        {
            if (target == null)
            {
                callWhenFinished?.Invoke();
                return;
            }

            Vector3 restScale = target.localScale;
            if (restScale.x <= 0f || restScale.y <= 0f || restScale.z <= 0f)
                restScale = Vector3.one;

            target.DOKill(true);
            target.localScale = new Vector3(
                restScale.x * startScale,
                restScale.y * startScale,
                restScale.z * startScale
            );

            Vector3 peakScaleVec = new Vector3(
                restScale.x * peakScale,
                restScale.y * peakScale,
                restScale.z * peakScale
            );

            var seq = DOTween.Sequence();
            seq.SetTarget(target);
            seq.Append(target.DOScale(peakScaleVec, durationToPeak).SetEase(Ease.OutQuad));
            seq.Append(target.DOScale(restScale, durationToNormal).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                target.localScale = restScale;
                callWhenFinished?.Invoke();
            });
        }
    }
}
