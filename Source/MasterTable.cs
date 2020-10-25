using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGConfigEditor
{
  public class MasterTable
  {
    public string TableId { get; set; }
    public string TableComment { get; set; }
    public List<CommonTable> CommonTables { get; set; }

    public MasterTable()
    {
      CommonTables = new List<CommonTable>();
    }

    public CommonTable GetTable(string itemId)
    {
      foreach (CommonTable table in CommonTables)
      {
        if (table.ItemId == itemId)
          return table;
      }
      return null;
    }
  }
}
