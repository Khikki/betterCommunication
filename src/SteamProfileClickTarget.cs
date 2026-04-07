using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SuperBattleGolf.BetterCommunication
{
    internal sealed class SteamProfileClickTarget : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const string SteamCommunityPrefix = "https://steamcommunity.com/profiles/";
        private const string SteamProtocolPrefix = "steam://openurl/";

        private Component _sourceComponent;
        private string _steamIdPath;
        private bool _useSteamProtocol;
        private Graphic _graphic;
        private Color _baseColor;
        private bool _hasBaseColor;
        private string _contextLabel;

        public void Initialize(Component sourceComponent, string steamIdPath, bool useSteamProtocol, string contextLabel)
        {
            _sourceComponent = sourceComponent;
            _steamIdPath = steamIdPath;
            _useSteamProtocol = useSteamProtocol;
            _contextLabel = contextLabel;

            _graphic = GetComponent<Graphic>();
            if (_graphic != null)
            {
                _graphic.raycastTarget = true;
                _baseColor = _graphic.color;
                _hasBaseColor = true;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            ulong steamId = ResolveSteamId();
            if (steamId == 0UL)
            {
                return;
            }

            string url = SteamCommunityPrefix + steamId + "/";
            if (_useSteamProtocol)
            {
                url = SteamProtocolPrefix + url;
            }

            Application.OpenURL(url);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_graphic != null && _hasBaseColor)
            {
                _graphic.color = Tint(_baseColor, 1.15f);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_graphic != null && _hasBaseColor)
            {
                _graphic.color = _baseColor;
            }
        }

        public void Refresh(Component sourceComponent, string steamIdPath, bool useSteamProtocol, string contextLabel)
        {
            _sourceComponent = sourceComponent;
            _steamIdPath = steamIdPath;
            _useSteamProtocol = useSteamProtocol;
            _contextLabel = contextLabel;
        }

        private ulong ResolveSteamId()
        {
            object current = _sourceComponent;
            if (current == null || string.IsNullOrWhiteSpace(_steamIdPath))
            {
                return 0UL;
            }

            string[] segments = _steamIdPath.Split('.');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (current == null)
                {
                    return 0UL;
                }

                Type currentType = current.GetType();
                PropertyInfo property = currentType.GetProperty(segment, AllMembers);
                if (property != null)
                {
                    current = property.GetValue(current, null);
                    continue;
                }

                FieldInfo field = currentType.GetField(segment, AllMembers);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }

                return 0UL;
            }

            if (current is ulong)
            {
                return (ulong)current;
            }

            if (current is long)
            {
                return (ulong)(long)current;
            }

            if (current is uint)
            {
                return (uint)current;
            }

            return 0UL;
        }

        private static Color Tint(Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a);
        }

        private const BindingFlags AllMembers =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static;
    }
}
