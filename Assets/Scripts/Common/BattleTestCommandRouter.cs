using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

internal sealed class BattleTestCommandRouter
{
    internal const string RejectedPrefix = "변경 불가: ";
    internal const string Help =
        "GUI 탭에서 버튼으로 조절할 수 있습니다. 아래 명령도 그대로 사용할 수 있습니다.\n"
        + "번호와 좌표는 0부터 시작합니다. 공백이 있는 이름은 큰따옴표로 감싸 주세요.\n"
        + "list bullet|enemy|relic|item [검색어] : 탄환·적·유물·아이템 검색\n"
        + "owned / enemies : 보유 목록 / 적 상태 조회\n"
        + "board <레인당 칸 수> [레인 수] : 빈 보드 생성 (2~30칸, 1~6레인)\n"
        + "bullet add <탄환> [강화 0~3] : 덱에 추가\n"
        + "deck set <탄환> [강화 0~3] : 덱 전체를 지정 탄환 1발로 교체\n"
        + "bullet level <보유 번호> <강화 수치> / bullet remove <보유 번호>\n"
        + "relic add <유물> / relic remove <보유 번호> : 추가 / 제거\n"
        + "spawn <적> <칸> <레인> / random <수> : 지정 생성 / 무작위 생성\n"
        + "refill on|off [점유율 0~100] / refill percent <점유율> : 전멸 시 자동 보충\n"
        + "spawnpool [<적> on|off] : 자동 스폰 대상 목록 조회 / 허용 설정\n"
        + "enemy kill|remove <칸> <레인> : 처치 / 보상 없이 제거\n"
        + "enemy hp|shield|damage <칸> <레인> <수치> : 체력·보호막·피해\n"
        + "status <칸> <레인> <표식|독|기절|약화|무방비> <중첩>\n"
        + "clear : 적·폭탄·드롭 모두 제거\n"
        + "player <칸> <레인> / hp <현재 체력> [최대 체력=100]\n"
        + "gold <수치> / gold random [최소] [최대] : 골드 설정\n"
        + "item set <슬롯> <아이템> / item remove <슬롯> / item random\n"
        + "ai on|off / step / god on|off : 자동 행동 / 한 사이클 / 무적\n"
        + "seed <정수> : 무작위 수 초기화 (동일 재현을 보장하지 않음)\n"
        + "checkpoint / reset : 테스트 상태 저장 / 복원\n"
        + "help / close : 도움말 / 전투로 돌아가기";

    internal static string StatusName(StatusEffectType type) => type switch
    {
        StatusEffectType.Mark => "표식", StatusEffectType.Poison => "독",
        StatusEffectType.Stun => "기절", StatusEffectType.Weakness => "약화",
        StatusEffectType.Exposed => "무방비", _ => "상태이상"
    };

    private readonly BattleTestController target;
    internal BattleTestCommandRouter(BattleTestController target) => this.target = target;

