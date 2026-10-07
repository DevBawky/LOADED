using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Test setup orchestration; combat state remains in its normal owners.</summary>
[DefaultExecutionOrder(-50)]
public sealed class BattleTestController : MonoBehaviour
{
    [SerializeField] private BoardManager board;
    [SerializeField] private WaveManager waves;
    [SerializeField] private DeckManager deck;
    [SerializeField] private RelicManager relics;
    [SerializeField] private PlayerMove player;
    [SerializeField] private PlayerShoot shooting;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private CurrencyManager currency;
    [SerializeField] private RewardManager rewards;
    [SerializeField] private CombatFeedbackController feedback;
    [SerializeField] private StateManager state;
    [SerializeField] private BattleWorld3DController world;
    [SerializeField] private GameObject mainGamePanel;
    [SerializeField] private BattleData environment;
    [SerializeField] private Vector3 playerOffset;
    [SerializeField] private BulletData[] bulletCatalog = Array.Empty<BulletData>();
    [SerializeField] private EnemyData[] enemyCatalog = Array.Empty<EnemyData>();
    [SerializeField] private RelicData[] relicCatalog = Array.Empty<RelicData>();
    [SerializeField] private ItemData[] itemCatalog = Array.Empty<ItemData>();

    private BattleTestCommandRouter commands;
    private RunSaveData checkpoint;
    private int checkpointColumns;
    private int checkpointLanes;
    private bool checkpointAutomatic;
    private bool checkpointInvulnerable;
    private bool checkpointEmergencyReload;
    private bool checkpointEnemyAutoRefill;
    private int checkpointEnemyRefillPercent;
    private Dictionary<EnemyData, bool> checkpointEnemySpawnAllowed;
    private readonly Dictionary<EnemyData, bool> enemySpawnAllowed = new Dictionary<EnemyData, bool>();
    private bool enemyAutoRefill;
    private int enemyRefillPercent = 50;
    private bool enemyRefillPending;
    private bool initialized;
    private long totalDamage;
    private int kills;

    public IReadOnlyList<BulletData> Bullets => bulletCatalog;
    public IReadOnlyList<EnemyData> Enemies => enemyCatalog;
    public IReadOnlyList<RelicData> Relics => relicCatalog;
    public IReadOnlyList<ItemData> Items => itemCatalog;
    internal DeckManager TestDeck => deck;
    internal RelicManager TestRelics => relics;
    internal WaveManager TestWaves => waves;
    internal PlayerInventory TestInventory => inventory;
    internal BoardManager TestBoard => board;
    internal PlayerHealth TestHealth => health;
    internal PlayerMove TestPlayer => player;
    internal CurrencyManager TestCurrency => currency;
    public bool IsReady => initialized;
    public bool IsSettled => initialized && !player.IsActing && !shooting.IsFiring
        && !waves.IsResolvingTurn && !player.IsEnemyTurnResolving;
    public bool AutomaticTurns => waves.TestAutomaticTurns;
    internal bool EnemyAutoRefill => enemyAutoRefill;
    internal int EnemyRefillPercent => enemyRefillPercent;
    internal string EnemyRefillSummary => !enemySpawnAllowed.ContainsValue(true)
        ? "자동 스폰 대상 없음 · 적 도감에서 켜 주세요"
        : $"전체 {board.TotalTileCount}칸 · {enemyRefillPercent}% → 최대 "
        + $"{CalculateEnemyRefillCount(board.TotalTileCount, enemyRefillPercent, board.TotalTileCount - 1)}마리";
    public string Summary => !initialized ? "전투 테스트를 준비하고 있습니다." :
        $"{board.LaneCount}레인 × {board.BoardCount}칸 | 체력 {health.CurrentHealth}/{health.MaxHealth}"
        + $" | 골드 {currency.CurrentMoney} | 적 {waves.ActiveEnemies.Count}"
        + $" | 적 행동 {(AutomaticTurns ? "자동" : "수동")} | 무적 {(BattleTestContext.Invulnerable ? "켜짐" : "꺼짐")}"
        + $"\n누적 피해 {totalDamage:N0} | 처치 {kills} | "
        + (IsSettled ? "조절 가능" : "행동 진행 중: 창을 닫고 행동이 끝날 때까지 기다려 주세요");

