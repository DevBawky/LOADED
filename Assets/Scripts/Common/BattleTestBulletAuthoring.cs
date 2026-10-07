using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draft inputs only. The Editor bridge owns persistent asset writes.</summary>
public sealed class BattleTestBulletAuthoring : MonoBehaviour
{
    public readonly struct Values
    {
        public readonly int Damage, Range;
        public readonly float CriticalChance, CriticalMultiplier;
        public Values(int damage, float chance, float multiplier, int range)
        { Damage = damage; CriticalChance = chance; CriticalMultiplier = multiplier; Range = range; }
        public bool IsValid => Damage >= 0 && Range >= 1 && Range <= 10
            && !float.IsNaN(CriticalChance) && CriticalChance >= 0 && CriticalChance <= 100
            && !float.IsNaN(CriticalMultiplier) && !float.IsInfinity(CriticalMultiplier) && CriticalMultiplier >= 1;
    }

    // Supplied by the Editor assembly; player builds cannot persist project assets.
    public static Func<BulletData, int, Values, string> SaveAsset;
    [SerializeField] private BattleTestController controller;
    [SerializeField] private BattleTestGui gui;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_Dropdown level;
    [SerializeField] private TMP_InputField damage;
    [SerializeField] private TMP_InputField chance;
    [SerializeField] private TMP_InputField multiplier;
    [SerializeField] private TMP_InputField range;
    [SerializeField] private Button save;
    [SerializeField] private Button revert;
    [SerializeField] private Button close;
    private BulletData selected;
    private int editingLevel;
    private bool loading;
    internal BulletData Selected => selected;

    private void OnEnable()
    {
        level.onValueChanged.AddListener(ChangeLevel);
        damage.onValueChanged.AddListener(Changed); chance.onValueChanged.AddListener(Changed);
        multiplier.onValueChanged.AddListener(Changed); range.onValueChanged.AddListener(Changed);
        save.onClick.AddListener(Save); revert.onClick.AddListener(Reload); close.onClick.AddListener(Hide);
    }
    private void OnDisable()
    {
        level.onValueChanged.RemoveListener(ChangeLevel);
        damage.onValueChanged.RemoveListener(Changed); chance.onValueChanged.RemoveListener(Changed);
        multiplier.onValueChanged.RemoveListener(Changed); range.onValueChanged.RemoveListener(Changed);
        save.onClick.RemoveListener(Save); revert.onClick.RemoveListener(Reload); close.onClick.RemoveListener(Hide);
    }
    private void Update() => save.interactable = selected != null && controller.IsSettled && SaveAsset != null;
    internal void Show(BulletData data, int upgradeLevel)
    {
        if (data == null) return;
        selected = data;
        editingLevel = Mathf.Clamp(upgradeLevel, 0, BulletData.MaximumUpgradeLevel);
        level.SetValueWithoutNotify(editingLevel);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Reload();
    }
    internal void Hide() => panel.SetActive(false);
    private void ChangeLevel(int value) { editingLevel = value; Reload(); }
    private void Changed(string _) { if (!loading) status.text = "미저장 · 저장해야 실제 탄환 수치가 바뀝니다."; }
    private void Reload()
    {
        if (selected == null) return;
        loading = true;
        title.text = selected.GetDisplayName(editingLevel) + " · 원본 수치 편집";
        damage.SetTextWithoutNotify(selected.GetDamage(editingLevel).ToString(CultureInfo.InvariantCulture));
        chance.SetTextWithoutNotify(selected.GetCriticalChance(editingLevel).ToString("R", CultureInfo.InvariantCulture));
        multiplier.SetTextWithoutNotify(selected.GetCriticalDamageMultiplier(editingLevel).ToString("R", CultureInfo.InvariantCulture));
        range.SetTextWithoutNotify(selected.GetMaxRange(editingLevel).ToString(CultureInfo.InvariantCulture));
        status.text = SaveAsset == null ? "원본 저장은 Unity 에디터에서만 사용할 수 있습니다."
            : "선택한 단계만 변경 · 같은 탄환 전체에 적용 · 재생 종료 후에도 유지";
        loading = false;
    }
    private void Save()
    {
        if (selected == null || SaveAsset == null) return;
        if (!controller.IsSettled) { status.text = "행동이 끝난 뒤 저장해 주세요."; return; }
        if (!int.TryParse(damage.text, out int d) || !int.TryParse(range.text, out int r)
            || !float.TryParse(chance.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float c)
            || !float.TryParse(multiplier.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float m)
            || !new Values(d, c, m, r).IsValid)
        {
            status.text = "대미지: 0 이상 정수 / 치명타: 0~100% / 배율: 1 이상 / 사거리: 1~10칸";
            return;
        }
        try
        {
            status.text = SaveAsset(selected, editingLevel, new Values(d, c, m, r));
            gui.RequestRefresh();
        }
        catch (Exception exception) { status.text = "저장 실패: " + exception.Message; }
    }
}
