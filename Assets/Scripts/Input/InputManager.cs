using System;
using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork;

namespace DemoFrameWork.Input
{
    /// <summary>
    /// 输入管理：返回键、动作注册与按键绑定，通过 DemoGameEntry.Input 访问。
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        private bool _enabled = true;
        private bool _backKeyEnabled = true;
        private Dictionary<GameAction, List<KeyCode>> _actionKeys = new Dictionary<GameAction, List<KeyCode>>();
        private Dictionary<GameAction, List<Func<bool>>> _callbacks = new Dictionary<GameAction, List<Func<bool>>>();

        public bool IsEnabled => _enabled;
        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void EnableBackKey(bool enabled) => _backKeyEnabled = enabled;

        public void Register(GameAction action, Func<bool> callback)
        {
            if (callback == null) return;
            if (!_callbacks.TryGetValue(action, out var list))
            {
                list = new List<Func<bool>>();
                _callbacks[action] = list;
            }
            if (!list.Contains(callback))
                list.Add(callback);
        }

        public void Unregister(GameAction action, Func<bool> callback)
        {
            if (_callbacks.TryGetValue(action, out var list))
                list.Remove(callback);
        }

        public void Bind(GameAction action, KeyCode key)
        {
            if (!_actionKeys.TryGetValue(action, out var list))
            {
                list = new List<KeyCode>();
                _actionKeys[action] = list;
            }
            if (!list.Contains(key))
                list.Add(key);
        }

        public void Unbind(GameAction action, KeyCode key)
        {
            if (_actionKeys.TryGetValue(action, out var list))
                list.Remove(key);
        }

        public void UnbindAll(GameAction action)
        {
            _actionKeys.Remove(action);
        }

        public bool IsActionDown(GameAction action)
        {
            if (!_enabled) return false;
            if (_actionKeys.TryGetValue(action, out var keys))
            {
                foreach (var k in keys)
                {
                    if (UnityEngine.Input.GetKeyDown(k))
                        return true;
                }
            }
            return false;
        }

        public void Initialize()
        {
            _actionKeys.Clear();
            _callbacks.Clear();
            Bind(GameAction.Back, KeyCode.Escape);
        }

        private void Update()
        {
            if (!_enabled) return;
            HandleBackKey();
            foreach (var kv in _actionKeys)
            {
                if (kv.Key == GameAction.Back) continue;
                foreach (var key in kv.Value)
                {
                    if (UnityEngine.Input.GetKeyDown(key))
                    {
                        TriggerAction(kv.Key);
                        return;
                    }
                }
            }
        }

        private bool TriggerAction(GameAction action)
        {
            if (_callbacks.TryGetValue(action, out var list))
            {
                foreach (var cb in list)
                {
                    try
                    {
                        if (cb != null && cb.Invoke())
                            return true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            return false;
        }

        private void HandleBackKey()
        {
            if (!_backKeyEnabled) return;
            if (!UnityEngine.Input.GetKeyDown(KeyCode.Escape)) return;
            if (TriggerAction(GameAction.Back)) return;
            if (DemoGameEntry.UI != null)
                DemoGameEntry.UI.CloseCurrentWindow();
        }
    }
}
