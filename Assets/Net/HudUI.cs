using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MarioNet
{
    // In-game HUD: live player count (top right) and a fading join/leave feed (top left).
    public class HudUI : MonoBehaviour
    {
        public Text playerCountText;
        public RectTransform feedRoot;
        public Font font;
        [Range(1, 10)] public float holdSeconds = 3.5f;
        [Range(0.2f, 5)] public float fadeSeconds = 1.5f;

        static readonly Color JoinColor = new Color(0.5f, 1f, 0.5f);
        static readonly Color LeaveColor = new Color(1f, 0.6f, 0.5f);
        static readonly Color SelfColor = new Color(1f, 0.85f, 0.2f);

        class Entry
        {
            public Text text;
            public float age;
        }

        readonly List<Entry> entries = new List<Entry>();

        void Start()
        {
            var nm = NetworkManager.Instance;
            if (nm == null)
            {
                Debug.LogError("HudUI: no NetworkManager in scene");
                enabled = false;
                return;
            }
            nm.OnJoinedRoom += HandleSelfJoined;
            nm.OnRemoteJoined += HandleRemoteJoined;
            nm.OnRemoteLeft += HandleRemoteLeft;
            nm.OnDisconnected += HandleDisconnected;
        }

        void OnDestroy()
        {
            var nm = NetworkManager.Instance;
            if (nm == null)
                return;
            nm.OnJoinedRoom -= HandleSelfJoined;
            nm.OnRemoteJoined -= HandleRemoteJoined;
            nm.OnRemoteLeft -= HandleRemoteLeft;
            nm.OnDisconnected -= HandleDisconnected;
        }

        void HandleSelfJoined() => AddEntry($"connected as {NetworkManager.Instance.MyUsername}", SelfColor);
        void HandleRemoteJoined(string username) => AddEntry($"{username} joined", JoinColor);
        void HandleRemoteLeft(string username) => AddEntry($"{username} left", LeaveColor);
        void HandleDisconnected() => AddEntry("disconnected", LeaveColor);

        public void AddEntry(string message, Color color)
        {
            var go = new GameObject("FeedEntry");
            go.transform.SetParent(feedRoot, false);
            go.transform.SetAsFirstSibling();
            var text = go.AddComponent<Text>();
            text.text = message;
            text.font = font;
            text.fontSize = 22;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            entries.Add(new Entry { text = text });
        }

        void Update()
        {
            var nm = NetworkManager.Instance;
            if (playerCountText != null)
                playerCountText.text = nm != null && nm.Joined ? $"players: {nm.PlayerCount}" : "";

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                entry.age += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01((entry.age - holdSeconds) / fadeSeconds);
                if (alpha <= 0)
                {
                    if (entry.text != null)
                        Destroy(entry.text.gameObject);
                    entries.RemoveAt(i);
                    continue;
                }
                var c = entry.text.color;
                c.a = alpha;
                entry.text.color = c;
            }
        }
    }
}
