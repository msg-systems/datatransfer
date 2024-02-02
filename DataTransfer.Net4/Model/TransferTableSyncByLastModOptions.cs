using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace msa.Data.Transfer.Model
{
    /// <summary>Varianten für SyncByLastMod-Synchronisation</summary>
    [Serializable]
    public enum SyncByLastModMode
    {
        /// <summary>Anhäng-Modus - Nimmt alle Datensätze aus der Quelle die ein größeres Datum haben als [MaxDatum] und hängt sie an das Ziel an (immer INSERT)</summary>
        APPEND,
        /// <summary>Anhang-Modus mit Max-Datum - Nimmt alle Datensätze aus der Quelle die ein größeres oder gleiches Datum haben als [MaxDatum] und hängt sie an das Ziel an (immer INSERT). Im Ziel wird vorher alles gelöscht mit datum = [MaxDatum]</summary>
        APPEND_INCLUDE_MAXDATE,
        /// <summary>Sync-Modus mit LastMod - Nimmt alle Datensätze aus der Quelle die ein größeres Datum haben als [MaxDatum] und aktualisiert / fügt sie im Ziel ein (INSERT/UPDATE). Im Ziel werden dafür alle zu aktualisierenden Datensätze gelöscht - Dies funktioniert nur wenn es einen einspaltigen Primary Key für die Löschung gibt.</summary>
        UPDATE_EXISTING
    }

    /// <summary>Optionen für die SyncByLastMod-Synchronisation</summary>
    [Serializable]
    public class TransferTableSyncByLastModOptions
    {
        /// <summary> Der Name des LastMod-Feldes für eine Synchronisation zwischen 2 Tabellen über ein LastMod-Datum </summary>
        [XmlAttribute()]
        public String SyncByLastModField { get; set; }

        /// <summary> Der Synchronisationsmodus für eine Datumsübertragung </summary>
        [XmlAttribute()]
        public SyncByLastModMode SyncByLastModMode { get; set; } = SyncByLastModMode.APPEND;
    }
}
