using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace msa.Data.Transfer.Model
{

	/// <summary> Ein Subjob eines Transferblocks der den Transfer genau einer Tabelle beschreibt </summary>
	[Serializable]
	public class TransferTableJob
	{
		/// <summary> Name der Quelltabelle - muss der Syntax des Datenanbieters entsprechen. Wird ignoriert wenn customSourceSelect angegeben ist. </summary>
		[XmlAttribute()]
		public String sourceTable { get; set; }

		/// <summary> WHERE-Bedingung um die Source-Tabelle einzuschränken </summary>
		[XmlAttribute()]
		public String sourceWhere { get; set; }

		/// <summary> WHERE-Bedingung um die target-Tabelle für den Sync einzuschränken </summary>
		[XmlAttribute()]
		public String targetSyncWhere { get; set; }

		/// <summary> Gibt an ob ein echter Sync durchgeführt wird </summary>
		[XmlAttribute()]
		public bool sync { get; set; }

		/// <summary> Eine selbstdefiniertes SELECT-Statement - Eine Angabe führt dazu, das das Attribut sourceTable ignoriert wird. </summary>
		public String customSourceSelect { get; set; }

		/// <summary> Eine Vorabbedingung für den Job, die auf der Ursprungs-Datenquelle geprüft wird </summary>
		public TransferTableCondition preCondition = new TransferTableCondition();

		/// <summary> Name der Zieltabelle - muss der Syntax des Datenanbieters entsprechen. Wird ignoriert wenn customSourceSelect angegeben ist. </summary>
		[XmlAttribute()]
		public String targetTable { get; set; }

		/// <summary> Angabe ob die Zieltabelle vor Befüllung gelöscht wird - generell empfohlen da kein Merge stattfindet und sonst Primärschlüsselkonflikte auftreten können </summary>
		[XmlAttribute()]
		public bool deleteBefore { get; set; }

		/// <summary> WHERE-Bedingung für den Delete-Befehl </summary>
		[XmlAttribute()]
		public String deleteWhere { get; set; }

        /// <summary> Aktiviert eine Synchronisation zwischen 2 Tabellen über ein LastMod-Datum, um nur Änderungen zu erfassen </summary>
        [XmlAttribute()]
        public bool SyncByLastMod { get; set; }

        /// <summary>Liste von Variablendeklarationen und Berechnungen</summary>
        [XmlElement("variable")]
        public List<TransferTableVariableDeclaration> variables = new List<TransferTableVariableDeclaration>();

        /// <summary> SQL-Statements die ausgeführt werden bevor die Bearbeitung der Zieltabelle beginnt </summary>
        [XmlElement("preStatement")]
		public List<String> preStatement = new List<string>();

		/// <summary> Gibt an ob in der Quell und Zieltabelle identische Spalten vorliegen - Eine Angabe führt dazu, das eine Angabe zu columnMap ignoriert wird </summary>
		[XmlAttribute()]
		public bool identicalColumns { get; set; }

        /// <summary> Gibt an ob für identische Spalteneinstellungen die Quellseite oder die Zielseite als Referenz der Spaltenübertragung dienen soll - so kann man die Option auch mit asynchronen Spaltenlisten verwenden </summary>
		[XmlAttribute()]
        public DBContextEnum identicalColumnsSource { get; set; } = DBContextEnum.Target;

        /// <summary> Gibt die maximale Abweichung von Anzahl Zieldatensätzen zu Anzahl Quelldatensätzen an </summary>
        [XmlAttribute()]
		public double maxRecordDiff { get; set; }

		/// <summary> Ein Spaltenmapping zwischen Quell- und Zieltabelle - wird ignoriert wenn das identicalColumn Attribut true ist </summary>
		public TransferTableColumnList columnMap = new TransferTableColumnList();

		/// <summary> Ein Spaltenmapping zwischen Quell- und Zieltabelle - wird ignoriert wenn das identicalColumn Attribut true ist </summary>
		public TransferTableColumnList customKeys = new TransferTableColumnList();

		/// <summary> SQL-Statements die nach dem Transfer, aber vor einem Merge ausgeführt werden </summary>
		[XmlElement("postStatement")]
		public List<String> postStatement = new List<string>();

        /// <summary>Ermöglicht Konfigurationsangaben für den SyncByLastMod-Modus</summary>
        public TransferTableSyncByLastModOptions syncByLastModOptions { get; set; }

        /// <summary> Definitionen zu einem Merge das die Zieltabelle in eine weitere Tabelle hinein merged </summary>
        public TransferTableMergeOptions mergeOptions = new TransferTableMergeOptions();

		/// <summary> Detail-Definitionen zu einem Sync </summary>
		public TransferTableSyncOptions syncOptions = new TransferTableSyncOptions();

        /// <summary>
        /// Gibt eine Liste aller Sync-Keys zurück, unabhängig wo sie sich in der Konfiguration befinden
        /// </summary>
        /// <returns>Liste der Sync-Keys</returns>
        public TransferTableColumn[] getKeys()
        {
            return (this.columnMap.Where((c) => c.isKey).Union(this.customKeys)).ToArray();
        }
	}
}
