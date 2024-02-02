using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace msa.Data.Transfer.Model
{
	/// <summary> Ein Transferblock beschreibt die Datenquelle und das Ziel und dient dazu mehrere Jobs zu gruppieren. 
	/// Zudem ist ein Block die Parallelisierungseinheit für einen Transferbatch - das heißt jeder Block läuft in einem eigenen Thread und erzeugt eigene DB-Verbindungen </summary>
	[Serializable]
	public class TransferBlock
	{
		/// <summary>Der Name des Blocks - dient zur Identifizierung des Blocks und sollte in einer Batch eindeutig sein - wird in Logs verwendet</summary>
		[XmlAttribute()]
		public string name { get; set; }

		/// <summary>Der ADO-Treiber der Datenquelle die als Ursprung des Datentransfers dient</summary>
		[XmlAttribute()]
		public string conStringSource { get; set; }

		/// <summary>Der ADO-ConnectionString der Datenquelle die als Ursprung des Datentransfers dient - Password und Pwd können als DPAPI verschlüsselte Werte für Nutzer/Hostnames vorliegen und werden entsprechend entschlüsselt </summary>
		[XmlAttribute()]
		public string conStringSourceType { get; set; }

		/// <summary>Der ADO-Treiber der Datenquelle die als Ziel des Datentransfers dient</summary>
		[XmlAttribute()]
		public string conStringTarget { get; set; }

		/// <summary>Der ADO-ConnectionString der Datenquelle die als Ziel des Datentransfers dient - Password und Pwd können als DPAPI verschlüsselte Werte für Nutzer/Hostnames vorliegen und werden entsprechend entschlüsselt </summary>
		[XmlAttribute()]
		public string conStringTargetType { get; set; }

		/// <summary> Gibt die maximale Batchsize für die Fill-Operation der TargetTable an - Diese muss evt. niedriger gesetzt werden, falls es zu Timeouts kommt<br/>
		/// 0 = Maximum, alles andere = reale Batchsize </summary>
		[XmlAttribute()]
		public int targetMaxBatchSize { get; set; }

		/// <summary>Der ADO-ConnectionString der Datenquelle die als Ziel des Datentransfers dient - Password und Pwd können als DPAPI verschlüsselte Werte für Nutzer/Hostnames vorliegen und werden entsprechend entschlüsselt </summary>
		[XmlAttribute()]
		public bool disableTransaction { get; set; }

		/// <summary>IsolationLevel wenn eine Transaktion verwendet wird - bei nicht Angabe wird 'Serializable' verwendet (höchste Isolation) </summary>
		[XmlAttribute()]
		public IsolationLevel transactionIsolationLevel { get; set; }

		/// <summary> Bedingungen die gelten müssen bevor der Job verarbeitet wird - Eine Bedingung muss genau eine einzelne Zeile der Ursprungsdatenquelle abfragen um dort Spaltenwerte zu prüfen </summary>
		public TransferTableCondition preCondition { get; set; }

		/// <summary> Die Transferjobs die sequentiell verarbeitet werden </summary>
		[XmlElement("TransferTableJob")]
		public List<TransferTableJob> transferJobs = new List<TransferTableJob>();

		/// <summary>
		/// Ermöglicht einen Zugriff auf die Jobs des Transferblocks über den Namen der Zieltabelle (targetTable) im Job
		/// </summary>
		/// <param name="targetTable">Name der Zieltabelle des zu ermittelnden Jobs</param>
		/// <returns>Der angegebene Job des Transferblocks</returns>
		public TransferTableJob this[string targetTable]
		{
			get
			{
				TransferTableJob selJob = (from job in this.transferJobs where job.targetTable == targetTable select job).FirstOrDefault();
				if (selJob == null) throw new ArgumentException(
					String.Format("TransferJob with targetTable '{0}' does not exist in Transferblock {1}",
					targetTable, this.name));
				return selJob;
			}
		}
	}
}
