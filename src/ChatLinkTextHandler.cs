using System;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace SuperBattleGolf.BetterCommunication
{
    internal sealed class ChatLinkTextHandler : MonoBehaviour
    {
        private const string LinkId = "bc-url";

        private static readonly Regex UrlRegex = new Regex(@"((?:https?://)?(?:www\.)?[A-Za-z0-9\-]+\.[A-Za-z]{2,}(?:/[^\s<]*)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InjectedLinkRegex = new Regex(
            "<link=\"(?<url>[^\"]*)\"><color=#79C7FF>(?<text>.*?)</color></link>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex NoParseRegex = new Regex(
            "<noparse>(?<text>.*?)</noparse>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        private static bool _inputSystemReflectionInitialized;
        private static PropertyInfo _mouseCurrentProperty;
        private static PropertyInfo _mousePositionProperty;
        private static PropertyInfo _mouseLeftButtonProperty;
        private static PropertyInfo _buttonWasPressedProperty;
        private static MethodInfo _readValueMethod;

        private TMP_Text _text;
        private string _lastAppliedText;
        private Canvas _canvas;
        private bool _legacyInputUnavailable;

        public void Initialize(TMP_Text text)
        {
            _text = text;
            if (_text != null)
            {
                _text.richText = true;
            }

            _canvas = GetComponentInParent<Canvas>();
        }

        public void Refresh()
        {
            if (_text == null)
            {
                return;
            }

            _text.richText = true;
            _text.raycastTarget = true;

            if (string.Equals(_text.text, _lastAppliedText, StringComparison.Ordinal))
            {
                return;
            }

            string sourceText = StripInjectedLinks(_text.text);
            string styledText = InjectLinksPreservingGameMarkup(sourceText);
            if (!string.Equals(_text.text, styledText, StringComparison.Ordinal))
            {
                _text.text = styledText;
            }

            _lastAppliedText = _text.text;
        }

        private void Update()
        {
            if (_text == null || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            Vector2 mousePosition;
            if (!TryGetMouseClickPosition(out mousePosition))
            {
                return;
            }

            Camera eventCamera = GetEventCamera();
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(_text, mousePosition, eventCamera);
            if (linkIndex < 0 || _text.textInfo == null || _text.textInfo.linkCount <= linkIndex)
            {
                return;
            }

            TMP_LinkInfo linkInfo = _text.textInfo.linkInfo[linkIndex];
            string url = NormalizeUrl(linkInfo.GetLinkText());
            if (!string.IsNullOrWhiteSpace(url))
            {
                Application.OpenURL(url);
            }
        }

        private bool TryGetMouseClickPosition(out Vector2 mousePosition)
        {
            if (!_legacyInputUnavailable)
            {
                try
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        mousePosition = Input.mousePosition;
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    _legacyInputUnavailable = true;
                }
            }

            return TryGetInputSystemMouseClickPosition(out mousePosition);
        }

        private static bool TryGetInputSystemMouseClickPosition(out Vector2 mousePosition)
        {
            mousePosition = Vector2.zero;
            EnsureInputSystemReflection();
            if (_mouseCurrentProperty == null || _mousePositionProperty == null || _mouseLeftButtonProperty == null || _buttonWasPressedProperty == null || _readValueMethod == null)
            {
                return false;
            }

            object mouse = _mouseCurrentProperty.GetValue(null, null);
            if (mouse == null)
            {
                return false;
            }

            object leftButton = _mouseLeftButtonProperty.GetValue(mouse, null);
            if (leftButton == null)
            {
                return false;
            }

            object pressedValue = _buttonWasPressedProperty.GetValue(leftButton, null);
            if (!(pressedValue is bool) || !((bool)pressedValue))
            {
                return false;
            }

            object positionControl = _mousePositionProperty.GetValue(mouse, null);
            if (positionControl == null)
            {
                return false;
            }

            object value = _readValueMethod.Invoke(positionControl, null);
            if (value is Vector2)
            {
                mousePosition = (Vector2)value;
                return true;
            }

            return false;
        }

        private Camera GetEventCamera()
        {
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return _canvas.worldCamera;
        }

        private static string NormalizeUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return string.Empty;
            }

            if (rawUrl.IndexOf("://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return rawUrl;
            }

            return "https://" + rawUrl;
        }

        private static string StripInjectedLinks(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return InjectedLinkRegex.Replace(text, "${text}");
        }

        private static string InjectLinks(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return UrlRegex.Replace(text, delegate(Match match)
            {
                return string.Format("<link=\"{0}\"><color=#79C7FF>{1}</color></link>", LinkId, match.Value);
            });
        }

        private static string InjectLinksPreservingGameMarkup(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string replacedNoParseBlocks = NoParseRegex.Replace(text, delegate(Match match)
            {
                string innerText = match.Groups["text"].Value;
                string linkedText = InjectLinks(innerText);
                if (string.Equals(innerText, linkedText, StringComparison.Ordinal))
                {
                    return match.Value;
                }

                return linkedText;
            });

            return InjectLinks(replacedNoParseBlocks);
        }

        private static void EnsureInputSystemReflection()
        {
            if (_inputSystemReflectionInitialized)
            {
                return;
            }

            _inputSystemReflectionInitialized = true;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                Type mouseType = assembly.GetType("UnityEngine.InputSystem.Mouse", false);
                if (mouseType == null)
                {
                    continue;
                }

                _mouseCurrentProperty = mouseType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                _mousePositionProperty = mouseType.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
                _mouseLeftButtonProperty = mouseType.GetProperty("leftButton", BindingFlags.Public | BindingFlags.Instance);

                Type buttonType = assembly.GetType("UnityEngine.InputSystem.Controls.ButtonControl", false);
                if (buttonType != null)
                {
                    _buttonWasPressedProperty = buttonType.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
                }

                Type vector2ControlType = assembly.GetType("UnityEngine.InputSystem.Controls.Vector2Control", false);
                if (vector2ControlType != null)
                {
                    _readValueMethod = vector2ControlType.GetMethod("ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                }

                break;
            }
        }
    }
}
