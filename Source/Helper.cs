using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGConfigEditor
{
  static class Helper
  {
    //public static string GetFirstWord(string line)
    //{
    //  string word = "";

    //  int i = 0;
    //  while (true)
    //  {
    //    if (line[i] != ' ' && line[i] != '\t')
    //      word += line[i];
    //    else break;
    //    i++;
    //  }

    //  return word;
    //}

    public static bool IsSectionSupported(ConfigSection section)
    {
      if (section == null || String.IsNullOrEmpty(section.Name))
        return false;

      foreach (string name in GameData.SupportedSections)
      {
        if (section.Name == name)
          return true;
      }

      return false;
    }

    public static bool IsSectionMasterTable(ConfigSection section)
    {
      return section != null && section.RawLines != null && section.RawLines.Count > 0 &&
        section.RawLines[0] != null && section.RawLines[0].Contains("MASTER_TABLE");
    }

    public static string ClearDoubleSpaces(string input)
    {
      if (input == null)
        return String.Empty;

      while (input.Contains("  "))
        input = input.Replace("  ", " ");
      return input;
    }

    public static string NormalizeLine(string line)
    {
      if (line == null)
        return String.Empty;

      line = line.Trim();
      //line = line.Replace("\t", " ");
      line = ClearDoubleSpaces(line);
      return line;
    }

    public static string NormalizeText(string text)
    {
      if (text == null)
        return String.Empty;

      text = text.Trim();
      text = text.Replace("\t", " ");
      text = text.Replace("\\", "");
      text = text.Replace("\"", "");
      text = ClearDoubleSpaces(text);
      return text;
    }
  }
}