    private void Awake()
    {
        if (!BattleTestContext.IsActive)
        {
            enabled = false;
            return;
        }
        commands = new BattleTestCommandRouter(this);
        BattleTestContext.Reset();
    }

    private void Start()
    {
        if (!enabled) return;
        mainGamePanel.SetActive(true);
        world?.ApplyProfile(environment.EnvironmentProfile);
        board.ConfigureBoard(board.BoardCount, board.LaneCount, environment.TilePrefab);
        PlacePlayer(board.BoardCount / 2, 0, true, 0, 0);
        if (!waves.BeginTestBattle(environment))
        {
            Debug.LogError("전투 테스트 초기화에 필요한 Battle 참조를 확인해 주세요.", this);
            return;
        }
        relics.BindPlayerMove(player);
        relics.BeginBattle();
        state.ConfigureExternalSceneState(0, 0, GameFlowState.Battle);
        player.SetInputLocked(false);
        shooting.DamageDealt += HandleDamage;
        waves.EnemyDefeated += HandleDefeat;
        waves.StateChanged += HandleEnemyStateChanged;
        foreach (EnemyData enemy in enemyCatalog)
            if (enemy != null) enemySpawnAllowed[enemy] = enemy.BehaviorType != EnemyBehaviorType.BigBarrel;
        initialized = true;
        CaptureCheckpoint();
    }

    private void OnDestroy()
    {
        if (shooting != null) shooting.DamageDealt -= HandleDamage;
        if (waves != null) waves.EnemyDefeated -= HandleDefeat;
        if (waves != null) waves.StateChanged -= HandleEnemyStateChanged;
        BattleTestContext.Reset();
    }

    private void HandleDamage(int amount) => totalDamage += Mathf.Max(0, amount);
    private void HandleDefeat(EnemyController _) => kills++;

    private void HandleEnemyStateChanged()
    {
        enemyRefillPending = enemyAutoRefill && waves.IsTestBattle && waves.ActiveEnemies.Count == 0;
    }

    private void LateUpdate()
    {
        // Do not let new targets enter the shot, chain effect or enemy cycle that cleared the board.
        if (!enemyRefillPending || !BattleTestContext.IsActive || !IsSettled
            || health.IsDefeated || !waves.IsTestBattle) return;
        enemyRefillPending = false;
        if (!enemyAutoRefill || enemyRefillPercent == 0 || waves.ActiveEnemies.Count != 0) return;
        List<Vector2Int> cells = GetEmptyEnemyCells();
        SpawnRandomEnemies(cells, CalculateEnemyRefillCount(board.TotalTileCount, enemyRefillPercent, cells.Count));
    }

    internal static int CalculateEnemyRefillCount(int totalTiles, int percent, int availableCells)
    {
        int desired = (Mathf.Max(0, totalTiles) * Mathf.Clamp(percent, 0, 100) + 99) / 100;
        return Mathf.Min(desired, Mathf.Max(0, availableCells));
    }

    internal string SetEnemyRefill(bool enabled, int percent)
    {
        RequireIdle();
        if (percent < 0 || percent > 100) throw new ArgumentException("적 점유율은 0~100%로 입력해 주세요.");
        enemyAutoRefill = enabled;
        enemyRefillPercent = percent;
        HandleEnemyStateChanged();
        return $"전멸 시 자동 보충: {(enabled ? "켜짐" : "꺼짐")} · {EnemyRefillSummary}";
    }

    internal bool IsEnemySpawnAllowed(EnemyData enemy) => enemy != null
        && enemySpawnAllowed.TryGetValue(enemy, out bool allowed) && allowed;

    internal string SetEnemySpawnAllowed(EnemyData enemy, bool allowed)
    {
        RequireIdle();
        if (enemy == null || !enemySpawnAllowed.ContainsKey(enemy))
            throw new ArgumentException("적 도감에 없는 항목입니다.");
        enemySpawnAllowed[enemy] = allowed;
        HandleEnemyStateChanged();
        return $"{enemy.DisplayName} 자동 스폰: {(allowed ? "켜짐" : "꺼짐")} · 자동 보충과 무작위 생성에 적용됩니다.";
    }

    internal string DescribeEnemySpawnPool() => string.Join("\n", enemyCatalog.Select((enemy, index) =>
        $"[{index}] {enemy.DisplayName} · 자동 스폰: {(IsEnemySpawnAllowed(enemy) ? "켜짐" : "꺼짐")}"));

