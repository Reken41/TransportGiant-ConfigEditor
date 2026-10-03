using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGConfigEditor
{
  public class CommonTable
  {
    public string ItemId { get; set; }
    public int RowsCount { get; set; }
    public int ColumnsCount { get; set; }
    public string UnknownValue { get; set; }
    public string TableComment { get; set; }
    public List<TableRow> Rows { get; set; }
    public List<string> ColumnsHeaders { get; set; }
    public int RawHeaderLineIndex { get; set; }
    public bool IsDirty { get; set; }

    public CommonTable()
    {
      Rows = new List<TableRow>();
      ColumnsHeaders = new List<string>();
      RawHeaderLineIndex = -1;
    }

    public TableRow GetRow(string itemId)
    {
      if (String.IsNullOrEmpty(itemId))
        return null;

      foreach (TableRow row in Rows)
      {
        if (row.ItemId == itemId)
          return row;
      }
      return null;
    }

    public int GetHeaderIndex(string headerName)
    {
      if (String.IsNullOrEmpty(headerName))
        return -1;

      for (int i = 0; i < ColumnsHeaders.Count; i++)
      {
        if (ColumnsHeaders[i] == headerName)
          return i;
      }
      return -1;
    }
  }
}
