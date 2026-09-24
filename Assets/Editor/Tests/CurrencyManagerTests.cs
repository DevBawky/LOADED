using NUnit.Framework;
using UnityEngine;

public sealed class CurrencyManagerTests
{
    [Test]
    public void GoldArrivalClipLookupDoesNotConsumeGameplayRandom()
    {
        var library = Resources.Load<SoundClipLibrary>("Sound/SoundClipLibrary");
        Assert.That(library, Is.Not.Null);
        Random.State before = Random.state;
        try
        {
            float expected = Random.value;
            Random.state = before;
            Assert.That(library.TryGetFixedSfx("SFX_GainGold", out AudioClip clip, out _, out _), Is.True);
            Assert.That(clip, Is.Not.Null);
            Assert.That(Random.value, Is.EqualTo(expected));
        }
        finally { Random.state = before; }
    }

    [TestCase(1, 1)]
    [TestCase(5, 2)]
    [TestCase(12, 3)]
    [TestCase(1000000, 4)]
    public void RewardCoinsRepresentAmountWithoutOneObjectPerGold(int amount, int count)
    {
        Assert.That(GoldRewardPresenter.CoinCountForAmount(amount), Is.EqualTo(count));
    }

    [Test]
    public void AnimatedRewardsCommitImmediatelyMergeAndFlushWithoutExtraEvents()
    {
        var root = new GameObject("Reward Test");
        var canvasObject = new GameObject("Reward Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(root.transform);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var panelObject = new GameObject("Panel | Money", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panelObject.transform.SetParent(canvas.transform, false);
        var textObject = new GameObject("Text | Current Money", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);
        var text = textObject.GetComponent<TMPro.TextMeshProUGUI>();
        var cameraObject = new GameObject("Reward Camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        var manager = root.AddComponent<CurrencyManager>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(CurrencyManager).GetField("currentMoneyText", flags).SetValue(manager, text);
        typeof(CurrencyManager).GetField("moneyPanel", flags).SetValue(manager, panelObject.transform);
        try
        {
            manager.RestoreRunMoney(7);
            int changes = 0;
            manager.MoneyChanged += _ => changes++;
            for (int i = 0; i < 10; i++) manager.AddMoneyFromWorld(100, Vector3.zero);
            Assert.That(manager.CurrentMoney, Is.EqualTo(1007));
            Assert.That(changes, Is.EqualTo(10));
            Assert.That(text.text, Is.EqualTo("$ 7"));
            int coins = 0;
            foreach (Transform child in canvas.transform) if (child.name == "Reward Coin") coins++;
            Assert.That(coins, Is.EqualTo(24));
            var presenter = (GoldRewardPresenter)typeof(CurrencyManager).GetField("goldPresenter", flags).GetValue(manager);
            presenter.Tick(1f, true);
            Assert.That(text.text, Is.EqualTo("$ 7"));
            presenter.Tick(1f, false);
            Assert.That(text.text, Is.EqualTo("$ 1007"));
            Assert.That(changes, Is.EqualTo(10));
            manager.AddMoneyFromWorld(5, Vector3.zero);
            Assert.That(manager.TrySpendMoney(10), Is.True);
            Assert.That(manager.CurrentMoney, Is.EqualTo(1002));
            Assert.That(text.text, Is.EqualTo("$ 1002"));
            manager.FlushPendingMoney();
            Assert.That(manager.CurrentMoney, Is.EqualTo(1002));
            Assert.That(manager.IsRewardPresentationActive, Is.False);
            Assert.That(changes, Is.EqualTo(12));
            manager.AddMoneyFromWorld(8, Vector3.zero);
            manager.enabled = false;
            manager.FlushPendingMoney();
            Assert.That(manager.CurrentMoney, Is.EqualTo(1010));
            Assert.That(text.text, Is.EqualTo("$ 1010"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void FinalDefeatRequiresExhaustedPoolOrFinalLegacyWave()
    {
        var root = new GameObject("Final Defeat Test");
        var waves = root.AddComponent<WaveManager>();
        var data = ScriptableObject.CreateInstance<EnemyData>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            Assert.That(waves.IsFinalDefeatForPresentation(), Is.False);
            typeof(WaveManager).GetField("waves", flags).SetValue(waves, new[] { new EnemyWave(), new EnemyWave() });
            typeof(WaveManager).GetField("currentWaveIndex", flags).SetValue(waves, 0);
            Assert.That(waves.IsFinalDefeatForPresentation(), Is.False);
            typeof(WaveManager).GetField("currentWaveIndex", flags).SetValue(waves, 1);
            Assert.That(waves.IsFinalDefeatForPresentation(), Is.True);
            typeof(WaveManager).GetField("combatPacingMode", flags).SetValue(waves, CombatPacingMode.DuelClock);
            typeof(WaveManager).GetField("isDuelClockEnemyPoolConfigured", flags).SetValue(waves, true);
            var pool = (DuelClockEnemySpawnPool)typeof(WaveManager).GetField("duelClockEnemySpawnPool", flags).GetValue(waves);
            Assert.That(pool.ConfigureFresh(new[] { new DuelClockEnemySpawnEntry(data, 1f) }, 1), Is.True);
            Assert.That(waves.IsFinalDefeatForPresentation(), Is.False);
            Assert.That(pool.TryCommitSpawn(0, data), Is.True);
            Assert.That(waves.IsFinalDefeatForPresentation(), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void WorldGoldCommitsBeforePresentationAndFlushDoesNotDuplicate()
    {
        GameObject managerObject = new GameObject("Currency");

        try
        {
            CurrencyManager manager =
                managerObject.AddComponent<CurrencyManager>();
            manager.RestoreRunMoney(7);
            int changeCount = 0;
            int lastAmount = -1;
            manager.MoneyChanged += amount =>
            {
                changeCount++;
                lastAmount = amount;
            };

            bool added = manager.AddMoneyFromWorld(5, Vector3.zero);

            Assert.That(added, Is.True);
            Assert.That(manager.CurrentMoney, Is.EqualTo(12));
            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(lastAmount, Is.EqualTo(12));

            manager.FlushPendingMoney();

            Assert.That(manager.CurrentMoney, Is.EqualTo(12));
            Assert.That(changeCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
        }
    }
}