    internal string Execute(string command)
    {
        try
        {
            string[] args = Tokenize(command);
            if (args.Length == 0) return string.Empty;
            switch (args[0].ToLowerInvariant())
            {
                case "도움말":
                case "help": Count(args, 1, 1); return Help;
                case "owned": Count(args, 1, 1); return target.DescribeOwned();
                case "enemies": Count(args, 1, 1); return target.DescribeEnemies();
                case "list": return ListCatalog(args);
                case "board": Count(args, 2, 3); return target.Resize(Number(args[1]), args.Length == 3 ? Number(args[2]) : 2);
                case "bullet": return Bullet(args);
                case "deck":
                    Count(args, 3, 4);
                    if (args[1] != "set") throw new ArgumentException("덱 교체: deck set <탄환> [강화 수치]");
                    return target.AddBullet(Find(target.Bullets, args[2], value => value.GetDisplayName(0)),
                        args.Length == 4 ? Number(args[3]) : 0, true);
                case "load": Count(args, 2, 2); return target.LoadBullet(Number(args[1]));
                case "fill": Count(args, 1, 1); return target.FillCylinder();
                case "relic":
                    Count(args, 3, 3);
                    if (args[1] == "add") return target.AddRelic(Find(target.Relics, args[2], value => value.DisplayName));
                    if (args[1] == "remove") return target.RemoveRelic(Number(args[2]));
                    throw new ArgumentException("유물 추가: relic add <도감 번호>, 제거: relic remove <보유 번호>");
                case "spawn":
                    Count(args, 4, 4);
                    return target.Spawn(Find(target.Enemies, args[1], value => value.DisplayName), Number(args[2]), Number(args[3]));
                case "random": Count(args, 2, 2); return target.SpawnRandom(Number(args[1]));
                case "spawnpool":
                    if (args.Length == 1) return target.DescribeEnemySpawnPool();
                    Count(args, 3, 3);
                    return target.SetEnemySpawnAllowed(Find(target.Enemies, args[1], value => value.DisplayName), Toggle(args[2]));
                case "refill":
                    Count(args, 2, 3);
                    if (args[1] == "percent")
                    {
                        Count(args, 3, 3);
                        return target.SetEnemyRefill(target.EnemyAutoRefill, Number(args[2]));
                    }
                    return target.SetEnemyRefill(Toggle(args[1]), args.Length == 3 ? Number(args[2]) : target.EnemyRefillPercent);
                case "enemy":
                    Count(args, 4, 5);
                    bool needsValue = args[1] == "hp" || args[1] == "shield" || args[1] == "damage";
                    Count(args, needsValue ? 5 : 4, needsValue ? 5 : 4);
                    return target.ChangeEnemy(Number(args[2]), Number(args[3]), args[1], needsValue ? Number(args[4]) : 0);
                case "status":
                    Count(args, 5, 5);
                    string status = args[3] switch { "표식" => "Mark", "독" => "Poison", "기절" => "Stun", "약화" => "Weakness", "무방비" => "Exposed", _ => args[3] };
                    if (!Enum.TryParse(status, true, out StatusEffectType type) || !Enum.IsDefined(typeof(StatusEffectType), type))
                        throw new ArgumentException("상태이상은 표식, 독, 기절, 약화, 무방비 중에서 선택해 주세요.");
                    return target.SetStatus(Number(args[1]), Number(args[2]), type, Number(args[4]));
                case "clear": Count(args, 1, 1); return target.ClearEnemies();
                case "player": Count(args, 3, 3); return target.MovePlayer(Number(args[1]), Number(args[2]));
                case "hp": Count(args, 2, 3); return target.SetHealth(Number(args[1]), args.Length == 3 ? Number(args[2]) : 100);
                case "gold": return Gold(args);
                case "item": return Item(args);
                case "ai": Count(args, 2, 2); return target.SetAutomatic(Toggle(args[1]));
                case "step": Count(args, 1, 1); return target.Step();
                case "god":
                    Count(args, 2, 2);
                    BattleTestContext.Invulnerable = Toggle(args[1]);
                    return "무적 " + (BattleTestContext.Invulnerable ? "켜짐" : "꺼짐");
                case "seed":
                    Count(args, 2, 2); target.RequireIdle();
                    UnityEngine.Random.InitState(Number(args[1]));
                    return "무작위 수를 초기화했습니다. 연출도 난수를 소비하므로 동일한 재현을 보장하지 않습니다.";
                case "checkpoint": Count(args, 1, 1); return target.CaptureCheckpoint();
                case "reset": Count(args, 1, 1); return target.RestoreCheckpoint();
                default: return "알 수 없는 명령입니다. 도움말 탭을 확인해 주세요.";
            }
        }
        catch (ArgumentException exception) { return RejectedPrefix + exception.Message; }
        catch (InvalidOperationException exception) { return RejectedPrefix + exception.Message; }
    }

    private string Bullet(string[] args)
    {
        Count(args, 3, 4);
        if (args[1] == "add")
            return target.AddBullet(Find(target.Bullets, args[2], value => value.GetDisplayName(0)),
                args.Length == 4 ? Number(args[3]) : 0, false);
        if (args[1] == "level")
        {
            Count(args, 4, 4);
            return target.SetBulletLevel(Number(args[2]), Number(args[3]));
        }
        if (args[1] == "remove")
        {
            Count(args, 3, 3);
            return target.RemoveBullet(Number(args[2]));
        }
        throw new ArgumentException("탄환 추가·강화·제거: bullet add / level / remove");
    }

