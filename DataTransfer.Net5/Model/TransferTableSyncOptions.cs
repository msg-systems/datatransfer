using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace msa.Data.Transfer.Model
{
    /// <summary>
    /// Detail-Optionen für einen Synch, was an Aktionen erlaubt werden soll
    /// </summary>
    [Serializable]
    public class TransferTableSyncOptions
    {
        /// <summary> Gibt an das Updates nicht durchgeführt werden - default false </summary>
        [XmlAttribute()]
        public bool noUpdates { get; set; }

        /// <summary> Gibt an das Deletes nicht durchgeführt werden - default false </summary>
        [XmlAttribute()]
        public bool noDeletes { get; set; }
        
        /// <summary> Gibt an das Inserts nicht durchgeführt werden - default false </summary>
        [XmlAttribute()]
        public bool noInserts { get; set; }
    }
}
