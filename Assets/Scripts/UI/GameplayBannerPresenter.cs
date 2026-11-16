using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameplayBannerToken
{
    internal GameplayBannerPresenter Presenter;
}

public sealed class GameplayBannerPresenter : MonoBehaviour
{
    private Canvas canvas;
    private GameplayOnboardingBannerView view;
    private GameplayBannerToken currentToken;
    private object currentRequestKey;
    private GameObject contentRoot;
    private RectTransform rowsRoot;
    private readonly List<GauntletEnemyRow> enemyRows = new List<GauntletEnemyRow>();
    private readonly List<KeyValuePair<string, int>> enemyRowValues = new List<KeyValuePair<string, int>>();
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private float lastScaleFactor;

    public static GameplayBannerToken Show(Canvas canvas, TMP_FontAsset font, string message,
        float durationSeconds = 0f, object requestKey = null)
    {
        GameplayBannerPresenter presenter = GetOrCreate(canvas, font);
        if (presenter == null) return null;
        return presenter.ShowMessage(font, message, durationSeconds, requestKey);
    }

    public static bool IsOwner(GameplayBannerToken token)
    {
        if (token == null || token.Presenter == null) return false;
        return token.Presenter.currentToken == token;
    }

    public static bool IsVisible(GameplayBannerToken token)
    {
        if (!IsOwner(token)) return false;
        return token.Presenter.view != null && token.Presenter.view.IsVisible;
    }

    public static bool UpdateMessage(GameplayBannerToken token, string message)
    {
        if (!IsOwner(token)) return false;
        token.Presenter.view.UpdateText(message);
        return true;
    }

    public static bool SetVisible(GameplayBannerToken token, bool visible)
    {
        if (!IsOwner(token)) return false;
        token.Presenter.SetPresentationVisible(visible);
        return true;
    }

    public static bool SetEnemyRows(GameplayBannerToken token, GauntletRequirementsHud hud,
        IReadOnlyList<KeyValuePair<string, int>> values)
    {
        if (!IsOwner(token) || hud == null) return false;
        return token.Presenter.SetEnemyRows(hud, values);
    }

    public static bool ClearEnemyRows(GameplayBannerToken token)
    {
        if (!IsOwner(token)) return false;
        token.Presenter.ClearEnemyRows();
        return true;
    }

    public static Transform CreateContentRoot(GameplayBannerToken token)
    {
        if (!IsOwner(token)) return null;
        GameplayBannerPresenter presenter = token.Presenter;
        if (presenter.contentRoot != null) return presenter.contentRoot.transform;
        RectTransform content = new GameObject("Gameplay Banner Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(presenter.transform, false);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        presenter.contentRoot = content.gameObject;
        return content;
    }

    public static void RefreshLayoutIfNeeded(GameplayBannerToken token)
    {
        if (!IsOwner(token)) return;
        token.Presenter.view.RefreshLayoutIfNeeded();
    }

    public static bool TryGetTextStyle(GameplayBannerToken token, out TMP_FontAsset font, out float fontSizeMax, out float fontSizeMin)
    {
        font = null;
        fontSizeMax = 0f;
        fontSizeMin = 0f;
        if (!IsOwner(token)) return false;
        GameplayOnboardingBannerView currentView = token.Presenter.view;
        currentView.RefreshLayoutIfNeeded();
        font = currentView.Font;
        fontSizeMax = currentView.FontSizeMax;
        fontSizeMin = currentView.FontSizeMin;
        return true;
    }

    public static void Dismiss(GameplayBannerToken token)
    {
        if (!IsOwner(token)) return;
        token.Presenter.ClearCurrentPresentation();
    }

    private static GameplayBannerPresenter GetOrCreate(Canvas requestedCanvas, TMP_FontAsset font)
    {
        if (requestedCanvas == null || !requestedCanvas.isActiveAndEnabled) return null;
        Canvas rootCanvas = requestedCanvas.rootCanvas;
        Scene scene = rootCanvas.gameObject.scene;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameplayBannerPresenter[] presenters = root.GetComponentsInChildren<GameplayBannerPresenter>(true);
            foreach (GameplayBannerPresenter existing in presenters)
            {
                if (existing == null) continue;
                existing.SetCanvas(rootCanvas, font);
                return existing;
            }
        }

        RectTransform presenterRoot = new GameObject("Gameplay Banner Presenter", typeof(RectTransform)).GetComponent<RectTransform>();
        presenterRoot.SetParent(rootCanvas.transform, false);
        SetFullCanvasRect(presenterRoot);
        GameplayBannerPresenter presenter = presenterRoot.gameObject.AddComponent<GameplayBannerPresenter>();
        presenter.SetCanvas(rootCanvas, font);
        return presenter;
    }

    private static void SetFullCanvasRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private GameplayBannerToken ShowMessage(TMP_FontAsset font, string message, float durationSeconds, object requestKey)
    {
        if (currentToken != null && requestKey != null && Equals(currentRequestKey, requestKey))
        {
            view.UpdateText(message);
            return currentToken;
        }

        ClearCurrentPresentation();
        currentToken = new GameplayBannerToken { Presenter = this };
        currentRequestKey = requestKey;
        view.SetFont(font);
        view.Show(message);
        if (durationSeconds > 0f && Application.isPlaying)
            StartCoroutine(DismissAfter(currentToken, durationSeconds));
        return currentToken;
    }