    private string Gold(string[] args)
    {
        Count(args, 2, 4);
        if (args[1] != "random")
        {
            Count(args, 2, 2);
            return target.SetMoney(Number(args[1]));
        }
        target.RequireIdle();
        int min = args.Length > 2 ? Number(args[2]) : 0;
        int max = args.Length > 3 ? Number(args[3]) : 1000;
        if (min < 0 || max < min || max > 1000000000)
            throw new ArgumentException("0 ≤ 최소 ≤ 최대 ≤ 1,000,000,000 범위로 입력해 주세요.");
        return target.SetMoney(UnityEngine.Random.Range(min, max + 1));
    }

    private string Item(string[] args)
    {
        Count(args, 2, 4);
        if (args[1] == "random")
        {
            Count(args, 2, 2); target.RequireIdle();
            for (int slot = 0; slot < PlayerInventory.MaximumSlotCount; slot++)
                target.SetItem(slot, target.Items[UnityEngine.Random.Range(0, target.Items.Count)]);
            return "세 아이템 슬롯을 무작위로 설정했습니다.";
        }
        if (args[1] == "remove")
        {
            Count(args, 3, 3);
            return target.SetItem(Number(args[2]), null);
        }
        if (args[1] == "set")
        {
            Count(args, 4, 4);
            return target.SetItem(Number(args[2]), Find(target.Items, args[3], value => value.DisplayName));
        }
        throw new ArgumentException("아이템 설정·제거·무작위: item set / remove / random");
    }

    private string ListCatalog(string[] args)
    {
        Count(args, 2, 3);
        string search = args.Length == 3 ? args[2] : string.Empty;
        switch (args[1])
        {
            case "bullet": return Catalog(target.Bullets, search, value => value.GetDisplayName(0));
            case "enemy": return Catalog(target.Enemies, search, value => value.DisplayName);
            case "relic": return Catalog(target.Relics, search, value => value.DisplayName);
            case "item": return Catalog(target.Items, search, value => value.DisplayName);
            default: throw new ArgumentException("도감 종류: bullet(탄환), enemy(적), relic(유물), item(아이템)");
        }
    }

    private static string Catalog<T>(IReadOnlyList<T> values, string search, Func<T, string> name) where T : UnityEngine.Object
    {
        return string.Join("\n", values.Select((value, index) => $"[{index}] {value.name} | {name(value)}")
            .Where(value => value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private static T Find<T>(IReadOnlyList<T> values, string query, Func<T, string> name) where T : UnityEngine.Object
    {
        if (int.TryParse(query, out int index))
        {
            if (index >= 0 && index < values.Count) return values[index];
            throw new ArgumentException("도감 번호가 범위를 벗어났습니다. 목록을 확인해 주세요.");
        }
        T[] exact = values.Where(value => string.Equals(value.name, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name(value), query, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (exact.Length == 1) return exact[0];
        T[] matches = values.Where(value => value.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || name(value).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        if (matches.Length == 1) return matches[0];
        throw new ArgumentException(matches.Length == 0 ? "일치하는 항목이 없습니다. 도감에서 검색해 주세요."
            : "여러 항목이 일치합니다. 도감 번호나 정확한 이름을 입력해 주세요.");
    }

    internal static string[] Tokenize(string command)
    {
        var result = new List<string>();
        var token = new StringBuilder();
        bool quoted = false;
        foreach (char character in command ?? string.Empty)
        {
            if (character == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (token.Length > 0) { result.Add(token.ToString()); token.Clear(); }
            }
            else token.Append(character);
        }
        if (quoted) throw new ArgumentException("이름을 감싼 큰따옴표를 닫아 주세요.");
        if (token.Length > 0) result.Add(token.ToString());
        return result.ToArray();
    }

    private static int Number(string value)
    {
        if (!int.TryParse(value.TrimStart('#'), out int parsed))
            throw new ArgumentException("정수를 입력해 주세요. 입력값: " + value);
        return parsed;
    }
    private static bool Toggle(string value) => value.ToLowerInvariant() switch
    {
        "on" => true, "off" => false,
        _ => throw new ArgumentException("on(켜기) 또는 off(끄기)를 입력해 주세요.")
    };
    private static void Count(string[] args, int minimum, int maximum)
    {
        if (args.Length < minimum || args.Length > maximum)
            throw new ArgumentException("입력한 항목 수가 올바르지 않습니다. 도움말을 확인해 주세요.");
    }
}
