using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGConfigEditor
{
  public class TableRow
  {
    public string ItemId { get; set; }
    public List<string> Values { get; set; }

    public TableRow()
    {
      Values = new List<string>();
    }
  }
}