    private bool SetEnemyRows(GauntletRequirementsHud hud, IReadOnlyList<KeyValuePair<string, int>> values)
    {
        if (values == null) values = new List<KeyValuePair<string, int>>();
        bool unchanged = enemyRowValues.Count == values.Count;
        if (unchanged)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (enemyRowValues[i].Key == values[i].Key && enemyRowValues[i].Value == values[i].Value) continue;
                unchanged = false;
                break;
            }
        }
        if (unchanged) return true;

        ClearEnemyRows();
        if (values.Count == 0) return true;
        RectTransform parent = CreateContentRoot(currentToken) as RectTransform;
        if (parent == null) return false;
        RectTransform root = new GameObject("Gameplay Banner Requirement Rows", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        rowsRoot = root;

        for (int i = 0; i < values.Count; i++)
        {
            KeyValuePair<string, int> item = values[i];
            if (item.Value <= 0) continue;
            GauntletEnemyRow row = hud.CreateNoticeRow(rowsRoot);
            if (row == null) continue;
            row.name = item.Key + " Outstanding Count";
            row.SetTextOnlyRemaining(item.Key, item.Value);
            row.gameObject.SetActive(true);
            enemyRows.Add(row);
            enemyRowValues.Add(item);
        }
        RefreshContentLayout();
        return true;
    }

    private void ClearEnemyRows()
    {
        foreach (GauntletEnemyRow row in enemyRows)
        {
            if (row == null) continue;
            row.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(row.gameObject);
            else DestroyImmediate(row.gameObject);
        }
        enemyRows.Clear();
        enemyRowValues.Clear();
        if (rowsRoot != null)
        {
            rowsRoot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(rowsRoot.gameObject);
            else DestroyImmediate(rowsRoot.gameObject);
        }
        rowsRoot = null;
    }

    private void RefreshContentLayout()
    {
        if (rowsRoot == null || canvas == null || view == null) return;
        Rect safe = Screen.safeArea;
        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);
        float scaleRatio = Mathf.Max(1f, safe.height) / 1080f / Mathf.Max(0.001f, canvas.scaleFactor);
        float lowerBannerEdge = (safe.yMin + safe.height * 0.76f) / screenHeight;
        rowsRoot.anchorMin = new Vector2(safe.xMin / screenWidth, lowerBannerEdge);
        rowsRoot.anchorMax = new Vector2(safe.xMax / screenWidth, lowerBannerEdge);
        rowsRoot.anchoredPosition = Vector2.zero;
        rowsRoot.sizeDelta = new Vector2(-24f * scaleRatio, 1f);
        view.RefreshLayoutIfNeeded();
        TMP_FontAsset font = view.Font;
        float fontSizeMax = view.FontSizeMax;
        float fontSizeMin = view.FontSizeMin;
        for (int i = 0; i < enemyRows.Count; i++)
        {
            RectTransform rowRect = (RectTransform)enemyRows[i].transform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -(20f + i * 68f) * scaleRatio);
            rowRect.sizeDelta = new Vector2(-12f * scaleRatio, 60f * scaleRatio);
            enemyRows[i].SetNoticeTextStyle(font, fontSizeMax, fontSizeMin);
        }
        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastScaleFactor = canvas.scaleFactor;
    }

    private void SetPresentationVisible(bool visible)
    {
        view.SetVisible(visible);
        if (contentRoot != null) contentRoot.SetActive(visible);
    }

    private void SetCanvas(Canvas ownerCanvas, TMP_FontAsset font)
    {
        if (canvas != ownerCanvas)
        {
            ClearCurrentPresentation();
            if (view != null) view.Dispose();
            view = null;
            canvas = ownerCanvas;
            transform.SetParent(canvas.transform, false);
            SetFullCanvasRect((RectTransform)transform);
        }

        if (view == null) view = GameplayOnboardingBannerView.Create(canvas, font, transform);
        else view.SetFont(font);
    }

    private IEnumerator DismissAfter(GameplayBannerToken token, float durationSeconds)
    {
        yield return new WaitForSecondsRealtime(durationSeconds);
        Dismiss(token);
    }

    private void Update()
    {
        if (currentToken == null || view == null) return;
        view.RefreshLayoutIfNeeded();
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height
            || !Mathf.Approximately(lastScaleFactor, canvas.scaleFactor))
            RefreshContentLayout();
    }

    private void ClearCurrentPresentation()
    {
        if (currentToken != null) currentToken.Presenter = null;
        currentToken = null;
        currentRequestKey = null;
        if (view != null) view.Hide();
        ClearEnemyRows();
        if (contentRoot != null)
        {
            contentRoot.SetActive(false);
            if (Application.isPlaying) Destroy(contentRoot);
            else DestroyImmediate(contentRoot);
            contentRoot = null;
        }
    }

    private void OnDestroy()
    {
        ClearCurrentPresentation();
        if (view != null) view.Dispose();
    }
}
