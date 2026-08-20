using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class RelicInstance
{
    [SerializeField] private RelicData data;
    [SerializeField] private int stackCount;
    [SerializeField] private int remainingCharges;
    [SerializeField] private int movementStacks;
    [SerializeField] private long storedDamage;
    [SerializeField] private int primaryCounter;
    [SerializeField] private int secondaryCounter;
    [SerializeField] private double storedValue;
    [SerializeField] private bool runtimeFlag;
    [SerializeField] private List<int> trackedBulletAcquisitionOrders =
        new List<int>();
    [SerializeField] private int acquisitionOrder;

    public RelicInstance(RelicData data, int acquisitionOrder)
    {
        this.data = data;
        this.acquisitionOrder = Mathf.Max(0, acquisitionOrder);
        stackCount = 1;
        remainingCharges = data == null ? 0 : data.InitialCharges;
    }

    public RelicData Data => data;
    public string Id => data == null ? string.Empty : data.Id;
    public int StackCount => Mathf.Max(1, stackCount);
    public int RemainingCharges => Mathf.Max(0, remainingCharges);
    public int MovementStacks => Mathf.Max(0, movementStacks);
    public long StoredDamage => Math.Max(0L, storedDamage);
    public int PrimaryCounter => Mathf.Max(0, primaryCounter);
    public int SecondaryCounter => Mathf.Max(0, secondaryCounter);
    public double StoredValue => double.IsNaN(storedValue)
        ? 0d
        : Math.Max(0d, storedValue);
    public bool RuntimeFlag => runtimeFlag;
    public IReadOnlyList<int> TrackedBulletAcquisitionOrders =>
        trackedBulletAcquisitionOrders
        ?? (IReadOnlyList<int>)Array.Empty<int>();
    public int AcquisitionOrder => Mathf.Max(0, acquisitionOrder);
    public bool IsSpent => data != null
        && data.LifetimeType == RelicLifetimeType.Consumable
        && remainingCharges <= 0;

    public bool TryAddStack()
    {
        if (data == null || !data.CanStack || stackCount >= data.MaxStack)
        {
            return false;
        }

        stackCount++;

        if (data.LifetimeType == RelicLifetimeType.Consumable)
        {
            remainingCharges = SaturatingAdd(
                remainingCharges,
                data.InitialCharges);
        }

        return true;
    }

    public bool TryConsumeCharge()
    {
        if (data == null || data.LifetimeType != RelicLifetimeType.Consumable
            || remainingCharges <= 0)
        {
            return false;
        }

        remainingCharges--;
        return true;
    }

    public void AddMovementStacks(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        movementStacks = SaturatingAdd(movementStacks, amount);
    }

    public void ResetMovementStacks()
    {
        movementStacks = 0;
    }

    public void ConsumeMovementStacks(int amount)
    {
        movementStacks = Mathf.Max(0, movementStacks - Mathf.Max(0, amount));
    }

    public void SetPrimaryCounter(int value)
    {
        primaryCounter = Mathf.Max(0, value);
    }

    public void AddPrimaryCounter(int amount)
    {
        primaryCounter = SaturatingAdd(primaryCounter, amount);
    }

    public bool TryConsumePrimaryCounter(int amount)
    {
        int cost = Mathf.Max(0, amount);

        if (primaryCounter < cost)
        {
            return false;
        }

        primaryCounter -= cost;
        return true;
    }

    public void SetSecondaryCounter(int value)
    {
        secondaryCounter = Mathf.Max(0, value);
    }

    public void AddSecondaryCounter(int amount)
    {
        secondaryCounter = SaturatingAdd(secondaryCounter, amount);
    }

    public bool TryConsumeSecondaryCounter(int amount = 1)
    {
        int cost = Mathf.Max(0, amount);

        if (secondaryCounter < cost)
        {
            return false;
        }

        secondaryCounter -= cost;
        return true;
    }

    public void SetStoredValue(double value)
    {
        storedValue = double.IsNaN(value) ? 0d : Math.Max(0d, value);
    }

    public void SetRuntimeFlag(bool value)
    {
        runtimeFlag = value;
    }

    public bool AddTrackedBullet(int acquisitionOrder)
    {
        trackedBulletAcquisitionOrders ??= new List<int>();
        int normalizedOrder = Mathf.Max(0, acquisitionOrder);

        if (trackedBulletAcquisitionOrders.Contains(normalizedOrder))
        {
            return false;
        }

        trackedBulletAcquisitionOrders.Add(normalizedOrder);
        return true;
    }

    public bool RemoveTrackedBullet(int acquisitionOrder)
    {
        return trackedBulletAcquisitionOrders != null
            && trackedBulletAcquisitionOrders.Remove(
                Mathf.Max(0, acquisitionOrder));
    }

    public void RestoreState(RunRelicSaveData state)
    {
        if (state == null)
        {
            return;
        }

        stackCount = Mathf.Clamp(
            state.stackCount,
            1,
            data == null ? 1 : data.MaxStack);
        remainingCharges = data != null
            && data.LifetimeType == RelicLifetimeType.Consumable
                ? Mathf.Max(0, state.remainingCharges)
                : 0;
        movementStacks = Mathf.Max(0, state.movementStacks);
        storedDamage = Math.Max(0L, state.storedDamage);
        primaryCounter = Mathf.Max(0, state.primaryCounter);
        secondaryCounter = Mathf.Max(0, state.secondaryCounter);
        storedValue = double.IsNaN(state.storedValue)
            ? 0d
            : Math.Max(0d, state.storedValue);
        runtimeFlag = state.runtimeFlag;
        trackedBulletAcquisitionOrders = state.trackedBulletAcquisitionOrders
            == null
                ? new List<int>()
                : new List<int>(state.trackedBulletAcquisitionOrders);
        acquisitionOrder = Mathf.Max(0, state.acquisitionOrder);
    }

    public RunRelicSaveData CaptureState()
    {
        return new RunRelicSaveData
        {
            relicId = Id,
            stackCount = StackCount,
            remainingCharges = RemainingCharges,
            movementStacks = MovementStacks,
            storedDamage = StoredDamage,
            primaryCounter = PrimaryCounter,
            secondaryCounter = SecondaryCounter,
            storedValue = StoredValue,
            runtimeFlag = RuntimeFlag,
            trackedBulletAcquisitionOrders =
                new List<int>(TrackedBulletAcquisitionOrders),
            acquisitionOrder = AcquisitionOrder
        };
    }

    private static int SaturatingAdd(int left, int right)
    {
        long result = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }
}
