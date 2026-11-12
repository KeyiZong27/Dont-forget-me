using System;
using NUnit.Framework;

namespace DontForgetMe.Tests
{
    [TestFixture]
    public sealed class SandboxTests
    {
        [Test]
        public void AWeaponCanBeDroppedAndPickedUpRepeatedlyWithoutDuplication()
        {
            SandboxEquipment equipment = new SandboxEquipment();
            for (int i = 0; i < 10; i++)
            {
                Assert.That(equipment.EquipWeapon(SandboxItem.Axe), Is.True);
                Assert.That(equipment.EquipWeapon(SandboxItem.Club), Is.False);
                Assert.That(equipment.DropWeapon(), Is.EqualTo(SandboxItem.Axe));
                Assert.That(equipment.Weapon, Is.Null);
                Assert.That(equipment.DropWeapon(), Is.Null);
            }
        }

        [Test]
        public void DroppedBackpackRetainsTheSameContentsAndCapacityOnReequip()
        {
            SandboxEquipment equipment = new SandboxEquipment();
            SandboxBackpack bag = new SandboxBackpack(4);
            bag.TryAdd(SandboxItem.Supply);
            bag.TryAdd(SandboxItem.Club);
            equipment.EquipBackpack(bag);
            SandboxBackpack dropped = equipment.DropBackpack();
            Assert.That(dropped, Is.SameAs(bag));
            Assert.That(equipment.Backpack, Is.Null);
            Assert.That(equipment.StoreWeapon(), Is.False);
            Assert.That(equipment.EquipBackpack(dropped), Is.True);
            Assert.That(equipment.Backpack.Capacity, Is.EqualTo(4));
            Assert.That(equipment.Backpack.Contents, Is.EqualTo(new[] { SandboxItem.Supply, SandboxItem.Club }));
        }

        [Test]
        public void DifferentGroundBackpacksHaveIndependentContents()
        {
            SandboxEquipment equipment = new SandboxEquipment();
            SandboxBackpack first = new SandboxBackpack();
            SandboxBackpack second = new SandboxBackpack();
            first.TryAdd(SandboxItem.Axe);
            equipment.EquipBackpack(first);
            Assert.That(equipment.EquipBackpack(second), Is.False);
            Assert.That(equipment.Backpack, Is.SameAs(first));
            equipment.DropBackpack();
            equipment.EquipBackpack(second);
            Assert.That(equipment.Backpack.Contents, Is.Empty);
            Assert.That(first.Contents, Is.EqualTo(new[] { SandboxItem.Axe }));
        }

        [Test]
        public void CapacityIsExactlySixItemsAndOverflowDoesNotLoseHeldWeapon()
        {
            SandboxEquipment equipment = new SandboxEquipment();
            SandboxBackpack bag = new SandboxBackpack();
            equipment.EquipBackpack(bag);
            for (int i = 0; i < 6; i++) Assert.That(bag.TryAdd(SandboxItem.Supply), Is.True);
            Assert.That(bag.TryAdd(SandboxItem.Supply), Is.False);
            equipment.EquipWeapon(SandboxItem.Axe);
            Assert.That(equipment.StoreWeapon(), Is.False);
            Assert.That(equipment.Weapon, Is.EqualTo(SandboxItem.Axe));
            Assert.That(bag.Contents.Count, Is.EqualTo(6));
            Assert.That(bag.RemoveAt(2), Is.EqualTo(SandboxItem.Supply));
            Assert.That(equipment.StoreWeapon(), Is.True);
            Assert.That(equipment.Weapon, Is.Null);
            Assert.That(bag.Contents.Count, Is.EqualTo(6));
            Assert.That(bag.Contents[5], Is.EqualTo(SandboxItem.Axe));
        }

        [Test]
        public void StoringAndDrawingTransfersExactlyOneWeapon()
        {
            SandboxEquipment equipment = new SandboxEquipment();
            SandboxBackpack bag = new SandboxBackpack();
            equipment.EquipBackpack(bag);
            equipment.EquipWeapon(SandboxItem.Axe);
            Assert.That(equipment.StoreWeapon(), Is.True);
            Assert.That(equipment.StoreWeapon(), Is.False);
            Assert.That(bag.Contents.Count, Is.EqualTo(1));
            equipment.EquipWeapon(SandboxItem.Club);
            Assert.That(equipment.DrawWeapon(0), Is.False);
            Assert.That(bag.Contents.Count, Is.EqualTo(1));
            equipment.DropWeapon();
            Assert.That(equipment.DrawWeapon(0), Is.True);
            Assert.That(equipment.Weapon, Is.EqualTo(SandboxItem.Axe));
            Assert.That(bag.Contents, Is.Empty);
        }

        [Test]
        public void SuppliesCannotBeHeldAsWeaponsAndRemovingAStackConsumesOneSlot()
        {
            SandboxEquipment equipment = new SandboxEquipment();
            SandboxBackpack bag = new SandboxBackpack();
            equipment.EquipBackpack(bag);
            bag.TryAdd(SandboxItem.Supply);
            bag.TryAdd(SandboxItem.Supply);
            Assert.Throws<ArgumentException>(() => equipment.EquipWeapon(SandboxItem.Supply));
            Assert.That(equipment.DrawWeapon(0), Is.False);
            bag.RemoveAt(0);
            Assert.That(bag.Contents.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => bag.RemoveAt(3));
            Assert.That(bag.Contents.Count, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidCapacityIsRejected(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SandboxBackpack(capacity));
        }

        [Test]
        public void InvalidEquipmentInputsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new SandboxEquipment().EquipBackpack(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SandboxBackpack().TryAdd((SandboxItem)99));
            Assert.Throws<ArgumentException>(() => SandboxItems.Damage(SandboxItem.Supply));
        }

        [TestCase(SandboxItem.Axe, 35, 18, 0.85f, 3)]
        [TestCase(SandboxItem.Club, 20, 12, 0.6f, 5)]
        public void WeaponsHaveExpectedStatsAndKnockdownThresholds(
            SandboxItem weapon, float damage, float cost, float cooldown, int hits)
        {
            Assert.That(SandboxItems.Damage(weapon), Is.EqualTo(damage));
            Assert.That(SandboxItems.StaminaCost(weapon), Is.EqualTo(cost));
            Assert.That(SandboxItems.Cooldown(weapon), Is.EqualTo(cooldown));
            SandboxHealth health = new SandboxHealth();
            for (int i = 0; i < hits - 1; i++)
            {
                health.Hit(damage);
                Assert.That(health.IsDown, Is.False);
            }
            Assert.That(health.Hit(damage), Is.True);
            Assert.That(health.IsDown, Is.True);
            Assert.That(health.Value, Is.Zero);
            Assert.That(health.Hit(damage), Is.False);
            health.Reset();
            Assert.That(health.Value, Is.EqualTo(100));
            Assert.That(health.IsDown, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDamageIsRejected(float damage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SandboxHealth().Hit(damage));
        }
    }
}
