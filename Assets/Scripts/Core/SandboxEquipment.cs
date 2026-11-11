using System;
using System.Collections.Generic;

namespace DontForgetMe
{
    public enum SandboxItem
    {
        Axe,
        Club,
        Supply
    }

    public static class SandboxItems
    {
        public static string Name(SandboxItem item)
        {
            switch (item)
            {
                case SandboxItem.Axe: return "斧头";
                case SandboxItem.Club: return "木棍";
                case SandboxItem.Supply: return "补给";
                default: throw new ArgumentOutOfRangeException(nameof(item));
            }
        }

        public static bool IsWeapon(SandboxItem item)
        {
            Name(item);
            return item != SandboxItem.Supply;
        }

        public static float Damage(SandboxItem item)
        {
            RequireWeapon(item);
            return item == SandboxItem.Axe ? 35f : 20f;
        }

        public static float StaminaCost(SandboxItem item)
        {
            RequireWeapon(item);
            return item == SandboxItem.Axe ? 18f : 12f;
        }

        public static float Cooldown(SandboxItem item)
        {
            RequireWeapon(item);
            return item == SandboxItem.Axe ? 0.85f : 0.6f;
        }

        private static void RequireWeapon(SandboxItem item)
        {
            if (!IsWeapon(item)) throw new ArgumentException("This item is not a weapon.", nameof(item));
        }
    }

    public sealed class SandboxBackpack
    {
        private readonly List<SandboxItem> contents = new List<SandboxItem>();
        public int Capacity { get; }
        public IReadOnlyList<SandboxItem> Contents { get; }

        public SandboxBackpack(int capacity = 6)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Contents = contents.AsReadOnly();
        }

        public bool TryAdd(SandboxItem item)
        {
            SandboxItems.Name(item);
            if (contents.Count >= Capacity) return false;
            contents.Add(item);
            return true;
        }

        public SandboxItem RemoveAt(int index)
        {
            SandboxItem item = contents[index];
            contents.RemoveAt(index);
            return item;
        }
    }

    public sealed class SandboxEquipment
    {
        public SandboxItem? Weapon { get; private set; }
        public SandboxBackpack Backpack { get; private set; }

        public bool EquipWeapon(SandboxItem item)
        {
            if (!SandboxItems.IsWeapon(item))
                throw new ArgumentException("Only weapons can be held.", nameof(item));
            if (Weapon.HasValue) return false;
            Weapon = item;
            return true;
        }

        public SandboxItem? DropWeapon()
        {
            SandboxItem? dropped = Weapon;
            Weapon = null;
            return dropped;
        }

        public bool EquipBackpack(SandboxBackpack backpack)
        {
            if (backpack == null) throw new ArgumentNullException(nameof(backpack));
            if (Backpack != null) return false;
            Backpack = backpack;
            return true;
        }

        public SandboxBackpack DropBackpack()
        {
            SandboxBackpack dropped = Backpack;
            Backpack = null;
            return dropped;
        }

        public bool StoreWeapon()
        {
            if (!Weapon.HasValue || Backpack == null || !Backpack.TryAdd(Weapon.Value)) return false;
            Weapon = null;
            return true;
        }

        public bool DrawWeapon(int index)
        {
            if (Backpack == null) return false;
            SandboxItem item = Backpack.Contents[index];
            if (Weapon.HasValue || !SandboxItems.IsWeapon(item)) return false;
            Weapon = Backpack.RemoveAt(index);
            return true;
        }
    }

    public sealed class SandboxHealth
    {
        public float Value { get; private set; } = 100f;
        public bool IsDown => Value <= 0;

        public bool Hit(float damage)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDown) return false;
            Value = Math.Max(0, Value - damage);
            return true;
        }

        public void Reset() { Value = 100f; }
    }
}
