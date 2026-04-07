using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
using UnityEngine;

namespace SuperBattleGolf.BetterCommunication
{
    internal sealed class ChatHistoryManager
    {
        private static readonly Regex LegacyLinkTagRegex =
            new Regex(@"</?(?:u|link)(?:=[^>]+)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LegacyColorTagRegex =
            new Regex(@"</?color(?:=[^>]+)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NoParseTagRegex =
            new Regex(@"</?noparse>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ManualLogSource _logger;
        private readonly ConfigEntry<bool> _enabled;
        private readonly ConfigEntry<int> _maxMessages;
        private readonly ConfigEntry<bool> _persistAcrossLaunches;
        private readonly Dictionary<string, int> _pendingRestoreIgnores = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _messages = new List<string>();
        private readonly string _historyFilePath;

        private Type _textChatUiType;
        private Type _textChatMessageUiType;
        private int _lastRestoredUiId = -1;
        private float _nextScanTime;

        public ChatHistoryManager(
            ManualLogSource logger,
            ConfigEntry<bool> enabled,
            ConfigEntry<int> maxMessages,
            ConfigEntry<bool> persistAcrossLaunches)
        {
            _logger = logger;
            _enabled = enabled;
            _maxMessages = maxMessages;
            _persistAcrossLaunches = persistAcrossLaunches;
            _historyFilePath = Path.Combine(Paths.ConfigPath, "local.marki.superbattlegolf.chat_history.txt");

            LoadHistory();
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

            _nextScanTime = Time.unscaledTime + 0.5f;
            EnsureTypes();
            AttachTrackers();
            RestoreToLatestChatUi();
        }

        public void TryCaptureMessage(string rawMessage)
        {
            if (!_enabled.Value)
            {
                return;
            }

            string normalized = NormalizeMessage(rawMessage);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            int pendingCount;
            if (_pendingRestoreIgnores.TryGetValue(normalized, out pendingCount) && pendingCount > 0)
            {
                pendingCount--;
                if (pendingCount == 0)
                {
                    _pendingRestoreIgnores.Remove(normalized);
                }
                else
                {
                    _pendingRestoreIgnores[normalized] = pendingCount;
                }

                return;
            }

            _messages.Add(normalized);
            TrimMessages();
            SaveHistory();
        }

        private void RestoreToLatestChatUi()
        {
            if (_textChatUiType == null || _messages.Count == 0)
            {
                return;
            }

            UnityEngine.Object[] chatUis = Resources.FindObjectsOfTypeAll(_textChatUiType);
            if (chatUis == null || chatUis.Length == 0)
            {
                return;
            }

            Component targetUi = chatUis[chatUis.Length - 1] as Component;
            if (targetUi == null)
            {
                return;
            }

            int instanceId = targetUi.GetInstanceID();
            if (_lastRestoredUiId == instanceId)
            {
                return;
            }

            if (GetVisibleMessageCount() > 0)
            {
                _lastRestoredUiId = instanceId;
                return;
            }

            MethodInfo showMessageInternal = _textChatUiType.GetMethod("ShowMessageInternal", AllMembers);
            if (showMessageInternal == null)
            {
                return;
            }

            for (int i = 0; i < _messages.Count; i++)
            {
                string message = _messages[i];
                IncrementPendingIgnore(message);
                showMessageInternal.Invoke(targetUi, new object[] { message });
            }

            _lastRestoredUiId = instanceId;
            _logger.LogInfo(string.Format("Restored {0} chat message(s) into a new TextChatUi instance.", _messages.Count));
        }

        private void AttachTrackers()
        {
            if (_textChatMessageUiType == null)
            {
                return;
            }

            UnityEngine.Object[] messageUis = Resources.FindObjectsOfTypeAll(_textChatMessageUiType);
            for (int i = 0; i < messageUis.Length; i++)
            {
                Component sourceComponent = messageUis[i] as Component;
                if (sourceComponent == null)
                {
                    continue;
                }

                ChatHistoryMessageTracker tracker = sourceComponent.GetComponent<ChatHistoryMessageTracker>();
                if (tracker == null)
                {
                    tracker = sourceComponent.gameObject.AddComponent<ChatHistoryMessageTracker>();
                }

                TMP_Text messageText = GetFieldValue(sourceComponent, "messageText") as TMP_Text;
                tracker.Initialize(this, messageText);
            }
        }

        private int GetVisibleMessageCount()
        {
            if (_textChatMessageUiType == null)
            {
                return 0;
            }

            UnityEngine.Object[] messageUis = Resources.FindObjectsOfTypeAll(_textChatMessageUiType);
            int count = 0;
            for (int i = 0; i < messageUis.Length; i++)
            {
                Component component = messageUis[i] as Component;
                if (component != null && component.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private void EnsureTypes()
        {
            if (_textChatUiType != null && _textChatMessageUiType != null)
            {
                return;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                CacheType(assembly, "TextChatUi", ref _textChatUiType);
                CacheType(assembly, "TextChatMessageUi", ref _textChatMessageUiType);
            }
        }

        private void LoadHistory()
        {
            _messages.Clear();

            if (!_persistAcrossLaunches.Value || !File.Exists(_historyFilePath))
            {
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(_historyFilePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    byte[] bytes = Convert.FromBase64String(line);
                    string normalized = NormalizeMessage(Encoding.UTF8.GetString(bytes));
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        _messages.Add(normalized);
                    }
                }

                TrimMessages();
                SaveHistory();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load chat history: " + ex.Message);
            }
        }

        private void SaveHistory()
        {
            if (!_persistAcrossLaunches.Value)
            {
                return;
            }

            try
            {
                string[] lines = _messages
                    .Select(delegate(string message)
                    {
                        return Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
                    })
                    .ToArray();
                File.WriteAllLines(_historyFilePath, lines);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to save chat history: " + ex.Message);
            }
        }

        private void TrimMessages()
        {
            int max = Math.Max(1, _maxMessages.Value);
            while (_messages.Count > max)
            {
                _messages.RemoveAt(0);
            }
        }

        private void IncrementPendingIgnore(string message)
        {
            int count;
            if (_pendingRestoreIgnores.TryGetValue(message, out count))
            {
                _pendingRestoreIgnores[message] = count + 1;
            }
            else
            {
                _pendingRestoreIgnores[message] = 1;
            }
        }

        private static string NormalizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            string normalized = message
                .Replace("\r", string.Empty)
                .Replace("\n", " ")
                .Trim();

            if (normalized.IndexOf("<link", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("<u>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("<noparse>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("#79C7FF", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                normalized = LegacyLinkTagRegex.Replace(normalized, string.Empty);
                normalized = NoParseTagRegex.Replace(normalized, string.Empty);
                normalized = LegacyColorTagRegex.Replace(normalized, string.Empty);
            }

            return normalized.Trim();
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

        private const BindingFlags AllMembers =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static;
    }
}
