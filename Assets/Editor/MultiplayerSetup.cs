using MarioNet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One-click scene wiring for multiplayer: Mario > Setup Multiplayer Scene.
// Safe to run repeatedly; it skips anything that already exists.
public static class MultiplayerSetup
{
    const string MarioPrefabPath = "Assets/mario/mario.prefab";
    const string RemotePrefabPath = "Assets/Net/RemotePlayer.prefab";

    [MenuItem("Mario/Setup Multiplayer Scene")]
    public static void Setup()
    {
        var remotePrefab = BuildRemotePlayerPrefab();

        var localMovement = Object.FindObjectOfType<movement>(true);
        if (localMovement == null)
        {
            Debug.LogError("MultiplayerSetup: no object with a movement component in the open scene");
            return;
        }
        if (localMovement.GetComponent<LocalPlayerSync>() == null)
            localMovement.gameObject.AddComponent<LocalPlayerSync>();

        var networkManager = Object.FindObjectOfType<NetworkManager>(true);
        if (networkManager == null)
        {
            var go = new GameObject("NetworkManager");
            networkManager = go.AddComponent<NetworkManager>();
        }
        networkManager.remotePlayerPrefab = remotePrefab;
        EditorUtility.SetDirty(networkManager);

        if (Object.FindObjectOfType<JoinUI>(true) == null)
            BuildJoinUI(localMovement);
        BuildHudIfMissing();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("MultiplayerSetup: done. Start mario-server and press Play.");
    }

    public static bool BuildHudIfMissing()
    {
        if (Object.FindObjectOfType<HudUI>(true) != null)
            return false;
        var font = GetFont();

        var canvasGo = new GameObject("HudCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var countGo = new GameObject("PlayerCount");
        countGo.transform.SetParent(canvasGo.transform, false);
        var count = countGo.AddComponent<Text>();
        count.font = font;
        count.fontSize = 28;
        count.fontStyle = FontStyle.Bold;
        count.color = Color.white;
        count.alignment = TextAnchor.UpperRight;
        count.horizontalOverflow = HorizontalWrapMode.Overflow;
        countGo.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        var countRect = countGo.GetComponent<RectTransform>();
        countRect.anchorMin = countRect.anchorMax = countRect.pivot = new Vector2(1, 1);
        countRect.anchoredPosition = new Vector2(-25, -20);
        countRect.sizeDelta = new Vector2(300, 40);

        var feedGo = new GameObject("Feed");
        feedGo.transform.SetParent(canvasGo.transform, false);
        var feedRect = feedGo.AddComponent<RectTransform>();
        feedRect.anchorMin = feedRect.anchorMax = feedRect.pivot = new Vector2(0, 1);
        feedRect.anchoredPosition = new Vector2(25, -20);
        feedRect.sizeDelta = new Vector2(400, 400);
        var layout = feedGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 4;

        var hud = canvasGo.AddComponent<HudUI>();
        hud.playerCountText = count;
        hud.feedRoot = feedRect;
        hud.font = font;
        EditorUtility.SetDirty(hud);
        Debug.Log("MultiplayerSetup: created HudCanvas");
        return true;
    }

    static GameObject BuildRemotePlayerPrefab()
    {
        var marioPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarioPrefabPath);
        if (marioPrefab == null)
        {
            Debug.LogError($"MultiplayerSetup: {MarioPrefabPath} not found");
            return null;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Net"))
            AssetDatabase.CreateFolder("Assets", "Net");

        var inst = Object.Instantiate(marioPrefab);
        inst.name = "RemotePlayer";

        float nameplateHeight = 2.4f;
        var controller = inst.GetComponent<CharacterController>();
        if (controller != null)
            nameplateHeight = controller.center.y + controller.height / 2f + 0.6f;

        // movement first: RequireComponent blocks removing its dependencies before it.
        foreach (var c in inst.GetComponents<movement>()) Object.DestroyImmediate(c);
        foreach (var c in inst.GetComponents<CharacterController>()) Object.DestroyImmediate(c);
        foreach (var c in inst.GetComponents<AudioSource>()) Object.DestroyImmediate(c);

        inst.AddComponent<RemotePlayer>();

        var font = GetFont();
        var plateGo = new GameObject("Nameplate");
        plateGo.transform.SetParent(inst.transform, false);
        plateGo.transform.localPosition = Vector3.up * nameplateHeight;
        var text = plateGo.AddComponent<TextMesh>();
        text.text = "player";
        text.font = font;
        text.fontSize = 48;
        text.characterSize = 0.05f;
        text.anchor = TextAnchor.LowerCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        plateGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        var prefab = PrefabUtility.SaveAsPrefabAsset(inst, RemotePrefabPath);
        Object.DestroyImmediate(inst);
        Debug.Log($"MultiplayerSetup: saved {RemotePrefabPath}");
        return prefab;
    }

    static void BuildJoinUI(movement localMovement)
    {
        if (Object.FindObjectOfType<EventSystem>(true) == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        };
        var font = GetFont();

        var canvasGo = new GameObject("JoinCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = DefaultControls.CreatePanel(resources);
        panel.name = "JoinPanel";
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460, 320);
        panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.12f, 0.92f);

        var title = MakeText(panel.transform, font, "MARIO ONLINE", 36, new Vector2(0, 110));
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(1f, 0.85f, 0.2f);

        var inputGo = DefaultControls.CreateInputField(resources);
        inputGo.name = "UsernameInput";
        inputGo.transform.SetParent(panel.transform, false);
        var inputRect = inputGo.GetComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(300, 44);
        inputRect.anchoredPosition = new Vector2(0, 35);
        var input = inputGo.GetComponent<InputField>();
        input.characterLimit = 16;
        foreach (var t in inputGo.GetComponentsInChildren<Text>())
        {
            t.font = font;
            t.fontSize = 22;
        }
        input.placeholder.GetComponent<Text>().text = "username";

        var buttonGo = DefaultControls.CreateButton(resources);
        buttonGo.name = "JoinButton";
        buttonGo.transform.SetParent(panel.transform, false);
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(180, 48);
        buttonRect.anchoredPosition = new Vector2(0, -35);
        var buttonText = buttonGo.GetComponentInChildren<Text>();
        buttonText.text = "Join";
        buttonText.font = font;
        buttonText.fontSize = 24;

        var status = MakeText(panel.transform, font, "", 18, new Vector2(0, -100));
        status.name = "StatusText";
        status.color = new Color(1f, 0.45f, 0.4f);

        var joinUI = canvasGo.AddComponent<JoinUI>();
        joinUI.panel = panel;
        joinUI.usernameInput = input;
        joinUI.joinButton = buttonGo.GetComponent<Button>();
        joinUI.statusText = status;
        joinUI.localMovement = localMovement;
        EditorUtility.SetDirty(joinUI);
        Debug.Log("MultiplayerSetup: created JoinCanvas UI");
    }

    static Text MakeText(Transform parent, Font font, string content, int size, Vector2 pos)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420, 50);
        rect.anchoredPosition = pos;
        return text;
    }

    static Font GetFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
