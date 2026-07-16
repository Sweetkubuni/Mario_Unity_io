using System;

// JSON message shapes shared with mario-server (D:\unity\mario-server).
// Parsed with JsonUtility, so field names must match the server's snake_case JSON.
namespace MarioNet
{
    [Serializable]
    public class MsgBase
    {
        public string type;
    }

    [Serializable]
    public class JoinSend
    {
        public string type = "join";
        public string username;
    }

    [Serializable]
    public class StateSend
    {
        public string type = "state";
        public float x, y, z, ry;
        public float speed;   // normalized 0..1 for the "latteralSpeed" animator param
        public float vspeed;  // premapped 0..2 for the "verticalSpeed" animator param
        public bool grounded;
    }

    [Serializable]
    public class PlayerInfo
    {
        public uint id;
        public string username;
        public float x, y, z, ry;
    }

    [Serializable]
    public class WelcomeMsg
    {
        public uint id;
        public PlayerInfo[] players;
    }

    [Serializable]
    public class PlayerJoinedMsg
    {
        public uint id;
        public string username;
    }

    [Serializable]
    public class PlayerLeftMsg
    {
        public uint id;
    }

    [Serializable]
    public class StateRecv
    {
        public uint id;
        public float x, y, z, ry;
        public float speed;
        public float vspeed;
        public bool grounded;
    }

    [Serializable]
    public class ErrorMsg
    {
        public string message;
    }
}