    public string ExecuteCommand(string command)
    {
        if (!initialized) return "전투 테스트를 준비하고 있습니다.";
        return commands.Execute(command);
    }

    internal void RequireIdle()
    {
        if (!BattleTestContext.IsActive || !IsSettled)
            throw new InvalidOperationException("행동이 진행 중입니다. F1로 창을 닫고 행동이 끝난 뒤 다시 시도해 주세요.");
        shooting.ClearLoadedBulletDamagePreview();
        player.ClearBufferedInput();
    }

    internal string Resize(int columns, int lanes)
    {
        RequireIdle();
        if (columns < 2 || columns > 30 || lanes < 1 || lanes > 6)
            throw new ArgumentException("레인당 칸 수는 2~30, 레인 수는 1~6으로 입력해 주세요.");
        waves.StopBattle();
        rewards.RestoreRunState(null, null);
        board.ConfigureBoard(columns, lanes, environment.TilePrefab);
        PlacePlayer(columns / 2, 0, true, 0, 0);
        waves.BeginTestBattle(environment);
        shooting.RestoreTestShotState(false);
        feedback.ResetCombo();
        return "보드를 다시 만들었습니다. 적·폭탄·드롭은 제거되며 덱과 소지품은 유지됩니다.";
    }

    private void PlacePlayer(int tile, int lane, bool facingRight, int turns, int nextPush)
    {
        if (!board.TryGetTilePosition(tile, lane, out Vector3 position))
            throw new ArgumentException("플레이어 위치가 보드 범위를 벗어납니다.");
        player.RestoreRunState(position + playerOffset, facingRight, turns, nextPush, lane);
    }

    internal string MovePlayer(int tile, int lane)
    {
        RequireIdle();
        if (waves.IsTileOccupied(tile, lane))
            throw new ArgumentException("해당 칸에 적이 있습니다.");
        PlacePlayer(tile, lane, player.transform.localScale.x >= 0,
            player.TurnCount, player.NextPushAvailableTurn);
        return "플레이어를 이동했습니다. 행동 횟수와 이동 효과에는 영향을 주지 않습니다.";
    }

    internal string SetHealth(int current, int maximum)
    {
        RequireIdle();
        if (maximum < 1 || maximum > 1000000 || current < 0 || current > maximum)
            throw new ArgumentException("현재 체력은 0~최대 체력, 최대 체력은 1~1,000,000으로 입력해 주세요.");
        health.SetTestHealth(current, maximum);
        return $"플레이어 체력: {health.CurrentHealth}/{health.MaxHealth}";
    }

    internal string SetMoney(int amount)
    {
        RequireIdle();
        if (amount < 0 || amount > 1000000000)
            throw new ArgumentException("골드는 0~1,000,000,000으로 입력해 주세요.");
        currency.RestoreRunMoney(amount);
        return $"골드를 {amount}로 설정했습니다. 골드 획득 유물 효과는 발동하지 않습니다.";
    }

    internal string AddBullet(BulletData data, int level, bool replace)
    {
        RequireIdle();
        ValidateLevel(level);
        if (replace)
        {
            var saved = new RunBulletSaveData
            {
                assetName = data.name, bulletId = data.BulletId, level = level,
                acquisitionOrder = 0, location = 0, locationIndex = 0
            };
            deck.RestoreRunState(new[] { saved }, ResolveBullet, 0, null);
            shooting.RestoreTestShotState(false);
        }
        else if (!deck.TryAddBullet(data, level))
            throw new InvalidOperationException("덱이 가득 찼습니다. 최대 20발까지 보유할 수 있습니다.");
        return $"{data.GetDisplayName(level)} 추가 완료. 보유: {deck.TotalBulletCount}/20";
    }

    internal string SetBulletLevel(int order, int level)
    {
        RequireIdle();
        ValidateLevel(level);
        var saved = new List<RunBulletSaveData>();
        var cycle = new List<int>();
        deck.CaptureRunState(saved, cycle);
        RunBulletSaveData bullet = saved.Find(value => value.acquisitionOrder == order);
        if (bullet == null) throw new ArgumentException("보유 탄환을 찾을 수 없습니다. 내 덱 목록을 확인해 주세요.");
        bullet.level = level;
        deck.RestoreRunState(saved, ResolveBullet, deck.PaidBulletRemovalCount, cycle);
        return $"보유 탄환 #{order}의 강화 수치를 +{level}로 설정했습니다.";
    }

