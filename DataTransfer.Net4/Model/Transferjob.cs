using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using msa.Logging.Model;

namespace msa.Data.Transfer.Model
{
	/// <summary> Root-Element für Konfigurationsdateien und Startpunkt für das Klassenmodell eines Transferjobs.
	/// Besteht aus mehreren Transferblöcken die parallel verarbeitet werden können.
	/// Besteht weiteren Einstellungen für das Logging und den Mailversand im Fehlerfall</summary>
	[Serializable]
	public class TransferJob
	{
		/// <summary> Globale Einstellungen für den Job wie etwa Log- und Maileinstellungen </summary>
		public TransferJobSettings settings = new TransferJobSettings();

		/// <summary> Globale Einstellungen für den Job wie etwa Log- und Maileinstellungen </summary>
		[XmlElement("transferBlock")]
		public List<TransferBlock> transferBlocks = new List<TransferBlock>();

	}

}
