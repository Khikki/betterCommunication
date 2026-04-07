using TMPro;
using UnityEngine;

namespace SuperBattleGolf.BetterCommunication
{
    internal sealed class ChatHistoryMessageTracker : MonoBehaviour
    {
        private ChatHistoryManager _manager;
        private TMP_Text _messageText;
        private bool _captured;

        public void Initialize(ChatHistoryManager manager, TMP_Text messageText)
        {
            _manager = manager;
            _messageText = messageText;
        }

        private void Update()
        {
            if (_captured || _manager == null || _messageText == null)
            {
                return;
            }

            string text = _messageText.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _manager.TryCaptureMessage(text);
            _captured = true;
        }
    }
}
