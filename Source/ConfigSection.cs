using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGConfigEditor
{
  public class ConfigSection
  {
    public string Name { get; set; }
    public bool IsSupported { get; set; }
    public bool IsMasterTable { get; set; }
    public List<string> RawLines { get; set; }

    public MasterTable MasterTable { get; set; }
    public CommonTable CommonTable { get; set; }

    public ConfigSection()
    {
      RawLines = new List<string>();
    }
  }
}
