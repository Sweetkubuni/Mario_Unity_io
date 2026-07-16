using System;
using System.Collections.Generic;
using NativeWebSocket;
using UnityEngine;

namespace MarioNet
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Connection")]
        public string serverUrl = "ws://127.0.0.1:8765";
        [Range(5, 30)] public float sendRate = 15f;

        [Header("Players")]
        public GameObject remotePlayerPrefab;

        public event Action OnJoinedRoom;
        public event Action<string> OnJoinError;
        public event Action OnDisconnected;
        public event Action<string> OnRemoteJoined;
        public event Action<string> OnRemoteLeft;

        public bool Joined { get; private set; }
        public uint MyId { get; private set; }
        public string MyUsername { get; private set; }
        public int PlayerCount => Joined ? remotePlayers.Count + 1 : 0;

        WebSocket websocket;
        string pendingUsername;
        float sendTimer;
        LocalPlayerSync localPlayer;
        readonly Dictionary<uint, RemotePlayer> remotePlayers = new Dictionary<uint, RemotePlayer>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            localPlayer = FindObjectOfType<LocalPlayerSync>();
            if (localPlayer == null)
                Debug.LogError("NetworkManager: no LocalPlayerSync found in scene");
            if (remotePlayerPrefab == null)
                Debug.LogError("NetworkManager: remotePlayerPrefab not assigned");
        }

        public async void Connect(string username)
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
                return;
            pendingUsername = username;

            websocket = new WebSocket(serverUrl);
            websocket.OnOpen += () =>
            {
                var join = new JoinSend { username = pendingUsername };
                _ = websocket.SendText(JsonUtility.ToJson(join));
            };
            websocket.OnMessage += bytes => HandleMessage(System.Text.Encoding.UTF8.GetString(bytes));
            websocket.OnError += err =>
            {
                Debug.LogWarning($"NetworkManager: websocket error: {err}");
                if (!Joined)
                    OnJoinError?.Invoke("could not reach server");
            };
            websocket.OnClose += code =>
            {
                bool wasJoined = Joined;
                Joined = false;
                ClearRemotePlayers();
                if (wasJoined)
                    OnDisconnected?.Invoke();
            };

            try
            {
                await websocket.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NetworkManager: connect failed: {e.Message}");
                if (!Joined)
                    OnJoinError?.Invoke("could not reach server");
            }
        }

        void HandleMessage(string json)
        {
            var baseMsg = JsonUtility.FromJson<MsgBase>(json);
            switch (baseMsg.type)
            {
                case "welcome":
                    var welcome = JsonUtility.FromJson<WelcomeMsg>(json);
                    MyId = welcome.id;
                    MyUsername = pendingUsername;
                    Joined = true;
                    foreach (var p in welcome.players)
                        SpawnRemote(p.id, p.username, new Vector3(p.x, p.y, p.z), p.ry);
                    OnJoinedRoom?.Invoke();
                    break;
                case "player_joined":
                    var joined = JsonUtility.FromJson<PlayerJoinedMsg>(json);
                    SpawnRemote(joined.id, joined.username, localPlayer != null ? localPlayer.transform.position : Vector3.zero, 0);
                    OnRemoteJoined?.Invoke(joined.username);
                    break;
                case "player_left":
                    var left = JsonUtility.FromJson<PlayerLeftMsg>(json);
                    if (remotePlayers.TryGetValue(left.id, out var leaver))
                        OnRemoteLeft?.Invoke(leaver.Username);
                    DespawnRemote(left.id);
                    break;
                case "state":
                    var state = JsonUtility.FromJson<StateRecv>(json);
                    if (remotePlayers.TryGetValue(state.id, out var remote))
                        remote.ApplyState(state);
                    break;
                case "error":
                    var error = JsonUtility.FromJson<ErrorMsg>(json);
                    OnJoinError?.Invoke(error.message);
                    break;
            }
        }

        void SpawnRemote(uint id, string username, Vector3 pos, float ry)
        {
            if (remotePlayers.ContainsKey(id))
                return;
            var go = Instantiate(remotePlayerPrefab, pos, Quaternion.Euler(0, ry, 0));
            go.name = $"Remote_{username}";
            var remote = go.GetComponent<RemotePlayer>();
            remote.Init(id, username);
            remotePlayers[id] = remote;
        }

        void DespawnRemote(uint id)
        {
            if (!remotePlayers.TryGetValue(id, out var remote))
                return;
            remotePlayers.Remove(id);
            if (remote != null)
                Destroy(remote.gameObject);
        }

        void ClearRemotePlayers()
        {
            foreach (var remote in remotePlayers.Values)
                if (remote != null)
                    Destroy(remote.gameObject);
            remotePlayers.Clear();
        }

        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            websocket?.DispatchMessageQueue();
#endif
            if (!Joined || localPlayer == null || websocket == null || websocket.State != WebSocketState.Open)
                return;
            sendTimer += Time.deltaTime;
            if (sendTimer < 1f / sendRate)
                return;
            sendTimer = 0;
            _ = websocket.SendText(JsonUtility.ToJson(localPlayer.BuildState()));
        }

        async void OnApplicationQuit()
        {
            if (websocket != null)
                await websocket.Close();
        }
    }
}
