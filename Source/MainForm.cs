using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TGConfigEditor
{
  public partial class MainForm : Form
  {
    List<ConfigSection> configSections = new List<ConfigSection>();
    List<ConfigSection> supportedConfigSections = new List<ConfigSection>();
    string selectedSectionName;
    CommonTable selectedCommonTable = null;
    bool refreshRequired = true;

    BackgroundWorker worker = new BackgroundWorker();
    string fileName = "";
    int lineCount = 0;

    public MainForm()
    {
      worker.WorkerReportsProgress = true;
      worker.WorkerSupportsCancellation = true;
      worker.DoWork += worker_DoWork;
      worker.ProgressChanged += worker_ProgressChanged;
      worker.RunWorkerCompleted += worker_RunWorkerCompleted;
      InitializeComponent();
    }

    void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
      Properties.Settings.Default.LastConfigFile = fileName;
      Properties.Settings.Default.Save();
      configSections = (List<ConfigSection>)e.Result;

      foreach (ConfigSection section in configSections)
      {
        if (section.IsSupported)
          supportedConfigSections.Add(section);
      }

      DataGridSection.AutoGenerateColumns = false;
      ReloadSectionsTree();
      SaveConfigBtn.Enabled = true;
    }

    void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
      MainPrgBar.Value = e.ProgressPercentage;
      //StatusLbl.Text = "Linia: " + e.ProgressPercentage.ToString() + " / " + lineCount.ToString();
    }

    void worker_DoWork(object sender, DoWorkEventArgs e)
    {
      int currentLine = 0;
      string wFileName = e.Argument.ToString();
      List<ConfigSection> configSectionsTemp = new List<ConfigSection>();
      string line = "";

      if (File.Exists(wFileName))
      {
        StreamReader configFile = new StreamReader(wFileName);
        ConfigSection section = null;
        Regex regex = new Regex("^([a-z]|[A-Z]|[_]){3,64}");
        Match result = null;

        while (configFile.Peek() != -1)
        {
          line = configFile.ReadLine();
          currentLine++;
          result = regex.Match(line);

          if (result.Success)
          {
            section = new ConfigSection();
            section.Name = result.Value;
            section.RawLines.Add(line);
            section.IsSupported = Helper.IsSectionSupported(section);
            section.IsMasterTable = Helper.IsSectionMasterTable(section);
            configSectionsTemp.Add(section);
            worker.ReportProgress(currentLine);
          }
          else
          {
            section.RawLines.Add(line);
          }
        }
        configFile.Close();
      }

      line = "";
      string[] lineSplit = null;

      foreach (ConfigSection section in configSectionsTemp)
      {
        if (section.IsSupported)
        {
          if (section.IsMasterTable)
          {
            section.MasterTable = new MasterTable();
            section.MasterTable.TableId = section.RawLines[1].Trim();
            section.MasterTable.TableComment = section.RawLines[2].Trim();

            for (int i = 3; i < section.RawLines.Count; i++)
            {
              line = section.RawLines[i];
              if (string.IsNullOrWhiteSpace(line.Trim()) == false)
              {
                line = line.Replace("\t", " ");
                lineSplit = line.Split(' ');
                CommonTable table = new CommonTable();
                table.ItemId = lineSplit[0];
                table.RowsCount = Convert.ToInt32(lineSplit[1]);
                table.ColumnsCount = Convert.ToInt32(lineSplit[2]);

                for (int k = 0; k < table.RowsCount; k++)
                {
                  i++;
                  line = section.RawLines[i].Trim();
                  TableRow row = new TableRow();
                  row.ItemId = table.ItemId;
                  line = line.Replace("\t", " ");
                  line = Helper.ClearDoubleSpaces(line);
                  lineSplit = line.Split(' ');

                  for (int r = 0; r < lineSplit.Length; r++)
                    row.Values.Add(lineSplit[r]);

                  table.Rows.Add(row);
                }

                section.MasterTable.CommonTables.Add(table);
              }
            }
          }
          else
          {
            lineSplit = section.RawLines[0].Split('\t');
            string tmpLine = Helper.ClearDoubleSpaces(lineSplit[0]);
            tmpLine = tmpLine.Trim();
            string[] partSplit = tmpLine.Split(' ');

            CommonTable table = new CommonTable();
            table.ItemId = partSplit[1].Trim();
            table.ColumnsCount = Convert.ToInt32(partSplit[2]) + 1;//+1 for item id
            table.RowsCount = Convert.ToInt32(partSplit[3]);
            table.UnknownValue = partSplit[4];
            table.TableComment = section.RawLines[3];

            table.ColumnsHeaders.Add("ItemId");

            for (int i = 0; i < table.ColumnsCount - 1; i++)
              table.ColumnsHeaders.Add(lineSplit[i + 1].Trim());

            for (int j = 4; j < section.RawLines.Count; j++)
            {
              line = Helper.NormalizeLine(section.RawLines[j]);
              if (string.IsNullOrWhiteSpace(line.Trim()) == false)
              {
                lineSplit = line.Split('\t');
                TableRow row = new TableRow();
                row.ItemId = lineSplit[0].Trim();

                row.Values.Add(row.ItemId);

                for (int r = 1; r < lineSplit.Length; r++)
                  row.Values.Add(lineSplit[r].Trim());

                table.Rows.Add(row);
              }
            }

            section.CommonTable = table;
          }
        }

        currentLine += section.RawLines.Count;
        worker.ReportProgress(currentLine);
      }

      e.Result = configSectionsTemp;
    }

    private void LoadConfigBtn_Click(object sender, EventArgs e)
    {
      if (OpenConfigFileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        fileName = OpenConfigFileDlg.FileName;
        LoadConfigFile();
      }
    }

    private void LoadConfigFile()
    {
      lineCount = 0;

      if (File.Exists(fileName))
      {
        using (var reader = File.OpenText(fileName))
        {
          while (reader.ReadLine() != null)
            lineCount++;
        }
        MainPrgBar.Maximum = lineCount * 2;
        MainPrgBar.Value = 0;

        DataGridSection.AutoGenerateColumns = false;
        DataGridSection.ClearSelection();
        DataGridSection.Rows.Clear();
        DataGridSection.Columns.Clear();

        TreeViewSection.Nodes.Clear();

        DetailsLbl.Text = "N/A";
        TableItemsGrid.Columns.Clear();
        TableItemsGrid.Rows.Clear();
        AllItemsCmbBx.Items.Clear();
        selectedCommonTable = null;
        selectedSectionName = "";
        supportedConfigSections.Clear();

        worker.RunWorkerAsync(fileName);
        this.Text = "Transport Giant - Config editor v0.5 - " + fileName;
      }
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
      fileName = Properties.Settings.Default.LastConfigFile;
      LoadConfigFile();
    }

    private void DataGridSection_CellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
      object newValue = DataGridSection.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
      UpdateSourceData(newValue, e.RowIndex, e.ColumnIndex);
    }

    private void UpdateSourceData(object newValue, int rowIndex, int columnIndex)
    {
      if (TreeViewSection.SelectedNode != null)
      {
        ConfigSection section = GetConfigSection(TreeViewSection.SelectedNode.Text);

        if (section.IsMasterTable)
        {
          int tableRow = rowIndex % (section.MasterTable.CommonTables[0].RowsCount + 1);
          int tableIndex = (rowIndex - tableRow) / (section.MasterTable.CommonTables[0].RowsCount + 1);
          section.MasterTable.CommonTables[tableIndex].Rows[tableRow - 1].Values[columnIndex] = newValue.ToString();
        }
        else
        {
          section.CommonTable.Rows[rowIndex].Values[columnIndex] = newValue.ToString();
        }
      }
    }

    private void SaveConfigBtn_Click(object sender, EventArgs e)
    {
      if (SaveConfigFileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        string line = "";
        using (var reader = File.CreateText(SaveConfigFileDlg.FileName))
        {
          foreach (ConfigSection section in configSections)
          {
            if (section.IsSupported)
            {
              if (section.IsMasterTable)
              {
                reader.WriteLine(section.RawLines[0]);
                reader.WriteLine(section.RawLines[1]);
                reader.WriteLine("	" + section.MasterTable.TableComment);

                foreach (CommonTable table in section.MasterTable.CommonTables)
                {
                  line = table.ItemId + "\t" + table.RowsCount + "\t" + table.ColumnsCount;
                  reader.WriteLine(line);
                  foreach (TableRow row in table.Rows)
                  {
                    line = "";
                    foreach (string value in row.Values)
                    {
                      line += "\t" + value;
                    }
                    reader.WriteLine(line.Trim());
                  }
                  reader.WriteLine();
                }
              }
              else
              {
                reader.WriteLine(section.RawLines[0]);
                reader.WriteLine(section.RawLines[1]);
                reader.WriteLine(section.RawLines[2]);
                reader.WriteLine(section.RawLines[3]);
                reader.WriteLine();

                foreach (TableRow row in section.CommonTable.Rows)
                {
                  line = "";
                  foreach (string value in row.Values)
                  {
                    line += "                            \t" + value;
                  }
                  reader.WriteLine(line);
                }
                reader.WriteLine();
              }
            }
            else
            {
              foreach (string item in section.RawLines)
                reader.WriteLine(item);
            }
          }
        }
      }
    }

    private void TreeViewSection_AfterSelect(object sender, TreeViewEventArgs e)
    {
      if (TreeViewSection.SelectedNode != null && refreshRequired)
      {
        AllItemsCmbBx.SelectedIndex = -1;
        DataGridSection.AutoGenerateColumns = false;
        DataGridSection.ClearSelection();
        DataGridSection.Rows.Clear();
        DataGridSection.Columns.Clear();

        DetailsLbl.Text = "N/A";

        TableItemsGrid.ClearSelection();
        TableItemsGrid.Columns.Clear();
        TableItemsGrid.Rows.Clear();

        AllItemsCmbBx.Items.Clear();
        selectedCommonTable = null;

        selectedSectionName = TreeViewSection.SelectedNode.Text;
        if (e.Node.Parent != null)
          selectedSectionName = e.Node.Parent.Text;

        ConfigSection section = GetConfigSection(selectedSectionName);

        if (section != null)
        {
          if (section.IsMasterTable)
          {
            CommentsTxtBx.Text = section.MasterTable.TableComment;
            if (e.Node.Name == "ID")
            {
              DisplayItemDetails(e.Node.Text, section);
            }
          }
          else
          {
            CommentsTxtBx.Text = section.CommonTable.TableComment;
            if (section.CommonTable.ColumnsCount == 0)
            {
              //do nothing - not supported nor needed
            }
            else
            {
              for (int i = 0; i < section.CommonTable.ColumnsCount; i++)
                DataGridSection.Columns.Add(section.CommonTable.ColumnsHeaders[i], section.CommonTable.ColumnsHeaders[i]);

              DataGridSection.Columns[0].ReadOnly = true;

              for (int i = 0; i < section.CommonTable.RowsCount; i++)
              {
                DataGridSection.Rows.Add(section.CommonTable.Rows[i].Values.ToArray());
              }
            }
          }
        }
      }
    }

    void DisplayItemDetails(string itemId, ConfigSection section)
    {
      AddBtn.Enabled = true;
      RemoveBtn.Enabled = true;

      switch (section.Name)
      {
        case "AcceptProduct":
          CommonTable selectedAcceptProduct = section.MasterTable.GetTable(itemId);
          selectedCommonTable = selectedAcceptProduct;

          DetailsLbl.Text = "(" + itemId + ") Factory: " + GetFactoryName(itemId) + "\n";
          DetailsLbl.Text += "Accepts:";

          TableItemsGrid.Columns.Add("Product", "Product");
          TableItemsGrid.Columns.Add("ProductId", "ProductId (E)");
          TableItemsGrid.Columns[0].ReadOnly = true;

          if (selectedAcceptProduct.RowsCount > 0)
          {
            foreach (TableRow row in selectedAcceptProduct.Rows)
              TableItemsGrid.Rows.Add(GetProductName(row.Values[0]), row.Values[0]);
          }
          else
            DetailsLbl.Text += "\nNOTHING";

          FillAllItemsCombo("Product");
          break;
        case "ProduceProduct":
          CommonTable selectedProduceProduct = section.MasterTable.GetTable(itemId);
          selectedCommonTable = selectedProduceProduct;

          DetailsLbl.Text = "(" + itemId + ") Factory: " + GetFactoryName(itemId) + "\n";
          DetailsLbl.Text += "Produces:";

          TableItemsGrid.Columns.Add("Product", "Product");
          TableItemsGrid.Columns.Add("ProductId", "ProductId (E)");
          TableItemsGrid.Columns.Add("MinimumProduction", "MinimumProduction (E)");
          TableItemsGrid.Columns.Add("MaximumProduction", "MaximumProduction (E)");
          TableItemsGrid.Columns.Add("Resourcesleft", "Resourcesleft (E)");
          TableItemsGrid.Columns[0].ReadOnly = true;

          if (selectedProduceProduct.RowsCount > 0)
          {
            foreach (TableRow row in selectedProduceProduct.Rows)
              TableItemsGrid.Rows.Add(GetProductName(row.Values[0]), row.Values[0], row.Values[1], row.Values[2], row.Values[3]);
          }
          else
            DetailsLbl.Text += "\nNOTHING";

          FillAllItemsCombo("Product");
          break;
        case "BuildingResources":
          CommonTable selectedBuildingResProduct = section.MasterTable.GetTable(itemId);
          selectedCommonTable = selectedBuildingResProduct;

          DetailsLbl.Text = "(" + itemId + ") Building: " + GetFactoryName(itemId) + "\n";
          DetailsLbl.Text += "Requires to build it:";

          TableItemsGrid.Columns.Add("Product", "Product");
          TableItemsGrid.Columns.Add("ProductId", "ProductId (E)");
          TableItemsGrid.Columns.Add("Amount", "Amount (E)");
          TableItemsGrid.Columns[0].ReadOnly = true;

          if (selectedBuildingResProduct.RowsCount > 0)
          {
            foreach (TableRow row in selectedBuildingResProduct.Rows)
              TableItemsGrid.Rows.Add(GetProductName(row.Values[0]), row.Values[0], row.Values[1]);
          }
          else
            DetailsLbl.Text += "\nNOTHING";

          FillAllItemsCombo("Product");
          break;
        case "Members":
          DetailsLbl.Text = "FACTORY LINE MEMBERS\n";

          CommonTable selectedMember = section.MasterTable.GetTable(itemId);
          selectedCommonTable = selectedMember;

          string regionCodeName = GetRegionCodeName(GetItemValue(selectedMember.ItemId, "FactoryLines", "RegionCode"));

          TableItemsGrid.Columns.Add("Factory", "Factory");
          TableItemsGrid.Columns.Add("FactoryId", "FactoryId (E)");
          TableItemsGrid.Columns[0].ReadOnly = true;

          foreach (TableRow row in selectedMember.Rows)
            TableItemsGrid.Rows.Add(GetFactoryName(row.Values[0]), row.Values[0]);

          DetailsLbl.Text += "REGION: " + regionCodeName;

          FillAllItemsCombo("Factory");
          break;
        case "TerminalPlatformLimitAircraft":
        case "TerminalPlatformLimitHeli":
        case "TerminalPlatformLimitRoad":
        case "TerminalPlatformLimitShip":
        case "TerminalPlatformLimitTrain":
        case "TerminalPlatformLimitZeppelin":
          DetailsLbl.Text = "Terminal size for each expand level\n";

          CommonTable selectedTerminal = section.MasterTable.GetTable(itemId);
          selectedCommonTable = selectedTerminal;

          TableItemsGrid.Columns.Add("Level", "Level");
          TableItemsGrid.Columns.Add("TerminalSize", "Terminal Size");
          TableItemsGrid.Columns[0].ReadOnly = true;

          for (int i = 0; i < selectedTerminal.Rows.Count; i++)
            TableItemsGrid.Rows.Add("Level " + (i + 1).ToString(), selectedTerminal.Rows[i].Values[0]);

          AddBtn.Enabled = false;
          RemoveBtn.Enabled = false;
          break;
        default:
          DetailsLbl.Text = "N/A";
          break;
      }
    }

    void FillAllItemsCombo(string sectionName)
    {
      ConfigSection section = GetConfigSection(sectionName);
      int nameIndex = section.CommonTable.GetHeaderIndex("Name");

      foreach (TableRow row in section.CommonTable.Rows)
      {
        AllItemsCmbBx.Items.Add(new ComboBoxItem(Helper.NormalizeText(row.Values[nameIndex]), row.ItemId));
      }
    }

    ConfigSection GetConfigSection(string sectionName)
    {
      if (supportedConfigSections != null)
      {
        foreach (ConfigSection section in supportedConfigSections)
        {
          if (section.Name == sectionName)
            return section;
        }
      }
      return null;
    }

    string GetItemValue(string itemId, string sectionName, string headerName)
    {
      string name = "";
      int nameIndex = -1;
      ConfigSection items = GetConfigSection(sectionName);
      TableRow item = items.CommonTable.GetRow(itemId);
      nameIndex = items.CommonTable.GetHeaderIndex(headerName);
      name = Helper.NormalizeText(item.Values[nameIndex]);
      return name;
    }

    string GetItemName(string itemId, string sectionName)
    {
      return GetItemValue(itemId, sectionName, "Name");
    }

    string GetFactoryName(string itemId)
    {
      return GetItemName(itemId, "Factory");
    }

    string GetProductName(string itemId)
    {
      return GetItemName(itemId, "Product");
    }

    string GetRegionCodeName(string regionCode)
    {
      //1 EU, 2 USA, 3 EU/USA, 4 AUS, 5 AUS/EU,  6 AUS/USA, 7 EU/USA/AUS
      switch (regionCode)
      {
        case "1":
          return "EU";
        case "2":
          return "USA";
        case "3":
          return "EU/USA";
        case "4":
          return "AUS";
        case "5":
          return "AUS/EU";
        case "6":
          return "AUS/USA";
        case "7":
          return "EU/USA/AUS";
        default:
          return "UNKNOWN";
      }
    }

    private void RemoveBtn_Click(object sender, EventArgs e)
    {
      if (selectedCommonTable != null && TableItemsGrid.SelectedCells.Count > 0)
      {
        int rowIndex = TableItemsGrid.SelectedCells[0].RowIndex;
        TableItemsGrid.Rows.RemoveAt(rowIndex);
        selectedCommonTable.Rows.RemoveAt(rowIndex);
        selectedCommonTable.RowsCount -= 1;

        if (selectedCommonTable.RowsCount == 0)
          selectedCommonTable.ColumnsCount = 0;

        ReloadSectionsTree();
      }
    }

    private void AddBtn_Click(object sender, EventArgs e)
    {
      if (selectedCommonTable != null && AllItemsCmbBx.SelectedItem != null)
      {
        int selectedIndex = AllItemsCmbBx.SelectedIndex;
        TableRow newRow = new TableRow();
        string itemIdValue = ((ComboBoxItem)AllItemsCmbBx.SelectedItem).Value;
        newRow.ItemId = selectedCommonTable.ItemId;
        newRow.Values.Add(itemIdValue);

        if (selectedSectionName == "BuildingResources")
          newRow.Values.Add("10");
        else if (selectedSectionName == "ProduceProduct")
        {
          newRow.Values.Add("1");
          newRow.Values.Add("2");
          newRow.Values.Add("-1");
        }


        selectedCommonTable.Rows.Add(newRow);
        selectedCommonTable.RowsCount++;
        selectedCommonTable.ColumnsCount = newRow.Values.Count;

        ReloadSectionsTree();
        AllItemsCmbBx.SelectedIndex = selectedIndex;
      }
    }

    private void TableItemsGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
      if (selectedCommonTable != null)
      {
        selectedCommonTable.Rows[e.RowIndex].Values[e.ColumnIndex - 1] = TableItemsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();
        refreshRequired = false;
        ReloadSectionsTree();
      }
    }

    void ReloadSectionsTree()
    {
      int selectedNodeIdx = -1;
      int selectedParentNodeIdx = -1;

      if (TreeViewSection.SelectedNode != null)
      {
        selectedNodeIdx = TreeViewSection.SelectedNode.Index;
        if (TreeViewSection.SelectedNode.Parent != null)
          selectedParentNodeIdx = TreeViewSection.SelectedNode.Parent.Index;
      }

      TreeViewSection.Nodes.Clear();
      foreach (ConfigSection section in supportedConfigSections)
      {
        TreeNode masterNode = TreeViewSection.Nodes.Add(section.Name);

        if (section.IsMasterTable)
        {
          for (int k = 0; k < section.MasterTable.CommonTables.Count; k++)
          {
            TreeNode parentNode = masterNode.Nodes.Add("ID", section.MasterTable.CommonTables[k].ItemId);

            for (int i = 0; i < section.MasterTable.CommonTables[k].RowsCount; i++)
            {
              TreeNode rowNode = parentNode.Nodes.Add(i.ToString(), "Row " + i.ToString());

              foreach (string val in section.MasterTable.CommonTables[k].Rows[i].Values)
                rowNode.Nodes.Add(val);
            }
          }
        }
      }

      if (selectedNodeIdx > -1)
      {
        TreeViewSection.Nodes[selectedParentNodeIdx].Expand();
        TreeViewSection.SelectedNode = TreeViewSection.Nodes[selectedParentNodeIdx].Nodes[selectedNodeIdx];
        if (TreeViewSection.SelectedNode != null)
          TreeViewSection.SelectedNode.Expand();
        refreshRequired = true;
      }
    }

    private void TranslateBtn_Click(object sender, EventArgs e)
    {
      //string test = "";
      //int i = 0;
      //foreach (var item in supportedConfigSections[14].CommonTable.Rows)
      //{
      //  test += "supportedConfigSections[14].CommonTable.Rows[" + i + "].Values[18] = \"" + item.Values[18].Replace("\"", "\\\"") + "\";\n";
      //  i++;
      //}

      if (supportedConfigSections != null && supportedConfigSections.Count > 0)
      {
        supportedConfigSections[1].CommonTable.Rows[0].Values[15] = "\"Iron ore mine\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[1].Values[15] = "\"Copper mine\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[2].Values[15] = "\"Gold mine\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[3].Values[15] = "\"Bauxit mine\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[4].Values[15] = "\"Coal mine\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[5].Values[15] = "\"Uranium mine\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[6].Values[15] = "\"Oil well\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[7].Values[15] = "\"Lumber camp\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[8].Values[15] = "\"Salt mine\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[9].Values[15] = "\"Gravel pit\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[10].Values[15] = "\"Quarry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[11].Values[15] = "\"Waterworks\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[12].Values[15] = "\"Orchard\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[13].Values[15] = "\"Coffee plantation\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[14].Values[15] = "\"Sugar plantation\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[15].Values[15] = "\"Hop farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[16].Values[15] = "\"Olive grove\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[17].Values[15] = "\"Tobacco plantation\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[18].Values[15] = "\"Cotton Plantation\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[19].Values[15] = "\"Crop Farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[20].Values[15] = "\"Sheep farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[21].Values[15] = "\"Pig farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[22].Values[15] = "\"Cattle farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[23].Values[15] = "\"Chicken farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[24].Values[15] = "\"Distillery\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[25].Values[15] = "\"Steel Mill\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[26].Values[15] = "\"Sawmill\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[27].Values[15] = "\"Paper mill\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[28].Values[15] = "\"Foundry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[29].Values[15] = "\"Aluminum smelter\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[30].Values[15] = "\"Jeweler\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[31].Values[15] = "\"Refinery\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[32].Values[15] = "\"Lab\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[33].Values[15] = "\"Cement factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[34].Values[15] = "\"Brickyard\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[35].Values[15] = "\"Ice factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[36].Values[15] = "\"Fish farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[37].Values[15] = "\"Coffee roastery\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[38].Values[15] = "\"Liquor factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[39].Values[15] = "\"Brewery\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[40].Values[15] = "\"Oil mill\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[41].Values[15] = "\"Tobacco factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[42].Values[15] = "\"Textile industry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[43].Values[15] = "\"Slaughterhouse\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[44].Values[15] = "\"Dairy\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[45].Values[15] = "\"Food factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[46].Values[15] = "\"Tool factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[47].Values[15] = "\"Building materials industry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[48].Values[15] = "\"Furniture factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[49].Values[15] = "\"Carpentry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[50].Values[15] = "\"Printing house\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[51].Values[15] = "\"Household goods factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[52].Values[15] = "\"Electronics industry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[53].Values[15] = "\"Paint factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[54].Values[15] = "\"Fertilizer plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[55].Values[15] = "\"Oil power plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[56].Values[15] = "\"Coal plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[57].Values[15] = "\"Nuclear power station\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[58].Values[15] = "\"Trash dump\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[59].Values[15] = "\"Trash incinerator\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[60].Values[15] = "\"Crocodile farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[61].Values[15] = "\"Ostrich farm\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[62].Values[15] = "\"Winery\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[63].Values[15] = "\"Sandpit\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[64].Values[15] = "\"Glassworks\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[65].Values[15] = "\"Solar cell industry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[66].Values[15] = "\"Auto industry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[67].Values[15] = "\"Brandy-Distillery\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[68].Values[15] = "\"Kangaroo breed\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[69].Values[15] = "\"Shoe factory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[70].Values[15] = "\"Opalmine\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[71].Values[15] = "\"Jewelry industry\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[72].Values[15] = "\"Kiwi plantation\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[73].Values[15] = "\"Atomium\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[74].Values[15] = "\"Observation Tower\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[75].Values[15] = "\"Eiffel Tower\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[76].Values[15] = "\"Statue of Liberty\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[77].Values[15] = "\"Lincoln monument\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[78].Values[15] = "\"Neuschwanstein Castle\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[79].Values[15] = "\"Ferris wheel\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[80].Values[15] = "\"St. Stephen's Cathedral\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[81].Values[15] = "\"Taj Mahal\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[82].Values[15] = "\"The White House\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[83].Values[15] = "\"Akropolis\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[84].Values[15] = "\"Space Museum\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[85].Values[15] = "\"Colossus of Rhodes\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[86].Values[15] = "\"Mount St. Michel\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[87].Values[15] = "\"Pyramide\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[88].Values[15] = "\"Stonehenge\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[89].Values[15] = "\"Triumphal Arch\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[90].Values[15] = "\"Fort\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[91].Values[15] = "\"Space Center\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[92].Values[15] = "\"Space Center\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[93].Values[15] = "\"Space Center\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[94].Values[15] = "\"Space Center\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[95].Values[15] = "\"Botanical Garden\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[96].Values[15] = "\"Sports Stadium\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[97].Values[15] = "\"Olympic Swimming Stadium\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[98].Values[15] = "\"Olympic Games Athletics\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[99].Values[15] = "\"Olympic Football Stadium\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[100].Values[15] = "\"Olympic Fire\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[101].Values[15] = "\"Adventure Casino\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[102].Values[15] = "\"Zoological Garden\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[103].Values[15] = "\"Amusement park\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[104].Values[15] = "\"Observatory\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[105].Values[15] = "\"Biosphere\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[106].Values[15] = "\"Thermenhotel\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[107].Values[15] = "\"Casino\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[108].Values[15] = "\"Radio telescope\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[109].Values[15] = "\"Television tower\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[110].Values[15] = "\"Walking park\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[111].Values[15] = "\"Comic Park\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[112].Values[15] = "\"Opera house\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[113].Values[15] = "\"Supply station\" \"Eine Art von Raststette, bei der Tiere und Menschen versorgt werden kunnen. Gebaut entlang einer langen Strase durch den australischen Kontinent. Im Jahr 1862.\"";
        supportedConfigSections[1].CommonTable.Rows[114].Values[15] = "\"Funkstation\" \"Eine grose Funkstation - also ein Gebaude!\"";
        supportedConfigSections[1].CommonTable.Rows[115].Values[15] = "\"Fusion power plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[116].Values[15] = "\"Fusion power plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[117].Values[15] = "\"Fusion power plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[118].Values[15] = "\"Fusion power plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[119].Values[15] = "\"Fusion power plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[120].Values[15] = "\"Fusion power plant\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[121].Values[15] = "\"World Exposition grounds\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[122].Values[15] = "\"World Exposition grounds\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[123].Values[15] = "\"World Exposition grounds\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[124].Values[15] = "\"World Exposition grounds\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[125].Values[15] = "\"World Exposition grounds\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[126].Values[15] = "\"World Exposition grounds\" \"\"";
        supportedConfigSections[1].CommonTable.Rows[127].Values[15] = "\"City\" \"\"";

        supportedConfigSections[14].CommonTable.Rows[0].Values[18] = "\"Iron ore\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[1].Values[18] = "\"Copper ore\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[2].Values[18] = "\"Gold\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[3].Values[18] = "\"Bauxite\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[4].Values[18] = "\"Coal\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[5].Values[18] = "\"Uranium ore\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[6].Values[18] = "\"Oil\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[7].Values[18] = "\"Logs\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[8].Values[18] = "\"Salt\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[9].Values[18] = "\"Gravel\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[10].Values[18] = "\"Clay\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[11].Values[18] = "\"Water\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[12].Values[18] = "\"Fruit\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[13].Values[18] = "\"Coffee beans\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[14].Values[18] = "\"Sugarcane\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[15].Values[18] = "\"Hop\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[16].Values[18] = "\"Olives\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[17].Values[18] = "\"Tobacco\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[18].Values[18] = "\"Cotton\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[19].Values[18] = "\"Grain\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[20].Values[18] = "\"Wool\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[21].Values[18] = "\"Pigs\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[22].Values[18] = "\"Milk\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[23].Values[18] = "\"Egs\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[24].Values[18] = "\"Whisky\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[25].Values[18] = "\"Steel\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[26].Values[18] = "\"Wooden boards\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[27].Values[18] = "\"Paper\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[28].Values[18] = "\"Copper sheet\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[29].Values[18] = "\"Aluminium\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[30].Values[18] = "\"Jewellery\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[31].Values[18] = "\"Fuels\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[32].Values[18] = "\"Chemicals\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[33].Values[18] = "\"Cement\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[34].Values[18] = "\"Brick\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[35].Values[18] = "\"Blocks of ice\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[36].Values[18] = "\"Fish\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[37].Values[18] = "\"Coffee\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[38].Values[18] = "\"Rum\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[39].Values[18] = "\"Beer\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[40].Values[18] = "\"Olive oil\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[41].Values[18] = "\"Cigars\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[42].Values[18] = "\"Clothes\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[43].Values[18] = "\"Meat\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[44].Values[18] = "\"Cheese\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[45].Values[18] = "\"Foods\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[46].Values[18] = "\"Tool\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[47].Values[18] = "\"Construction materials\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[48].Values[18] = "\"Furniture\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[49].Values[18] = "\"Wooden houses\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[50].Values[18] = "\"Newspapers\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[51].Values[18] = "\"Household goods\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[52].Values[18] = "\"Electric devices\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[53].Values[18] = "\"Colors\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[54].Values[18] = "\"Fertilizer\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[55].Values[18] = "\"Rubbish\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[56].Values[18] = "\"Crocodile skin\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[57].Values[18] = "\"Leather boots\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[58].Values[18] = "\"Wine\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[59].Values[18] = "\"Quartz sand\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[60].Values[18] = "\"Glass\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[61].Values[18] = "\"Solar cells\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[62].Values[18] = "\"Solar car\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[63].Values[18] = "\"Brandy\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[64].Values[18] = "\"Opals\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[65].Values[18] = "\"Kiwis\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[66].Values[18] = "\"Jewellery (opals)\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[67].Values[18] = "\"Passengers\" \"\"";
        supportedConfigSections[14].CommonTable.Rows[68].Values[18] = "\"Mail\" \"\"";

        AllItemsCmbBx.SelectedIndex = -1;
        DataGridSection.AutoGenerateColumns = false;
        DataGridSection.ClearSelection();
        DataGridSection.Rows.Clear();
        DataGridSection.Columns.Clear();

        DetailsLbl.Text = "N/A";

        TableItemsGrid.ClearSelection();
        TableItemsGrid.Columns.Clear();
        TableItemsGrid.Rows.Clear();

        AllItemsCmbBx.Items.Clear();
        selectedCommonTable = null;
        TreeViewSection.SelectedNode = null;
      }
    }

    private void SetValueBtn_Click(object sender, EventArgs e)
    {
      if (DataGridSection != null && DataGridSection.SelectedCells.Count > 0)
      {
        for (int i = 0; i < DataGridSection.SelectedCells.Count; i++)
        {
          DataGridSection.SelectedCells[i].Value = CustomValueTxtBx.Text;
          UpdateSourceData(CustomValueTxtBx.Text, DataGridSection.SelectedCells[i].RowIndex, DataGridSection.SelectedCells[i].ColumnIndex);
        }
      }
    }

    private void PercentageBtn_Click(object sender, EventArgs e)
    {
      if (DataGridSection != null && DataGridSection.SelectedCells.Count > 0)
      {
        for (int i = 0; i < DataGridSection.SelectedCells.Count; i++)
        {
          int value = Convert.ToInt32(DataGridSection.SelectedCells[i].Value);
          int percentage = Convert.ToInt32(CustomValueTxtBx.Text);
          DataGridSection.SelectedCells[i].Value = value + (value * (percentage / 100.0));
          UpdateSourceData(CustomValueTxtBx.Text, DataGridSection.SelectedCells[i].RowIndex, DataGridSection.SelectedCells[i].ColumnIndex);
        }
      }
    }
  }
}
