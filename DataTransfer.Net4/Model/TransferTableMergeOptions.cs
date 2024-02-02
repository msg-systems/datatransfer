using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace msa.Data.Transfer.Model
{
	/// <summary> Beschreibt wie ein Merge innerhalb einer Datenquelle von einer Tablle zu einer anderen stattfindet - Die Quelltabelle ist implizit die targetTable des beinhaltenden Jobs </summary>
	[Serializable]
	public class TransferTableMergeOptions
	{
		/// <summary> Gibt an ob ein merge gewünscht ist </summary>
		[XmlAttribute()]
		public bool merge { get; set; }

		/// <summary> Gibt an das die Zieltabelle mindestens die Spalten der Quelltabelle besitzt und diese automatisch zuweisen soll - Eine Angabe führt dazu, dass die Werte unter columnMap ignoriert werden </summary>
		[XmlAttribute()]
		public bool autoMergeColumns { get; set; }

		/// <summary> Gibt die Liste/das Mapping der MergeKeys an um identische Datensätze zu identifizieren</summary>
		[XmlElement("mergeKey")]
		public TransferTableColumnList mergeKey = new TransferTableColumnList();

		/// <summary> Der Name der Zieltabelle in der der merge erfolgen soll</summary>
		[XmlAttribute()]
		public String targetTable { get; set; }

		/// <summary> Ein Spaltenmapping für einen Merge von der Quell zur Zieltabelle. Wird ignoriert wenn das Attribut autoMergeColumns true ist</summary>
		public TransferTableColumnList columnMap = new TransferTableColumnList();
	}
}
