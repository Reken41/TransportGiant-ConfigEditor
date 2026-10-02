using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    Encoding configEncoding = new UTF8Encoding(false);

    sealed class ConfigLoadResult
    {
      public List<ConfigSection> Sections { get; set; }
      public Encoding Encoding { get; set; }
    }

    public MainForm()
    {
      worker.WorkerReportsProgress = true;
      worker.WorkerSupportsCancellation = true;
      worker.DoWork += worker_DoWork;
      worker.ProgressChanged += worker_ProgressChanged;
      worker.RunWorkerCompleted += worker_RunWorkerCompleted;
      InitializeComponent();
      CommentsTxtBx.ReadOnly = true;
      UpdateWindowTitle();
    }

    void UpdateWindowTitle()
    {
      Version version = Assembly.GetExecutingAssembly().GetName().Version;
      string versionText = version.Major + "." + version.Minor;
      string fileText = String.IsNullOrWhiteSpace(fileName) ? "" : " - " + fileName;
      Text = "Transport Giant - Config editor v" + versionText + fileText;
    }

    void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
      if (e.Cancelled)
        return;

      if (e.Error != null)
      {
        SaveConfigBtn.Enabled = false;
        MessageBox.Show("The configuration could not be loaded.\n\n" + e.Error.Message,
          "Load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      ConfigLoadResult loadResult = e.Result as ConfigLoadResult;
      if (loadResult == null)
      {
        SaveConfigBtn.Enabled = false;
        MessageBox.Show("The configuration could not be loaded.", "Load error",
          MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      Properties.Settings.Default.LastConfigFile = fileName;
      Properties.Settings.Default.Save();
      configSections = loadResult.Sections;
      configEncoding = loadResult.Encoding;

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
        Encoding detectedEncoding = DetectFileEncoding(wFileName);
        using (StreamReader configFile = new StreamReader(wFileName, detectedEncoding, true))
        {
          ConfigSection section = null;
          Regex regex = new Regex(@"^([A-Za-z_][A-Za-z0-9_]{2,63})\b");
          Match result = null;

          while (configFile.Peek() != -1)
          {
            line = configFile.ReadLine();
            currentLine++;
            result = regex.Match(line);

            if (result.Success)
            {
              section = new ConfigSection();
              section.Name = result.Groups[1].Value;
              section.RawLines.Add(line);
              section.IsSupported = Helper.IsSectionSupported(section);
              section.IsMasterTable = Helper.IsSectionMasterTable(section);
              configSectionsTemp.Add(section);
              worker.ReportProgress(currentLine);
            }
            else if (section != null)
            {
              section.RawLines.Add(line);
            }
            else
            {
              section = new ConfigSection();
              section.Name = String.Empty;
              section.RawLines.Add(line);
              configSectionsTemp.Add(section);
            }
          }

          if (configFile.CurrentEncoding != null)
            detectedEncoding = configFile.CurrentEncoding;

          configEncoding = detectedEncoding;
        }
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
                lineSplit = Regex.Split(line.Trim(), @"\s+");
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
                  lineSplit = Regex.Split(line, @"\s+");

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
              line = section.RawLines[j].Trim();
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

      e.Result = new ConfigLoadResult
      {
        Sections = configSectionsTemp,
        Encoding = configEncoding
      };
    }

    static Encoding DetectFileEncoding(string path)
    {
      byte[] bytes = File.ReadAllBytes(path);

      if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        return new UTF8Encoding(true);
      if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        return new UnicodeEncoding(false, true);
      if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        return new UnicodeEncoding(true, true);

      try
      {
        new UTF8Encoding(false, true).GetString(bytes);
        return new UTF8Encoding(false);
      }
      catch (DecoderFallbackException)
      {
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback,
          DecoderFallback.ExceptionFallback);
      }
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
        UpdateWindowTitle();
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
      if (newValue != null)
        UpdateSourceData(newValue, e.RowIndex, e.ColumnIndex);
    }

    private void UpdateSourceData(object newValue, int rowIndex, int columnIndex)
    {
      if (TreeViewSection.SelectedNode != null && newValue != null)
      {
        ConfigSection section = GetConfigSection(selectedSectionName);
        if (section == null)
          return;

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
      List<string> errors;
      List<string> warnings;
      ValidateConfig(out errors, out warnings);

      if (errors.Count > 0)
      {
        MessageBox.Show("The configuration contains errors and cannot be saved:\n\n" +
          String.Join("\n", errors.Take(20)), "Validation error",
          MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      if (warnings.Count > 0)
      {
        DialogResult warningResult = MessageBox.Show(
          "The configuration contains possible inconsistencies:\n\n" +
          String.Join("\n", warnings.Take(20)) +
          "\n\nSave anyway?",
          "Validation warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (warningResult != DialogResult.Yes)
          return;
      }

      if (SaveConfigFileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        try
        {
          WriteConfigFile(SaveConfigFileDlg.FileName);
          MessageBox.Show("Configuration saved successfully.", "Save complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
          MessageBox.Show("The configuration could not be saved.\n\n" + ex.Message,
            "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }
    }

    void WriteConfigFile(string outputFileName)
    {
      string fullOutputPath = Path.GetFullPath(outputFileName);
      string outputDirectory = Path.GetDirectoryName(fullOutputPath);
      string temporaryFile = Path.Combine(outputDirectory,
        "." + Path.GetFileName(fullOutputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

      try
      {
        WriteConfigContent(temporaryFile);
        if (File.Exists(fullOutputPath))
          File.Replace(temporaryFile, fullOutputPath, null, true);
        else
          File.Move(temporaryFile, fullOutputPath);
      }
      finally
      {
        if (File.Exists(temporaryFile))
          File.Delete(temporaryFile);
      }
    }

    void WriteConfigContent(string outputFileName)
    {
      string line = "";
      using (StreamWriter writer = new StreamWriter(outputFileName, false, configEncoding))
      {
        foreach (ConfigSection section in configSections)
        {
          if (section.IsSupported)
          {
            if (section.IsMasterTable)
            {
              writer.WriteLine(section.RawLines[0]);
              writer.WriteLine(section.RawLines[1]);
              writer.WriteLine("\t" + section.MasterTable.TableComment);

              foreach (CommonTable table in section.MasterTable.CommonTables)
              {
                line = table.ItemId + "\t" + table.RowsCount + "\t" + table.ColumnsCount;
                writer.WriteLine(line);
                foreach (TableRow row in table.Rows)
                {
                  writer.WriteLine(String.Join("\t", row.Values));
                }
                writer.WriteLine();
              }
            }
            else
            {
              writer.WriteLine(section.RawLines[0]);
              writer.WriteLine(section.RawLines[1]);
              writer.WriteLine(section.RawLines[2]);
              writer.WriteLine(section.RawLines[3]);
              writer.WriteLine();

              foreach (TableRow row in section.CommonTable.Rows)
              {
                writer.Write("                            \t");
                writer.WriteLine(String.Join("                            \t", row.Values));
              }
              writer.WriteLine();
            }
          }
          else
          {
            foreach (string item in section.RawLines)
              writer.WriteLine(item);
          }
        }
      }
    }

    void ValidateConfig(out List<string> errors, out List<string> warnings)
    {
      errors = new List<string>();
      warnings = new List<string>();

      foreach (ConfigSection section in supportedConfigSections)
      {
        if (section.IsMasterTable)
        {
          if (section.MasterTable == null)
          {
            errors.Add(section.Name + ": missing master table.");
            continue;
          }

          foreach (CommonTable table in section.MasterTable.CommonTables)
          {
            if (table.RowsCount != table.Rows.Count)
              errors.Add(section.Name + "[" + table.ItemId + "]: row count does not match the data.");

            foreach (TableRow row in table.Rows)
            {
              if (row.Values.Count != table.ColumnsCount)
                errors.Add(section.Name + "[" + table.ItemId + "]: expected " +
                  table.ColumnsCount + " values, found " + row.Values.Count + ".");
            }
          }
        }
        else
        {
          if (section.CommonTable == null)
          {
            errors.Add(section.Name + ": missing table.");
            continue;
          }

          if (section.CommonTable.RowsCount != section.CommonTable.Rows.Count)
            errors.Add(section.Name + ": row count does not match the data.");

          foreach (TableRow row in section.CommonTable.Rows)
          {
            if (row.Values.Count != section.CommonTable.ColumnsCount)
              errors.Add(section.Name + "[" + row.ItemId + "]: expected " +
                section.CommonTable.ColumnsCount + " values, found " + row.Values.Count + ".");
            else if (row.Values.Count > 0 && row.ItemId != row.Values[0])
              errors.Add(section.Name + "[" + row.ItemId + "]: item ID was changed inconsistently.");
          }
        }
      }

      ConfigSection factorySection = GetConfigSection("Factory");
      ConfigSection productSection = GetConfigSection("Product");
      if (factorySection == null || factorySection.CommonTable == null ||
          productSection == null || productSection.CommonTable == null)
        return;

      HashSet<string> factoryIds = new HashSet<string>(
        factorySection.CommonTable.Rows.Select(row => row.ItemId));
      HashSet<string> productIds = new HashSet<string>(
        productSection.CommonTable.Rows.Select(row => row.ItemId));

      ValidateProductMasterTable("AcceptProduct", factoryIds, productIds, false, errors, warnings);
      ValidateProductMasterTable("BuildingResources", factoryIds, productIds, false, errors, warnings);
      ValidateProductMasterTable("ProduceProduct", factoryIds, productIds, true, errors, warnings);
      ValidateMembers(factoryIds, errors, warnings);
      ValidateProductionLinks(factorySection, productSection, productIds, warnings);
    }

    void ValidateProductMasterTable(string sectionName, HashSet<string> factoryIds,
      HashSet<string> productIds, bool validateProductionValues, List<string> errors,
      List<string> warnings)
    {
      ConfigSection section = GetConfigSection(sectionName);
      if (section == null || section.MasterTable == null)
        return;

      foreach (CommonTable table in section.MasterTable.CommonTables)
      {
        if (!factoryIds.Contains(table.ItemId))
          errors.Add(sectionName + ": unknown factory ID " + table.ItemId + ".");

        HashSet<string> seenProducts = new HashSet<string>();
        foreach (TableRow row in table.Rows)
        {
          if (row.Values.Count == 0)
            continue;

          string productId = row.Values[0];
          if (!productIds.Contains(productId))
            errors.Add(sectionName + "[" + table.ItemId + "]: unknown product ID " + productId + ".");
          if (!seenProducts.Add(productId))
            warnings.Add(sectionName + "[" + table.ItemId + "]: duplicate product ID " + productId + ".");

          if (validateProductionValues && row.Values.Count == 4)
          {
            double minimum;
            double maximum;
            double resourcesLeft;
            if (!TryParseConfigNumber(row.Values[1], out minimum) ||
                !TryParseConfigNumber(row.Values[2], out maximum) ||
                !TryParseConfigNumber(row.Values[3], out resourcesLeft))
            {
              errors.Add(sectionName + "[" + table.ItemId + "]: invalid numeric production value.");
            }
            else if (minimum > maximum)
            {
              errors.Add(sectionName + "[" + table.ItemId + "]: minimum production is greater than maximum.");
            }
          }
        }
      }
    }

    void ValidateMembers(HashSet<string> factoryIds, List<string> errors, List<string> warnings)
    {
      ConfigSection members = GetConfigSection("Members");
      if (members == null || members.MasterTable == null)
        return;

      foreach (CommonTable table in members.MasterTable.CommonTables)
      {
        HashSet<string> seenFactories = new HashSet<string>();
        foreach (TableRow row in table.Rows)
        {
          if (row.Values.Count == 0)
            continue;
          string factoryId = row.Values[0];
          if (!factoryIds.Contains(factoryId))
            errors.Add("Members[" + table.ItemId + "]: unknown factory ID " + factoryId + ".");
          if (!seenFactories.Add(factoryId))
            warnings.Add("Members[" + table.ItemId + "]: duplicate factory ID " + factoryId + ".");
        }
      }
    }

    void ValidateProductionLinks(ConfigSection factorySection, ConfigSection productSection,
      HashSet<string> productIds, List<string> warnings)
    {
      ConfigSection accepts = GetConfigSection("AcceptProduct");
      ConfigSection produces = GetConfigSection("ProduceProduct");
      if (accepts == null || accepts.MasterTable == null ||
          produces == null || produces.MasterTable == null)
        return;

      int resourceNeededIndex = productSection.CommonTable.GetHeaderIndex("ResourceNeeded");
      int factoryIdIndex = productSection.CommonTable.GetHeaderIndex("FactoryID");
      if (resourceNeededIndex < 0 || factoryIdIndex < 0)
        return;

      Dictionary<string, TableRow> products = productSection.CommonTable.Rows
        .ToDictionary(row => row.ItemId);
      HashSet<string> factoryIds = new HashSet<string>(
        factorySection.CommonTable.Rows.Select(row => row.ItemId));

      foreach (CommonTable productionTable in produces.MasterTable.CommonTables)
      {
        CommonTable acceptedTable = accepts.MasterTable.GetTable(productionTable.ItemId);
        HashSet<string> acceptedProducts = acceptedTable == null
          ? new HashSet<string>()
          : new HashSet<string>(acceptedTable.Rows.Where(row => row.Values.Count > 0)
              .Select(row => row.Values[0]));

        foreach (TableRow productionRow in productionTable.Rows)
        {
          if (productionRow.Values.Count == 0 || !products.ContainsKey(productionRow.Values[0]))
            continue;

          TableRow product = products[productionRow.Values[0]];
          string resourceNeeded = product.Values[resourceNeededIndex];
          if (resourceNeeded != "-1" && productIds.Contains(resourceNeeded) &&
              !acceptedProducts.Contains(resourceNeeded))
          {
            warnings.Add("Factory " + productionTable.ItemId + " produces product " +
              productionRow.Values[0] + " requiring product " + resourceNeeded +
              ", but does not accept it.");
          }
        }
      }

      foreach (TableRow product in productSection.CommonTable.Rows)
      {
        string primaryFactoryId = product.Values[factoryIdIndex];
        string resourceNeeded = product.Values[resourceNeededIndex];
        if (resourceNeeded != "-1" && !productIds.Contains(resourceNeeded))
          warnings.Add("Product " + product.ItemId + " requires unknown product " +
            resourceNeeded + ".");

        if (!factoryIds.Contains(primaryFactoryId))
        {
          warnings.Add("Product " + product.ItemId + " points to unknown factory " +
            primaryFactoryId + ".");
          continue;
        }

        CommonTable primaryFactoryProduction = produces.MasterTable.GetTable(primaryFactoryId);
        if (primaryFactoryProduction == null || !primaryFactoryProduction.Rows.Any(row =>
              row.Values.Count > 0 && row.Values[0] == product.ItemId))
        {
          warnings.Add("Product " + product.ItemId + " points to factory " + primaryFactoryId +
            ", but that factory does not produce it.");
        }
      }
    }

    static bool TryParseConfigNumber(string value, out double result)
    {
      return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
        Double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
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

        if (selectedCommonTable.Rows.Any(row => row.Values.Count > 0 &&
            row.Values[0] == itemIdValue))
        {
          MessageBox.Show("This item is already present in the selected table.",
            "Duplicate item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }

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
      if (selectedCommonTable != null && e.ColumnIndex > 0 &&
          TableItemsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value != null)
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
      ConfigSection factorySection = GetConfigSection("Factory");
      ConfigSection productSection = GetConfigSection("Product");

      string[] factoryIds =
      {
        "1", "2", "3", "4",
        "5", "6", "7", "8",
        "9", "10", "11", "12",
        "13", "14", "15", "16",
        "17", "18", "19", "20",
        "21", "22", "23", "24",
        "25", "26", "27", "28",
        "29", "30", "31", "32",
        "33", "34", "35", "36",
        "37", "38", "39", "40",
        "41", "42", "43", "44",
        "45", "46", "47", "48",
        "49", "50", "51", "52",
        "53", "54", "55", "56",
        "57", "58", "59", "60",
        "61", "62", "63", "64",
        "65", "66", "67", "68",
        "69", "70", "71", "72",
        "73", "100", "101", "102",
        "103", "104", "105", "106",
        "107", "108", "109", "110",
        "111", "112", "113", "114",
        "115", "116", "117", "118",
        "119", "120", "121", "122",
        "123", "124", "125", "126",
        "127", "128", "129", "130",
        "131", "132", "133", "134",
        "135", "136", "137", "138",
        "139", "140", "141", "142",
        "143", "144", "145", "146",
        "147", "148", "149", "150",
        "151", "152", "153", "999"
      };
      string[] factoryNames =
      {
        "Iron ore mine", "Copper mine", "Gold mine", "Bauxit mine",
        "Coal mine", "Uranium mine", "Oil well", "Lumber camp",
        "Salt mine", "Gravel pit", "Quarry", "Waterworks",
        "Orchard", "Coffee plantation", "Sugar plantation", "Hop farm",
        "Olive grove", "Tobacco plantation", "Cotton Plantation", "Crop Farm",
        "Sheep farm", "Pig farm", "Cattle farm", "Chicken farm",
        "Distillery", "Steel Mill", "Sawmill", "Paper mill",
        "Foundry", "Aluminum smelter", "Jeweler", "Refinery",
        "Lab", "Cement factory", "Brickyard", "Ice factory",
        "Fish farm", "Coffee roastery", "Liquor factory", "Brewery",
        "Oil mill", "Tobacco factory", "Textile industry", "Slaughterhouse",
        "Dairy", "Food factory", "Tool factory", "Building materials industry",
        "Furniture factory", "Carpentry", "Printing house", "Household goods factory",
        "Electronics industry", "Paint factory", "Fertilizer plant", "Oil power plant",
        "Coal plant", "Nuclear power station", "Trash dump", "Trash incinerator",
        "Crocodile farm", "Ostrich farm", "Winery", "Sandpit",
        "Glassworks", "Solar cell industry", "Auto industry", "Brandy-Distillery",
        "Kangaroo breed", "Shoe factory", "Opalmine", "Jewelry industry",
        "Kiwi plantation", "Atomium", "Observation Tower", "Eiffel Tower",
        "Statue of Liberty", "Lincoln monument", "Neuschwanstein Castle", "Ferris wheel",
        "St. Stephen's Cathedral", "Taj Mahal", "The White House", "Akropolis",
        "Space Museum", "Colossus of Rhodes", "Mount St. Michel", "Pyramide",
        "Stonehenge", "Triumphal Arch", "Fort", "Space Center",
        "Space Center", "Space Center", "Space Center", "Botanical Garden",
        "Sports Stadium", "Olympic Swimming Stadium", "Olympic Games Athletics", "Olympic Football Stadium",
        "Olympic Fire", "Adventure Casino", "Zoological Garden", "Amusement park",
        "Observatory", "Biosphere", "Thermenhotel", "Casino",
        "Radio telescope", "Television tower", "Walking park", "Comic Park",
        "Opera house", "Supply station", "Funkstation", "Fusion power plant",
        "Fusion power plant", "Fusion power plant", "Fusion power plant", "Fusion power plant",
        "Fusion power plant", "World Exposition grounds", "World Exposition grounds", "World Exposition grounds",
        "World Exposition grounds", "World Exposition grounds", "World Exposition grounds", "City"
      };
      string[] productIds =
      {
        "1", "2", "3", "4",
        "5", "6", "7", "8",
        "9", "10", "11", "12",
        "13", "14", "15", "16",
        "17", "18", "19", "20",
        "21", "22", "23", "24",
        "25", "26", "27", "28",
        "29", "30", "31", "32",
        "33", "34", "35", "36",
        "37", "38", "39", "40",
        "41", "42", "43", "44",
        "45", "46", "47", "48",
        "49", "50", "51", "52",
        "53", "54", "55", "56",
        "61", "62", "63", "64",
        "65", "66", "67", "68",
        "69", "70", "71", "998",
        "999"
      };
      string[] productNames =
      {
        "Iron ore", "Copper ore", "Gold", "Bauxite",
        "Coal", "Uranium ore", "Oil", "Logs",
        "Salt", "Gravel", "Clay", "Water",
        "Fruit", "Coffee beans", "Sugarcane", "Hop",
        "Olives", "Tobacco", "Cotton", "Grain",
        "Wool", "Pigs", "Milk", "Egs",
        "Whisky", "Steel", "Wooden boards", "Paper",
        "Copper sheet", "Aluminium", "Jewellery", "Fuels",
        "Chemicals", "Cement", "Brick", "Blocks of ice",
        "Fish", "Coffee", "Rum", "Beer",
        "Olive oil", "Cigars", "Clothes", "Meat",
        "Cheese", "Foods", "Tool", "Construction materials",
        "Furniture", "Wooden houses", "Newspapers", "Household goods",
        "Electric devices", "Colors", "Fertilizer", "Rubbish",
        "Crocodile skin", "Leather boots", "Wine", "Quartz sand",
        "Glass", "Solar cells", "Solar car", "Brandy",
        "Opals", "Kiwis", "Jewellery (opals)", "Passengers",
        "Mail"
      };

      string validationError;
      if (!CanApplyNameTranslations(factorySection, factoryIds, factoryNames, out validationError) ||
          !CanApplyNameTranslations(productSection, productIds, productNames, out validationError))
      {
        MessageBox.Show(validationError, "Translation error",
          MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      int factoryNameIndex = factorySection.CommonTable.GetHeaderIndex("Name");
      int productNameIndex = productSection.CommonTable.GetHeaderIndex("Name");
      ApplyNameTranslations(factorySection.CommonTable, factoryNameIndex, factoryIds, factoryNames);
      ApplyNameTranslations(productSection.CommonTable, productNameIndex, productIds, productNames);

      TreeViewSection.SelectedNode = null;
      ReloadSectionsTree();
      DataGridSection.Rows.Clear();
      DataGridSection.Columns.Clear();
      TableItemsGrid.Rows.Clear();
      TableItemsGrid.Columns.Clear();
      AllItemsCmbBx.Items.Clear();
      selectedCommonTable = null;
      selectedSectionName = "";
      DetailsLbl.Text = "N/A";

      MessageBox.Show("Factory and product names were translated successfully.",
        "Translation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    bool CanApplyNameTranslations(ConfigSection section, string[] itemIds,
      string[] translatedNames, out string error)
    {
      if (section == null || section.CommonTable == null)
      {
        error = "The required Factory or Product section is missing.";
        return false;
      }

      if (itemIds.Length != translatedNames.Length)
      {
        error = section.Name + ": the translation table is invalid.";
        return false;
      }

      if (section.CommonTable.GetHeaderIndex("Name") < 0)
      {
        error = section.Name + ": the Name column is missing.";
        return false;
      }

      foreach (string itemId in itemIds)
      {
        if (section.CommonTable.GetRow(itemId) == null)
        {
          error = section.Name + ": item ID " + itemId + " is missing. No translations were applied.";
          return false;
        }
      }

      error = null;
      return true;
    }

    void ApplyNameTranslations(CommonTable table, int nameIndex, string[] itemIds,
      string[] translatedNames)
    {
      for (int i = 0; i < itemIds.Length; i++)
      {
        TableRow row = table.GetRow(itemIds[i]);
        row.Values[nameIndex] = ReplaceTranslatedName(row.Values[nameIndex], translatedNames[i]);
      }
    }

    static string ReplaceTranslatedName(string originalValue, string translatedName)
    {
      string escapedName = translatedName.Replace("\\", "\\\\").Replace("\"", "\\\"");
      if (String.IsNullOrEmpty(originalValue))
        return "\"" + escapedName + "\" \"\"";

      int openingQuote = originalValue.IndexOf('"');
      int closingQuote = openingQuote < 0 ? -1 : originalValue.IndexOf('"', openingQuote + 1);
      if (openingQuote < 0 || closingQuote < 0)
        return "\"" + escapedName + "\" \"\"";

      return originalValue.Substring(0, openingQuote) + "\"" + escapedName + "\"" +
        originalValue.Substring(closingQuote + 1);
    }

    private void SetValueBtn_Click(object sender, EventArgs e)
    {
      if (DataGridSection != null && DataGridSection.SelectedCells.Count > 0)
      {
        for (int i = 0; i < DataGridSection.SelectedCells.Count; i++)
        {
          DataGridViewCell cell = DataGridSection.SelectedCells[i];
          if (cell.ReadOnly)
            continue;

          cell.Value = CustomValueTxtBx.Text;
          UpdateSourceData(CustomValueTxtBx.Text, cell.RowIndex, cell.ColumnIndex);
        }
      }
    }

    private void PercentageBtn_Click(object sender, EventArgs e)
    {
      if (DataGridSection != null && DataGridSection.SelectedCells.Count > 0)
      {
        double percentage;
        if (!TryParseConfigNumber(CustomValueTxtBx.Text, out percentage))
        {
          MessageBox.Show("Enter a valid percentage.", "Invalid value",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }

        List<DataGridViewCell> editableCells = DataGridSection.SelectedCells
          .Cast<DataGridViewCell>().Where(cell => !cell.ReadOnly).ToList();
        List<double> sourceValues = new List<double>();

        foreach (DataGridViewCell cell in editableCells)
        {
          double value;
          if (cell.Value == null || !TryParseConfigNumber(cell.Value.ToString(), out value))
          {
            MessageBox.Show("One of the selected cells is not numeric.", "Invalid value",
              MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
          }
          sourceValues.Add(value);
        }

        for (int i = 0; i < editableCells.Count; i++)
        {
          DataGridViewCell cell = editableCells[i];
          double newValue = sourceValues[i] + (sourceValues[i] * (percentage / 100.0));
          string formattedValue = newValue.ToString("G15", CultureInfo.InvariantCulture);
          cell.Value = formattedValue;
          UpdateSourceData(formattedValue, cell.RowIndex, cell.ColumnIndex);
        }
      }
    }
  }
}
