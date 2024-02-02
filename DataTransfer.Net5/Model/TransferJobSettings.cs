using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using msa.Logging.Model;

namespace msa.Data.Transfer.Model
{
	/// <summary>
	/// Globale Einstellungen für einen Transferjob
	/// </summary>
	[Serializable]
	public class TransferJobSettings
	{
		/// <summary>Angabe zum Logdateinamen der für diesen Transferjob verwendet werden soll - ist nur relevant wenn Sublogs auf der Kommandozeile als aktiv markiert sind (-sl)</summary>
		[XmlAttribute()]
		public string writeLogToFile { get; set; }

		/// <summary>Angaben zum Mailversand im Fehlerfall</summary>
		public MailSettings mailSettings = new MailSettings();
	}
}