    internal string RemoveBullet(int order)
    {
        RequireIdle();
        if (!deck.TryRemoveBullet(deck.FindByAcquisitionOrder(order)))
            throw new ArgumentException("탄환을 찾을 수 없거나 마지막 1발입니다. 덱에는 최소 1발을 남겨야 합니다.");
        return "탄환을 제거했습니다.";
    }

    internal string LoadBullet(int order)
    {
        RequireIdle();
        BulletInstance bullet = deck.FindByAcquisitionOrder(order);
        if (bullet == null || deck.LoadedBullets.Contains(bullet))
            throw new ArgumentException("탄환을 찾을 수 없거나 이미 장전되어 있습니다.");
        if (deck.LoadedBullets.Count >= deck.MaxReloadAmount)
            throw new InvalidOperationException("실린더가 가득 찼습니다.");
        deck.QueueBulletForNextReload(order);
        deck.TryReload();
        return "행동 소모 없이 장전했습니다. 마지막에 장전한 탄환부터 발사합니다.";
    }

    internal string FillCylinder()
    {
        RequireIdle();
        int loaded = 0;
        while (loaded < deck.MaxReloadAmount && deck.TryReload()) loaded++;
        return $"행동 소모 없이 {loaded}발을 장전했습니다.";
    }

    internal string AddRelic(RelicData data)
    {
        RequireIdle();
        return relics.TryAcquire(data) switch
        {
            RelicAcquireResult.Acquired => "유물을 추가했습니다.",
            RelicAcquireResult.Stacked => "유물 중첩을 추가했습니다.",
            _ => BattleTestCommandRouter.RejectedPrefix + "유물을 추가할 수 없습니다. 중복 보유 조건을 확인해 주세요."
        };
    }

    internal string RemoveRelic(int index)
    {
        RequireIdle();
        if (!relics.TryRemoveAt(index)) throw new ArgumentException("보유 유물을 찾을 수 없습니다. 유물 목록을 확인해 주세요.");
        return "유물을 제거했습니다.";
    }

    internal string SetItem(int slot, ItemData data)
    {
        RequireIdle();
        if (slot < 0 || slot >= inventory.SlotCount) throw new ArgumentException("아이템 슬롯은 0~2로 입력해 주세요.");
        var names = new List<string>();
        inventory.CaptureRunState(names);
        names[slot] = data == null ? string.Empty : data.name;
        inventory.RestoreRunState(names, ResolveItem);
        return $"아이템 슬롯 {slot}: {(data == null ? "비어 있음" : data.DisplayName)}";
    }

    internal string Spawn(EnemyData data, int tile, int lane)
    {
        RequireIdle();
        if (!waves.TrySpawnTestEnemy(data, tile, lane, out EnemyController enemy))
            throw new ArgumentException("생성할 수 없습니다. 좌표가 잘못되었거나 해당 칸이 사용 중입니다.");
        return $"{lane}번 레인 {tile}번 칸에 {enemy.Data.DisplayName}을 생성했습니다.";
    }

    internal string SpawnRandom(int count)
    {
        RequireIdle();
        if (count < 1 || count > 100) throw new ArgumentException("무작위 생성 수는 1~100으로 입력해 주세요.");
        if (!enemySpawnAllowed.ContainsValue(true))
            throw new InvalidOperationException("자동 스폰 대상이 없습니다. 적 도감에서 원하는 적의 자동 스폰을 켜 주세요.");
        List<Vector2Int> cells = GetEmptyEnemyCells();
        if (count > cells.Count) throw new ArgumentException($"빈칸은 {cells.Count}개입니다.");
        int spawned = SpawnRandomEnemies(cells, count);
        return $"서로 다른 빈칸에 무작위 적 {spawned}마리를 생성했습니다. (요청 {count}마리)";
    }

