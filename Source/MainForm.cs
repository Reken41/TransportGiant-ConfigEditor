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
    const string RecipeSectionName = "Production recipes";
    string selectedSectionName;
    string selectedRecipeFactoryId;
    CommonTable selectedCommonTable = null;
    bool refreshRequired = true;

    BackgroundWorker worker = new BackgroundWorker();
    string fileName = "";
    int lineCount = 0;
    Encoding configEncoding = new UTF8Encoding(false);
    string configLineEnding = Environment.NewLine;
    bool configEndsWithLineEnding = true;
    bool configHasPreamble = false;

    sealed class ConfigLoadResult
    {
      public List<ConfigSection> Sections { get; set; }
      public Encoding Encoding { get; set; }
      public string LineEnding { get; set; }
      public bool EndsWithLineEnding { get; set; }
      public bool HasPreamble { get; set; }
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
      DataGridSection.DataError += Grid_DataError;
      TableItemsGrid.DataError += Grid_DataError;
      UpdateWindowTitle();
    }

    void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
      e.ThrowException = false;
      MessageBox.Show("The entered value is not valid for this field.", "Invalid value",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
      configLineEnding = loadResult.LineEnding;
      configEndsWithLineEnding = loadResult.EndsWithLineEnding;
      configHasPreamble = loadResult.HasPreamble;
      supportedConfigSections.Clear();

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
      MainPrgBar.Value = Math.Max(MainPrgBar.Minimum,
        Math.Min(MainPrgBar.Maximum, e.ProgressPercentage));
      //StatusLbl.Text = "Linia: " + e.ProgressPercentage.ToString() + " / " + lineCount.ToString();
    }

    void worker_DoWork(object sender, DoWorkEventArgs e)
    {
      int currentLine = 0;
      if (e.Argument == null)
        throw new InvalidDataException("No configuration file was selected.");

      string wFileName = e.Argument.ToString();
      List<ConfigSection> configSectionsTemp = new List<ConfigSection>();
      if (!File.Exists(wFileName))
        throw new FileNotFoundException("The configuration file does not exist.", wFileName);

      Encoding detectedEncoding = DetectFileEncoding(wFileName);
      byte[] fileBytes = File.ReadAllBytes(wFileName);
      bool hasPreamble = HasEncodingPreamble(fileBytes, detectedEncoding);
      string fileText = DecodeFileText(fileBytes, detectedEncoding, hasPreamble);
      string lineEnding = DetectLineEnding(fileText);
      bool endsWithLineEnding = EndsWithLineEnding(fileText);
      List<string> fileLines = SplitFileLines(fileText, endsWithLineEnding);

      ConfigSection currentSection = null;
      Regex sectionRegex = new Regex(@"^([A-Za-z_][A-Za-z0-9_]{2,63})\b");
      foreach (string fileLine in fileLines)
      {
        currentLine++;
        Match result = sectionRegex.Match(fileLine ?? String.Empty);
        if (result.Success)
        {
          currentSection = new ConfigSection();
          currentSection.Name = result.Groups[1].Value;
          currentSection.RawLines.Add(fileLine);
          currentSection.IsSupported = Helper.IsSectionSupported(currentSection);
          currentSection.IsMasterTable = Helper.IsSectionMasterTable(currentSection);
          configSectionsTemp.Add(currentSection);
        }
        else if (currentSection != null)
        {
          currentSection.RawLines.Add(fileLine);
        }
        else
        {
          currentSection = new ConfigSection();
          currentSection.Name = String.Empty;
          currentSection.RawLines.Add(fileLine);
          configSectionsTemp.Add(currentSection);
        }
        worker.ReportProgress(Math.Min(lineCount, currentLine));
      }

      foreach (ConfigSection section in configSectionsTemp)
      {
        if (section.IsSupported)
        {
          try
          {
            if (section.IsMasterTable)
              ParseMasterSection(section);
            else
              ParseCommonSection(section);
          }
          catch (Exception ex)
          {
            throw new InvalidDataException("Section " + section.Name + ": " + ex.Message, ex);
          }
        }

        currentLine += section.RawLines.Count;
        worker.ReportProgress(Math.Min(lineCount * 2, currentLine));
      }

      e.Result = new ConfigLoadResult
      {
        Sections = configSectionsTemp,
        Encoding = detectedEncoding,
        LineEnding = lineEnding,
        EndsWithLineEnding = endsWithLineEnding,
        HasPreamble = hasPreamble
      };
    }

    static void ParseMasterSection(ConfigSection section)
    {
      if (section.RawLines == null || section.RawLines.Count < 3)
        throw new InvalidDataException("incomplete master table header.");

      section.MasterTable = new MasterTable();
      section.MasterTable.TableId = section.RawLines[1].Trim();
      section.MasterTable.TableComment = section.RawLines[2].Trim();

      for (int i = 3; i < section.RawLines.Count; i++)
      {
        string line = section.RawLines[i];
        if (String.IsNullOrWhiteSpace(line))
          continue;

        string[] values = Regex.Split(line.Trim(), @"\s+");
        int rowsCount;
        int columnsCount;
        if (values.Length < 3 || !Int32.TryParse(values[1], NumberStyles.Integer,
              CultureInfo.InvariantCulture, out rowsCount) ||
            !Int32.TryParse(values[2], NumberStyles.Integer,
              CultureInfo.InvariantCulture, out columnsCount) ||
            rowsCount < 0 || columnsCount < 0)
          throw new InvalidDataException("invalid table header at local line " + (i + 1) + ".");

        CommonTable table = new CommonTable();
        table.ItemId = values[0];
        table.RowsCount = rowsCount;
        table.ColumnsCount = columnsCount;
        table.RawHeaderLineIndex = i;

        for (int rowIndex = 0; rowIndex < rowsCount; rowIndex++)
        {
          i++;
          if (i >= section.RawLines.Count || String.IsNullOrWhiteSpace(section.RawLines[i]))
            throw new InvalidDataException("missing row " + (rowIndex + 1) +
              " in table " + table.ItemId + ".");

          TableRow row = new TableRow();
          row.ItemId = table.ItemId;
          row.RawLineIndex = i;
          row.Values.AddRange(Regex.Split(section.RawLines[i].Trim(), @"\s+"));
          table.Rows.Add(row);
        }

        section.MasterTable.CommonTables.Add(table);
      }
    }

    static void ParseCommonSection(ConfigSection section)
    {
      if (section.RawLines == null || section.RawLines.Count < 4)
        throw new InvalidDataException("incomplete table header.");

      string[] headerParts = section.RawLines[0].Split('\t');
      string[] tableParts = Regex.Split(headerParts[0].Trim(), @"\s+");
      int declaredColumns;
      int rowsCount;
      if (tableParts.Length < 5 ||
          !Int32.TryParse(tableParts[2], NumberStyles.Integer, CultureInfo.InvariantCulture,
            out declaredColumns) ||
          !Int32.TryParse(tableParts[3], NumberStyles.Integer, CultureInfo.InvariantCulture,
            out rowsCount) || declaredColumns < 0 || rowsCount < 0)
        throw new InvalidDataException("invalid common table header.");

      CommonTable table = new CommonTable();
      table.ItemId = tableParts[1];
      table.ColumnsCount = declaredColumns + 1;
      table.RowsCount = rowsCount;
      table.UnknownValue = tableParts[4];
      table.TableComment = section.RawLines[3];
      table.RawHeaderLineIndex = 0;
      table.ColumnsHeaders.Add("ItemId");

      if (headerParts.Length < table.ColumnsCount)
        throw new InvalidDataException("column names are missing from the header.");
      for (int i = 0; i < table.ColumnsCount - 1; i++)
        table.ColumnsHeaders.Add(headerParts[i + 1].Trim());

      for (int i = 4; i < section.RawLines.Count; i++)
      {
        string line = section.RawLines[i];
        if (String.IsNullOrWhiteSpace(line))
          continue;

        string[] values = line.Trim().Split('\t');
        if (values.Length == 0 || String.IsNullOrWhiteSpace(values[0]))
          throw new InvalidDataException("row without an item ID at local line " + (i + 1) + ".");

        TableRow row = new TableRow();
        row.RawLineIndex = i;
        row.ItemId = values[0].Trim();
        row.Values.Add(row.ItemId);
        for (int valueIndex = 1; valueIndex < values.Length; valueIndex++)
          row.Values.Add(values[valueIndex].Trim());
        table.Rows.Add(row);
      }

      section.CommonTable = table;
    }

    static bool HasEncodingPreamble(byte[] bytes, Encoding encoding)
    {
      if (bytes == null || encoding == null)
        return false;
      byte[] preamble = encoding.GetPreamble();
      if (preamble == null || preamble.Length == 0 || bytes.Length < preamble.Length)
        return false;
      for (int i = 0; i < preamble.Length; i++)
        if (bytes[i] != preamble[i])
          return false;
      return true;
    }

    static string DecodeFileText(byte[] bytes, Encoding encoding, bool hasPreamble)
    {
      int offset = hasPreamble ? encoding.GetPreamble().Length : 0;
      return encoding.GetString(bytes, offset, bytes.Length - offset);
    }

    static string DetectLineEnding(string text)
    {
      if (text != null)
      {
        int crlf = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (crlf >= 0)
          return "\r\n";
        if (text.IndexOf('\n') >= 0)
          return "\n";
        if (text.IndexOf('\r') >= 0)
          return "\r";
      }
      return Environment.NewLine;
    }

    static bool EndsWithLineEnding(string text)
    {
      return !String.IsNullOrEmpty(text) &&
        (text.EndsWith("\r\n", StringComparison.Ordinal) ||
         text.EndsWith("\n", StringComparison.Ordinal) ||
         text.EndsWith("\r", StringComparison.Ordinal));
    }

    static List<string> SplitFileLines(string text, bool endsWithLineEnding)
    {
      List<string> lines = Regex.Split(text ?? String.Empty, "\r\n|\n|\r").ToList();
      if (endsWithLineEnding && lines.Count > 0 && lines[lines.Count - 1].Length == 0)
        lines.RemoveAt(lines.Count - 1);
      return lines;
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

      if (worker.IsBusy)
      {
        MessageBox.Show("A configuration file is already being loaded.", "Load in progress",
          MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      if (String.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
      {
        SaveConfigBtn.Enabled = false;
        if (!String.IsNullOrWhiteSpace(fileName))
          MessageBox.Show("The selected configuration file does not exist.", "Load error",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      try
      {
        Encoding countEncoding = DetectFileEncoding(fileName);
        using (var reader = new StreamReader(fileName, countEncoding, true))
        {
          while (reader.ReadLine() != null)
            lineCount++;
        }
        MainPrgBar.Maximum = Math.Max(1, lineCount * 2);
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
        selectedRecipeFactoryId = null;
        supportedConfigSections.Clear();

        worker.RunWorkerAsync(fileName);
        UpdateWindowTitle();
      }
      catch (Exception ex)
      {
        SaveConfigBtn.Enabled = false;
        MessageBox.Show("The configuration could not be opened.\n\n" + ex.Message,
          "Load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
      fileName = Properties.Settings.Default.LastConfigFile;
      LoadConfigFile();
    }

    private void DataGridSection_CellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
      if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
          e.RowIndex >= DataGridSection.Rows.Count ||
          e.ColumnIndex >= DataGridSection.Columns.Count)
        return;

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
          return;
        }
        else
        {
          if (section.CommonTable == null || rowIndex < 0 ||
              rowIndex >= section.CommonTable.Rows.Count ||
              section.CommonTable.Rows[rowIndex] == null || columnIndex < 0 ||
              columnIndex >= section.CommonTable.ColumnsCount ||
              columnIndex >= section.CommonTable.ColumnsHeaders.Count ||
              columnIndex >= section.CommonTable.Rows[rowIndex].Values.Count)
            return;

          string value = newValue.ToString().Trim();
          string valueError;
          if (!TryValidateCommonCellValue(section, columnIndex, value, out valueError))
          {
            MessageBox.Show(valueError, "Invalid value", MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
            DataGridSection.Rows[rowIndex].Cells[columnIndex].Value =
              section.CommonTable.Rows[rowIndex].Values[columnIndex];
            return;
          }
          if (section.Name == "Product" &&
              section.CommonTable.ColumnsHeaders[columnIndex] == "ResourceNeeded")
          {
            string error;
            if (!TrySetProductRequiredInput(section.CommonTable.Rows[rowIndex].ItemId,
                  value, out error))
            {
              MessageBox.Show(error, "Invalid recipe", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
              DataGridSection.Rows[rowIndex].Cells[columnIndex].Value =
                section.CommonTable.Rows[rowIndex].Values[columnIndex];
              return;
            }
            RefreshCurrentView();
            return;
          }

          section.CommonTable.Rows[rowIndex].Values[columnIndex] = value;
          section.IsDirty = true;
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

    static bool TryValidateCommonCellValue(ConfigSection section, int columnIndex,
      string value, out string error)
    {
      error = null;
      if (section == null || section.CommonTable == null || columnIndex <= 0 ||
          section.RawLines == null || section.RawLines.Count < 2)
        return true;
      string[] typeColumns = section.RawLines[1].Split('\t');
      if (columnIndex >= typeColumns.Length)
        return true;
      string valueType = typeColumns[columnIndex].Trim();
      if (valueType == "MASTER_VALUE")
      {
        double parsed;
        if (!TryParseConfigNumber(value, out parsed))
        {
          error = "This field requires a valid number.";
          return false;
        }
      }
      else if (valueType == "MASTER_FLAG")
      {
        int parsed;
        if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
              out parsed) || (parsed != 0 && parsed != 1))
        {
          error = "This flag accepts only 0 or 1.";
          return false;
        }
      }
      return true;
    }

    void WriteConfigFile(string outputFileName)
    {
      string fullOutputPath = Path.GetFullPath(outputFileName);
      string outputDirectory = Path.GetDirectoryName(fullOutputPath);
      if (String.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        throw new DirectoryNotFoundException("The destination directory does not exist.");
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
      List<string> outputLines = new List<string>();
      foreach (ConfigSection section in configSections)
      {
        if (!section.IsSupported || !section.IsDirty)
        {
          outputLines.AddRange(section.RawLines);
          continue;
        }

        outputLines.AddRange(SerializeModifiedSection(section));
      }

      string text = String.Join(configLineEnding, outputLines);
      if (configEndsWithLineEnding)
        text += configLineEnding;

      byte[] content = configEncoding.GetBytes(text);
      byte[] preamble = configHasPreamble ? configEncoding.GetPreamble() : new byte[0];
      if (preamble.Length == 0)
      {
        File.WriteAllBytes(outputFileName, content);
        return;
      }

      byte[] complete = new byte[preamble.Length + content.Length];
      Buffer.BlockCopy(preamble, 0, complete, 0, preamble.Length);
      Buffer.BlockCopy(content, 0, complete, preamble.Length, content.Length);
      File.WriteAllBytes(outputFileName, complete);
    }

    static IEnumerable<string> SerializeModifiedSection(ConfigSection section)
    {
      List<string> lines = new List<string>();
      if (section.IsMasterTable)
      {
        if (section.MasterTable == null || section.RawLines.Count < 3)
          throw new InvalidDataException(section.Name + ": the table cannot be serialized.");

        lines.Add(section.RawLines[0]);
        lines.Add(section.RawLines[1]);
        lines.Add("\t" + section.MasterTable.TableComment);
        foreach (CommonTable table in section.MasterTable.CommonTables)
        {
          table.RowsCount = table.Rows.Count;
          table.ColumnsCount = table.RowsCount == 0 ? 0 : table.Rows[0].Values.Count;
          lines.Add(table.ItemId + "\t" + table.RowsCount + "\t" + table.ColumnsCount);
          foreach (TableRow row in table.Rows)
            lines.Add(String.Join("\t", row.Values));
          lines.Add(String.Empty);
        }
      }
      else
      {
        if (section.CommonTable == null || section.RawLines.Count < 4)
          throw new InvalidDataException(section.Name + ": the table cannot be serialized.");

        lines.Add(section.RawLines[0]);
        lines.Add(section.RawLines[1]);
        lines.Add(section.RawLines[2]);
        lines.Add(section.RawLines[3]);
        lines.Add(String.Empty);
        section.CommonTable.RowsCount = section.CommonTable.Rows.Count;
        foreach (TableRow row in section.CommonTable.Rows)
          lines.Add("                            \t" +
            String.Join("                            \t", row.Values));
        lines.Add(String.Empty);
      }
      return lines;
    }

    void ValidateConfig(out List<string> errors, out List<string> warnings)
    {
      errors = new List<string>();
      warnings = new List<string>();

      if (supportedConfigSections == null || supportedConfigSections.Count == 0)
      {
        errors.Add("No supported configuration sections were loaded.");
        return;
      }

      HashSet<string> sectionNames = new HashSet<string>(StringComparer.Ordinal);

      foreach (ConfigSection section in supportedConfigSections)
      {
        if (section == null || String.IsNullOrWhiteSpace(section.Name))
        {
          errors.Add("A supported section has no name.");
          continue;
        }
        if (!sectionNames.Add(section.Name))
          errors.Add("Duplicate section " + section.Name + ".");

        if (section.IsMasterTable)
        {
          if (section.MasterTable == null)
          {
            errors.Add(section.Name + ": missing master table.");
            continue;
          }

          HashSet<string> tableIds = new HashSet<string>(StringComparer.Ordinal);
          foreach (CommonTable table in section.MasterTable.CommonTables)
          {
            if (table == null)
            {
              errors.Add(section.Name + ": null table entry.");
              continue;
            }
            if (!IsValidIdentifier(table.ItemId, false))
              errors.Add(section.Name + ": invalid table ID '" + table.ItemId + "'.");
            if (!tableIds.Add(table.ItemId ?? String.Empty))
              errors.Add(section.Name + ": duplicate table ID " + table.ItemId + ".");
            if (table.ColumnsCount < 0)
              errors.Add(section.Name + "[" + table.ItemId + "]: negative column count.");
            if (table.RowsCount != table.Rows.Count)
              errors.Add(section.Name + "[" + table.ItemId + "]: row count does not match the data.");

            foreach (TableRow row in table.Rows)
            {
              if (row == null)
              {
                errors.Add(section.Name + "[" + table.ItemId + "]: null row.");
                continue;
              }
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

          if (section.CommonTable.ColumnsCount != section.CommonTable.ColumnsHeaders.Count)
            errors.Add(section.Name + ": column count does not match the header.");
          if (section.CommonTable.ColumnsHeaders.GroupBy(header => header,
                StringComparer.Ordinal).Any(group => group.Count() > 1))
            errors.Add(section.Name + ": duplicate column name in the header.");
          if (section.CommonTable.RowsCount != section.CommonTable.Rows.Count)
            errors.Add(section.Name + ": row count does not match the data.");

          HashSet<string> rowIds = new HashSet<string>(StringComparer.Ordinal);
          foreach (TableRow row in section.CommonTable.Rows)
          {
            if (row == null)
            {
              errors.Add(section.Name + ": null row.");
              continue;
            }
            if (!IsValidIdentifier(row.ItemId, false))
              errors.Add(section.Name + ": invalid item ID '" + row.ItemId + "'.");
            if (!rowIds.Add(row.ItemId ?? String.Empty))
              errors.Add(section.Name + ": duplicate item ID " + row.ItemId + ".");
            if (row.Values.Count != section.CommonTable.ColumnsCount)
              errors.Add(section.Name + "[" + row.ItemId + "]: expected " +
                section.CommonTable.ColumnsCount + " values, found " + row.Values.Count + ".");
            else if (row.Values.Count > 0 && row.ItemId != row.Values[0])
              errors.Add(section.Name + "[" + row.ItemId + "]: item ID was changed inconsistently.");
          }
          ValidateCommonValueTypes(section, errors);
        }
      }

      ConfigSection factorySection = GetConfigSection("Factory");
      ConfigSection productSection = GetConfigSection("Product");
      if (factorySection == null || factorySection.CommonTable == null ||
          productSection == null || productSection.CommonTable == null)
      {
        errors.Add("The Factory and Product sections are required.");
        return;
      }

      RequireColumn(factorySection, "Name", errors);
      RequireColumn(productSection, "Name", errors);
      RequireColumn(productSection, "FactoryID", errors);
      RequireColumn(productSection, "ResourceNeeded", errors);

      foreach (string requiredMaster in new[] { "AcceptProduct", "ProduceProduct" })
      {
        ConfigSection required = GetConfigSection(requiredMaster);
        if (required == null || required.MasterTable == null)
          errors.Add("The " + requiredMaster + " section is required for production editing.");
      }

      HashSet<string> factoryIds = new HashSet<string>(
        factorySection.CommonTable.Rows.Where(row => row != null).Select(row => row.ItemId));
      HashSet<string> productIds = new HashSet<string>(
        productSection.CommonTable.Rows.Where(row => row != null).Select(row => row.ItemId));

      ValidateProductMasterTable("AcceptProduct", factoryIds, productIds, false, errors, warnings);
      ValidateProductMasterTable("BuildingResources", factoryIds, productIds, false, errors, warnings);
      ValidateProductMasterTable("ProduceProduct", factoryIds, productIds, true, errors, warnings);
      ValidateMembers(factoryIds, errors, warnings);
      ValidateTerminalTables(errors);
      ValidateProductionLinks(factorySection, productSection, productIds, errors, warnings);
    }

    static void RequireColumn(ConfigSection section, string columnName, List<string> errors)
    {
      if (section != null && section.CommonTable != null &&
          section.CommonTable.GetHeaderIndex(columnName) < 0)
        errors.Add(section.Name + ": required column " + columnName + " is missing.");
    }

    static void ValidateCommonValueTypes(ConfigSection section, List<string> errors)
    {
      if (section == null || section.CommonTable == null || section.RawLines == null ||
          section.RawLines.Count < 2)
        return;
      string[] typeColumns = section.RawLines[1].Split('\t');
      foreach (TableRow row in section.CommonTable.Rows)
      {
        if (row == null)
          continue;
        int valueCount = Math.Min(row.Values.Count, section.CommonTable.ColumnsCount);
        for (int valueIndex = 1; valueIndex < valueCount; valueIndex++)
        {
          if (valueIndex >= typeColumns.Length)
            continue;
          string valueType = typeColumns[valueIndex].Trim();
          string value = row.Values[valueIndex];
          if (valueType == "MASTER_VALUE")
          {
            double parsed;
            if (!TryParseConfigNumber(value, out parsed))
            {
              string columnName = valueIndex < section.CommonTable.ColumnsHeaders.Count
                ? section.CommonTable.ColumnsHeaders[valueIndex] : "column " + valueIndex;
              errors.Add(section.Name + "[" + row.ItemId + "]." +
                columnName + ": invalid number '" +
                value + "'.");
            }
          }
          else if (valueType == "MASTER_FLAG")
          {
            int parsed;
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                  out parsed) || (parsed != 0 && parsed != 1))
            {
              string columnName = valueIndex < section.CommonTable.ColumnsHeaders.Count
                ? section.CommonTable.ColumnsHeaders[valueIndex] : "column " + valueIndex;
              errors.Add(section.Name + "[" + row.ItemId + "]." +
                columnName + ": a flag must be 0 or 1.");
            }
          }
        }
      }
    }

    static bool IsValidIdentifier(string value, bool allowMinusOne)
    {
      int identifier;
      if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out identifier))
        return false;
      return identifier >= 0 || (allowMinusOne && identifier == -1);
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
        if (table == null)
          continue;
        int expectedColumns = sectionName == "ProduceProduct" ? 4 :
          (sectionName == "BuildingResources" ? 2 : 1);
        if (table.Rows.Count > 0 && table.ColumnsCount != expectedColumns)
          errors.Add(sectionName + "[" + table.ItemId + "]: expected " +
            expectedColumns + " columns.");
        if (!factoryIds.Contains(table.ItemId))
          errors.Add(sectionName + ": unknown factory ID " + table.ItemId + ".");

        HashSet<string> seenProducts = new HashSet<string>();
        foreach (TableRow row in table.Rows)
        {
          if (row == null)
            continue;
          if (row.Values.Count == 0)
            continue;

          string productId = row.Values[0];
          if (!IsValidIdentifier(productId, false))
            errors.Add(sectionName + "[" + table.ItemId + "]: invalid product ID '" +
              productId + "'.");
          if (!productIds.Contains(productId))
            errors.Add(sectionName + "[" + table.ItemId + "]: unknown product ID " + productId + ".");
          if (!seenProducts.Add(productId))
            errors.Add(sectionName + "[" + table.ItemId + "]: duplicate product ID " + productId + ".");

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
            else if (minimum < 0 || maximum < 0)
            {
              errors.Add(sectionName + "[" + table.ItemId + "]: production values cannot be negative.");
            }
          }
          else if (sectionName == "BuildingResources" && row.Values.Count == 2)
          {
            double amount;
            if (!TryParseConfigNumber(row.Values[1], out amount) || amount < 0)
              errors.Add(sectionName + "[" + table.ItemId + "]: invalid resource amount.");
          }
        }
      }
    }

    void ValidateMembers(HashSet<string> factoryIds, List<string> errors, List<string> warnings)
    {
      ConfigSection members = GetConfigSection("Members");
      if (members == null || members.MasterTable == null)
        return;

      ConfigSection factoryLines = GetConfigSection("FactoryLines");
      HashSet<string> factoryLineIds = factoryLines == null || factoryLines.CommonTable == null
        ? new HashSet<string>() : new HashSet<string>(factoryLines.CommonTable.Rows
            .Where(row => row != null).Select(row => row.ItemId));

      foreach (CommonTable table in members.MasterTable.CommonTables)
      {
        if (table == null)
          continue;
        if (!factoryLineIds.Contains(table.ItemId))
          errors.Add("Members: unknown FactoryLines ID " + table.ItemId + ".");
        if (table.Rows.Count > 0 && table.ColumnsCount != 1)
          errors.Add("Members[" + table.ItemId + "]: expected one factory ID per row.");
        HashSet<string> seenFactories = new HashSet<string>();
        foreach (TableRow row in table.Rows)
        {
          if (row == null)
            continue;
          if (row.Values.Count == 0)
            continue;
          string factoryId = row.Values[0];
          if (!factoryIds.Contains(factoryId))
            errors.Add("Members[" + table.ItemId + "]: unknown factory ID " + factoryId + ".");
          if (!seenFactories.Add(factoryId))
            errors.Add("Members[" + table.ItemId + "]: duplicate factory ID " + factoryId + ".");
        }
      }
    }

    void ValidateTerminalTables(List<string> errors)
    {
      foreach (ConfigSection section in supportedConfigSections.Where(item => item != null &&
          item.Name.StartsWith("TerminalPlatformLimit", StringComparison.Ordinal)))
      {
        if (section.MasterTable == null)
          continue;
        foreach (CommonTable table in section.MasterTable.CommonTables)
        {
          if (table == null)
            continue;
          if (table.Rows.Count > 0 && table.ColumnsCount != 1)
            errors.Add(section.Name + "[" + table.ItemId + "]: expected one value per row.");
          foreach (TableRow row in table.Rows)
          {
            double value;
            if (row == null || row.Values.Count != 1 ||
                !TryParseConfigNumber(row.Values[0], out value) || value < 0)
              errors.Add(section.Name + "[" + table.ItemId + "]: invalid platform limit.");
          }
        }
      }
    }

    void ValidateProductionLinks(ConfigSection factorySection, ConfigSection productSection,
      HashSet<string> productIds, List<string> errors, List<string> warnings)
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
        .Where(row => row != null && !String.IsNullOrEmpty(row.ItemId))
        .GroupBy(row => row.ItemId).ToDictionary(group => group.Key, group => group.First());
      HashSet<string> factoryIds = new HashSet<string>(
        factorySection.CommonTable.Rows.Where(row => row != null).Select(row => row.ItemId));

      foreach (CommonTable productionTable in produces.MasterTable.CommonTables)
      {
        if (productionTable == null)
          continue;
        CommonTable acceptedTable = accepts.MasterTable.GetTable(productionTable.ItemId);
        HashSet<string> acceptedProducts = acceptedTable == null
          ? new HashSet<string>()
          : new HashSet<string>(acceptedTable.Rows.Where(row => row != null && row.Values.Count > 0)
              .Select(row => row.Values[0]));

        foreach (TableRow productionRow in productionTable.Rows)
        {
          if (productionRow == null || productionRow.Values.Count == 0 ||
              !products.ContainsKey(productionRow.Values[0]))
            continue;

          TableRow product = products[productionRow.Values[0]];
          if (resourceNeededIndex >= product.Values.Count)
          {
            errors.Add("Product " + product.ItemId + ": ResourceNeeded value is missing.");
            continue;
          }
          string resourceNeeded = product.Values[resourceNeededIndex];
          if (resourceNeeded != "-1" && productIds.Contains(resourceNeeded) &&
              !acceptedProducts.Contains(resourceNeeded))
          {
            string message = "Factory " + productionTable.ItemId + " produces product " +
              productionRow.Values[0] + " requiring product " + resourceNeeded +
              ", but does not accept it.";
            if (productionTable.IsDirty ||
                (acceptedTable != null && acceptedTable.IsDirty))
              errors.Add(message);
            else
              warnings.Add(message);
          }
        }
      }

      foreach (TableRow product in productSection.CommonTable.Rows)
      {
        if (product == null || factoryIdIndex >= product.Values.Count ||
            resourceNeededIndex >= product.Values.Count)
          continue;
        string primaryFactoryId = product.Values[factoryIdIndex];
        string resourceNeeded = product.Values[resourceNeededIndex];
        if (!IsValidIdentifier(primaryFactoryId, true))
          errors.Add("Product " + product.ItemId + " has invalid factory ID '" +
            primaryFactoryId + "'.");
        if (!IsValidIdentifier(resourceNeeded, true))
          errors.Add("Product " + product.ItemId + " has invalid ResourceNeeded ID '" +
            resourceNeeded + "'.");
        if (resourceNeeded == product.ItemId)
          errors.Add("Product " + product.ItemId + " cannot require itself.");
        if (resourceNeeded != "-1" && !productIds.Contains(resourceNeeded))
          errors.Add("Product " + product.ItemId + " requires unknown product " +
            resourceNeeded + ".");

        if (primaryFactoryId == "-1")
          continue;
        if (!factoryIds.Contains(primaryFactoryId))
        {
          errors.Add("Product " + product.ItemId + " points to unknown factory " +
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

      ValidateRecipeCycles(products, resourceNeededIndex, errors);
    }

    static void ValidateRecipeCycles(Dictionary<string, TableRow> products,
      int resourceNeededIndex, List<string> errors)
    {
      foreach (string productId in products.Keys)
      {
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        string current = productId;
        bool cycleDetected = false;
        while (products.ContainsKey(current) && visited.Add(current))
        {
          TableRow row = products[current];
          if (resourceNeededIndex < 0 || resourceNeededIndex >= row.Values.Count)
            break;
          string next = row.Values[resourceNeededIndex];
          if (next == "-1" || !products.ContainsKey(next))
            break;
          if (visited.Contains(next))
          {
            cycleDetected = true;
            break;
          }
          current = next;
        }
        if (cycleDetected)
          errors.Add("Production recipe cycle detected for product " + productId + ".");
      }
    }

    static bool TryParseConfigNumber(string value, out double result)
    {
      bool parsed = Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
        out result) || Double.TryParse(value, NumberStyles.Float,
        CultureInfo.CurrentCulture, out result);
      return parsed && !Double.IsNaN(result) && !Double.IsInfinity(result);
    }

    bool TrySetProductRequiredInput(string productId, string requiredInputId,
      out string error)
    {
      error = null;
      ConfigSection products = GetConfigSection("Product");
      ConfigSection produces = GetConfigSection("ProduceProduct");
      ConfigSection accepts = GetConfigSection("AcceptProduct");
      if (products == null || products.CommonTable == null || produces == null ||
          produces.MasterTable == null || accepts == null || accepts.MasterTable == null)
      {
        error = "Product, ProduceProduct or AcceptProduct data is missing.";
        return false;
      }

      TableRow product = products.CommonTable.GetRow(productId);
      int resourceIndex = products.CommonTable.GetHeaderIndex("ResourceNeeded");
      if (product == null || resourceIndex < 0 || resourceIndex >= product.Values.Count)
      {
        error = "Product " + productId + " or its ResourceNeeded field is missing.";
        return false;
      }
      if (!IsValidIdentifier(requiredInputId, true) ||
          (requiredInputId != "-1" && products.CommonTable.GetRow(requiredInputId) == null))
      {
        error = "Required input ID " + requiredInputId + " is invalid or unknown.";
        return false;
      }
      if (requiredInputId == productId)
      {
        error = "A product cannot require itself.";
        return false;
      }
      if (WouldCreateRecipeCycle(productId, requiredInputId, products.CommonTable,
          resourceIndex))
      {
        error = "This change would create a production recipe cycle.";
        return false;
      }

      string oldInputId = product.Values[resourceIndex];
      product.Values[resourceIndex] = requiredInputId;
      products.IsDirty = true;

      foreach (CommonTable production in produces.MasterTable.CommonTables)
      {
        if (production == null || !production.Rows.Any(row => row != null &&
            row.Values.Count > 0 && row.Values[0] == productId))
          continue;
        CommonTable accepted = GetOrCreateMasterTable(accepts, production.ItemId);
        int acceptedRowsBefore = accepted.Rows.Count;
        if (requiredInputId != "-1" && !accepted.Rows.Any(row => row != null &&
            row.Values.Count > 0 && row.Values[0] == requiredInputId))
        {
          TableRow acceptedRow = new TableRow();
          acceptedRow.ItemId = accepted.ItemId;
          acceptedRow.Values.Add(requiredInputId);
          accepted.Rows.Add(acceptedRow);
        }
        RemoveUnusedAcceptedInput(accepted, production, oldInputId,
          products.CommonTable, resourceIndex);
        UpdateTableDimensions(accepted);
        if (accepted.Rows.Count != acceptedRowsBefore)
          accepted.IsDirty = true;
      }
      accepts.IsDirty = true;
      return true;
    }

    static bool WouldCreateRecipeCycle(string productId, string requiredInputId,
      CommonTable products, int resourceIndex)
    {
      HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
      string current = requiredInputId;
      while (current != "-1" && visited.Add(current))
      {
        if (current == productId)
          return true;
        TableRow row = products.GetRow(current);
        if (row == null || resourceIndex < 0 || resourceIndex >= row.Values.Count)
          return false;
        current = row.Values[resourceIndex];
      }
      return current != "-1";
    }

    static CommonTable GetOrCreateMasterTable(ConfigSection section, string itemId)
    {
      CommonTable table = section.MasterTable.GetTable(itemId);
      if (table != null)
        return table;
      table = new CommonTable();
      table.ItemId = itemId;
      table.RowsCount = 0;
      table.ColumnsCount = 0;
      section.MasterTable.CommonTables.Add(table);
      return table;
    }

    static void RemoveUnusedAcceptedInput(CommonTable accepted, CommonTable production,
      string inputId, CommonTable products, int resourceIndex)
    {
      if (accepted == null || production == null || inputId == "-1" ||
          String.IsNullOrWhiteSpace(inputId))
        return;
      bool stillNeeded = production.Rows.Any(row =>
      {
        if (row == null || row.Values.Count == 0)
          return false;
        TableRow output = products.GetRow(row.Values[0]);
        return output != null && resourceIndex >= 0 && resourceIndex < output.Values.Count &&
          output.Values[resourceIndex] == inputId;
      });
      if (!stillNeeded)
        accepted.Rows.RemoveAll(row => row != null && row.Values.Count > 0 &&
          row.Values[0] == inputId);
    }

    static void UpdateTableDimensions(CommonTable table)
    {
      if (table == null)
        return;
      table.RowsCount = table.Rows.Count;
      TableRow firstRow = table.Rows.FirstOrDefault(row => row != null);
      table.ColumnsCount = firstRow == null ? 0 : firstRow.Values.Count;
    }

    bool AddRecipeProduct(string factoryId, string productId, out string error)
    {
      error = null;
      ConfigSection products = GetConfigSection("Product");
      ConfigSection produces = GetConfigSection("ProduceProduct");
      if (products == null || products.CommonTable == null || produces == null ||
          produces.MasterTable == null || products.CommonTable.GetRow(productId) == null)
      {
        error = "The selected factory or product is unavailable.";
        return false;
      }
      CommonTable production = produces.MasterTable.GetTable(factoryId);
      if (production == null)
      {
        error = "Factory " + factoryId + " has no production table.";
        return false;
      }
      if (production.Rows.Any(row => row != null && row.Values.Count > 0 &&
          row.Values[0] == productId))
      {
        error = "This product is already produced by the selected factory.";
        return false;
      }

      TableRow product = products.CommonTable.GetRow(productId);
      int factoryIndex = products.CommonTable.GetHeaderIndex("FactoryID");
      int resourceIndex = products.CommonTable.GetHeaderIndex("ResourceNeeded");
      string requiredInput = resourceIndex >= 0 && resourceIndex < product.Values.Count
        ? product.Values[resourceIndex] : "-1";
      if (!TrySetProductRequiredInput(productId, requiredInput, out error))
        return false;

      TableRow newRow = new TableRow();
      newRow.ItemId = factoryId;
      newRow.Values.AddRange(new[] { productId, "1", "2", "-1" });
      production.Rows.Add(newRow);
      UpdateTableDimensions(production);
      production.IsDirty = true;
      produces.IsDirty = true;

      if (factoryIndex >= 0 && factoryIndex < product.Values.Count &&
          (product.Values[factoryIndex] == "-1" ||
           produces.MasterTable.GetTable(product.Values[factoryIndex]) == null))
      {
        product.Values[factoryIndex] = factoryId;
        products.IsDirty = true;
      }
      if (!TrySetProductRequiredInput(productId, requiredInput, out error))
      {
        production.Rows.Remove(newRow);
        UpdateTableDimensions(production);
        return false;
      }
      return true;
    }

    bool RemoveRecipeProduct(string factoryId, string productId, out string error)
    {
      error = null;
      ConfigSection products = GetConfigSection("Product");
      ConfigSection produces = GetConfigSection("ProduceProduct");
      ConfigSection accepts = GetConfigSection("AcceptProduct");
      if (products == null || products.CommonTable == null || produces == null ||
          produces.MasterTable == null || accepts == null || accepts.MasterTable == null)
      {
        error = "Production data is incomplete.";
        return false;
      }
      CommonTable production = produces.MasterTable.GetTable(factoryId);
      TableRow product = products.CommonTable.GetRow(productId);
      if (production == null || product == null)
      {
        error = "The selected recipe no longer exists.";
        return false;
      }
      TableRow productionRow = production.Rows.FirstOrDefault(row => row != null &&
        row.Values.Count > 0 && row.Values[0] == productId);
      if (productionRow == null)
      {
        error = "The selected recipe no longer exists.";
        return false;
      }

      int resourceIndex = products.CommonTable.GetHeaderIndex("ResourceNeeded");
      int factoryIndex = products.CommonTable.GetHeaderIndex("FactoryID");
      string requiredInput = resourceIndex >= 0 && resourceIndex < product.Values.Count
        ? product.Values[resourceIndex] : "-1";
      production.Rows.Remove(productionRow);
      UpdateTableDimensions(production);
      production.IsDirty = true;
      produces.IsDirty = true;

      CommonTable accepted = accepts.MasterTable.GetTable(factoryId);
      if (accepted != null)
      {
        int acceptedRowsBefore = accepted.Rows.Count;
        RemoveUnusedAcceptedInput(accepted, production, requiredInput,
          products.CommonTable, resourceIndex);
        UpdateTableDimensions(accepted);
        if (accepted.Rows.Count != acceptedRowsBefore)
          accepted.IsDirty = true;
        accepts.IsDirty = true;
      }

      if (factoryIndex >= 0 && factoryIndex < product.Values.Count &&
          product.Values[factoryIndex] == factoryId)
      {
        CommonTable alternate = produces.MasterTable.CommonTables.FirstOrDefault(table =>
          table != null && table.Rows.Any(row => row != null && row.Values.Count > 0 &&
            row.Values[0] == productId));
        product.Values[factoryIndex] = alternate == null ? "-1" : alternate.ItemId;
        products.IsDirty = true;
      }
      return true;
    }

    private void TreeViewSection_AfterSelect(object sender, TreeViewEventArgs e)
    {
      if (e == null || e.Node == null || !refreshRequired)
        return;

      ClearEditors();
      if (e.Node.Name == "RECIPE")
      {
        selectedSectionName = RecipeSectionName;
        selectedRecipeFactoryId = e.Node.Tag as string;
        DisplayRecipeDetails(selectedRecipeFactoryId);
        return;
      }

      selectedRecipeFactoryId = null;
      TreeNode sectionNode = e.Node;
      while (sectionNode.Parent != null)
        sectionNode = sectionNode.Parent;
      selectedSectionName = sectionNode.Text;

      ConfigSection section = GetConfigSection(selectedSectionName);
      if (section == null)
        return;

      if (section.IsMasterTable)
      {
        CommentsTxtBx.Text = section.MasterTable == null
          ? String.Empty : section.MasterTable.TableComment;
        if (e.Node.Name == "ID")
          DisplayItemDetails(e.Node.Tag as string ?? e.Node.Text, section);
        return;
      }

      if (section.CommonTable == null)
        return;
      CommentsTxtBx.Text = section.CommonTable.TableComment ?? String.Empty;
      int columnCount = Math.Min(section.CommonTable.ColumnsCount,
        section.CommonTable.ColumnsHeaders.Count);
      for (int i = 0; i < columnCount; i++)
        DataGridSection.Columns.Add(section.CommonTable.ColumnsHeaders[i],
          section.CommonTable.ColumnsHeaders[i]);
      if (DataGridSection.Columns.Count > 0)
        DataGridSection.Columns[0].ReadOnly = true;
      foreach (TableRow row in section.CommonTable.Rows)
        if (row != null)
          DataGridSection.Rows.Add(row.Values.ToArray());
    }

    void ClearEditors()
    {
      AllItemsCmbBx.SelectedIndex = -1;
      AllItemsCmbBx.Items.Clear();
      DataGridSection.AutoGenerateColumns = false;
      DataGridSection.ClearSelection();
      DataGridSection.Rows.Clear();
      DataGridSection.Columns.Clear();
      TableItemsGrid.ClearSelection();
      TableItemsGrid.Rows.Clear();
      TableItemsGrid.Columns.Clear();
      CommentsTxtBx.Text = String.Empty;
      DetailsLbl.Text = "N/A";
      AddBtn.Enabled = false;
      RemoveBtn.Enabled = false;
      selectedCommonTable = null;
    }

    void DisplayItemDetails(string itemId, ConfigSection section)
    {
      if (section == null || section.MasterTable == null ||
          String.IsNullOrWhiteSpace(itemId))
        return;

      CommonTable selectedTable = section.MasterTable.GetTable(itemId);
      if (selectedTable == null)
      {
        DetailsLbl.Text = "The selected table is missing.";
        return;
      }

      selectedCommonTable = selectedTable;
      AddBtn.Enabled = true;
      RemoveBtn.Enabled = true;

      switch (section.Name)
      {
        case "AcceptProduct":
          DetailsLbl.Text = "(" + itemId + ") Factory: " + GetFactoryName(itemId) + "\n";
          DetailsLbl.Text += "Accepts:";
          TableItemsGrid.Columns.Add("Product", "Product");
          TableItemsGrid.Columns.Add("ProductId", "Product ID");
          TableItemsGrid.Columns[0].ReadOnly = true;
          TableItemsGrid.Columns[1].ReadOnly = true;
          if (selectedTable.Rows.Count > 0)
            foreach (TableRow row in selectedTable.Rows)
              if (row != null && row.Values.Count >= 1)
              TableItemsGrid.Rows.Add(GetProductName(row.Values[0]), row.Values[0]);
          else
            DetailsLbl.Text += "\nNOTHING";
          FillAllItemsCombo("Product");
          break;
        case "ProduceProduct":
          DetailsLbl.Text = "(" + itemId + ") Factory: " + GetFactoryName(itemId) + "\n";
          DetailsLbl.Text += "Produces (use Production recipes to change inputs):";
          TableItemsGrid.Columns.Add("Product", "Product");
          TableItemsGrid.Columns.Add("ProductId", "Product ID");
          TableItemsGrid.Columns.Add("MinimumProduction", "Minimum production");
          TableItemsGrid.Columns.Add("MaximumProduction", "Maximum production");
          TableItemsGrid.Columns.Add("Resourcesleft", "Resources left");
          TableItemsGrid.Columns[0].ReadOnly = true;
          TableItemsGrid.Columns[1].ReadOnly = true;
          if (selectedTable.Rows.Count > 0)
            foreach (TableRow row in selectedTable.Rows)
              if (row != null && row.Values.Count >= 4)
              TableItemsGrid.Rows.Add(GetProductName(row.Values[0]), row.Values[0], row.Values[1], row.Values[2], row.Values[3]);
          else
            DetailsLbl.Text += "\nNOTHING";
          FillAllItemsCombo("Product");
          break;
        case "BuildingResources":
          DetailsLbl.Text = "(" + itemId + ") Building: " + GetFactoryName(itemId) + "\n";
          DetailsLbl.Text += "Requires to build it:";
          TableItemsGrid.Columns.Add("Product", "Product");
          TableItemsGrid.Columns.Add("ProductId", "Product ID");
          TableItemsGrid.Columns.Add("Amount", "Amount");
          TableItemsGrid.Columns[0].ReadOnly = true;
          TableItemsGrid.Columns[1].ReadOnly = true;
          if (selectedTable.Rows.Count > 0)
            foreach (TableRow row in selectedTable.Rows)
              if (row != null && row.Values.Count >= 2)
              TableItemsGrid.Rows.Add(GetProductName(row.Values[0]), row.Values[0], row.Values[1]);
          else
            DetailsLbl.Text += "\nNOTHING";
          FillAllItemsCombo("Product");
          break;
        case "Members":
          DetailsLbl.Text = "FACTORY LINE MEMBERS\n";
          string regionCodeName = GetRegionCodeName(GetItemValue(itemId,
            "FactoryLines", "RegionCode"));
          TableItemsGrid.Columns.Add("Factory", "Factory");
          TableItemsGrid.Columns.Add("FactoryId", "Factory ID");
          TableItemsGrid.Columns[0].ReadOnly = true;
          TableItemsGrid.Columns[1].ReadOnly = true;
          foreach (TableRow row in selectedTable.Rows)
            if (row != null && row.Values.Count >= 1)
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

          TableItemsGrid.Columns.Add("Level", "Level");
          TableItemsGrid.Columns.Add("TerminalSize", "Terminal Size");
          TableItemsGrid.Columns[0].ReadOnly = true;
          for (int i = 0; i < selectedTable.Rows.Count; i++)
            if (selectedTable.Rows[i] != null && selectedTable.Rows[i].Values.Count >= 1)
              TableItemsGrid.Rows.Add("Level " + (i + 1).ToString(),
                selectedTable.Rows[i].Values[0]);
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
      if (section == null || section.CommonTable == null)
        return;
      int nameIndex = section.CommonTable.GetHeaderIndex("Name");

      foreach (TableRow row in section.CommonTable.Rows)
      {
        if (row == null)
          continue;
        string name = nameIndex >= 0 && nameIndex < row.Values.Count
          ? Helper.NormalizeText(row.Values[nameIndex]) : sectionName;
        AllItemsCmbBx.Items.Add(new ComboBoxItem(name + " [" + row.ItemId + "]", row.ItemId));
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
      ConfigSection items = GetConfigSection(sectionName);
      if (items == null || items.CommonTable == null)
        return String.Empty;
      TableRow item = items.CommonTable.GetRow(itemId);
      int valueIndex = items.CommonTable.GetHeaderIndex(headerName);
      if (item == null || valueIndex < 0 || valueIndex >= item.Values.Count)
        return String.Empty;
      return Helper.NormalizeText(item.Values[valueIndex]);
    }

    string GetItemName(string itemId, string sectionName)
    {
      string name = GetItemValue(itemId, sectionName, "Name");
      return String.IsNullOrWhiteSpace(name) ? "#" + itemId : name;
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
      if (selectedCommonTable == null || TableItemsGrid.SelectedCells.Count == 0)
        return;
      int rowIndex = TableItemsGrid.SelectedCells[0].RowIndex;
      if (rowIndex < 0 || rowIndex >= selectedCommonTable.Rows.Count)
        return;

      if (selectedSectionName == RecipeSectionName || selectedSectionName == "ProduceProduct")
      {
        string productId = selectedCommonTable.Rows[rowIndex].Values.Count > 0
          ? selectedCommonTable.Rows[rowIndex].Values[0] : null;
        string factoryId = selectedSectionName == RecipeSectionName
          ? selectedRecipeFactoryId : selectedCommonTable.ItemId;
        string error;
        if (!RemoveRecipeProduct(factoryId, productId, out error))
          MessageBox.Show(error, "Recipe error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        RefreshCurrentView();
        return;
      }

      selectedCommonTable.Rows.RemoveAt(rowIndex);
      selectedCommonTable.RowsCount = selectedCommonTable.Rows.Count;
      if (selectedCommonTable.RowsCount == 0)
        selectedCommonTable.ColumnsCount = 0;
      selectedCommonTable.IsDirty = true;
      ConfigSection section = GetConfigSection(selectedSectionName);
      if (section != null)
        section.IsDirty = true;
      RefreshCurrentView();
    }

    private void AddBtn_Click(object sender, EventArgs e)
    {
      ComboBoxItem selectedItem = AllItemsCmbBx.SelectedItem as ComboBoxItem;
      if (selectedCommonTable == null || selectedItem == null)
        return;

      int selectedIndex = AllItemsCmbBx.SelectedIndex;
      string itemIdValue = selectedItem.Value;
      if (selectedCommonTable.Rows.Any(row => row != null && row.Values.Count > 0 &&
          row.Values[0] == itemIdValue))
      {
        MessageBox.Show("This item is already present in the selected table.",
          "Duplicate item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      if (selectedSectionName == RecipeSectionName || selectedSectionName == "ProduceProduct")
      {
        string factoryId = selectedSectionName == RecipeSectionName
          ? selectedRecipeFactoryId : selectedCommonTable.ItemId;
        string error;
        if (!AddRecipeProduct(factoryId, itemIdValue, out error))
        {
          MessageBox.Show(error, "Recipe error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }
        RefreshCurrentView();
        return;
      }

      TableRow newRow = new TableRow();
      newRow.ItemId = selectedCommonTable.ItemId;
      newRow.Values.Add(itemIdValue);
      if (selectedSectionName == "BuildingResources")
        newRow.Values.Add("10");
      selectedCommonTable.Rows.Add(newRow);
      selectedCommonTable.RowsCount = selectedCommonTable.Rows.Count;
      selectedCommonTable.ColumnsCount = newRow.Values.Count;
      selectedCommonTable.IsDirty = true;
      ConfigSection section = GetConfigSection(selectedSectionName);
      if (section != null)
        section.IsDirty = true;
      RefreshCurrentView();
      if (selectedIndex >= 0 && selectedIndex < AllItemsCmbBx.Items.Count)
        AllItemsCmbBx.SelectedIndex = selectedIndex;
    }

    private void TableItemsGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
      if (selectedCommonTable == null || e == null || e.RowIndex < 0 ||
          e.RowIndex >= selectedCommonTable.Rows.Count || e.ColumnIndex < 0 ||
          e.RowIndex >= TableItemsGrid.Rows.Count)
        return;

      object cellValue = TableItemsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
      if (cellValue == null)
        return;
      string value = cellValue.ToString().Trim();

      if (selectedSectionName == RecipeSectionName)
      {
        TableRow productionRow = selectedCommonTable.Rows[e.RowIndex];
        if (productionRow == null || productionRow.Values.Count < 4)
          return;
        if (e.ColumnIndex == 2)
        {
          string error;
          if (!TrySetProductRequiredInput(productionRow.Values[0], value, out error))
            MessageBox.Show(error, "Invalid recipe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
          RefreshCurrentView();
          return;
        }
        if (e.ColumnIndex >= 3 && e.ColumnIndex <= 5)
        {
          double parsed;
          if (!TryParseConfigNumber(value, out parsed) ||
              ((e.ColumnIndex == 3 || e.ColumnIndex == 4) && parsed < 0))
          {
            MessageBox.Show("Enter a valid numeric production value.", "Invalid value",
              MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshCurrentView();
            return;
          }
          productionRow.Values[e.ColumnIndex - 2] = value;
          selectedCommonTable.IsDirty = true;
          ConfigSection produce = GetConfigSection("ProduceProduct");
          if (produce != null)
            produce.IsDirty = true;
          RefreshCurrentView();
        }
        return;
      }

      if (e.ColumnIndex <= 0 || e.ColumnIndex - 1 >=
          selectedCommonTable.Rows[e.RowIndex].Values.Count)
        return;
      if ((selectedSectionName == "ProduceProduct" && e.ColumnIndex >= 2) ||
          (selectedSectionName == "BuildingResources" && e.ColumnIndex == 2) ||
          selectedSectionName.StartsWith("TerminalPlatformLimit", StringComparison.Ordinal))
      {
        double parsed;
        if (!TryParseConfigNumber(value, out parsed))
        {
          MessageBox.Show("Enter a valid numeric value.", "Invalid value",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
          RefreshCurrentView();
          return;
        }
      }
      selectedCommonTable.Rows[e.RowIndex].Values[e.ColumnIndex - 1] = value;
      selectedCommonTable.IsDirty = true;
      ConfigSection editedSection = GetConfigSection(selectedSectionName);
      if (editedSection != null)
        editedSection.IsDirty = true;
      refreshRequired = false;
      ReloadSectionsTree();
    }

    void ReloadSectionsTree()
    {
      string restoreSection = selectedSectionName;
      string restoreFactory = selectedRecipeFactoryId;
      string restoreTableId = selectedCommonTable == null ? null : selectedCommonTable.ItemId;
      TreeViewSection.Nodes.Clear();
      foreach (ConfigSection section in supportedConfigSections)
      {
        if (section == null)
          continue;
        TreeNode masterNode = TreeViewSection.Nodes.Add(section.Name);
        masterNode.Name = "SECTION";
        if (section.IsMasterTable && section.MasterTable != null)
        {
          foreach (CommonTable table in section.MasterTable.CommonTables)
          {
            if (table == null)
              continue;
            TreeNode parentNode = masterNode.Nodes.Add("ID", table.ItemId);
            parentNode.Tag = table.ItemId;
            for (int i = 0; i < table.Rows.Count; i++)
            {
              TreeNode rowNode = parentNode.Nodes.Add(i.ToString(), "Row " + i.ToString());
              if (table.Rows[i] != null)
                foreach (string val in table.Rows[i].Values)
                  rowNode.Nodes.Add(val ?? String.Empty);
            }
          }
        }
      }

      ConfigSection produces = GetConfigSection("ProduceProduct");
      if (produces != null && produces.MasterTable != null)
      {
        TreeNode recipeRoot = TreeViewSection.Nodes.Add(RecipeSectionName);
        recipeRoot.Name = "RECIPES_ROOT";
        foreach (CommonTable table in produces.MasterTable.CommonTables)
        {
          if (table == null)
            continue;
          TreeNode recipeNode = recipeRoot.Nodes.Add("RECIPE",
            table.ItemId + " - " + GetFactoryName(table.ItemId));
          recipeNode.Tag = table.ItemId;
        }
      }

      refreshRequired = true;
      TreeNode restoreNode = FindTreeNode(restoreSection, restoreTableId, restoreFactory);
      if (restoreNode != null)
      {
        if (restoreNode.Parent != null)
          restoreNode.Parent.Expand();
        TreeViewSection.SelectedNode = restoreNode;
        restoreNode.Expand();
      }
    }

    TreeNode FindTreeNode(string sectionName, string tableId, string recipeFactoryId)
    {
      foreach (TreeNode root in TreeViewSection.Nodes)
      {
        if (sectionName == RecipeSectionName && root.Name == "RECIPES_ROOT")
          foreach (TreeNode node in root.Nodes)
            if (node.Name == "RECIPE" && String.Equals(node.Tag as string,
                recipeFactoryId, StringComparison.Ordinal))
              return node;
        if (root.Text != sectionName)
          continue;
        if (!String.IsNullOrEmpty(tableId))
          foreach (TreeNode node in root.Nodes)
            if (node.Name == "ID" && String.Equals(node.Tag as string, tableId,
                StringComparison.Ordinal))
              return node;
        return root;
      }
      return null;
    }

    void RefreshCurrentView()
    {
      ReloadSectionsTree();
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
      factorySection.IsDirty = true;
      productSection.IsDirty = true;

      TreeViewSection.SelectedNode = null;
      selectedCommonTable = null;
      selectedSectionName = "";
      selectedRecipeFactoryId = null;
      ReloadSectionsTree();
      ClearEditors();

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

    void DisplayRecipeDetails(string factoryId)
    {
      ConfigSection produceSection = GetConfigSection("ProduceProduct");
      ConfigSection productSection = GetConfigSection("Product");
      if (String.IsNullOrWhiteSpace(factoryId) || produceSection == null ||
          produceSection.MasterTable == null || productSection == null ||
          productSection.CommonTable == null)
      {
        DetailsLbl.Text = "Production data is incomplete.";
        return;
      }

      CommonTable production = produceSection.MasterTable.GetTable(factoryId);
      if (production == null)
      {
        DetailsLbl.Text = "The selected factory has no production table.";
        return;
      }

      selectedCommonTable = production;
      DetailsLbl.Text = "(" + factoryId + ") Factory: " + GetFactoryName(factoryId) +
        "\nProduction recipes";
      CommentsTxtBx.Text = "Changing the required input updates Product.ResourceNeeded and " +
        "the AcceptProduct tables of every factory producing this product.";
      AddBtn.Enabled = true;
      RemoveBtn.Enabled = true;

      TableItemsGrid.Columns.Add("Product", "Output product");
      TableItemsGrid.Columns.Add("ProductId", "Output ID");
      DataGridViewComboBoxColumn requiredColumn = new DataGridViewComboBoxColumn();
      requiredColumn.Name = "RequiredInput";
      requiredColumn.HeaderText = "Required input";
      requiredColumn.DisplayMember = "Text";
      requiredColumn.ValueMember = "Value";
      requiredColumn.DataSource = CreateProductChoices(true);
      TableItemsGrid.Columns.Add(requiredColumn);
      TableItemsGrid.Columns.Add("MinimumProduction", "Minimum production");
      TableItemsGrid.Columns.Add("MaximumProduction", "Maximum production");
      TableItemsGrid.Columns.Add("Resourcesleft", "Resources left");
      TableItemsGrid.Columns[0].ReadOnly = true;
      TableItemsGrid.Columns[1].ReadOnly = true;

      int resourceIndex = productSection.CommonTable.GetHeaderIndex("ResourceNeeded");
      foreach (TableRow row in production.Rows)
      {
        if (row == null || row.Values.Count < 4)
          continue;
        TableRow product = productSection.CommonTable.GetRow(row.Values[0]);
        string requiredInput = product != null && resourceIndex >= 0 &&
          resourceIndex < product.Values.Count ? product.Values[resourceIndex] : "-1";
        int gridRow = TableItemsGrid.Rows.Add(GetProductName(row.Values[0]), row.Values[0],
          requiredInput, row.Values[1], row.Values[2], row.Values[3]);
        TableItemsGrid.Rows[gridRow].Tag = row.Values[0];
      }
      FillAllItemsCombo("Product");
    }

    List<ComboBoxItem> CreateProductChoices(bool includeNone)
    {
      List<ComboBoxItem> choices = new List<ComboBoxItem>();
      if (includeNone)
        choices.Add(new ComboBoxItem("(none) [-1]", "-1"));
      ConfigSection products = GetConfigSection("Product");
      if (products == null || products.CommonTable == null)
        return choices;
      int nameIndex = products.CommonTable.GetHeaderIndex("Name");
      foreach (TableRow row in products.CommonTable.Rows)
      {
        if (row == null)
          continue;
        string name = nameIndex >= 0 && nameIndex < row.Values.Count
          ? Helper.NormalizeText(row.Values[nameIndex]) : "Product";
        choices.Add(new ComboBoxItem(name + " [" + row.ItemId + "]", row.ItemId));
      }
      return choices;
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
