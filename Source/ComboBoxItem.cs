using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGConfigEditor
{
  public class ComboBoxItem
  {
    string _text;
    string _value;

    //Constructor
    public ComboBoxItem(string text, string value)
    {
      _text = text;
      _value = value;
    }

    //Accessor
    public string Value
    {
      get
      {
        return _value;
      }
    }

    //Override ToString method
    public override string ToString()
    {
      return _text;
    }
  }
}
