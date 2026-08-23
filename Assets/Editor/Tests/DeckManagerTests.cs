using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DeckManagerTests
{
    private GameObject gameObject;
    private DeckManager deckManager;
    private BulletData bulletData;

    [SetUp]
    public void SetUp()
    {
        gameObject = new GameObject("Deck Manager Test");
        deckManager = gameObject.AddComponent<DeckManager>();
        bulletData = ScriptableObject.CreateInstance<BulletData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gameObject);
        Object.DestroyImmediate(bulletData);
    }

    [Test]
    public void AllBulletsCountTowardTwentyBulletLimit()
    {
        for (int index = 0;
             index < DeckManager.MaximumOwnedBulletCount;
             index++)
        {
            Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        }

        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(20));
        Assert.That(deckManager.CanAddBullet(bulletData), Is.False);
    }

    [Test]
    public void ManualRemovalPreservesLastOwnedBullet()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        BulletInstance onlyBullet = deckManager.PeekNextBullet();

        Assert.That(deckManager.CanRemoveBullet(onlyBullet), Is.False);
        Assert.That(deckManager.TryRemoveBullet(onlyBullet), Is.False);
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(1));
    }

    [Test]
    public void SingleBulletIsPreviewedBeforeItCanBeReloadedAgain()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        BulletInstance onlyBullet = deckManager.PeekNextBullet();

        Assert.That(deckManager.TryReload(out BulletInstance loaded), Is.True);
        Assert.That(loaded, Is.SameAs(onlyBullet));
        Assert.That(deckManager.ReloadableBulletCount, Is.Zero);
        Assert.That(deckManager.PeekNextBullet(), Is.SameAs(onlyBullet));

        Assert.That(deckManager.TryFireLoadedBullet(out BulletInstance fired),
            Is.True);
        Assert.That(fired, Is.SameAs(onlyBullet));
        deckManager.CompleteFiringSequence();

        Assert.That(deckManager.ReloadableBulletCount, Is.EqualTo(1));
        Assert.That(deckManager.PeekNextBullet(), Is.SameAs(onlyBullet));
    }

    [Test]
    public void EventRewardCanBeAddedAtAuthoredUpgradeLevel()
    {
        Assert.That(deckManager.TryAddBullet(bulletData, 2), Is.True);

        BulletInstance added = deckManager.PeekNextBullet();
        Assert.That(added, Is.Not.Null);
        Assert.That(added.Level, Is.EqualTo(2));
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(1));
    }

    [Test]
    public void EjectNextLoadedBulletMovesOnlyFirstShotToGraveyard()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryReload(out BulletInstance first), Is.True);
        Assert.That(deckManager.TryReload(out BulletInstance next), Is.True);

        Assert.That(
            deckManager.TryEjectNextLoadedBullet(out BulletInstance ejected),
            Is.True);
        Assert.That(ejected, Is.SameAs(next));
        Assert.That(deckManager.LoadedBullets, Has.Count.EqualTo(1));
        Assert.That(deckManager.LoadedBullets[0], Is.SameAs(first));
        Assert.That(deckManager.Graveyard, Has.Count.EqualTo(1));
        Assert.That(deckManager.Graveyard[0], Is.SameAs(next));
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(3));
    }

    [Test]
    public void EjectSelectedLoadedBulletMovesOnlyRequestedChamber()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryReload(out BulletInstance first), Is.True);
        Assert.That(deckManager.TryReload(out BulletInstance second), Is.True);

        Assert.That(
            deckManager.TryEjectLoadedBullet(0, out BulletInstance ejected),
            Is.True);
        Assert.That(ejected, Is.SameAs(first));
        Assert.That(deckManager.LoadedBullets, Has.Count.EqualTo(1));
        Assert.That(deckManager.LoadedBullets[0], Is.SameAs(second));
        Assert.That(deckManager.Graveyard, Does.Contain(first));
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(3));
    }

    [Test]
    public void ChamberEjectThroughPlayerShootDoesNotConsumeTurn()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(
            deckManager.TryReload(out BulletInstance loadedBullet),
            Is.True);

        PlayerMove playerMove = gameObject.AddComponent<PlayerMove>();
        PlayerShoot playerShoot = gameObject.AddComponent<PlayerShoot>();
        SerializedObject serializedShoot = new SerializedObject(playerShoot);
        serializedShoot.FindProperty("deckManager").objectReferenceValue =
            deckManager;
        serializedShoot.FindProperty("playerMove").objectReferenceValue =
            playerMove;
        serializedShoot.ApplyModifiedPropertiesWithoutUndo();
        BulletInstance notifiedBullet = null;
        playerShoot.LoadedBulletEjected += bullet => notifiedBullet = bullet;

        Assert.That(playerShoot.TryEjectLoadedBullet(0), Is.True);
        Assert.That(notifiedBullet, Is.SameAs(loadedBullet));
        Assert.That(playerMove.TurnCount, Is.Zero);
        Assert.That(deckManager.LoadedBullets, Is.Empty);
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(2));
    }

    [Test]
    public void ChamberEjectGuideFollowsReloadAndPrecedesCylinderInspection()
    {
        FirstRunGuideContent.GuideStepDefinition[] steps =
            FirstRunGuideContent.CombatSteps;
        int reloadIndex = System.Array.FindIndex(
            steps,
            step => step.Step == FirstRunGuideContent.CombatStep.ReloadThree);
        int ejectIndex = System.Array.FindIndex(
            steps,
            step => step.Step == FirstRunGuideContent.CombatStep.EjectChamber);
        int inspectIndex = System.Array.FindIndex(
            steps,
            step => step.Step
                == FirstRunGuideContent.CombatStep.InspectBulletInfo);

        Assert.That(ejectIndex, Is.EqualTo(reloadIndex + 1));
        Assert.That(inspectIndex, Is.EqualTo(ejectIndex + 1));
        Assert.That(
            steps[ejectIndex].TargetKind,
            Is.EqualTo(FirstRunGuideContent.TargetKind.Cylinder));
        Assert.That(steps[ejectIndex].Mission, Does.Contain("우클릭"));
    }

    [Test]
    public void CombatGuideExplainsDuelClockAndEightCountCombo()
    {
        FirstRunGuideContent.GuidePage[] pages =
            FirstRunGuideContent.CombatSystemPages;

        Assert.That(
            System.Array.Exists(
                pages,
                page => page.Title.Contains("DUEL CLOCK")
                    && page.Description.Contains("COUNT")),
            Is.True);
        Assert.That(
            System.Array.Exists(
                pages,
                page => page.Title.Contains("8 COUNT")
                    && page.Description.Contains("8 COUNT")),
            Is.True);
    }

    [Test]
    public void UpdatedGuideRequiresOneTimeProgressReset()
    {
        Assert.That(
            FirstRunGuideController.RequiresGuideProgressReset(0),
            Is.True);
        Assert.That(
            FirstRunGuideController.RequiresGuideProgressReset(1),
            Is.True);
        Assert.That(
            FirstRunGuideController.RequiresGuideProgressReset(2),
            Is.True);
        Assert.That(
            FirstRunGuideController.RequiresGuideProgressReset(3),
            Is.False);
    }

    [Test]
    public void GuideTooltipRendersAboveGuideAndBelowSystemOverlays()
    {
        Assert.That(
            FirstRunGuideController.GuideTooltipSortingOrder,
            Is.GreaterThan(FirstRunGuideController.GuideSortingOrder));
        Assert.That(
            FirstRunGuideController.GuideTooltipSortingOrder,
            Is.LessThan(short.MaxValue));
    }

    [Test]
    public void NodeMapGuideExplainsNodeTypesAndSelection()
    {
        FirstRunGuideContent.GuidePage[] pages =
            FirstRunGuideContent.NodeMapPages;

        Assert.That(pages, Has.Length.GreaterThanOrEqualTo(3));
        Assert.That(
            System.Array.Exists(
                pages,
                page => page.Description.Contains("시작")
                    && page.Description.Contains("전투")
                    && page.Description.Contains("정예 전투")
                    && page.Description.Contains("상점")
                    && page.Description.Contains("이벤트")
                    && page.Description.Contains("보물")
                    && page.Description.Contains("보스")),
            Is.True);
        Assert.That(
            System.Array.Exists(
                pages,
                page => page.Description.Contains("마우스 왼쪽 클릭")
                    && page.TargetKind
                        == FirstRunGuideContent.TargetKind.AvailableNode),
            Is.True);
    }

    [Test]
    public void EventAndTreasureGuidesExplainTheirCoreChoices()
    {
        FirstRunGuideContent.GuidePage[] eventPages =
            FirstRunGuideContent.EventPages;
        FirstRunGuideContent.GuidePage[] treasurePages =
            FirstRunGuideContent.TreasurePages;

        Assert.That(eventPages, Has.Length.GreaterThanOrEqualTo(2));
        Assert.That(
            System.Array.Exists(
                eventPages,
                page => page.Description.Contains("비용")
                    && page.Description.Contains("조건")
                    && page.Description.Contains("확률")),
            Is.True);
        Assert.That(treasurePages, Has.Length.GreaterThanOrEqualTo(2));
        Assert.That(
            System.Array.Exists(
                treasurePages,
                page => page.TargetName == "Button | Treasure Chest"),
            Is.True);
        Assert.That(
            System.Array.Exists(
                treasurePages,
                page => page.TargetName == "Panel | Relic Choices"
                    && page.Description.Contains("하나를 선택")),
            Is.True);
    }

    [Test]
    public void ShopPresentationUsesDedicatedTitleAndGuideCanvasAnchor()
    {
        Assert.That(
            StageProgressUI.ShopStageTitle,
            Is.EqualTo("마을. 상점"));
        Assert.That(
            FirstRunGuideController.IsGuideCanvasAnchor(
                "Shop",
                "Panel | Shop"),
            Is.True);
    }

    [TestCase(NodeMapNodeType.Shop)]
    [TestCase(NodeMapNodeType.Event)]
    [TestCase(NodeMapNodeType.Treasure)]
    public void PlaceGuideOnlyClassifiesFirstActiveNodeOfItsType(
        NodeMapNodeType nodeType)
    {
        NodeMapRunData map = new NodeMapRunData
        {
            currentNodeId = 0,
            activeNodeId = 1,
            awaitingNodeSelection = false
        };
        map.nodes.Add(new NodeMapNodeData
        {
            id = 0,
            type = NodeMapNodeType.Start
        });
        map.nodes.Add(new NodeMapNodeData
        {
            id = 1,
            type = nodeType
        });
        map.completedNodeIds.Add(0);

        Assert.That(
            FirstRunGuideController.IsFirstActiveNodeOfType(
                map,
                nodeType),
            Is.True);

        map.nodes.Add(new NodeMapNodeData
        {
            id = 2,
            type = nodeType
        });
        map.completedNodeIds.Add(2);

        Assert.That(
            FirstRunGuideController.IsFirstActiveNodeOfType(
                map,
                nodeType),
            Is.False);
    }

    [Test]
    public void FirstSelectedBattleNodeStartsCombatGuideRegardlessOfBattleIndex()
    {
        NodeMapRunData map = CreateFirstSelectedBattleMap(
            NodeMapNodeType.NormalBattle);

        Assert.That(
            FirstRunGuideController.IsFirstBattleNode(map),
            Is.True);

        Assert.That(
            FirstRunGuideController.IsFirstBattleNode(
                CreateFirstSelectedBattleMap(
                    NodeMapNodeType.EliteBattle)),
            Is.True);
        Assert.That(
            FirstRunGuideController.IsFirstBattleNode(
                CreateFirstSelectedBattleMap(
                    NodeMapNodeType.Boss)),
            Is.True);

        map.nodes[1].battleIndex = 2;

        Assert.That(
            FirstRunGuideController.IsFirstBattleNode(map),
            Is.True);
    }

    [Test]
    public void CompletedBattlePreventsFirstBattleGuideClassification()
    {
        NodeMapRunData map = CreateFirstSelectedBattleMap(
            NodeMapNodeType.NormalBattle);
        map.nodes.Add(new NodeMapNodeData
        {
            id = 2,
            type = NodeMapNodeType.NormalBattle
        });
        map.completedNodeIds.Add(2);

        Assert.That(
            FirstRunGuideController.IsFirstBattleNode(map),
            Is.False);
    }

    [Test]
    public void FreshNodeMapSelectionResolvesFirstReachableNode()
    {
        NodeMapRunData map = new NodeMapRunData
        {
            currentNodeId = 0,
            activeNodeId = -1,
            awaitingNodeSelection = true
        };
        map.nodes.Add(new NodeMapNodeData
        {
            id = 0,
            type = NodeMapNodeType.Start,
            nextNodeIds = new System.Collections.Generic.List<int> { 3, 4 }
        });
        map.nodes.Add(new NodeMapNodeData
        {
            id = 3,
            type = NodeMapNodeType.NormalBattle
        });
        map.completedNodeIds.Add(0);

        Assert.That(
            FirstRunGuideController.IsInitialNodeSelection(map),
            Is.True);
        Assert.That(
            FirstRunGuideController.ResolveFirstAvailableNodeId(map),
            Is.EqualTo(3));
    }

    private static NodeMapRunData CreateFirstSelectedBattleMap(
        NodeMapNodeType battleType)
    {
        NodeMapRunData map = new NodeMapRunData
        {
            currentNodeId = 0,
            activeNodeId = 1,
            awaitingNodeSelection = false
        };
        map.nodes.Add(new NodeMapNodeData
        {
            id = 0,
            type = NodeMapNodeType.Start,
            nextNodeIds = new System.Collections.Generic.List<int> { 1 }
        });
        map.nodes.Add(new NodeMapNodeData
        {
            id = 1,
            type = battleType,
            battleIndex = 1
        });
        map.completedNodeIds.Add(0);
        return map;
    }

    [Test]
    public void WaitCompletesExactlyOneTurn()
    {
        PlayerMove playerMove = gameObject.AddComponent<PlayerMove>();
        int completionCount = 0;
        PlayerBehaviourAction startedAction = default;
        playerMove.TurnCompleted += () => completionCount++;
        playerMove.BehaviourActionStarted += action =>
            startedAction = action;

        playerMove.Wait();

        Assert.That(playerMove.TurnCount, Is.EqualTo(1));
        Assert.That(completionCount, Is.EqualTo(1));
        Assert.That(startedAction, Is.EqualTo(PlayerBehaviourAction.Wait));
    }

    [Test]
    public void SuccessfulReloadCompletesExactlyOneTurn()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        PlayerMove playerMove = gameObject.AddComponent<PlayerMove>();
        PlayerShoot playerShoot = gameObject.AddComponent<PlayerShoot>();
        SerializedObject serializedShoot = new SerializedObject(playerShoot);
        serializedShoot.FindProperty("deckManager").objectReferenceValue =
            deckManager;
        serializedShoot.FindProperty("playerMove").objectReferenceValue =
            playerMove;
        serializedShoot.ApplyModifiedPropertiesWithoutUndo();
        int completionCount = 0;
        playerMove.TurnCompleted += () => completionCount++;

        playerShoot.Reload();

        Assert.That(deckManager.LoadedBullets, Has.Count.EqualTo(1));
        Assert.That(playerMove.TurnCount, Is.EqualTo(1));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    [Test]
    public void FailedReloadDoesNotCompleteTurn()
    {
        PlayerMove playerMove = gameObject.AddComponent<PlayerMove>();
        PlayerShoot playerShoot = gameObject.AddComponent<PlayerShoot>();
        SerializedObject serializedShoot = new SerializedObject(playerShoot);
        serializedShoot.FindProperty("deckManager").objectReferenceValue =
            deckManager;
        serializedShoot.FindProperty("playerMove").objectReferenceValue =
            playerMove;
        serializedShoot.ApplyModifiedPropertiesWithoutUndo();
        int completionCount = 0;
        playerMove.TurnCompleted += () => completionCount++;

        playerShoot.Reload();

        Assert.That(deckManager.LoadedBullets, Is.Empty);
        Assert.That(playerMove.TurnCount, Is.Zero);
        Assert.That(completionCount, Is.Zero);
    }

    [Test]
    public void EjectFromFullyLoadedDeckPreservesAdvertisedReloadOrder()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryReload(out _), Is.True);
        Assert.That(deckManager.TryReload(out BulletInstance next), Is.True);

        Assert.That(deckManager.TryEjectNextLoadedBullet(out _), Is.True);
        Assert.That(deckManager.PeekNextBullet(), Is.SameAs(next));
        Assert.That(deckManager.TryReload(out BulletInstance reloaded), Is.True);
        Assert.That(reloaded, Is.SameAs(next));
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(2));
    }

    [Test]
    public void EjectNextLoadedBulletFailsWithoutChangingEmptyCylinder()
    {
        Assert.That(
            deckManager.TryEjectNextLoadedBullet(out BulletInstance ejected),
            Is.False);
        Assert.That(ejected, Is.Null);
        Assert.That(deckManager.TotalBulletCount, Is.Zero);
    }

    [Test]
    public void DestroyedLastBulletRaisesDepletedAfterSequenceCompletes()
    {
        bool depleted = false;
        deckManager.BulletsDepleted += () => depleted = true;
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryReload(out _), Is.True);
        Assert.That(deckManager.TryFireLoadedBullet(out BulletInstance fired),
            Is.True);

        Assert.That(deckManager.TryDestroyBullet(fired), Is.True);
        Assert.That(depleted, Is.False);

        deckManager.CompleteFiringSequence();

        Assert.That(depleted, Is.True);
        Assert.That(deckManager.TotalBulletCount, Is.Zero);
        Assert.That(deckManager.PeekNextBullet(), Is.Null);
    }
}

