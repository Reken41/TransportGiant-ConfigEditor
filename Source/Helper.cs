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
      foreach (string name in GameData.SupportedSections)
      {
        if (section.Name == name)
          return true;
      }

      return false;
    }

    public static bool IsSectionMasterTable(ConfigSection section)
    {
      return section.RawLines[0].Contains("MASTER_TABLE");
    }

    public static string ClearDoubleSpaces(string input)
    {
      while (input.Contains("  "))
        input = input.Replace("  ", " ");
      return input;
    }

    public static string NormalizeLine(string line)
    {
      line = line.Trim();
      //line = line.Replace("\t", " ");
      line = ClearDoubleSpaces(line);
      return line;
    }

    public static string NormalizeText(string text)
    {
      text = text.Trim();
      text = text.Replace("\t", " ");
      text = text.Replace("\\", "");
      text = text.Replace("\"", "");
      text = ClearDoubleSpaces(text);
      return text;
    }
  }
}
