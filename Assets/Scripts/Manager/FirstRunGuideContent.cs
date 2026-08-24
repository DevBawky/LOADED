internal static class FirstRunGuideContent
{
    internal enum CombatStep
    {
        Move,
        Rotate,
        Wait,
        InspectEnemyAction,
        ReloadThree,
        EjectChamber,
        InspectBulletInfo,
        ReorderCylinder,
        PreviewDamage,
        UseItem,
        Kick,
        Fire
    }

    internal enum TargetKind
    {
        Named,
        Cylinder,
        TutorialEnemyAction,
        MoveButtons,
        AvailableNode
    }

    internal readonly struct GuideStepDefinition
    {
        public GuideStepDefinition(
            CombatStep step,
            string title,
            string description,
            string mission,
            string videoPath,
            string targetName,
            TargetKind targetKind = TargetKind.Named)
        {
            Step = step;
            Title = title;
            Description = description;
            Mission = mission;
            VideoPath = videoPath;
            TargetName = targetName;
            TargetKind = targetKind;
        }

        public CombatStep Step { get; }
        public string Title { get; }
        public string Description { get; }
        public string Mission { get; }
        public string VideoPath { get; }
        public string TargetName { get; }
        public TargetKind TargetKind { get; }
    }

    internal readonly struct GuidePage
    {
        public GuidePage(
            string title,
            string description,
            string videoPath,
            string targetName,
            TargetKind targetKind = TargetKind.Named)
        {
            Title = title;
            Description = description;
            VideoPath = videoPath;
            TargetName = targetName;
            TargetKind = targetKind;
        }

        public string Title { get; }
        public string Description { get; }
        public string VideoPath { get; }
        public string TargetName { get; }
        public TargetKind TargetKind { get; }
    }

    internal readonly struct PriorityMission
    {
        public PriorityMission(
            string text,
            string targetName,
            TargetKind targetKind = TargetKind.Named)
        {
            Text = text;
            TargetName = targetName;
            TargetKind = targetKind;
        }

        public string Text { get; }
        public string TargetName { get; }
        public TargetKind TargetKind { get; }
    }

    internal static readonly GuidePage[] CombatSystemPages =
    {
        new GuidePage(
            "DUEL CLOCK과 카운트",
            "시간이 흐르거나 이동, 회전, 대기, 장전, 발사를 하면 <color=#FFD05A><b>DUEL CLOCK</b></color>이 충전됩니다.\n시계가 <color=#FFD05A><b>100%</b></color>에 도달하면 <color=#FFD05A><b>COUNT가 1 증가</b></color>하고 모든 적이 행동합니다.\n툴팁과 전투 연출 중에도 시간은 흐르며 <color=#FF5757><b>일시정지 메뉴</b></color>에서만 멈춥니다.",
            null,
            "Layout | Duel Clock"),
        new GuidePage(
            "적의 공격 예고",
            "공격 준비 시 <color=#FF5757><b>경고음</b></color>이 울립니다.\n<color=#FF5757><b>적 아래의 행동 패널</b></color>도 붉어집니다.\n원거리 공격은 경로와 범위도 표시됩니다.\n다음 COUNT 전에 피하거나 대비하세요.",
            null,
            "Image | Queue",
            TargetKind.TutorialEnemyAction),
        new GuidePage(
            "회피와 무방비",
            "적의 공격이 맞기 직전에 <color=#62D9FF><b>피격 범위 밖으로 이동</b></color>하면 회피합니다.\n회피에 실패한 적은 <color=#FF9F5A><b>무방비</b></color>가 되어 다음 피격이 크리티컬로 확정됩니다.\n무방비는 <color=#FFD05A><b>한 번 피격</b></color>되거나, 공격하지 않고 <color=#FF5757><b>다른 행동</b></color>을 하면 사라집니다.",
            null,
            "Image | Queue",
            TargetKind.TutorialEnemyAction),
        new GuidePage(
            "핵심 전략: 탄환 순서",
            "<color=#FFD05A><b>탄환 순서에 따라 피해량이 달라집니다.</b></color>\n실린더는 <color=#FFD05A><b>나중에 장전한 탄환부터</b></color> 발사하며, 탄환 효과도 앞뒤 순서와 연계됩니다.\n발사 전에 <color=#FF5757><b>마우스 드래그</b></color>로 순서를 바꾸고 <color=#FFD05A><b>예상 피해</b></color>를 비교하세요.",
            "Videos/Switch_Bullet_Queue.mp4",
            null,
            TargetKind.Cylinder),
        new GuidePage(
            "사거리와 공격 방향",
            "탄환은 <color=#FFD05A><b>바라보는 방향</b></color>으로 발사됩니다.\n탄환마다 <color=#FFD05A><b>사거리</b></color>가 다릅니다.\n탄환 정보에서 <color=#FFD05A><b>유효 범위 N칸</b></color>을 확인하세요.\n예상 피해가 없다면 <color=#FFD05A><b>방향, 거리, 앞을 막는 적</b></color>을 확인하세요.",
            "Videos/Show_Expectation.mp4",
            null,
            TargetKind.Cylinder),
        new GuidePage(
            "콤보와 8 COUNT",
            "적을 처치하면 <color=#FFD05A><b>콤보 게이지 8칸</b></color>이 충전됩니다.\n적 처치 없이 COUNT가 진행될 때마다 한 칸씩 줄어듭니다.\n<color=#FF5757><b>8 COUNT 안에 추가 적을 처치</b></color>하면 게이지가 다시 8칸이 되고 콤보가 이어집니다.",
            null,
            "Image | Combo Timer BG"),
        new GuidePage(
            "디버프 종류",
            "<color=#FF7D7D><b>표식: 받는 피해 50% 증가</b></color>\n<color=#78D987><b>독: COUNT 종료 시 스택만큼 피해, 이후 1 감소</b></color>\n<color=#75C7FF><b>기절: COUNT마다 행동 불가, 이후 1 감소</b></color>\n<color=#C69CFF><b>약화: 공격력 30% 감소</b></color>\n<color=#FF9F5A><b>무방비: 회피 성공 시 공격한 적에게 적용되는 비스택 디버프. 다음 피격이 크리티컬로 확정되며, 첫 피격 또는 다른 행동 시 해제</b></color>\n적에게 디버프가 있다면 아래와 같은 상태 아이콘이 표시됩니다.\n아이콘에 <color=#FF5757><b>마우스 커서를 올리면</b></color> 효과를 확인할 수 있습니다.",
            null,
            null)
    };

    internal static readonly GuidePage[] NodeMapPages =
    {
        new GuidePage(
            "노드맵과 경로",
            "노드맵은 이번 런에서 이동할 경로를 선택하는 화면입니다.\n현재 위치에서 이어진 <color=#8FE6FF><b>밝은 경로</b></color>를 따라 다음 노드로 이동할 수 있습니다.\n한 번 선택한 경로는 되돌릴 수 없으니 앞으로 만날 노드를 확인하세요.",
            null,
            "Scroll View | Map"),
        new GuidePage(
            "노드의 종류",
            "<color=#B8C6D9><b>시작</b></color>: 현재 런의 출발점\n<color=#FFD05A><b>전투</b></color>: 일반 적과 전투\n<color=#FF8D6B><b>정예 전투</b></color>: 더 강한 적과 전투\n<color=#76E38A><b>상점</b></color>: 구매, 판매와 탄환 관리\n<color=#C69CFF><b>이벤트</b></color>: 선택에 따라 결과가 달라지는 사건\n<color=#8FE6FF><b>보물</b></color>: 유물과 보상 획득\n<color=#FF5757><b>보스</b></color>: 스테이지의 마지막 전투",
            null,
            null,
            TargetKind.AvailableNode),
        new GuidePage(
            "다음 노드 선택",
            "지금 이동할 수 있는 노드는 <color=#FFD05A><b>밝게 표시</b></color>됩니다.\n노드에 마우스 커서를 올리면 종류와 설명을 확인할 수 있습니다.\n이동할 노드를 <color=#FF5757><b>마우스 왼쪽 클릭</b></color>하면 해당 장소로 이동합니다.",
            null,
            null,
            TargetKind.AvailableNode)
    };

    internal static readonly GuideStepDefinition[] CombatSteps =
    {
        new GuideStepDefinition(
            CombatStep.Move,
            "이동",
            "<color=#FF5757><b>A/D 키</b></color> 또는 <color=#FF5757><b>이동 버튼 클릭</b></color>으로 한 칸 이동합니다.\n이동은 <color=#FFD05A><b>DUEL CLOCK을 충전</b></color>하며, 100%에 도달하면 적이 행동합니다.",
            "한 칸 이동",
            "Videos/Movement.mp4",
            null,
            TargetKind.MoveButtons),
        new GuideStepDefinition(
            CombatStep.Rotate,
            "회전",
            "<color=#FF5757><b>W 키</b></color>, <color=#FF5757><b>마우스 휠 클릭</b></color> 또는 <color=#FF5757><b>회전 버튼 클릭</b></color>으로 방향을 바꿉니다.\n탄환은 <color=#FFD05A><b>바라보는 방향</b></color>으로 발사됩니다.",
            "한 번 회전",
            "Videos/Rotate.mp4",
            "Button | Rotate"),
        new GuideStepDefinition(
            CombatStep.Wait,
            "대기",
            "<color=#FF5757><b>S 키</b></color> 또는 <color=#FF5757><b>대기 버튼 클릭</b></color>으로 제자리에서 DUEL CLOCK을 충전합니다.",
            "한 번 대기",
            "Videos/Wait.mp4",
            "Button | Wait"),
        new GuideStepDefinition(
            CombatStep.InspectEnemyAction,
            "적 행동 확인",
            "<color=#FFD05A><b>적 아래의 행동 아이콘</b></color>에서 다음 행동을 확인하세요.\n아이콘이 없다면 DUEL CLOCK을 진행한 뒤 <color=#FF5757><b>마우스 커서를 올리거나 클릭</b></color>하세요.",
            "적 행동 아이콘 확인",
            null,
            "Image | Queue",
            TargetKind.TutorialEnemyAction),
        new GuideStepDefinition(
            CombatStep.ReloadThree,
            "장전",
            "<color=#FF5757><b>R 키</b></color> 또는 <color=#FF5757><b>장전 버튼 클릭</b></color>으로 다음 탄환을 장전합니다.\n장전은 <color=#FFD05A><b>DUEL CLOCK을 충전</b></color>합니다.\n시계와 적 행동을 먼저 확인하세요.",
            "탄환 3회 장전",
            "Videos/Reload.mp4",
            "Button | Reload"),
        new GuideStepDefinition(
            CombatStep.EjectChamber,
            "약실 제거",
            "실린더에서 제거할 탄환을 <color=#FF5757><b>마우스 우클릭</b></color>하세요.\n제거한 탄환은 파괴되지 않고 <color=#FFD05A><b>사용한 탄환 순환</b></color>으로 이동합니다.\n약실 제거는 <color=#FFD05A><b>DUEL CLOCK을 충전하지 않습니다.</b></color>",
            "실린더 탄환 한 발 우클릭해 제거",
            null,
            null,
            TargetKind.Cylinder),
        new GuideStepDefinition(
            CombatStep.InspectBulletInfo,
            "탄환 정보 읽기",
            "실린더 탄환이나 다음 탄환에 <color=#FF5757><b>마우스 커서를 올리세요.</b></color>\n<color=#FFD05A><b>피해, 유효 범위, 치명타 확률, 특수 효과</b></color>를 확인할 수 있습니다.",
            "탄환 정보에서 피해와 사거리 확인",
            null,
            null,
            TargetKind.Cylinder),
        new GuideStepDefinition(
            CombatStep.ReorderCylinder,
            "실린더 순서 조작",
            "<color=#FFD05A><b>탄환 순서에 따라 피해량이 달라집니다.</b></color>\n실린더 탄환을 다른 탄환 위로 <color=#FF5757><b>마우스 드래그</b></color>하세요.\n<color=#FFD05A><b>나중에 장전한 탄환부터</b></color> 발사됩니다.",
            "탄환 순서 한 번 변경",
            "Videos/Switch_Bullet_Queue.mp4",
            null,
            TargetKind.Cylinder),
        new GuideStepDefinition(
            CombatStep.PreviewDamage,
            "적 피해 예상치",
            "실린더 탄환에 <color=#FF5757><b>마우스 커서를 올리세요.</b></color>\n해당 탄환까지 발사할 <color=#FFD05A><b>예상 피해</b></color>가 적 체력에 표시됩니다.",
            "실린더 탄환의 예상 피해 확인",
            "Videos/Show_Expectation.mp4",
            null,
            TargetKind.Cylinder),
        new GuideStepDefinition(
            CombatStep.UseItem,
            "아이템 사용",
            "모든 적을 기절시키는 <color=#FFD05A><b>전기충격</b></color>을 지급합니다.\n<color=#FF5757><b>1/2/3 키</b></color> 또는 <color=#FF5757><b>인벤토리 슬롯 클릭</b></color>으로 사용하세요.",
            "전기충격 한 번 사용",
            null,
            "Layout | Inventory"),
        new GuideStepDefinition(
            CombatStep.Kick,
            "발차기",
            "바로 앞의 적 방향으로 이동하면 발차기합니다.\n<color=#FF5757><b>A/D 키</b></color> 또는 <color=#FF5757><b>이동 버튼 클릭</b></color>을 사용하세요.\n발차기 후 <color=#FFD05A><b>유료 행동 3회</b></color> 동안 다시 사용할 수 없습니다.\n적끼리 부딪히면 <color=#FFD05A><b>둘 다 피해</b></color>를 받습니다.",
            "적을 한 번 발차기",
            "Videos/Kick.mp4",
            "Panel | Behaviour Tile"),
        new GuideStepDefinition(
            CombatStep.Fire,
            "발사",
            "<color=#FF5757><b>화면에 마우스 왼쪽 클릭</b></color>, <color=#FF5757><b>스페이스 바</b></color> 또는 <color=#FF5757><b>사격 버튼 클릭</b></color>으로 탄환을 <color=#FFD05A><b>순서대로 모두 발사</b></color>합니다.\n발사 전에 <color=#FFD05A><b>방향, 사거리, 탄환 순서</b></color>를 확인하세요.",
            "실린더 발사",
            "Videos/Shoot.mp4",
            "Button | Shoot")
    };

    internal static readonly GuidePage[] ShopPages =
    {
        new GuidePage(
            "상품 구매",
            "상단에서 <color=#FFD05A><b>탄환과 아이템</b></color>을 구매할 수 있습니다.\n상품에 마우스 커서를 올려 <color=#FFD05A><b>효과와 가격</b></color>을 확인하세요.",
            "Videos/Shop_Purchase.mp4",
            "Layout | Shop Items"),
        new GuidePage(
            "탄환 관리",
            "탄환 관리에서 보유 탄환을 <color=#FFD05A><b>강화하거나 제거</b></color>할 수 있습니다.\n비용은 선택한 탄환 아래에 표시됩니다.",
            "Videos/Shop_Idle.mp4",
            "Button | Manage Bullet"),
        new GuidePage(
            "인벤토리",
            "왼쪽 인벤토리에서 <color=#FFD05A><b>보유 아이템</b></color>을 확인하세요.\n상점에서는 아이템을 <color=#FF5757><b>마우스 우클릭</b></color>해 판매할 수 있습니다.",
            null,
            "Layout | Inventory"),
        new GuidePage(
            "새로고침",
            "원하는 상품이 없다면 <color=#FFD05A><b>새로고침</b></color>하세요.\n새로고침할 때마다 다음 비용이 증가합니다.\n<color=#67E480><b>(현재는 데모 버전이므로 새로고침 비용이 무료입니다!)</b></color>",
            null,
            "Button | Refresh"),
        new GuidePage(
            "다음 전투",
            "<color=#FFD05A><b>구매와 탄환 관리</b></color>를 마친 뒤 이 버튼을 누르세요.\n<color=#FFD05A><b>다음 전투</b></color>가 시작됩니다.",
            null,
            "Button | Go To Battle")
    };

    internal static readonly GuidePage[] EventPages =
    {
        new GuidePage(
            "이벤트 읽기",
            "이벤트의 <color=#FFD05A><b>제목과 상황 설명</b></color>을 읽어 보세요.\n같은 이벤트라도 선택에 따라 결과가 달라질 수 있습니다.",
            null,
            "Text | Event Dialogue"),
        new GuidePage(
            "선택과 결과",
            "선택지에는 <color=#FFD05A><b>비용, 조건, 확률</b></color>이 포함될 수 있습니다.\n내용을 확인하고 원하는 선택지를 클릭한 뒤 결과를 확인하세요.",
            null,
            "Button | Event Choice 1")
    };

    internal static readonly GuidePage[] TreasurePages =
    {
        new GuidePage(
            "보물상자 열기",
            "보물상자를 클릭하면 이번에 획득할 수 있는 <color=#FFD05A><b>유물 후보</b></color>가 나타납니다.",
            null,
            "Button | Treasure Chest"),
        new GuidePage(
            "유물 선택",
            "유물에 마우스를 올려 효과를 확인하고 <color=#FF5757><b>하나를 선택</b></color>하세요.\n선택한 유물은 현재 런 동안 특별한 효과를 제공합니다.",
            null,
            "Panel | Relic Choices")
    };

}
