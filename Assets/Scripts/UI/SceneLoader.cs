using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    public const string MainMenuSceneName = "MainMenu";
    public const string RaceSceneName = "OutdoorsScene";
    public const string LoadingScreenResourcePath = "LoadingScreen";

    [Tooltip("Optional scene instance. If empty, the prefab below is instantiated.")]
    [SerializeField] LoadingScreenView loadingScreen;

    [Tooltip("Designer prefab. Defaults to Resources/LoadingScreen when null.")]
    [SerializeField] LoadingScreenView loadingScreenPrefab;

    bool loading;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveLoadingScreen();
        if (loadingScreen != null)
            loadingScreen.SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void LoadRaceScene()
    {
        EnsureExists().LoadScene(RaceSceneName);
    }

    public static void LoadMainMenu()
    {
        EnsureExists().LoadScene(MainMenuSceneName);
    }

    public static void ReloadActiveScene()
    {
        EnsureExists().LoadScene(SceneManager.GetActiveScene().name);
    }

    static SceneLoader EnsureExists()
    {
        if (Instance != null)
            return Instance;

        // Prefer a fully wired prefab from Resources (includes loading UI ref).
        var loaderPrefab = Resources.Load<GameObject>("SceneLoader");
        if (loaderPrefab != null)
        {
            var instance = Instantiate(loaderPrefab);
            instance.name = "SceneLoader";
            return instance.GetComponent<SceneLoader>() ?? instance.AddComponent<SceneLoader>();
        }

        var go = new GameObject("SceneLoader");
        return go.AddComponent<SceneLoader>();
    }

    public void LoadScene(string sceneName)
    {
        if (loading || string.IsNullOrEmpty(sceneName))
            return;

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    IEnumerator LoadSceneRoutine(string sceneName)
    {
        loading = true;
        ResolveLoadingScreen();

        if (loadingScreen != null)
        {
            loadingScreen.SetVisible(true);
            loadingScreen.SetProgress(0f, "Loading...");
        }
        else
        {
            Debug.LogWarning(
                "[SceneLoader] No LoadingScreenView assigned. " +
                "Create/edit Assets/Resources/LoadingScreen.prefab and assign it on SceneLoader.");
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            loadingScreen?.SetProgress(op.progress / 0.9f, "Loading...");
            yield return null;
        }

        loadingScreen?.SetProgress(1f, "Almost ready...");
        yield return null;
        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        yield return null;
        yield return null;
        loadingScreen?.SetVisible(false);
        loading = false;
    }

    void ResolveLoadingScreen()
    {
        if (loadingScreen != null)
            return;

        if (loadingScreenPrefab == null)
            loadingScreenPrefab = Resources.Load<LoadingScreenView>(LoadingScreenResourcePath);

        if (loadingScreenPrefab == null)
            return;

        loadingScreen = Instantiate(loadingScreenPrefab, transform);
        loadingScreen.name = "LoadingScreen";
    }
}
