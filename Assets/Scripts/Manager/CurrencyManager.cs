using System;
using TMPro;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    [Header("Money")]
    [Min(0)]
    [SerializeField] private int startingMoney;
    [SerializeField] private TMP_Text currentMoneyText;

    [Header("Runtime State")]
    [SerializeField] private int currentMoney;

    public event Action<int> MoneyChanged;

    public int CurrentMoney => currentMoney;

    private void Awake()
    {
        currentMoney = Mathf.Max(0, startingMoney);
        RefreshText();
    }

    public bool AddMoney(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        long increasedMoney = (long)currentMoney + amount;
        currentMoney = (int)Math.Min(int.MaxValue, increasedMoney);
        NotifyMoneyChanged();
        return true;
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount < 0 || currentMoney < amount)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        currentMoney -= amount;
        NotifyMoneyChanged();
        return true;
    }

    private void NotifyMoneyChanged()
    {
        RefreshText();
        MoneyChanged?.Invoke(currentMoney);
    }

    private void RefreshText()
    {
        if (currentMoneyText != null)
        {
            currentMoneyText.text = $"$ {currentMoney}";
        }
    }
}
