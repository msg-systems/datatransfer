using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.Model
{
    /// <summary>Definition eines SQL-Parameters inkusive seines Bedingungskontextes</summary>
    public class ParameterDef
    {
        /// <summary>Name der Spalte gegen die der Parameter verglichen wird</summary>
        public string colName { get; set; }
        /// <summary>Name des Parameters wie er gewünscht wäre</summary>
        public string paramName { get; set; }
        /// <summary>Relation für den Vergleich zwischen Spalte und Parameterwert z.B. &lt; &gt; = != </summary>
        public string relation { get; set; }
        /// <summary>Der Wert den der Parameter annehmen soll</summary>
        public object value { get; set; }
        /// <summary>Der Datentyp des Parameters</summary>
        public DbType type { get; set; }
        /// <summary>Die Art wir der Parameter logisch verknüpft werden soll AND/OR </summary>
        public string logicJoin { get; set; }

    }
}
