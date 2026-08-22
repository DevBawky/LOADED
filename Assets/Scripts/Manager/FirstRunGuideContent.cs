internal static class FirstRunGuideContent
{
    internal enum CombatStep
    {
        Move,
        Rotate,
        Wait,
        InspectEnemyAction,
        ReloadThree,
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
        MoveButtons
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
            "턴의 기본",
            "<color=#FFD05A><b>행동 1회마다 1턴</b></color>이 흐릅니다.\n이동, 회전, 대기, 장전, 발사를 하면 <color=#FFD05A><b>적도 바로 행동</b></color>합니다.\n실린더 탄환을 <color=#FF5757><b>우클릭</b></color>해 약실에서 제거하는 행동은 턴을 사용하지 않습니다.",
            null,
            "Panel | Behaviour Tile"),
        new GuidePage(
            "적의 공격 예고",
            "공격 준비 시 <color=#FF5757><b>경고음</b></color>이 울립니다.\n<color=#FF5757><b>적 아래의 행동 패널</b></color>도 붉어집니다.\n원거리 공격은 경로와 범위도 표시됩니다.\n다음 턴 전에 피하거나 대비하세요.",
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
            "디버프 종류",
            "<color=#FF7D7D><b>표식: 받는 피해 50% 증가</b></color>\n<color=#78D987><b>독: 턴 종료 시 스택만큼 피해, 이후 1 감소</b></color>\n<color=#75C7FF><b>기절: 행동 불가, 행동할 때마다 1 감소</b></color>\n<color=#C69CFF><b>약화: 공격력 30% 감소</b></color>\n적에게 디버프가 있다면 아래와 같은 상태 아이콘이 표시됩니다.\n아이콘에 <color=#FF5757><b>마우스 커서를 올리면</b></color> 남은 스택을 확인할 수 있습니다.",
            null,
            null)
    };

    internal static readonly GuideStepDefinition[] CombatSteps =
    {
        new GuideStepDefinition(
            CombatStep.Move,
            "이동",
            "<color=#FF5757><b>A/D 키</b></color> 또는 <color=#FF5757><b>이동 버튼 클릭</b></color>으로 한 칸 이동합니다.\n이동하면 <color=#FFD05A><b>적도 바로 한 턴 행동</b></color>합니다.",
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
            "<color=#FF5757><b>S 키</b></color> 또는 <color=#FF5757><b>대기 버튼 클릭</b></color>으로 행동 없이 한 턴을 넘깁니다.\n실린더의 탄환을 <color=#FF5757><b>우클릭</b></color>하면 턴을 사용하지 않고 해당 탄환을 약실에서 제거할 수 있습니다.",
            "한 번 대기",
            "Videos/Wait.mp4",
            "Button | Wait"),
        new GuideStepDefinition(
            CombatStep.InspectEnemyAction,
            "적 행동 확인",
            "<color=#FFD05A><b>적 아래의 행동 아이콘</b></color>에서 다음 행동을 확인하세요.\n아이콘이 없다면 한 턴 진행한 뒤 <color=#FF5757><b>마우스 커서를 올리거나 클릭</b></color>하세요.",
            "적 행동 아이콘 확인",
            null,
            "Image | Queue",
            TargetKind.TutorialEnemyAction),
        new GuideStepDefinition(
            CombatStep.ReloadThree,
            "장전",
            "<color=#FF5757><b>R 키</b></color> 또는 <color=#FF5757><b>장전 버튼 클릭</b></color>으로 다음 탄환을 장전합니다.\n장전은 <color=#FFD05A><b>한 턴</b></color>을 사용합니다.\n적 행동을 먼저 확인하세요.",
            "탄환 3회 장전",
            "Videos/Reload.mp4",
            "Button | Reload"),
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
            "바로 앞의 적 방향으로 이동하면 발차기합니다.\n<color=#FF5757><b>A/D 키</b></color> 또는 <color=#FF5757><b>이동 버튼 클릭</b></color>을 사용하세요.\n재사용 대기시간은 <color=#FFD05A><b>3턴</b></color>입니다.\n적끼리 부딪히면 <color=#FFD05A><b>둘 다 피해</b></color>를 받습니다.",
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

}
