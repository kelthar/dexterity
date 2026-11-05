using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // REL-01: When the Longsword is added to Hero, then there is one relation `Inventory` with ParentId Hero and TargetId Longsword, and that relation is the Longsword's only parent relation (A31).
    [Fact]
    public void Add_Should_CreateAnInventoryRelation()
    {
        var g = new Game();
        var sword = g.Make("Longsword");
        Ok(g.Relations.Add(g.Hero.Id, sword.Id));
        var relation = g.Relation(sword);
        relation.ParentId.ShouldBe(g.Hero.Id);
        relation.Type.ShouldBe(RelationType.Inventory);
        relation.SlotIds.ShouldBeEmpty();
        g.Repo.GetParentRelations(sword.Id).Count.ShouldBe(1);
    }

    // REL-02: When the Longsword is equipped to MainHand, then the relation is `Equipped` with `SlotIds = [MainHand]`.
    [Fact]
    public void Equip_Should_RecordEquipmentSlots()
    {
        var g = new Game();
        var sword = g.Own("Longsword");
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(sword).Type.ShouldBe(RelationType.Equipped);
        g.Relation(sword).SlotIds.ShouldBe(new[] { MainHand });
    }

    // REL-03: When the Longsword (Sword ⊂ Weapon) is equipped to MainHand (`Categories = [Weapon]`), then it is allowed (listed slot categories match descendants).
    [Fact]
    public void Longsword_Should_EquipInMainHand()
    {
        var g = new Game();
        var sword = g.Own("Longsword");
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(sword).Type.ShouldBe(RelationType.Equipped);
        g.Relation(sword).SlotIds.ShouldBe(new[] { MainHand });
    }

    // REL-04: When RingOfFire and WardRing are equipped to RingLeft and RingRight, then both are Equipped.
    [Fact]
    public void Rings_Should_EquipInSeparateRingSlots()
    {
        var g = new Game();
        var fire = g.Own("RingOfFire");
        var ward = g.Own("WardRing");
        Ok(g.Relations.Equip(g.Hero.Id, fire.Id, RingLeft));
        Ok(g.Relations.Equip(g.Hero.Id, ward.Id, RingRight));
        g.Relation(fire).SlotIds.ShouldBe(new[] { RingLeft });
        g.Relation(ward).SlotIds.ShouldBe(new[] { RingRight });
    }

    // REL-05: Given an EquipmentSlot named Belt has `Categories = null` or `[]` (separate cases), when one of the Item components Longsword, RingOfFire or PlateArmour is equipped there from Hero's Inventory (separate cases), then it is allowed regardless of category. The item's relation becomes `Equipped`, with ParentId Hero, TargetId the item and `SlotIds = [Belt]` (the slot's Id). The item keeps its TypeId and CategoryId.
    [Fact]
    public void UnrestrictedSlots_Should_AcceptEveryEquipableCategory()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "RingOfFire",
            "PlateArmour"
        }

        )
            foreach (var categories in new List<int>? []
            {
                null,
                new()
            }

            )
            {
                var g = new Game();
                var item = g.Own(name);
                g.World.Slots.Single(x => x.Id == Belt).Categories = categories;
                var category = item.CategoryId;
                Ok(g.Relations.Equip(g.Hero.Id, item.Id, Belt));
                var relation = g.Relation(item);
                relation.Type.ShouldBe(RelationType.Equipped);
                relation.ParentId.ShouldBe(g.Hero.Id);
                relation.TargetId.ShouldBe(item.Id);
                relation.SlotIds.ShouldBe(new[] { Belt });
                item.TypeId.ShouldBe(Item);
                item.CategoryId.ShouldBe(category);
            }
    }

    // REL-06: When the Greatsword is equipped to `[MainHand, OffHand]` and both slots are free, then it is Equipped in both slots.
    [Fact]
    public void Greatsword_Should_OccupyBothHandSlots()
    {
        var g = new Game();
        var sword = g.Own("Greatsword");
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand, OffHand));
        g.Relation(sword).SlotIds.ShouldBe(new[] { MainHand, OffHand });
    }

    // REL-07: When an equipped item is unequipped, then its relation becomes `Inventory` and its slots are free.
    [Fact]
    public void Unequip_Should_MoveItemsToInventoryAndFreeSlots()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        Ok(g.Relations.Unequip(g.Hero.Id, sword.Id));
        g.Relation(sword).Type.ShouldBe(RelationType.Inventory);
        g.Relation(sword).SlotIds.ShouldBeEmpty();
    }

    // REL-08: When Cleave1 is connected to Hero, then the relation is `Connected`.
    [Fact]
    public void Connect_Should_CreateConnectedSkillRelations()
    {
        var g = new Game();
        var skill = g.Make("Cleave1");
        var sword = g.Make("Longsword");
        var ruby = g.Make("Ruby");
        Ok(g.Relations.Connect(g.Hero.Id, skill.Id));
        Ok(g.Relations.Connect(sword.Id, ruby.Id));
        g.Relation(skill).Type.ShouldBe(RelationType.Connected);
        g.Relation(ruby).ParentId.ShouldBe(sword.Id);
    }

    // REL-09: When the Ruby is connected to the Longsword, then the relation is `Connected`, with the Longsword as parent.
    [Fact]
    public void Ruby_Should_ConnectDirectlyToLongsword()
    {
        var g = new Game();
        var skill = g.Make("Cleave1");
        var sword = g.Make("Longsword");
        var ruby = g.Make("Ruby");
        Ok(g.Relations.Connect(g.Hero.Id, skill.Id));
        Ok(g.Relations.Connect(sword.Id, ruby.Id));
        g.Relation(skill).Type.ShouldBe(RelationType.Connected);
        g.Relation(ruby).ParentId.ShouldBe(sword.Id);
    }

    // REL-10: When Cleave1 (shared, D25) is connected to both Hero and Ally, then both relations exist.
    [Fact]
    public void SharedSkills_Should_ConnectToMultipleActors()
    {
        var g = new Game();
        var skill = g.Make("Cleave1");
        Ok(g.Relations.Connect(g.Hero.Id, skill.Id));
        Ok(g.Relations.Connect(g.Ally.Id, skill.Id));
        g.Repo.GetParentRelations(skill.Id).Select(x => x.ParentId).ShouldBe(new[] { g.Hero.Id, g.Ally.Id });
    }

    // REL-25: Given Cleave1 is connected to both Hero and Ally, then the relations with TargetId Cleave1 list both Hero and Ally as parents (Skill has `MultipleParentComponents = true`, A31).
    [Fact]
    public void SharedSkills_Should_ExposeBothParentRelations()
    {
        var g = new Game();
        var skill = g.Make("Cleave1");
        Ok(g.Relations.Connect(g.Hero.Id, skill.Id));
        Ok(g.Relations.Connect(g.Ally.Id, skill.Id));
        g.Repo.GetParentRelations(skill.Id).Select(x => x.ParentId).ShouldBe(new[] { g.Hero.Id, g.Ally.Id });
    }

    // REL-11: When RingOfFire is equipped to MainHand (Weapon), then it fails and the ring stays in Inventory.
    [Fact]
    public void RingOfFire_Should_NotEquipInMainHand()
    {
        var name = "RingOfFire";
        var slot = MainHand;
        var g = new Game();
        var item = g.Own(name);
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, item.Id, slot));
        g.Snapshot().ShouldBe(before);
    }

    // REL-12: When a Ring is equipped to Neck (Amulet), then it fails (Ring isn't a descendant of Amulet).
    [Fact]
    public void RingOfFire_Should_NotEquipInNeck()
    {
        var name = "RingOfFire";
        var slot = Neck;
        var g = new Game();
        var item = g.Own(name);
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, item.Id, slot));
        g.Snapshot().ShouldBe(before);
    }

    // REL-13: When the HealingPotion (not equipable) is equipped, then it fails.
    [Fact]
    public void HealingPotion_Should_NotEquip()
    {
        var name = "HealingPotion";
        var slot = Belt;
        var g = new Game();
        var item = g.Own(name);
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, item.Id, slot));
        g.Snapshot().ShouldBe(before);
    }

    // REL-19: When an item is equipped to a slot Id that doesn't exist, then it fails.
    [Fact]
    public void Equip_Should_RejectUnknownSlots()
    {
        var name = "Longsword";
        var slot = 999;
        var g = new Game();
        var item = g.Own(name);
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, item.Id, slot));
        g.Snapshot().ShouldBe(before);
    }

    // REL-39: Given Accessory has `Categories = [Ring, Amulet]` and is empty, when Longsword is equipped there, then it fails, the sword stays in Inventory, and Accessory stays empty. Its category matches neither entry.
    [Fact]
    public void Accessory_Should_RejectUnlistedCategories()
    {
        var name = "Longsword";
        var slot = Accessory;
        var g = new Game();
        var item = g.Own(name);
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, item.Id, slot));
        g.Snapshot().ShouldBe(before);
    }

    // REL-14: When the Battleaxe is equipped to MainHand while the Longsword is there, then the Battleaxe is Equipped in MainHand, and the Longsword's relation becomes `Inventory` (D26).
    [Fact]
    public void Battleaxe_Should_DisplaceLongswordIntoInventory()
    {
        var g = new Game();
        var fists = g.Own("Fists", MainHand);
        var sword = g.Own("Longsword");
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(fists).Type.ShouldBe(RelationType.Inventory);
        var axe = g.Own("Battleaxe");
        Ok(g.Relations.Equip(g.Hero.Id, axe.Id, MainHand));
        g.Relation(sword).Type.ShouldBe(RelationType.Inventory);
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, OffHand));
        var great = g.Own("Greatsword");
        Ok(g.Relations.Equip(g.Hero.Id, great.Id, MainHand, OffHand));
        foreach (var item in new[]
        {
            sword,
            axe
        }

        )
        {
            g.Relation(item).Type.ShouldBe(RelationType.Inventory);
            g.Relation(item).SlotIds.ShouldBeEmpty();
        }

        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(great).Type.ShouldBe(RelationType.Inventory);
        g.Relation(great).SlotIds.ShouldBeEmpty();
        g.Repo.Relations.ShouldNotContain(x => x.SlotIds.Contains(OffHand));
        g.Out(sword)[0].Sum(Physical).ShouldBe(-10);
    }

    // REL-15: When the Greatsword is equipped to `[MainHand, OffHand]` while the Longsword is in MainHand and the Battleaxe is in OffHand, then the Greatsword is Equipped in both slots, and both the Longsword and the Battleaxe go to Inventory.
    [Fact]
    public void Greatsword_Should_DisplaceBothEquippedWeapons()
    {
        var g = new Game();
        var fists = g.Own("Fists", MainHand);
        var sword = g.Own("Longsword");
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(fists).Type.ShouldBe(RelationType.Inventory);
        var axe = g.Own("Battleaxe");
        Ok(g.Relations.Equip(g.Hero.Id, axe.Id, MainHand));
        g.Relation(sword).Type.ShouldBe(RelationType.Inventory);
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, OffHand));
        var great = g.Own("Greatsword");
        Ok(g.Relations.Equip(g.Hero.Id, great.Id, MainHand, OffHand));
        foreach (var item in new[]
        {
            sword,
            axe
        }

        )
        {
            g.Relation(item).Type.ShouldBe(RelationType.Inventory);
            g.Relation(item).SlotIds.ShouldBeEmpty();
        }

        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(great).Type.ShouldBe(RelationType.Inventory);
        g.Relation(great).SlotIds.ShouldBeEmpty();
        g.Repo.Relations.ShouldNotContain(x => x.SlotIds.Contains(OffHand));
        g.Out(sword)[0].Sum(Physical).ShouldBe(-10);
    }

    // REL-28: Given the Greatsword is Equipped in `[MainHand, OffHand]`, when the Longsword is equipped to MainHand, then the Greatsword goes to Inventory, the Longsword is in MainHand, and OffHand is empty.
    [Fact]
    public void Longsword_Should_DisplaceGreatswordAndFreeOffHand()
    {
        var g = new Game();
        var fists = g.Own("Fists", MainHand);
        var sword = g.Own("Longsword");
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(fists).Type.ShouldBe(RelationType.Inventory);
        var axe = g.Own("Battleaxe");
        Ok(g.Relations.Equip(g.Hero.Id, axe.Id, MainHand));
        g.Relation(sword).Type.ShouldBe(RelationType.Inventory);
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, OffHand));
        var great = g.Own("Greatsword");
        Ok(g.Relations.Equip(g.Hero.Id, great.Id, MainHand, OffHand));
        foreach (var item in new[]
        {
            sword,
            axe
        }

        )
        {
            g.Relation(item).Type.ShouldBe(RelationType.Inventory);
            g.Relation(item).SlotIds.ShouldBeEmpty();
        }

        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(great).Type.ShouldBe(RelationType.Inventory);
        g.Relation(great).SlotIds.ShouldBeEmpty();
        g.Repo.Relations.ShouldNotContain(x => x.SlotIds.Contains(OffHand));
        g.Out(sword)[0].Sum(Physical).ShouldBe(-10);
    }

    // REL-30: Given Fists are in MainHand, when the Longsword is equipped to MainHand, then the Fists go to Inventory. The next attack uses the Longsword (D5).
    [Fact]
    public void Longsword_Should_DisplaceFistsIntoInventory()
    {
        var g = new Game();
        var fists = g.Own("Fists", MainHand);
        var sword = g.Own("Longsword");
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(fists).Type.ShouldBe(RelationType.Inventory);
        var axe = g.Own("Battleaxe");
        Ok(g.Relations.Equip(g.Hero.Id, axe.Id, MainHand));
        g.Relation(sword).Type.ShouldBe(RelationType.Inventory);
        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, OffHand));
        var great = g.Own("Greatsword");
        Ok(g.Relations.Equip(g.Hero.Id, great.Id, MainHand, OffHand));
        foreach (var item in new[]
        {
            sword,
            axe
        }

        )
        {
            g.Relation(item).Type.ShouldBe(RelationType.Inventory);
            g.Relation(item).SlotIds.ShouldBeEmpty();
        }

        Ok(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Relation(great).Type.ShouldBe(RelationType.Inventory);
        g.Relation(great).SlotIds.ShouldBeEmpty();
        g.Repo.Relations.ShouldNotContain(x => x.SlotIds.Contains(OffHand));
        g.Out(sword)[0].Sum(Physical).ShouldBe(-10);
    }

    // REL-16: When Hero equips a sword from Ally's inventory, then it fails.
    [Fact]
    public void Hero_Should_NotEquipFromAllyInventory()
    {
        var g = new Game();
        var sword = g.Own("Longsword", owner: g.Ally);
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, sword.Id, MainHand));
        g.Snapshot().ShouldBe(before);
    }

    // REL-17: When an item is equipped while it is already Equipped, then it fails.
    [Fact]
    public void Equip_Should_RejectAlreadyEquippedItems()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, sword.Id, OffHand));
        g.Snapshot().ShouldBe(before);
    }

    // REL-18: When an item that is not Equipped is unequipped, then it fails.
    [Fact]
    public void Unequip_Should_RejectUnequippedItems()
    {
        var g = new Game();
        var sword = g.Own("Longsword");
        var before = g.Snapshot();
        Fail(g.Relations.Unequip(g.Hero.Id, sword.Id));
        g.Snapshot().ShouldBe(before);
    }

    // REL-20: When an item that is in Hero's inventory is added to Ally without first removing its relation to Hero, then it fails (an Inventory or Equipped component has one parent, A31). Removing the old relation first allows a transfer (A26).
    [Fact]
    public void Add_Should_RejectAnotherParentForUnshareableItems()
    {
        var g = new Game();
        var sword = g.Own("Longsword");
        var before = g.Snapshot();
        Fail(g.Relations.Add(g.Ally.Id, sword.Id));
        g.Snapshot().ShouldBe(before);
        Ok(g.Relations.Remove(g.Hero.Id, sword.Id));
        Ok(g.Relations.Add(g.Ally.Id, sword.Id));
        g.Relation(sword).ParentId.ShouldBe(g.Ally.Id);
    }

    // REL-21: When a component is made its own ancestor (sword → gem → sword), then it fails.
    [Fact]
    public void Relations_Should_RejectComponentCycles()
    {
        var g = new Game();
        var sword = g.Make("Longsword");
        var gem = g.Make("Ruby");
        g.Link(sword, gem);
        var before = g.Snapshot();
        Fail(g.Relations.Connect(gem.Id, sword.Id));
        g.Snapshot().ShouldBe(before);
    }

    // REL-22: When Cleave2 is connected to Hero while Cleave1 is connected, then it fails (both are named `Cleave`, so this is one level per skill per character, A19).
    [Fact]
    public void CleaveLevels_Should_NotStackOnOneActor()
    {
        var g = new Game();
        g.Own("Cleave1");
        var next = g.Make("Cleave2");
        foreach (var category in new[]
        {
            Combat,
            Spell
        }

        )
        {
            next.CategoryId = category;
            var before = g.Snapshot();
            Fail(g.Relations.Connect(g.Hero.Id, next.Id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // REL-24: Given Cleave1 is connected to Hero, when a skill named `Cleave` in the **Spell** category is connected to Hero, then it fails (the name decides, not the category).
    [Fact]
    public void SkillUniqueness_Should_ApplyAcrossDescendantCategories()
    {
        var g = new Game();
        g.Own("Cleave1");
        var next = g.Make("Cleave2");
        foreach (var category in new[]
        {
            Combat,
            Spell
        }

        )
        {
            next.CategoryId = category;
            var before = g.Snapshot();
            Fail(g.Relations.Connect(g.Hero.Id, next.Id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // SKL-06: Connecting Cleave2 while Cleave1 is connected fails (see REL-22).
    [Fact]
    public void CleaveTwo_Should_NotConnectAlongsideCleaveOne()
    {
        var g = new Game();
        g.Own("Cleave1");
        var next = g.Make("Cleave2");
        foreach (var category in new[]
        {
            Combat,
            Spell
        }

        )
        {
            next.CategoryId = category;
            var before = g.Snapshot();
            Fail(g.Relations.Connect(g.Hero.Id, next.Id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // REL-23: When AxeMastery and Precision (same category Combat, different names) are both connected to Hero, then both relations exist (the category doesn't group skills, A19).
    [Fact]
    public void DifferentSkills_Should_ConnectToOneActor()
    {
        var g = new Game();
        var axe = g.Make("AxeMastery");
        var precision = g.Make("Precision");
        Ok(g.Relations.Connect(g.Hero.Id, axe.Id));
        Ok(g.Relations.Connect(g.Hero.Id, precision.Id));
        g.Repo.GetChildren(g.Hero.Id).ShouldBe(new[] { axe, precision });
    }

    // REL-26: Given KnifeRuby is connected to the ThrowingKnife, when it is also connected to the Longsword, then it fails (Item has `MultipleParentComponents = false`, A31).
    [Fact]
    public void Ruby_Should_NotConnectToMultipleParents()
    {
        var g = new Game();
        var knife = g.Make("ThrowingKnife");
        var ruby = g.Make("KnifeRuby");
        g.Link(knife, ruby);
        var sword = g.Make("Longsword");
        var before = g.Snapshot();
        Fail(g.Relations.Connect(sword.Id, ruby.Id));
        g.Snapshot().ShouldBe(before);
    }

    // REL-27: Given Cleave1 is connected to Hero, when it is connected to Hero again, then it fails (still one relation per parent).
    [Fact]
    public void Connect_Should_RejectDuplicateSkillRelations()
    {
        var g = new Game();
        var skill = g.Own("Cleave1");
        var before = g.Snapshot();
        Fail(g.Relations.Connect(g.Hero.Id, skill.Id));
        g.Snapshot().ShouldBe(before);
    }

    // REL-29: Given StrengthRing is in RingLeft (STR 12/12), when RingOfFire is equipped to RingLeft, then StrengthRing goes to Inventory and STR is 10/10 without a manual Recalculate (D22).
    [Fact]
    public void RingReplacement_Should_RecalculateStatsAutomatically()
    {
        var g = new Game();
        var strength = g.Own("StrengthRing", RingLeft);
        Set(g.Hero, Str, 10, 12);
        g.Hero.Stats[Str].ModifiedMax = 12;
        g.Hero.Stats[Str].Bonus = 2;
        var fire = g.Own("RingOfFire");
        Ok(g.Relations.Equip(g.Hero.Id, fire.Id, RingLeft));
        g.Relation(strength).Type.ShouldBe(RelationType.Inventory);
        Current(g.Hero, Str).ShouldBe(10);
        g.Hero.Stats[Str].Bonus.ShouldBe(0);
    }

    // REL-31: Given the Longsword is in MainHand, when equipping RingOfFire to MainHand fails (wrong category), then the Longsword stays Equipped and nothing moves to Inventory. A failed equip changes nothing.
    [Fact]
    public void FailedEquip_Should_PreserveExistingEquipment()
    {
        var g = new Game();
        g.Own("Longsword", MainHand);
        var ring = g.Own("RingOfFire");
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, ring.Id, MainHand));
        g.Snapshot().ShouldBe(before);
    }

    // RSL-05: Given RingOfFire is in Hero's Inventory, when equipping it to MainHand is rejected, then the failed Result contains an Error state with a non-empty message explaining the category or slot mismatch, and the equipment and inventory are unchanged. Exact wording and numeric code are not fixed by this criterion (A5, REL-11).
    [Fact]
    public void FailedEquip_Should_DescribeMismatchAndPreserveRelations()
    {
        var g = new Game();
        g.Own("Longsword", MainHand);
        var ring = g.Own("RingOfFire");
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, ring.Id, MainHand));
        g.Snapshot().ShouldBe(before);
    }

    // REL-32: Given the Longsword was moved to Inventory by a swap, when Hero uses it, then the call fails (D24: an equipable item must be Equipped).
    [Fact]
    public void UnownedItems_Should_NotBeUsable()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var axe = g.Own("Battleaxe");
        Ok(g.Relations.Equip(g.Hero.Id, axe.Id, MainHand));
        var before = g.Snapshot();
        Fail(g.Effects.Outgoing(g.Hero.Id, sword.Id, g.Orc.Id));
        g.Snapshot().ShouldBe(before);
    }

    // REL-33: Given FireWand at CHARGES 2/3 is Equipped to Hero in MainHand, Hero has MANA 55/55, and WandRuby is Connected to the wand, when the Hero → FireWand relation is removed, then MainHand is free and Hero's MANA is 50/50 without a manual Recalculate. FireWand remains Active in ComponentRepository at CHARGES 2/3 with no parent relation, and WandRuby remains Active in ComponentRepository and Connected to it.
    [Fact]
    public void Remove_Should_FreeSlotsAndRecalculateFormerOwners()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        wand.Stats[Charges].Current = 2;
        wand.MetaData["origin"] = "wand";
        wand.Tags.Add("unique");
        wand.Effects.Add(g.Hit(Recharge, 1, 2, wand, wand));
        var ruby = g.Make("WandRuby");
        g.Link(wand, ruby);
        Set(g.Hero, Mana, 50, 55);
        g.Hero.Stats[Mana].ModifiedMax = 55;
        g.Hero.Stats[Mana].Bonus = 5;
        g.World.ComponentTypes.Add(new() { Id = 4, Name = "Container" });
        g.Category(22, "Container");
        var chest = g.Seed("Chest", 4, 22);
        var content = System.Text.Json.JsonSerializer.Serialize(wand);
        Ok(g.Relations.Remove(g.Hero.Id, wand.Id));
        Current(g.Hero, Mana).ShouldBe(50);
        g.Repo.GetParentRelations(wand.Id).ShouldBeEmpty();
        g.Relation(ruby).ParentId.ShouldBe(wand.Id);
        Ok(g.Relations.Add(chest.Id, wand.Id));
        g.Relation(wand).ParentId.ShouldBe(chest.Id);
        g.Relation(wand).Type.ShouldBe(RelationType.Inventory);
        System.Text.Json.JsonSerializer.Serialize(wand).ShouldBe(content);
        Current(g.Hero, Mana).ShouldBe(50);
        Ok(g.Relations.Remove(chest.Id, wand.Id));
        Ok(g.Relations.Add(g.Hero.Id, wand.Id));
        Ok(g.Relations.Equip(g.Hero.Id, wand.Id, MainHand));
        System.Text.Json.JsonSerializer.Serialize(wand).ShouldBe(content);
        Current(g.Hero, Mana).ShouldBe(55);
        g.Repo.Items[ruby.Id].State.ShouldBe(ComponentState.Active);
        g.Relation(ruby).ParentId.ShouldBe(wand.Id);
        g.Repo.GetChildren(chest.Id).ShouldBeEmpty();
    }

    // REL-34: Given REL-33, when FireWand is added to Chest, then its only parent relation is Chest → FireWand, of type `Inventory` with no SlotIds. The wand keeps its Id, Name, TypeId, CategoryId, Stats (including CHARGES 2/3), Data, Tags, ModifierData and Effects, and WandRuby and its connection are unchanged. Hero has no relation to the wand and remains at MANA 50/50. The wand's passive bonuses are inactive in Chest's Inventory (A7).
    [Fact]
    public void FireWand_Should_TransferToChestInventory()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        wand.Stats[Charges].Current = 2;
        wand.MetaData["origin"] = "wand";
        wand.Tags.Add("unique");
        wand.Effects.Add(g.Hit(Recharge, 1, 2, wand, wand));
        var ruby = g.Make("WandRuby");
        g.Link(wand, ruby);
        Set(g.Hero, Mana, 50, 55);
        g.Hero.Stats[Mana].ModifiedMax = 55;
        g.Hero.Stats[Mana].Bonus = 5;
        g.World.ComponentTypes.Add(new() { Id = 4, Name = "Container" });
        g.Category(22, "Container");
        var chest = g.Seed("Chest", 4, 22);
        var content = System.Text.Json.JsonSerializer.Serialize(wand);
        Ok(g.Relations.Remove(g.Hero.Id, wand.Id));
        Current(g.Hero, Mana).ShouldBe(50);
        g.Repo.GetParentRelations(wand.Id).ShouldBeEmpty();
        g.Relation(ruby).ParentId.ShouldBe(wand.Id);
        Ok(g.Relations.Add(chest.Id, wand.Id));
        g.Relation(wand).ParentId.ShouldBe(chest.Id);
        g.Relation(wand).Type.ShouldBe(RelationType.Inventory);
        System.Text.Json.JsonSerializer.Serialize(wand).ShouldBe(content);
        Current(g.Hero, Mana).ShouldBe(50);
        Ok(g.Relations.Remove(chest.Id, wand.Id));
        Ok(g.Relations.Add(g.Hero.Id, wand.Id));
        Ok(g.Relations.Equip(g.Hero.Id, wand.Id, MainHand));
        System.Text.Json.JsonSerializer.Serialize(wand).ShouldBe(content);
        Current(g.Hero, Mana).ShouldBe(55);
        g.Repo.Items[ruby.Id].State.ShouldBe(ComponentState.Active);
        g.Relation(ruby).ParentId.ShouldBe(wand.Id);
        g.Repo.GetChildren(chest.Id).ShouldBeEmpty();
    }

    // REL-35: Given REL-34, when the Chest → FireWand relation is removed, the wand is added to Hero's Inventory and then Equipped in MainHand, then the same wand and WandRuby remain Active in ComponentRepository, CHARGES is still 2/3, Chest has no relation to the wand, and Hero's MANA returns to 55/55 without a manual Recalculate.
    [Fact]
    public void FireWandTransfer_Should_PreserveChargesAndConnectedRuby()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        wand.Stats[Charges].Current = 2;
        wand.MetaData["origin"] = "wand";
        wand.Tags.Add("unique");
        wand.Effects.Add(g.Hit(Recharge, 1, 2, wand, wand));
        var ruby = g.Make("WandRuby");
        g.Link(wand, ruby);
        Set(g.Hero, Mana, 50, 55);
        g.Hero.Stats[Mana].ModifiedMax = 55;
        g.Hero.Stats[Mana].Bonus = 5;
        g.World.ComponentTypes.Add(new() { Id = 4, Name = "Container" });
        g.Category(22, "Container");
        var chest = g.Seed("Chest", 4, 22);
        var content = System.Text.Json.JsonSerializer.Serialize(wand);
        Ok(g.Relations.Remove(g.Hero.Id, wand.Id));
        Current(g.Hero, Mana).ShouldBe(50);
        g.Repo.GetParentRelations(wand.Id).ShouldBeEmpty();
        g.Relation(ruby).ParentId.ShouldBe(wand.Id);
        Ok(g.Relations.Add(chest.Id, wand.Id));
        g.Relation(wand).ParentId.ShouldBe(chest.Id);
        g.Relation(wand).Type.ShouldBe(RelationType.Inventory);
        System.Text.Json.JsonSerializer.Serialize(wand).ShouldBe(content);
        Current(g.Hero, Mana).ShouldBe(50);
        Ok(g.Relations.Remove(chest.Id, wand.Id));
        Ok(g.Relations.Add(g.Hero.Id, wand.Id));
        Ok(g.Relations.Equip(g.Hero.Id, wand.Id, MainHand));
        System.Text.Json.JsonSerializer.Serialize(wand).ShouldBe(content);
        Current(g.Hero, Mana).ShouldBe(55);
        g.Repo.Items[ruby.Id].State.ShouldBe(ComponentState.Active);
        g.Relation(ruby).ParentId.ShouldBe(wand.Id);
        g.Repo.GetChildren(chest.Id).ShouldBeEmpty();
    }

    // REL-36: Given Cleave1 is Connected to both Hero and Ally, when only Hero's relation to Cleave1 is removed, then Cleave1 is not deleted, Ally's relation is unchanged, and the skill still applies to Ally's actions. Removing one parent relation does not remove the others (A26, A31).
    [Fact]
    public void Disconnect_Should_PreserveSharedSkillsForOtherActors()
    {
        var g = new Game();
        var skill = g.Own("Cleave1");
        g.Link(g.Ally, skill);
        Ok(g.Relations.Remove(g.Hero.Id, skill.Id));
        g.Repo.Items[skill.Id].State.ShouldBe(ComponentState.Active);
        g.Repo.GetParents(skill.Id).ShouldBe(new[] { g.Ally });
        var sword = g.Own("Longsword", MainHand, g.Ally);
        Ok(g.Effects.Outgoing(g.Ally.Id, sword.Id, g.Orc.Id, new[] { g.Goblin1.Id }))[1].Sum(Physical).ShouldBe(-5);
    }

    // REL-37: Given Accessory has `Categories = [Ring, Amulet]` and is empty, when RingOfFire or VigorAmulet is equipped there (separate cases), then it is Equipped in Accessory. Matching either entry is sufficient; an item does not have to match every listed category.
    [Fact]
    public void Accessory_Should_AcceptMultipleListedCategories()
    {
        foreach (var name in new[]
        {
            "RingOfFire",
            "VigorAmulet",
            "Longsword"
        }

        )
            foreach (var categories in new[]
            {
                new List<int>
                {
                    Ring,
                    Amulet
                },
                new List<int>
                {
                    Melee,
                    Jewelry
                }
            }

            )
            {
                if (name == "Longsword" && categories[0] == Ring)
                    continue;
                var g = new Game();
                var item = g.Own(name);
                var slot = g.World.Slots.Single(x => x.Id == Accessory);
                slot.Categories = categories;
                Ok(g.Relations.Equip(g.Hero.Id, item.Id, Accessory));
                g.Relation(item).SlotIds.ShouldBe(new[] { Accessory });
                g.Relation(item).Type.ShouldBe(RelationType.Equipped);
            }
    }

    // REL-38: Given Accessory instead has `Categories = [Melee, Jewelry]`, when Longsword, RingOfFire or VigorAmulet is equipped there (separate cases), then it is allowed because the item's category descends from one of the listed categories.
    [Fact]
    public void Slots_Should_AcceptDescendantsOfListedCategories()
    {
        foreach (var name in new[]
        {
            "RingOfFire",
            "VigorAmulet",
            "Longsword"
        }

        )
            foreach (var categories in new[]
            {
                new List<int>
                {
                    Ring,
                    Amulet
                },
                new List<int>
                {
                    Melee,
                    Jewelry
                }
            }

            )
            {
                if (name == "Longsword" && categories[0] == Ring)
                    continue;
                var g = new Game();
                var item = g.Own(name);
                var slot = g.World.Slots.Single(x => x.Id == Accessory);
                slot.Categories = categories;
                Ok(g.Relations.Equip(g.Hero.Id, item.Id, Accessory));
                g.Relation(item).SlotIds.ShouldBe(new[] { Accessory });
                g.Relation(item).Type.ShouldBe(RelationType.Equipped);
            }
    }

    // REL-40: Given Belt has `Categories = null` or `[]` (separate cases), when the non-equipable HealingPotion is equipped there, then it fails and the potion stays in Inventory. An unrestricted category list does not override Equipable.
    [Fact]
    public void UnrestrictedSlots_Should_StillRejectNonEquipableItems()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        foreach (var categories in new List<int>? []
        {
            null,
            new()
        }

        )
        {
            g.World.Slots.Single(x => x.Id == Belt).Categories = categories;
            var before = g.Snapshot();
            Fail(g.Relations.Equip(g.Hero.Id, potion.Id, Belt));
            g.Snapshot().ShouldBe(before);
        }
    }

    // REL-41: Given MainHand has `Categories = [Weapon]` with Longsword Equipped, and OffHand has `Categories = [Bow, Ring]` with Shortbow Equipped, when Greatsword is equipped to `[MainHand, OffHand]`, then it fails because it does not match OffHand's list. Greatsword stays in Inventory, Longsword and Shortbow stay Equipped, and neither is displaced. Every selected slot must accept the item before any replacement (D26).
    [Fact]
    public void Greatsword_Should_MatchEveryRequestedSlot()
    {
        var g = new Game();
        g.Own("Longsword", MainHand);
        g.Own("Shortbow", OffHand);
        var great = g.Own("Greatsword");
        g.World.Slots.Single(x => x.Id == OffHand).Categories = new()
        {
            Bow,
            Ring
        };
        var before = g.Snapshot();
        Fail(g.Relations.Equip(g.Hero.Id, great.Id, MainHand, OffHand));
        g.Snapshot().ShouldBe(before);
    }

    // REL-42: Given RingOfFire is Equipped in Accessory (`Categories = [Ring, Amulet]`) and Hero has HP 100/100, when VigorAmulet is equipped there, then RingOfFire moves to Inventory, VigorAmulet occupies Accessory, and Hero's HP becomes 110/110 without a manual Recalculate (D26, D22). Replacement works across different allowed categories.
    [Fact]
    public void AccessoryReplacement_Should_DisplaceAndRecalculate()
    {
        var g = new Game();
        var ring = g.Own("RingOfFire", Accessory);
        var amulet = g.Own("VigorAmulet");
        Ok(g.Relations.Equip(g.Hero.Id, amulet.Id, Accessory));
        g.Relation(ring).Type.ShouldBe(RelationType.Inventory);
        g.Relation(amulet).SlotIds.ShouldBe(new[] { Accessory });
        Current(g.Hero).ShouldBe(110);
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(110);
    }

    // REL-43: Given a retained Deleted component, when adding, equipping or connecting it to an Active parent, or adding an Active child to it (separate cases), then the operation returns a failed Result without changing states, relations, slots or stats. Retained origins are not available for new gameplay relations (D39, A5).
    [Fact]
    public void DeletedComponents_Should_NotJoinNewRelations()
    {
        for (var operation = 0; operation < 4; operation++)
        {
            var g = new Game();
            var dead = g.Make("Longsword");
            dead.State = ComponentState.Deleted;
            var child = g.Make("Ruby");
            var before = g.Snapshot();
            var result = operation switch
            {
                0 => g.Relations.Add(g.Hero.Id, dead.Id),
                1 => g.Relations.Equip(g.Hero.Id, dead.Id, MainHand),
                2 => g.Relations.Connect(g.Hero.Id, dead.Id),
                _ => g.Relations.Add(dead.Id, child.Id)};
            Fail(result);
            g.Snapshot().ShouldBe(before);
        }
    }
}
