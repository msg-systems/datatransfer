using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace msa.Data.Transfer.Model
{
	/// <summary>
	/// Beschreibt eine Bedingung die auf einer Datenquelle geprüft werden soll - Diese muss genau eine Datenzeile selektieren auf der die Prüfung durchgeführt wird.
	/// Im Rahmen des Batches werden Bedingungen immer auf der Ursprungsdatenquelle evaluiert
	/// </summary>
	[Serializable]
	public class TransferTableCondition
	{
		/// <summary>Gibt an wo die Bedingung geprüft werden soll, auf target oder auf source-Seite</summary>
		[XmlAttribute()]
		public string checkOn { get; set; }

		/// <summary>Das Select welches auf der Datenquelle ausgeführt werden soll um genau einen Datensatz zu liefertn der geprüft werden kann</summary>
		[XmlAttribute()]
		public string select { get; set; }

		/// <summary>Die Bedingung die geprüft werden soll im Format [Spalte1]:[Wert1];[Spalte2]:[Wert2] wobei durch [] Platzhalter gemeint sind <br/>
		/// Beispiel: Suchwert:1</summary>
		[XmlAttribute()]
		public string condition { get; set; }

		/// <summary>Angabe wie oft versucht werden soll die Bedingung zu prüfen, wenn sie fehlschlägt. Default 1</summary>
		[XmlAttribute()]
		public int retryCount { get; set; }

		/// <summary>Angabe wie lange gewartet werden soll, bis nach einer fehlgeschlagenen Prüfung erneut geprüft werden soll. Default 0</summary>
		[XmlAttribute()]
		public int retryDelay { get; set; }
	}
}
