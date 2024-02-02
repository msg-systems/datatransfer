using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.Model
{
    /// <summary> Auflistung für Angabe ob Quell- oder Ziel-DB betroffen ist</summary>
    [Serializable]
    public enum DBContextEnum
    {
        /// <summary> Befehl wird im Kontext der Quellanwendung durchgeführt</summary>
        Source,
        /// <summary> Befehl wird im Kontext der Zielanwendung durchgeführt</summary>
        Target
    }
}
