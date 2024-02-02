using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.Model
{
    /// <summary>Eine Variableninstanz mit Typ und Wert - kann aus einer Variablendeklaration <see cref="msa.Data.Transfer.Model.TransferTableVariableDeclaration"/>  entstehen</summary>
    public class Variable
    {
        /// <summary>Aktueller Wert der Variablen</summary>
        public object value { get; set; }

        /// <summary>Typ der Variablen</summary>
        public DbType type { get; set; }
    }
}
