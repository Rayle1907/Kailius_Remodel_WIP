#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class ReviveOnboardingPageTests
{
    private GameObject root;
    private ReviveOnboardingPresenter presenter;
    private TMP_FontAsset font;
    private RectTransform panel;
    private int dismissals;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Onboarding test", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ReviveOnboardingView.prefab");
        presenter = Object.Instantiate(prefab, root.transform).GetComponent<ReviveOnboardingPresenter>();
        font = presenter.GetComponentInChildren<TextMeshProUGUI>(true).font;
        panel = (RectTransform)root.transform;
        presenter.Show(font, panel, () => dismissals++);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        Object.DestroyImmediate(root);
    }

    [Test]
    public void FreshTapsAdvanceThenDismissWhilePaused()
    {
        Time.timeScale = 0f;
        presenter.Advance(100f, false, false);
        Tap();
        Assert.That(presenter.CurrentPage, Is.EqualTo(1));
        Assert.That(presenter.State, Is.EqualTo(ReviveOnboardingPresenter.TutorialState.Typing));
        Assert.That(dismissals, Is.Zero);
        Assert.That(presenter.GetComponentInChildren<TextMeshProUGUI>(true).text,
            Is.EqualTo(ReviveOnboardingPresenter.IntroductionPageTwo));
        presenter.Advance(100f, false, false);
        Tap();
        Assert.That(presenter.State, Is.EqualTo(ReviveOnboardingPresenter.TutorialState.Dismissed));
        Assert.That(dismissals, Is.EqualTo(1));
        Tap();
        Assert.That(dismissals, Is.EqualTo(1));
    }

    [Test]
    public void EarlyAndHeldInputCannotSkipEitherPage()
    {
        for (int page = 0; page < 2; page++)
        {
            presenter.Advance(0f, true, true);
            Assert.That(presenter.CurrentPage, Is.EqualTo(page));
            Assert.That(presenter.State, Is.EqualTo(ReviveOnboardingPresenter.TutorialState.Typing));
            presenter.Advance(100f, true, true);
            presenter.Advance(0f, true, true);
            Assert.That(presenter.CurrentPage, Is.EqualTo(page));
            Assert.That(presenter.State, Is.EqualTo(ReviveOnboardingPresenter.TutorialState.AwaitingDismissal));
            Assert.That(dismissals, Is.Zero);
            Tap();
        }
        Assert.That(dismissals, Is.EqualTo(1));
    }

    [Test]
    public void ReopeningStartsOnFirstPage()
    {
        presenter.Advance(100f, false, false);
        Tap();
        presenter.Show(font, panel, () => dismissals++);
        Assert.That(presenter.CurrentPage, Is.Zero);
        Assert.That(presenter.State, Is.EqualTo(ReviveOnboardingPresenter.TutorialState.Typing));
        Assert.That(presenter.GetComponentInChildren<TextMeshProUGUI>(true).text,
            Is.EqualTo(ReviveOnboardingPresenter.IntroductionPageOne));
        Assert.That(dismissals, Is.Zero);
    }

    private void Tap()
    {
        presenter.Advance(0f, false, false);
        presenter.Advance(0f, true, true);
    }
}
#endif