public sealed class EnemyDamageNumberDisplayTests
{
    private readonly System.Collections.Generic.List<GameObject>
        createdObjects = new System.Collections.Generic.List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void FindAvailableOffset_SeparatesActiveDamageNumbers()
    {
        DamageNumberSpawnLayout layout = new DamageNumberSpawnLayout();
        Vector3 requestedOffset = new Vector3(0f, 0.75f, -1f);
        const float minimumSeparation = 0.65f;

        Vector3 firstOffset = layout.FindAvailableOffset(
            requestedOffset,
            minimumSeparation);
        layout.Track(firstOffset, CreateDamageNumber());

        Vector3 secondOffset = layout.FindAvailableOffset(
            requestedOffset,
            minimumSeparation);

        Assert.That(
            Vector2.Distance(firstOffset, secondOffset),
            Is.GreaterThanOrEqualTo(minimumSeparation));
    }

    [Test]
    public void FindAvailableOffset_ReusesSlotAfterDamageNumberIsDestroyed()
    {
        DamageNumberSpawnLayout layout = new DamageNumberSpawnLayout();
        Vector3 requestedOffset = new Vector3(0f, 0.75f, -1f);
        DamageNumbersPro.DamageNumber firstNumber = CreateDamageNumber();
        Vector3 firstOffset = layout.FindAvailableOffset(
            requestedOffset,
            0.65f);
        layout.Track(firstOffset, firstNumber);

        Object.DestroyImmediate(firstNumber.gameObject);
        Vector3 reusedOffset = layout.FindAvailableOffset(
            requestedOffset,
            0.65f);

        Assert.That(reusedOffset, Is.EqualTo(requestedOffset));
    }

