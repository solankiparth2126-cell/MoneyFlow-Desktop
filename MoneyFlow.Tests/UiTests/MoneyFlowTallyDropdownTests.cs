using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Desktop.Controls.Dropdown;
using MoneyFlow.Desktop.Controls.Dropdown.Events;
using MoneyFlow.Desktop.Controls.Dropdown.Models;
using MoneyFlow.Desktop.Services;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class MoneyFlowTallyDropdownTests
{
    [Fact]
    public void DropdownConfiguration_Presets_Should_Configure_Correct_MasterTypes()
    {
        var countryCfg = DropdownConfiguration.ForCountry();
        countryCfg.MasterType.Should().Be(MasterType.Country);
        countryCfg.HeaderText.Should().Be("LIST OF COUNTRIES");
        countryCfg.AllowCreate.Should().BeTrue();
        countryCfg.CreateShortcut.Should().Be("Alt+C");

        var stateCfg = DropdownConfiguration.ForState();
        stateCfg.MasterType.Should().Be(MasterType.State);
        stateCfg.HeaderText.Should().Be("LIST OF STATES");

        var ledgerCfg = DropdownConfiguration.ForLedger();
        ledgerCfg.MasterType.Should().Be(MasterType.Ledger);
        ledgerCfg.HeaderText.Should().Be("LIST OF LEDGERS");

        var custCfg = DropdownConfiguration.ForCustomer();
        custCfg.MasterType.Should().Be(MasterType.Customer);
        custCfg.HeaderText.Should().Be("LIST OF CUSTOMERS");

        var itemCfg = DropdownConfiguration.ForStockItem();
        itemCfg.MasterType.Should().Be(MasterType.StockItem);
        itemCfg.HeaderText.Should().Be("LIST OF STOCK ITEMS");
    }

    [Fact]
    public void MoneyFlowTallyDropdown_SetItems_And_SelectValue_Should_Update_SelectedText()
    {
        var dropdown = new MoneyFlowTallyDropdown
        {
            Configuration = DropdownConfiguration.ForCountry()
        };

        var items = new List<DropdownItemModel>
        {
            new DropdownItemModel("IND", "India", "IND (+91)"),
            new DropdownItemModel("USA", "United States", "USA (+1)"),
            new DropdownItemModel("ARE", "United Arab Emirates", "ARE (+971)")
        };

        dropdown.SetItems(items, "IND");

        dropdown.SelectedValue.Should().Be("IND");
        dropdown.SelectedText.Should().Be("India");
        dropdown.SelectedItem.Should().NotBeNull();
        dropdown.SelectedItem!.Code.Should().Be("IND (+91)");
    }

    [Fact]
    public void MoneyFlowTallyListControl_Filter_Should_Isolate_Matching_Records()
    {
        var list = new MoneyFlowTallyListControl();
        var items = new List<DropdownItemModel>
        {
            DropdownItemModel.CreateSpecial("◆ Not Applicable", "NA"),
            new DropdownItemModel("IND", "India", "IND (+91)"),
            new DropdownItemModel("IDN", "Indonesia", "IDN (+62)"),
            new DropdownItemModel("USA", "United States", "USA (+1)")
        };

        list.SetItems(items);
        list.TotalCount.Should().Be(4);
        list.ItemCount.Should().Be(4);

        // Filter by "Ind"
        list.ApplyFilter("Ind");

        // Special items always stay visible, plus India & Indonesia
        list.ItemCount.Should().Be(3);
        list.SelectedItem.Should().NotBeNull();
        list.SelectedItem!.Name.Should().Be("◆ Not Applicable");

        list.MoveNext();
        list.SelectedItem!.Name.Should().Be("India");

        list.MoveNext();
        list.SelectedItem!.Name.Should().Be("Indonesia");
    }

    [Fact]
    public void MoneyFlowTallyListControl_Navigation_Should_Respect_Bounds()
    {
        var list = new MoneyFlowTallyListControl();
        var items = new List<DropdownItemModel>
        {
            new DropdownItemModel("1", "Item 1"),
            new DropdownItemModel("2", "Item 2"),
            new DropdownItemModel("3", "Item 3")
        };

        list.SetItems(items);
        list.SelectedIndex.Should().Be(0);

        list.MovePrevious();
        list.SelectedIndex.Should().Be(0); // Cannot go below 0

        list.MoveNext();
        list.SelectedIndex.Should().Be(1);

        list.MoveNext();
        list.SelectedIndex.Should().Be(2);

        list.MoveNext();
        list.SelectedIndex.Should().Be(2); // Cannot exceed max

        list.MoveFirst();
        list.SelectedIndex.Should().Be(0);

        list.MoveLast();
        list.SelectedIndex.Should().Be(2);
    }

    [Fact]
    public void MasterNavigationService_CustomCreateHandler_Should_Return_Created_Master()
    {
        MasterNavigationService.RegisterCreateHandler(MasterType.Country, owner =>
        {
            return new DropdownItemModel("FR", "France", "FRA (+33)");
        });

        var navService = new MasterNavigationService();
        var created = navService.OpenCreateMaster(MasterType.Country);

        created.Should().NotBeNull();
        created!.Name.Should().Be("France");
        created.Code.Should().Be("FRA (+33)");
    }
}
