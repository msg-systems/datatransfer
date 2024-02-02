using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace msa.Data.Transfer.Model
{
    /// <summary>Beschreibt eine Variablendeklaration und wie sie initialisiert werden soll</summary>
    [Serializable]
    public class TransferTableVariableDeclaration
    {
        /// <summary>Der Name der Variablen</summary>
        [XmlAttribute]
        public string name { get; set; }

        /// <summary>Fixwert für eine Variable - wenn angegeben werden expression oder sql-Statements ignoriert</summary>
        [XmlAttribute]
        public string value { get; set; }

        /// <summary>Datentyp der Variable</summary>
        [XmlAttribute]
        public DbType type { get; set; }

        /// <summary>SELECT-Statement zur Ermittlung des Variablenwertes. Es wird die erste Spalte des ersten Ergebnisdatensatzes als Ergebnis verwendet.
        /// Wenn value genutzt, wird dies ignoriert. Wenn gesetzt werden expressions ignoriert</summary>
        [XmlAttribute]
        public string selectStmt { get; set; }

        /// <summary>Beschreibt auf welcher Position das SQL-Statement gestartet werden soll - Target/Source-Verbindung eines Transfers</summary>
        [XmlAttribute]
        public DBContextEnum selectContext { get; set; }

        /// <summary>Eine Berechnung zur Initialisierung der Variablen - Arbeitet laut Regeln des CodeEvaluators. Wenn value oder selectStmt gesetzt ist, wird der Wert ignoriert-</summary>
        [XmlAttribute]
        public string expression { get; set; }
    }
}