    [Test]
    public void FindAvailableOffset_ZeroSeparationAddsNoOffset()
    {
        DamageNumberSpawnLayout layout = new DamageNumberSpawnLayout();
        Vector3 requestedOffset = new Vector3(0f, 0.75f, -1f);
        layout.Track(requestedOffset, CreateDamageNumber());

        Vector3 nextOffset = layout.FindAvailableOffset(
            requestedOffset,
            0f);

        Assert.That(nextOffset, Is.EqualTo(requestedOffset));
    }

    private DamageNumbersPro.DamageNumber CreateDamageNumber()
    {
        GameObject gameObject = new GameObject("Damage Number Test");
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<DamageNumbersPro.DamageNumberMesh>();
    }
}

public sealed class ComboFeedbackProgressionTests
{
    [Test]
    public void ComboExpiresAfterEightCountsWithoutAnotherDefeat()
    {
        int remainingCounts = 8;

        for (int count = 0; count < 8; count++)
        {
            remainingCounts = CombatFeedbackController
                .CalculateRemainingComboCounts(
                    remainingCounts,
                    false);
        }

        Assert.That(remainingCounts, Is.Zero);
    }

    [Test]
    public void DefeatDuringCountRefreshDoesNotConsumeThatCount()
    {
        int remainingCounts = CombatFeedbackController
            .CalculateRemainingComboCounts(8, true);

        Assert.That(remainingCounts, Is.EqualTo(8));
    }

