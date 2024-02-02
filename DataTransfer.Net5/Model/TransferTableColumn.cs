using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace msa.Data.Transfer.Model
{
	/// <summary>Stellt ein Mapping zwischen einer Quell- und Ziel-Spalte in Tabellen dar</summary>
	public struct TransferTableColumn
	{
		/// <summary>Der Name der Spalte in der Quelltabelle</summary>
		[XmlAttribute()]
		public String sourceCol { get; set; }

		/// <summary>Der Name der Spalte in der Zieltabelle</summary>
		[XmlAttribute()]
		public String targetCol { get; set; }

        /// <summary>Angabe ob die Spalte ein Key ist</summary>
        [XmlAttribute()]
		public bool isKey { get; set; }
	}
}
