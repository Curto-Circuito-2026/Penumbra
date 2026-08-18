using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Utilitário de Editor para instanciar e configurar automaticamente o HUD de Moedas
/// (Fragmentos de Estrela e Estrelas) na cena Main, Hub e outras cenas do projeto.
/// </summary>
public static class SetupCurrencyUI
{
    [MenuItem("Praia Games/Configurar UI de Moedas na Cena Atual", false, 10)]
    public static void SetupInActiveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        SetupCurrencyUIInScene(scene);
    }

    [MenuItem("Praia Games/Configurar UI de Moedas na Cena Main", false, 11)]
    public static void SetupInMainScene()
    {
        string scenePath = "Assets/Scenes/Main.unity";
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogError($"[SetupCurrencyUI] Cena não encontrada em {scenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        SetupCurrencyUIInScene(scene);
    }

    [MenuItem("Praia Games/Configurar UI de Moedas em Todas as Cenas de Jogo", false, 12)]
    public static void SetupInAllScenes()
    {
        string[] scenePaths = new string[]
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Hub.unity",
            "Assets/Scenes/Region_Forest.unity",
            "Assets/Scenes/Region_Swamp.unity",
            "Assets/Scenes/Region_City.unity",
            "Assets/Scenes/Region_End.unity"
        };

        foreach (string path in scenePaths)
        {
            if (System.IO.File.Exists(path))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                SetupCurrencyUIInScene(scene);
            }
        }

        Debug.Log("[SetupCurrencyUI] Configuração concluída em todas as cenas!");
    }


    private static void SetupCurrencyUIInScene(UnityEngine.SceneManagement.Scene scene)
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            // Cria um Canvas se não existir
            GameObject canvasObj = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
        }

        // Verifica se já existe um CurrencyHUD no Canvas
        CurrencyUIHUD existingHUD = canvas.GetComponentInChildren<CurrencyUIHUD>(true);
        if (existingHUD != null)
        {
            Debug.Log($"[SetupCurrencyUI] CurrencyUIHUD já existe na cena '{scene.name}'!");
            return;
        }

        // 1. Cria o painel container (Canto Superior Direito) com visibilidade restrita a Playing
        GameObject panelObj = new GameObject("Currency_HUD_Panel", typeof(RectTransform), typeof(Image), typeof(UIStateVisibility), typeof(CurrencyUIHUD));
        panelObj.transform.SetParent(canvas.transform, false);

        UIStateVisibility visibility = panelObj.GetComponent<UIStateVisibility>();
        if (visibility != null)
        {
            visibility.SetVisibleStates(GameState.Playing);
        }

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-30f, -30f);
        panelRect.sizeDelta = new Vector2(230f, 90f);

        // Fundo semitransparente estilizado
        Image panelBg = panelObj.GetComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.75f);

        // Adiciona outline / borda sutil se desejado
        Outline outline = panelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.9f, 0.75f, 0.2f, 0.4f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 2. Cria o Texto de Fragmentos de Estrela
        GameObject fragTextObj = new GameObject("Fragments_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        fragTextObj.transform.SetParent(panelObj.transform, false);

        RectTransform fragRect = fragTextObj.GetComponent<RectTransform>();
        fragRect.anchorMin = new Vector2(0f, 0.5f);
        fragRect.anchorMax = new Vector2(1f, 1f);
        fragRect.offsetMin = new Vector2(15f, 0f);
        fragRect.offsetMax = new Vector2(-15f, -5f);

        TextMeshProUGUI fragTMP = fragTextObj.GetComponent<TextMeshProUGUI>();
        fragTMP.text = "Frag: 0/10";
        fragTMP.fontSize = 24f;
        fragTMP.fontStyle = FontStyles.Bold;
        fragTMP.color = new Color(1f, 0.88f, 0.25f, 1f);
        fragTMP.alignment = TextAlignmentOptions.MidlineLeft;

        // 3. Cria o Texto de Estrelas
        GameObject starsTextObj = new GameObject("Stars_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        starsTextObj.transform.SetParent(panelObj.transform, false);

        RectTransform starsRect = starsTextObj.GetComponent<RectTransform>();
        starsRect.anchorMin = new Vector2(0f, 0f);
        starsRect.anchorMax = new Vector2(1f, 0.5f);
        starsRect.offsetMin = new Vector2(15f, 5f);
        starsRect.offsetMax = new Vector2(-15f, 0f);

        TextMeshProUGUI starsTMP = starsTextObj.GetComponent<TextMeshProUGUI>();
        starsTMP.text = "Estrelas: 0";
        starsTMP.fontSize = 24f;
        starsTMP.fontStyle = FontStyles.Bold;
        starsTMP.color = new Color(1f, 0.95f, 0.5f, 1f);
        starsTMP.alignment = TextAlignmentOptions.MidlineLeft;

        // 4. Conecta as referências no CurrencyUIHUD
        CurrencyUIHUD hud = panelObj.GetComponent<CurrencyUIHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("fragmentsText").objectReferenceValue = fragTMP;
        so.FindProperty("starsText").objectReferenceValue = starsTMP;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[SetupCurrencyUI] UI de Moedas adicionada com sucesso na cena '{scene.name}'!");
    }
}
