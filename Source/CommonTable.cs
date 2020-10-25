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

    public CommonTable()
    {
      Rows = new List<TableRow>();
      ColumnsHeaders = new List<string>();
    }

    public TableRow GetRow(string itemId)
    {
      foreach (TableRow row in Rows)
      {
        if (row.ItemId == itemId)
          return row;
      }
      return null;
    }

    public int GetHeaderIndex(string headerName)
    {
      for (int i = 0; i < ColumnsHeaders.Count; i++)
      {
        if (ColumnsHeaders[i] == headerName)
          return i;
      }
      return -1;
    }
  }
}
