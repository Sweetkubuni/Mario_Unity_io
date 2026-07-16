using UnityEngine;
using UnityEngine.UI;

namespace MarioNet
{
    // Username entry screen. Gates local control until the player is in the room.
    public class JoinUI : MonoBehaviour
    {
        public GameObject panel;
        public InputField usernameInput;
        public Button joinButton;
        public Text statusText;
        public movement localMovement;

        void Start()
        {
            if (localMovement == null)
                localMovement = FindObjectOfType<movement>();
            SetMenuState(true, "");
            joinButton.onClick.AddListener(Join);
            usernameInput.onEndEdit.AddListener(_ =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    Join();
            });

            NetworkManager.Instance.OnJoinedRoom += HandleJoined;
            NetworkManager.Instance.OnJoinError += HandleJoinError;
            NetworkManager.Instance.OnDisconnected += HandleDisconnected;
        }

        void OnDestroy()
        {
            if (NetworkManager.Instance == null)
                return;
            NetworkManager.Instance.OnJoinedRoom -= HandleJoined;
            NetworkManager.Instance.OnJoinError -= HandleJoinError;
            NetworkManager.Instance.OnDisconnected -= HandleDisconnected;
        }

        void Join()
        {
            string username = usernameInput.text.Trim();
            if (username.Length == 0 || username.Length > 16)
            {
                statusText.text = "username must be 1-16 characters";
                return;
            }
            statusText.text = "connecting...";
            joinButton.interactable = false;
            NetworkManager.Instance.Connect(username);
        }

        void HandleJoined() => SetMenuState(false, "");

        void HandleJoinError(string message)
        {
            statusText.text = message;
            joinButton.interactable = true;
        }

        void HandleDisconnected() => SetMenuState(true, "disconnected from server");

        void SetMenuState(bool inMenu, string status)
        {
            panel.SetActive(inMenu);
            statusText.text = status;
            joinButton.interactable = true;
            if (localMovement != null)
                localMovement.enabled = !inMenu;
            Cursor.lockState = inMenu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = inMenu;
            if (inMenu)
                usernameInput.Select();
        }
    }
}