    [Test]
    public void CountDisplayUsesCountTerminology()
    {
        Assert.That(TurnCountText.FormatCount(12), Is.EqualTo("COUNT 12"));
    }

    [Test]
    public void CumulativeCountSaturatesWithoutOverflow()
    {
        Assert.That(
            StateManager.CalculateCumulativeBattleCount(10, 3),
            Is.EqualTo(13));
        Assert.That(
            StateManager.CalculateCumulativeBattleCount(
                int.MaxValue,
                1),
            Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void FeedbackMultiplier_IncreasesForEveryFiringSequenceKill()
    {
        float first = CombatFeedbackController
            .CalculateFiringSequenceFeedbackMultiplier(1, 0.2f);
        float second = CombatFeedbackController
            .CalculateFiringSequenceFeedbackMultiplier(2, 0.2f);
        float third = CombatFeedbackController
            .CalculateFiringSequenceFeedbackMultiplier(3, 0.2f);

        Assert.That(first, Is.EqualTo(1f));
        Assert.That(second, Is.GreaterThan(first));
        Assert.That(third, Is.GreaterThan(second));
    }

    [Test]
    public void KillPitch_IncreasesForEveryFiringSequenceKill()
    {
        float first = SoundManager.CalculateFiringSequenceKillPitch(1);
        float second = SoundManager.CalculateFiringSequenceKillPitch(2);
        float third = SoundManager.CalculateFiringSequenceKillPitch(3);

        Assert.That(first, Is.EqualTo(1f));
        Assert.That(second, Is.GreaterThan(first));
        Assert.That(third, Is.GreaterThan(second));
    }

    [Test]
    public void DefeatPresentationTime_SpacesKillsRecordedTogether()
    {
        const float currentTime = 10f;
        const float interval = 0.18f;
        float first = CombatFeedbackController
            .CalculateDefeatPresentationTime(currentTime, 0f);
        float second = CombatFeedbackController
            .CalculateDefeatPresentationTime(
                currentTime,
                first + interval);
        float third = CombatFeedbackController
            .CalculateDefeatPresentationTime(
                currentTime,
                second + interval);

        Assert.That(first, Is.EqualTo(currentTime));
        Assert.That(second - first, Is.EqualTo(interval).Within(0.0001f));
        Assert.That(third - second, Is.EqualTo(interval).Within(0.0001f));
    }

    [Test]
    public void DefeatPresentationTime_PlaysImmediatelyAfterIdleGap()
    {
        float presentationTime = CombatFeedbackController
            .CalculateDefeatPresentationTime(12f, 10.18f);

        Assert.That(presentationTime, Is.EqualTo(12f));
    }
}

public sealed class CombatImpactTierUtilityTests
{
    [Test]
    public void Resolve_UsesSeventyFivePercentForDevastatingHit()
    {
        CombatImpactTier belowThreshold = CombatImpactTierUtility.Resolve(
            false,
            74,
            100,
            false);
        CombatImpactTier atThreshold = CombatImpactTierUtility.Resolve(
            false,
            75,
            100,
            false);

        Assert.That(belowThreshold, Is.EqualTo(CombatImpactTier.Normal));
        Assert.That(atThreshold, Is.EqualTo(CombatImpactTier.Devastating));
    }

    [Test]
    public void Resolve_DevastatingAndDefeatOverrideCritical()
    {
        CombatImpactTier critical = CombatImpactTierUtility.Resolve(
            true,
            74,
            100,
            false);
        CombatImpactTier devastating = CombatImpactTierUtility.Resolve(
            true,
            75,
            100,
            false);
        CombatImpactTier defeat = CombatImpactTierUtility.Resolve(
            false,
            10,
            100,
            true);

        Assert.That(critical, Is.EqualTo(CombatImpactTier.Critical));
        Assert.That(devastating, Is.EqualTo(CombatImpactTier.Devastating));
        Assert.That(defeat, Is.EqualTo(CombatImpactTier.Defeat));
    }
}

public sealed class CombatPresentationSignatureTests
{
    [Test]
    public void EnemySnapshot_PositionOnlyCaptureRemainsValid()
    {
        CombatPresentation.EnemySnapshot missingSnapshot = default;
        CombatPresentation.EnemySnapshot positionOnlySnapshot =
            new CombatPresentation.EnemySnapshot
            {
                Captured = true,
                Position = new Vector3(2f, 3f, 0f)
            };

        Assert.That(missingSnapshot.IsValid, Is.False);
        Assert.That(positionOnlySnapshot.IsValid, Is.True);
        Assert.That(positionOnlySnapshot.HasSprite, Is.False);
    }

    [Test]
    public void FindFirstAvailableFullscreenImpactSlot_ReturnsNoSlotWhenAllActive()
    {
        int availableSlot = CombatFeedbackController
            .FindFirstAvailableFullscreenImpactSlot(0b1011, 4);
        int fullResult = CombatFeedbackController
            .FindFirstAvailableFullscreenImpactSlot(0b1111, 4);

        Assert.That(availableSlot, Is.EqualTo(2));
        Assert.That(fullResult, Is.EqualTo(-1));
    }

    [Test]
    public void ImpactSignatureAnimation_ProtectsMinimumVisibleFrames()
    {
        const float duration = 0.1f;
        float elapsed = 0f;

        elapsed = CombatImpactSignaturePresenter.AdvanceAnimationTime(
            elapsed,
            duration,
            1f,
            1);

        Assert.That(elapsed, Is.LessThan(duration));
        Assert.That(
            CombatImpactSignaturePresenter.ShouldContinueAnimation(
                duration,
                duration,
                CombatImpactSignaturePresenter.MinimumVisibleFrameCount - 1),
            Is.True);
        Assert.That(
            CombatImpactSignaturePresenter.ShouldContinueAnimation(
                duration,
                duration,
                CombatImpactSignaturePresenter.MinimumVisibleFrameCount),
            Is.False);
    }

    [Test]
    public void ResolveImpactWaveColor_BlendsBothBulletLineColors()
    {
        Color waveColor = CombatPresentation.ResolveImpactWaveColor(
            Color.red,
            Color.blue);
        Color darkWaveColor = CombatPresentation.ResolveImpactWaveColor(
            Color.black,
            Color.black);

        Assert.That(waveColor.r, Is.GreaterThan(0f));
        Assert.That(waveColor.b, Is.GreaterThan(waveColor.r));
        Assert.That(waveColor.a, Is.EqualTo(1f));
        Assert.That(darkWaveColor.r, Is.GreaterThan(0f));
    }

    [Test]
    public void ResolveImpactSignature_GivesEachImpactTierDistinctBeats()
    {
        CombatPresentation.ImpactSignature normal = CombatPresentation
            .ResolveImpactSignature(CombatImpactTier.Normal, false);
        CombatPresentation.ImpactSignature critical = CombatPresentation
            .ResolveImpactSignature(CombatImpactTier.Critical, false);
        CombatPresentation.ImpactSignature devastating = CombatPresentation
            .ResolveImpactSignature(CombatImpactTier.Devastating, false);
        CombatPresentation.ImpactSignature defeat = CombatPresentation
            .ResolveImpactSignature(CombatImpactTier.Defeat, false);

        Assert.That(normal.UsesSnapAccent, Is.True);
        Assert.That(normal.UsesPrecisionLock, Is.False);
        Assert.That(normal.UsesCompressionBurst, Is.False);
        Assert.That(normal.UsesDefeatSilhouette, Is.False);
        Assert.That(critical.UsesSnapAccent, Is.False);
        Assert.That(critical.UsesPrecisionLock, Is.True);
        Assert.That(critical.UsesCompressionBurst, Is.False);
        Assert.That(devastating.UsesPrecisionLock, Is.False);
        Assert.That(devastating.UsesCompressionBurst, Is.True);
        Assert.That(devastating.UsesDefeatSilhouette, Is.False);
        Assert.That(defeat.UsesCompressionBurst, Is.True);
        Assert.That(defeat.UsesDefeatSilhouette, Is.True);
    }

    [Test]
    public void ResolveImpactSignature_ReservesExecutionSealForFinalDefeat()
    {
        CombatPresentation.ImpactSignature finalDevastating =
            CombatPresentation.ResolveImpactSignature(
                CombatImpactTier.Devastating,
                true);
        CombatPresentation.ImpactSignature regularDefeat =
            CombatPresentation.ResolveImpactSignature(
                CombatImpactTier.Defeat,
                false);
        CombatPresentation.ImpactSignature finalDefeat =
            CombatPresentation.ResolveImpactSignature(
                CombatImpactTier.Defeat,
                true);

        Assert.That(finalDevastating.UsesFinalExecutionSeal, Is.False);
        Assert.That(regularDefeat.UsesFinalExecutionSeal, Is.False);
        Assert.That(finalDefeat.UsesFinalExecutionSeal, Is.True);
    }
}