    private List<Vector2Int> GetEmptyEnemyCells()
    {
        var cells = new List<Vector2Int>();
        board.TryGetTileIndex(player.transform.position, player.CurrentLaneIndex, out int playerTile);
        for (int lane = 0; lane < board.LaneCount; lane++)
            for (int tile = 0; tile < board.BoardCount; tile++)
                if (!waves.IsTileOccupied(tile, lane)
                    && !waves.IsTileReservedForMovement(tile, lane)
                    && (waves.BombManager == null || !waves.BombManager.HasBombAtTile(tile, lane))
                    && !(tile == playerTile && lane == player.CurrentLaneIndex))
                    cells.Add(new Vector2Int(tile, lane));
        return cells;
    }

    private int SpawnRandomEnemies(List<Vector2Int> cells, int count)
    {
        // Filter before rolling so excluded entries never consume a spawn attempt or RNG draw.
        EnemyData[] candidates = enemyCatalog.Where(IsEnemySpawnAllowed).ToArray();
        if (candidates.Length == 0) return 0;
        int spawned = 0;
        while (spawned < count && cells.Count > 0)
        {
            int choice = UnityEngine.Random.Range(0, cells.Count);
            Vector2Int cell = cells[choice];
            cells.RemoveAt(choice);
            EnemyData data = candidates[UnityEngine.Random.Range(0, candidates.Length)];
            if (waves.TrySpawnTestEnemy(data, cell.x, cell.y, out _)) spawned++;
        }
        return spawned;
    }

    internal EnemyController EnemyAt(int tile, int lane)
    {
        if (!waves.TryGetEnemyAtTile(tile, lane, out EnemyController enemy))
            throw new ArgumentException("해당 좌표에 적이 없습니다.");
        return enemy;
    }

    internal string ChangeEnemy(int tile, int lane, string operation, int value = 0)
    {
        RequireIdle();
        EnemyController enemy = EnemyAt(tile, lane);
        if (operation == "remove")
            return waves.TryRemoveTestEnemy(enemy) ? "보상 없이 적을 제거했습니다." : "적을 제거할 수 없습니다.";
        if (operation == "kill")
        {
            if (enemy.CurrentShield > 0) enemy.ApplyEnvironmentalDamage(enemy.CurrentShield);
            enemy.ApplyEnvironmentalDamage(enemy.CurrentHealth);
            return "정상 사망·보상 처리를 거쳐 적을 처치했습니다.";
        }
        if (operation == "damage")
        {
            if (value < 1) throw new ArgumentException("피해는 1 이상으로 입력해 주세요.");
            enemy.ApplyAttackDamage(value);
            return "보호막과 피격 효과를 반영하여 피해를 적용했습니다.";
        }
        RunEnemySaveData saved = enemy.CaptureRunState(waves.ActiveEnemies);
        if (operation == "hp")
        {
            if (value < 1 || value > enemy.MaxHealth)
                throw new ArgumentException($"적 체력은 1~{enemy.MaxHealth}로 입력해 주세요. 처치 버튼으로 사망시킬 수 있습니다.");
            saved.currentHealth = value;
        }
        else if (operation == "shield")
        {
            if (value < 0 || value > 1000000) throw new ArgumentException("보호막은 0~1,000,000으로 입력해 주세요.");
            saved.currentShield = value;
        }
        else throw new ArgumentException("알 수 없는 적 조작입니다.");
        int support = saved.preparedSupportTargetIndex;
        enemy.RestoreRunState(saved, support >= 0 && support < waves.ActiveEnemies.Count
            ? waves.ActiveEnemies[support] : null);
        return "적 수치를 변경했습니다. 준비한 공격은 유지됩니다.";
    }

    internal string SetStatus(int tile, int lane, StatusEffectType type, int stacks)
    {
        RequireIdle();
        if (stacks < 0 || stacks > 1000000 || type == StatusEffectType.Exposed && stacks > 1)
            throw new ArgumentException("중첩은 0~1,000,000으로 입력해 주세요. 무방비는 0 또는 1만 가능합니다.");
        StatusEffectController target = EnemyAt(tile, lane).GetComponent<StatusEffectController>();
        RunStatusEffectSaveData saved = target.CaptureRunState();
        switch (type)
        {
            case StatusEffectType.Mark: saved.markStacks = stacks; break;
            case StatusEffectType.Poison: saved.poisonStacks = stacks; saved.poisonCreditedToPlayer = true; break;
            case StatusEffectType.Stun: saved.stunStacks = stacks; break;
            case StatusEffectType.Weakness: saved.weaknessStacks = stacks; break;
            case StatusEffectType.Exposed: saved.isExposed = stacks > 0; break;
        }
        target.RestoreRunState(saved);
        return $"{BattleTestCommandRouter.StatusName(type)} 중첩을 {stacks}로 설정했습니다.";
    }

