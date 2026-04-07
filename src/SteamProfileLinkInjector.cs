using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuperBattleGolf.BetterCommunication
{
    internal sealed class SteamProfileLinkInjector
    {
        private readonly ManualLogSource _logger;
        private readonly ConfigEntry<bool> _enabled;
        private readonly ConfigEntry<bool> _useSteamProtocol;
        private readonly ConfigEntry<bool> _enableWorldNameTags;
        private readonly ConfigEntry<bool> _enableAvatarLinks;
        private readonly ConfigEntry<bool> _enableChatLinks;
        private readonly Dictionary<string, int> _loggedInjectionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private Type _matchSetupPlayerType;
        private Type _pauseMenuPlayerEntryType;
        private Type _nameTagUiType;
        private Type _textChatMessageUiType;
        private float _nextScanTime;

        public SteamProfileLinkInjector(
            ManualLogSource logger,
            ConfigEntry<bool> enabled,
            ConfigEntry<bool> useSteamProtocol,
            ConfigEntry<bool> enableWorldNameTags,
            ConfigEntry<bool> enableAvatarLinks,
            ConfigEntry<bool> enableChatLinks)
        {
            _logger = logger;
            _enabled = enabled;
            _useSteamProtocol = useSteamProtocol;
            _enableWorldNameTags = enableWorldNameTags;
            _enableAvatarLinks = enableAvatarLinks;
            _enableChatLinks = enableChatLinks;
        }

        public void Update()
        {
            if (!_enabled.Value)
            {
                return;
            }

            if (Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + 1f;
            EnsureTypes();
            InjectLinks();
        }

        private void InjectLinks()
        {
            InjectForType(_matchSetupPlayerType, "playerNickname", "Guid", "MatchSetupPlayer");
            InjectForType(_pauseMenuPlayerEntryType, "playerName", "CurrentPlayerGuid", "PauseMenuPlayerEntry");

            if (_enableAvatarLinks.Value)
            {
                InjectForType(_matchSetupPlayerType, "playerIcon", "Guid", "MatchSetupPlayer Icon");
                InjectForType(_pauseMenuPlayerEntryType, "playerIcon", "CurrentPlayerGuid", "PauseMenuPlayerEntry Icon");
            }

            if (_enableWorldNameTags.Value)
            {
                InjectForType(_nameTagUiType, "tag", "playerInfo.PlayerId.Guid", "NameTagUi");
            }

            if (_enableChatLinks.Value)
            {
                InjectChatLinks();
            }
        }

        private void InjectForType(Type sourceType, string targetFieldName, string steamIdPath, string contextLabel)
        {
            if (sourceType == null)
            {
                return;
            }

            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(sourceType);
            int injectedCount = 0;

            for (int i = 0; i < objects.Length; i++)
            {
                Component sourceComponent = objects[i] as Component;
                if (sourceComponent == null)
                {
                    continue;
                }

                Component targetComponent = GetFieldValue(sourceComponent, targetFieldName) as Component;
                if (targetComponent == null)
                {
                    continue;
                }

                SteamProfileClickTarget clickTarget = targetComponent.GetComponent<SteamProfileClickTarget>();
                if (clickTarget == null)
                {
                    clickTarget = targetComponent.gameObject.AddComponent<SteamProfileClickTarget>();
                    injectedCount++;
                }

                clickTarget.Refresh(sourceComponent, steamIdPath, _useSteamProtocol.Value, contextLabel);
                EnsureGraphicTargeting(targetComponent);
            }

            if (injectedCount > 0)
            {
                LogInjectionCount(contextLabel, injectedCount);
            }
        }

        private void EnsureTypes()
        {
            if (_matchSetupPlayerType != null && _pauseMenuPlayerEntryType != null && _nameTagUiType != null && _textChatMessageUiType != null)
            {
                return;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                CacheType(assembly, "MatchSetupPlayer", ref _matchSetupPlayerType);
                CacheType(assembly, "PauseMenuPlayerEntry", ref _pauseMenuPlayerEntryType);
                CacheType(assembly, "NameTagUi", ref _nameTagUiType);
                CacheType(assembly, "TextChatMessageUi", ref _textChatMessageUiType);
            }
        }

        private void InjectChatLinks()
        {
            if (_textChatMessageUiType == null)
            {
                return;
            }

            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(_textChatMessageUiType);
            int injectedCount = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                Component sourceComponent = objects[i] as Component;
                if (sourceComponent == null)
                {
                    continue;
                }

                TMP_Text messageText = GetFieldValue(sourceComponent, "messageText") as TMP_Text;
                if (messageText == null)
                {
                    continue;
                }

                GameObject overlay = EnsureChatLinkOverlay(sourceComponent.gameObject);
                if (overlay == null)
                {
                    continue;
                }

                ChatLinkTextHandler handler = overlay.GetComponent<ChatLinkTextHandler>();
                if (handler == null)
                {
                    handler = overlay.AddComponent<ChatLinkTextHandler>();
                    injectedCount++;
                }

                handler.Initialize(messageText);
                handler.Refresh();
            }

            if (injectedCount > 0)
            {
                LogInjectionCount("TextChatMessageUi Links", injectedCount);
            }
        }

        private void LogInjectionCount(string contextLabel, int injectedCount)
        {
            int previousCount;
            if (_loggedInjectionCounts.TryGetValue(contextLabel, out previousCount) && previousCount == injectedCount)
            {
                return;
            }

            _loggedInjectionCounts[contextLabel] = injectedCount;
            _logger.LogInfo(string.Format("Injected {0} Steam profile link target(s) for {1}.", injectedCount, contextLabel));
        }

        private static void CacheType(Assembly assembly, string typeName, ref Type cache)
        {
            if (cache != null)
            {
                return;
            }

            cache = assembly.GetType(typeName, false);
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo field = instance.GetType().GetField(fieldName, AllMembers);
            return field != null ? field.GetValue(instance) : null;
        }

        private static void EnsureGraphicTargeting(Component component)
        {
            PropertyInfo property = component.GetType().GetProperty("raycastTarget", AllMembers);
            if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
            {
                property.SetValue(component, true, null);
            }
        }

        private static GameObject EnsureChatLinkOverlay(GameObject messageRoot)
        {
            if (messageRoot == null)
            {
                return null;
            }

            Transform existing = messageRoot.transform.Find("BetterCommunicationChatOverlay");
            if (existing != null)
            {
                Image existingImage = existing.GetComponent<Image>();
                if (existingImage != null)
                {
                    existingImage.raycastTarget = true;
                    Color color = existingImage.color;
                    existingImage.color = new Color(color.r, color.g, color.b, 0f);
                }

                return existing.gameObject;
            }

            RectTransform parentRect = messageRoot.GetComponent<RectTransform>();
            if (parentRect == null)
            {
                return null;
            }

            GameObject overlay = new GameObject("BetterCommunicationChatOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(messageRoot.transform, false);

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.SetAsLastSibling();

            Image image = overlay.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            return overlay;
        }

        private const BindingFlags AllMembers =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static;
    }
}
