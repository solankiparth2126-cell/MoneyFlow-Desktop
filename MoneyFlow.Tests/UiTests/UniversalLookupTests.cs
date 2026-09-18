using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Enums;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Styling;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class UniversalLookupTests
{
    [Fact]
    public void LookupItem_MatchingLogic_MatchesStartsWithContainsAndCode()
    {
        var item = new LookupItem
        {
            Id = 1,
            Name = "Current Assets",
            Code = "CA",
            Subtitle = "Assets"
        };

        item.Matches(null).Should().BeTrue();
        item.Matches("").Should().BeTrue();
        item.Matches("   ").Should().BeTrue();
        item.Matches("curr").Should().BeTrue("Starts with match");
        item.Matches("assets").Should().BeTrue("Contains match");
        item.Matches("CA").Should().BeTrue("Code match");
        item.Matches("xyz").Should().BeFalse("Non-matching query");
    }

    [Fact]
    public void LookupKeyboardController_Navigation_ClampsAndMovesCorrectly()
    {
        var controller = new LookupKeyboardController { PageSize = 3 };
        var items = new List<LookupItem>
        {
            new() { Id = 1, Name = "Item 1" },
            new() { Id = 2, Name = "Item 2" },
            new() { Id = 3, Name = "Item 3" },
            new() { Id = 4, Name = "Item 4" },
            new() { Id = 5, Name = "Item 5" }
        };

        controller.ResetSelection();
        controller.SelectedIndex.Should().Be(0);

        // Down
        var downKey = new KeyEventArgs(Keys.Down);
        controller.HandleKeyDown(downKey, items, null, null, null, null);
        controller.SelectedIndex.Should().Be(1);

        // Page Down
        var pgDnKey = new KeyEventArgs(Keys.PageDown);
        controller.HandleKeyDown(pgDnKey, items, null, null, null, null);
        controller.SelectedIndex.Should().Be(4, "Clamped to max index");

        // Up
        var upKey = new KeyEventArgs(Keys.Up);
        controller.HandleKeyDown(upKey, items, null, null, null, null);
        controller.SelectedIndex.Should().Be(3);

        // Home & End
        var homeKey = new KeyEventArgs(Keys.Home);
        controller.HandleKeyDown(homeKey, items, null, null, null, null);
        controller.SelectedIndex.Should().Be(0);

        var endKey = new KeyEventArgs(Keys.End);
        controller.HandleKeyDown(endKey, items, null, null, null, null);
        controller.SelectedIndex.Should().Be(4);
    }

    [Fact]
    public void LookupKeyboardController_ActionShortcuts_FiresExpectedCallbacks()
    {
        var controller = new LookupKeyboardController();
        var items = new List<LookupItem>
        {
            new() { Id = 1, Name = "Selected Item" }
        };

        bool created = false;
        bool cancelled = false;
        bool more = false;
        LookupItem? selected = null;

        // Alt+C -> Create
        var altC = new KeyEventArgs(Keys.Alt | Keys.C);
        controller.HandleKeyDown(altC, items, null, null, () => created = true, null);
        created.Should().BeTrue();

        // Alt+M -> More
        var altM = new KeyEventArgs(Keys.Alt | Keys.M);
        controller.HandleKeyDown(altM, items, null, null, null, () => more = true);
        more.Should().BeTrue();

        // Escape -> Cancel
        var esc = new KeyEventArgs(Keys.Escape);
        controller.HandleKeyDown(esc, items, null, () => cancelled = true, null, null);
        cancelled.Should().BeTrue();

        // Enter -> Select
        var enter = new KeyEventArgs(Keys.Enter);
        controller.HandleKeyDown(enter, items, it => selected = it, null, null, null);
        selected.Should().NotBeNull();
        selected!.Name.Should().Be("Selected Item");
    }

    [Fact]
    public void LookupKeyboardController_Hierarchy_ExpandAndCollapse()
    {
        var controller = new LookupKeyboardController();
        var parentItem = new LookupItem
        {
            Id = 1,
            Name = "Parent",
            HasChildren = true,
            IsExpanded = true
        };
        var items = new List<LookupItem> { parentItem };

        bool toggled = false;

        // Left -> Collapse
        var left = new KeyEventArgs(Keys.Left);
        controller.HandleKeyDown(left, items, null, null, null, null, (exp) => toggled = true);
        parentItem.IsExpanded.Should().BeFalse();
        toggled.Should().BeTrue();

        // Right -> Expand
        toggled = false;
        var right = new KeyEventArgs(Keys.Right);
        controller.HandleKeyDown(right, items, null, null, null, null, (exp) => toggled = true);
        parentItem.IsExpanded.Should().BeTrue();
        toggled.Should().BeTrue();
    }

    [Fact]
    public async Task ListLookupProvider_RetrievalAndFiltering()
    {
        var rawData = new[]
        {
            new { Id = 10, Name = "Cash-in-hand", Code = "CASH", Sub = "Current Assets" },
            new { Id = 20, Name = "Bank Accounts", Code = "BANK", Sub = "Current Assets" },
            new { Id = 30, Name = "Sales Account", Code = "SALE", Sub = "Sales" }
        };

        var provider = new ListLookupProvider<dynamic>(
            rawData,
            x => (int)x.Id,
            x => (string)x.Name,
            x => (string)x.Code,
            x => (string)x.Sub);

        var all = await provider.GetItemsAsync();
        all.Should().HaveCount(3);

        var filtered = await provider.GetItemsAsync("cash");
        filtered.Should().ContainSingle();
        filtered[0].Name.Should().Be("Cash-in-hand");

        var byId = provider.FindById(20);
        byId.Should().NotBeNull();
        byId!.Name.Should().Be("Bank Accounts");

        var byName = provider.FindByName("Sales Account");
        byName.Should().NotBeNull();
        byName!.Id.Should().Be(30);
    }

    [Fact]
    public async Task LedgerGroupLookupProvider_FlattensTreeWithHierarchyLevels()
    {
        var tree = new List<GroupTreeNodeDto>
        {
            new()
            {
                GroupId = 1,
                GroupName = "Current Assets",
                Nature = GroupNature.Assets,
                Children = new List<GroupTreeNodeDto>
                {
                    new()
                    {
                        GroupId = 2,
                        GroupName = "Bank Accounts",
                        Nature = GroupNature.Assets,
                        ParentGroupId = 1
                    },
                    new()
                    {
                        GroupId = 3,
                        GroupName = "Cash-in-hand",
                        Nature = GroupNature.Assets,
                        ParentGroupId = 1
                    }
                }
            }
        };

        var provider = new LedgerGroupLookupProvider((ct) => Task.FromResult<IReadOnlyList<GroupTreeNodeDto>>(tree));
        var items = await provider.GetItemsAsync();

        items.Should().HaveCount(3);
        items[0].Name.Should().Be("Current Assets");
        items[0].Level.Should().Be(0);
        items[0].HasChildren.Should().BeTrue();
        items[0].Code.Should().Be("ASST");

        items[1].Name.Should().Be("Bank Accounts");
        items[1].Level.Should().Be(1);
        items[1].HasChildren.Should().BeFalse();

        items[2].Name.Should().Be("Cash-in-hand");
        items[2].Level.Should().Be(1);
    }

    [Fact]
    public async Task EnumLookupProvider_ProvidesAllEnumOptions()
    {
        var provider = new EnumLookupProvider<GroupNature>();
        var items = await provider.GetItemsAsync();

        items.Should().HaveCount(4);
        items.Select(i => i.Name).Should().Contain(new[] { "Assets", "Liabilities", "Income", "Expenses" });
    }

    [Fact]
    public void MoneyFlowTextLookup_Styling_PureWhiteBackground_NoYellowOrCream()
    {
        var lookup = new MoneyFlowTextLookup();

        // Must be pure white background as specified
        lookup.BackColor.Should().Be(Color.White);
        lookup.ForeColor.Should().NotBe(Color.Empty);

        // Verify it can hold selected values cleanly
        var items = new List<LookupItem>
        {
            new() { Id = 101, Name = "Capital Account" }
        };
        var provider = new ListLookupProvider<LookupItem>(items, x => x.Id, x => x.Name);
        lookup.SetProvider(provider);

        lookup.SelectedValue = 101;
        lookup.Text.Should().Be("Capital Account");
        lookup.SelectedItem.Should().NotBeNull();
        lookup.SelectedItem!.Id.Should().Be(101);

        lookup.SelectedValue = null;
        lookup.Text.Should().BeEmpty();
        lookup.SelectedItem.Should().BeNull();
    }

    [Fact]
    public void MoneyFlowLookupPopup_PositionNear_PositionsCorrectlyRelativeToAnchor()
    {
        using var form = new Form { Size = new Size(1000, 800), StartPosition = FormStartPosition.Manual, Location = new Point(100, 100) };
        var anchor = new Control { Size = new Size(200, 30), Location = new Point(50, 50) };
        form.Controls.Add(anchor);
        form.Show();

        var provider = new ListLookupProvider<string>(new[] { "A", "B" }, x => x, x => x);
        using var popup = new MoneyFlowLookupPopup(provider, new LookupConfig { DefaultWidth = 500, DefaultHeight = 400 });

        popup.PositionNear(anchor);

        // Popup should be located horizontally near anchor and vertically below or above
        popup.Location.X.Should().BeGreaterThan(0);
        popup.Location.Y.Should().BeGreaterThan(0);
        popup.Width.Should().Be(500);
        popup.Height.Should().Be(400);

        form.Close();
    }
}