    internal string ClearEnemies()
    {
        RequireIdle();
        foreach (EnemyController enemy in waves.ActiveEnemies.ToArray()) waves.TryRemoveTestEnemy(enemy);
        waves.BombManager.ClearAll();
        rewards.RestoreRunState(null, null);
        return "보상 없이 모든 적·폭탄·드롭을 제거했습니다.";
    }

    internal string SetAutomatic(bool value)
    {
        RequireIdle();
        waves.TestAutomaticTurns = value;
        return value ? "적 행동을 자동으로 진행합니다. 행동 기반 전투를 사용합니다." : "적 행동을 수동으로 진행합니다. F3으로 한 사이클씩 진행할 수 있습니다.";
    }

    internal string Step()
    {
        RequireIdle();
        if (!waves.TryStepTestBattle()) throw new InvalidOperationException("진행할 수 없습니다. 플레이어가 사망했다면 체력을 먼저 회복해 주세요.");
        return "적 행동 한 사이클을 예약했습니다. 창을 닫으면 진행합니다.";
    }

    internal string CaptureCheckpoint()
    {
        RequireIdle();
        if (deck.TotalBulletCount == 0) throw new InvalidOperationException("탄환을 1발 이상 추가한 뒤 테스트 상태를 저장해 주세요.");
        var saved = new RunSaveData
        {
            currentHealth = health.CurrentHealth, maxHealth = health.MaxHealth,
            money = currency.CurrentMoney, playerLaneIndex = player.CurrentLaneIndex,
            playerFacingRight = player.transform.localScale.x >= 0,
            playerTurnCount = player.TurnCount, nextPushAvailableTurn = player.NextPushAvailableTurn,
            playerStatusEffects = health.CaptureStatusRunState(),
            randomStateJson = JsonUtility.ToJson(UnityEngine.Random.state)
        };
        board.TryGetTileIndex(player.transform.position, player.CurrentLaneIndex, out saved.playerTileIndex);
        deck.CaptureRunState(saved.bullets, saved.nextCycleAcquisitionOrders);
        relics.CaptureRunState(saved.relics);
        inventory.CaptureRunState(saved.inventoryItemAssetNames);
        waves.CaptureRunState(saved);
        feedback.CaptureRunState(saved);
        checkpoint = saved;
        checkpointColumns = board.BoardCount;
        checkpointLanes = board.LaneCount;
        checkpointAutomatic = AutomaticTurns;
        checkpointInvulnerable = BattleTestContext.Invulnerable;
        checkpointEmergencyReload = shooting.TestPendingEmergencyReload;
        checkpointEnemyAutoRefill = enemyAutoRefill;
        checkpointEnemyRefillPercent = enemyRefillPercent;
        checkpointEnemySpawnAllowed = new Dictionary<EnemyData, bool>(enemySpawnAllowed);
        return "테스트 상태를 저장했습니다. F5로 복원할 수 있으며 Play 종료 시 사라집니다.";
    }

