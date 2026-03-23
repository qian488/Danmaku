using System;
using UnityEngine;
using DG.Tweening;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// 窗口抖动动画：对 RectTransform 的 anchoredPosition 做短时抖动。
    /// 可用于错误提示、强调等。依赖 DOTween。
    /// </summary>
    public class ShakeAni : AniComponent
    {
        [Tooltip("抖动时长")]
        [SerializeField] private float duration = 0.4f;

        [Tooltip("抖动强度（像素）")]
        [SerializeField] private float strength = 25f;

        [Tooltip("振动次数")]
        [SerializeField] private int vibrato = 15;

        [Tooltip("随机度 (0~90 推荐)")]
        [SerializeField] private float randomness = 60f;

        [Tooltip("是否在结束时平滑淡出")]
        [SerializeField] private bool fadeOut = true;

        public override void Animate(Transform target, Action callWhenFinished)
        {
            if (target == null)
            {
                callWhenFinished?.Invoke();
                return;
            }

            var rect = target as RectTransform;
            if (rect == null)
            {
                callWhenFinished?.Invoke();
                return;
            }

            rect.DOKill(true);

            var tween = rect.DOShakeAnchorPos(duration, strength, vibrato, randomness, false, fadeOut);
            tween.SetTarget(rect);
            tween.OnComplete(() => callWhenFinished?.Invoke());
        }
    }
}