    internal string RestoreCheckpoint()
    {
        RequireIdle();
        if (checkpoint == null) throw new InvalidOperationException("저장된 테스트 상태가 없습니다.");
        RunSaveData saved = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(checkpoint));
        waves.StopBattle();
        rewards.RestoreRunState(null, null);
        board.ConfigureBoard(checkpointColumns, checkpointLanes, environment.TilePrefab);
        PlacePlayer(saved.playerTileIndex, saved.playerLaneIndex, saved.playerFacingRight,
            saved.playerTurnCount, saved.nextPushAvailableTurn);
        health.SetTestHealth(saved.currentHealth, saved.maxHealth);
        health.RestoreStatusRunState(saved.playerStatusEffects);
        deck.RestoreRunState(saved.bullets, ResolveBullet, 0, saved.nextCycleAcquisitionOrders);
        relics.RestoreRunState(saved.relics, id => relicCatalog.FirstOrDefault(value => value.Id == id));
        relics.ResumeBattle();
        inventory.RestoreRunState(saved.inventoryItemAssetNames, ResolveItem);
        currency.RestoreRunMoney(saved.money);
        if (!waves.RestoreTestBattle(environment, saved,
                name => enemyCatalog.FirstOrDefault(value => value.name == name)))
            throw new InvalidOperationException("저장된 적 상태를 복원하지 못했습니다.");
        // Fresh battle initialization resets kick cooldown; restore the saved player counters afterward.
        PlacePlayer(saved.playerTileIndex, saved.playerLaneIndex, saved.playerFacingRight,
            saved.playerTurnCount, saved.nextPushAvailableTurn);
        waves.TestAutomaticTurns = checkpointAutomatic;
        BattleTestContext.Invulnerable = checkpointInvulnerable;
        shooting.RestoreTestShotState(checkpointEmergencyReload);
        enemyAutoRefill = checkpointEnemyAutoRefill;
        enemyRefillPercent = checkpointEnemyRefillPercent;
        enemySpawnAllowed.Clear();
        foreach (var entry in checkpointEnemySpawnAllowed) enemySpawnAllowed.Add(entry.Key, entry.Value);
        HandleEnemyStateChanged();
        feedback.RestoreRunState(saved);
        UnityEngine.Random.state = JsonUtility.FromJson<UnityEngine.Random.State>(saved.randomStateJson);
        totalDamage = 0;
        kills = 0;
        return "테스트 상태를 복원했습니다. 누적 피해·처치 횟수와 드롭은 초기화했습니다.";
    }

    internal string DescribeOwned()
    {
        var lines = new List<string>();
        var bullets = new List<BulletInstance>();
        deck.GetOwnedBullets(bullets);
        foreach (BulletInstance bullet in bullets.OrderBy(value => value.AcquisitionOrder))
        {
            string location = deck.LoadedBullets.Contains(bullet) ? "장전됨"
                : deck.Graveyard.Contains(bullet) ? "사용 후 대기" : "덱";
            lines.Add($"탄환 #{bullet.AcquisitionOrder}: {bullet.Data.GetDisplayName(bullet.Level)} [{location}]");
        }
        lines.Add("발사 순서: " + string.Join(" -> ", deck.LoadedBullets.Reverse().Select(value => "#" + value.AcquisitionOrder)));
        for (int i = 0; i < relics.Count; i++) lines.Add($"유물 [{i}]: {relics.OwnedRelics[i].Data.DisplayName}");
        for (int i = 0; i < inventory.SlotCount; i++) lines.Add($"아이템 [{i}]: {inventory.GetItem(i)?.DisplayName ?? "비어 있음"}");
        return string.Join("\n", lines);
    }

    internal string DescribeEnemies()
    {
        return string.Join("\n", waves.ActiveEnemies.Select(enemy =>
        {
            RunEnemySaveData saved = enemy.CaptureRunState(waves.ActiveEnemies);
            return $"({saved.tileIndex},{saved.laneIndex}) {enemy.Data.DisplayName} 체력 {enemy.CurrentHealth}/{enemy.MaxHealth}"
                + $" 보호막 {saved.currentShield} | 공격 준비: {(saved.isAttackPrepared ? "완료" : "대기")}"
                + $" | 표식 {saved.statusEffects.markStacks} 독 {saved.statusEffects.poisonStacks}"
                + $" 기절 {saved.statusEffects.stunStacks} 약화 {saved.statusEffects.weaknessStacks}";
        }));
    }

    private BulletData ResolveBullet(RunBulletSaveData saved)
    {
        // Legacy assets can share an ID; an exact authored name must win over that fallback.
        BulletData exact = bulletCatalog.FirstOrDefault(data => data.name == saved.assetName);
        if (exact != null) return exact;
        if (string.IsNullOrEmpty(saved.bulletId)) return null;
        BulletData match = null;
        foreach (BulletData data in bulletCatalog)
        {
            if (data.BulletId != saved.bulletId) continue;
            if (match != null) return null;
            match = data;
        }
        return match;
    }
    private ItemData ResolveItem(string name) => itemCatalog.FirstOrDefault(data => data.name == name);
    private static void ValidateLevel(int level)
    {
        if (level < 0 || level > BulletData.MaximumUpgradeLevel)
            throw new ArgumentException($"강화 수치는 0~{BulletData.MaximumUpgradeLevel}로 입력해 주세요.");
    }
}
