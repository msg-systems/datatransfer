using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using msa.Data.Transfer.Model;
using msa.Logging;
using System.Data.OleDb;
using System.Data.Common;
using System.DirectoryServices;
using System.Text.RegularExpressions;
using msa.DSL.CodeParser;
using msa.DSL;
using System.Runtime.InteropServices;
using System.Security.Principal;
using msa.Data.Transfer.SQL;

namespace msa.Data.Transfer.Database.Custom
{
	/// <summary>
	/// Die Domain specific Language die in SELECT-Attribut-Ausdrücken für Custom-LDAP-Export genutzt wird
	/// </summary>
	public class ldapDSL : DSLDef
	{
		/// <summary>
		/// Der ValueProvider für die LDAPDSL - Arbeitet mit DirectoryEntries
		/// </summary>
		public class LdapValueProvider : DSLValueProvider
		{
			/// <summary>Kontexteintrag mit dem der ValueProvider arbeitet </summary>
			public SearchResult context;

			/// <summary>Löst eine Variable einer DSL auf</summary>
			/// <param name="refName">Der Name der Variable</param>
			/// <returns>Der Ergebniswert</returns>
			public override object getValue(string refName)
			{
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new ArgumentException("LDAP-classes only supported in windows");
                
				// Prüfen ob Property vorhanden
				if (!context.Properties.Contains(refName)) return DBNull.Value;

				// Wenn Einfachwert, dann einfach zurückgeben
				if (context.Properties[refName].Count == 1)
				{
					return context.Properties[refName][0];
				}
				else // Sonst Join einer Mehrfacheigenschaft mit NewLines
				{
					return String.Join("\n", (from string val in context.Properties[refName] select val.ToString()).ToList());
				}
            }
		}

		/// <summary>
		/// Funktioonsevaluierer für die LDAP-Sprache
		/// </summary>
		public class LdapFunctionHandler : DSLFunctionHandler
		{

			/// <summary> Hilfsfunktion um große Property-Arrays aus dem AD zu lesen (ab 1500 Einträgen aufwärts) </summary>
			/// <param name="context">DirectoryEntry der die Property enthält</param>
			/// <param name="propName">Der Name der Property mit mehr als 1500 Einträgen</param>
			/// <returns>Die komplette Liste der Propertywerte</returns>
			public List<string> getBigPropArray(DirectoryEntry context, String propName)
			{
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new ArgumentException("LDAP-classes only supported in windows");
				
				// Magische Zahl = Maximum an zurückgelieferten Werten vom AD für eine Property -> Gib einfach zurück wenn es weniger sind
				if (context.Properties[propName].Count < 1500)
				{
					return (from string val in context.Properties[propName] select val).ToList();
				}
				else // sonst wirds komplizierter
				{
					// merke erstmal bisher gefundene
					List<string> resultList = (from string val in context.Properties[propName] select val).ToList();

					// Führe wiederholt Suchen auf dem gleichen Eintrag mit anderen Range´s für das Attribut aus
					using (DirectorySearcher ds = new DirectorySearcher(context, "(samAccountname=" + context.Properties["samAccountname"][0].ToString() + ")", new string[] { propName }, SearchScope.Base))
					{
						int startRange = 1500, stepRange = 1500; // Wieder mal die magische Zahl... - Erfasse FolgeRanges in 1500-er Schritten 

						// Endlosschleife
						while (true)
						{
							// Formatierung der Anfrage so das AD dies versteht
							ds.PropertiesToLoad[0] = String.Format("{0};range={1}-{2}", propName, startRange, startRange + startRange - 1);

							// Suche Eintrag
							SearchResult sr = ds.FindOne();

							// Ergebnis enthält entweder genau die Suchproperty oder suchprop;range=x-* wenn keine weiteren Eigenschaften existieren
							// Wenn Eigenschaft also existiert gibt es noch mehr zu holen -> füge Ergebnis nur hinzu
							if (sr.Properties.Contains(ds.PropertiesToLoad[0]))
							{
								resultList.AddRange((from string val in sr.Properties[ds.PropertiesToLoad[0]] select val));
							}
							else // Wenn Eigenschaft nicht unter definierten Suchnamen existiert -> füge Ergebnis zu + Ende, weil nichts mehr zu holen
							{
								resultList.AddRange((from string val in sr.Properties[String.Format("{0};range={1}-*", propName, startRange)] select val));
								return resultList;
							}

							// Nächsten Range festlegen für nächsten Schleifendurchlauf
							startRange += stepRange;
						}
					}
				}
            }


            /// <summary> Functionhandler für LDAP-Funktionen: <br/>
            /// cint, coctet, cdate8, cdategen = Konvertierungen von Datentypen <br/>
            /// loadBigArray (feldname als string) = Listen mit mehr als 1500 Membern ermitteln (memberof, member)
            /// splitrows = Teilt ein MultivalueFeld in Mehrfachwerte auf wodurch sich der Datensatz multipliziert - Darf nur einmal pro Anfrage verwendet werden, loadBigArray wirkt analog
            /// if = klassisches if Parameter: Bedinging, True-Case, False-Case
            /// </summary>
            public LdapFunctionHandler()
            {
				this.addConversions();
                this.addLogic();

                this["coctet"] = (args) =>
                {
                    if (args[0] is DBNull) return DBNull.Value;
                    return (byte[])args[0];
                };

                this["sid"] = (args) =>
                {
                    if (args[0] is DBNull) return DBNull.Value;
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						return new SecurityIdentifier((byte[])args[0], 0).ToString();
					else
						throw new ArgumentException("LDAP-classes only supported in windows");
                };

                this["cdate8"] = (args) =>
                {
                    if (args[0] is DBNull) return DBNull.Value;

                    long intValue = (Int64)args[0];
                    if (intValue == 0) return DBNull.Value;
                    if (intValue <= DateTime.MaxValue.Ticks)
                        return new DateTime(intValue).AddYears(1600).AddHours(2);
                    else
                        return DBNull.Value;
                };

                this["cdategen"] = (args) =>
                {
                    if (args[0] is DBNull) return DBNull.Value;
                    return ((DateTime)args[0]).AddHours(2);
                };

                this["loadbigarray"] = (args) =>
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new ArgumentException("LDAP-classes only supported in windows");
                    if (args[0] is DBNull) return DBNull.Value;
                    using (DirectoryEntry entry = (this.parentDsl.valueProvider as LdapValueProvider).context.GetDirectoryEntry())
                    {
                        List<String> result = getBigPropArray(entry, args[0].ToString());
                        //return String.Join("\n", result.ToArray()); // Sorgt für Memory Overflow (gigantische Strings mit 5000 Mitgliedern * 200 Zeichen = 1.000.000 - Zeichen Strings)
                        return result;
                    }
                };

                this["splitRows"] = (args) =>
                {
                    if (args[0] is DBNull) return DBNull.Value;
                    return args[0].ToString().Split('\n').ToList<String>();
                    // Anmerkung: Sobald eine List<String> zurückgegeben wird, sorgt dies für Splittung
                };
            }
            
		}

		/// <summary>
		/// Konstruktur - Erstellt eine neue LDAP-DSL für das Parsen und auswerten von LDAP-Ausdrücken
		/// </summary>
		public ldapDSL() : base(new LdapValueProvider(), new LdapFunctionHandler())
		{}
	}


	/// <summary>
	/// Spezifische Implementierung des DBInterface für OleDB-Access-MDB-Dateien
	/// </summary>
	class Import_LDAP : CustomInterfaceBase
	{
		/// <summary> DSL für Ausdrucksauswertung </summary>
		public ldapDSL dslDef = new ldapDSL();
		/// <summary> Evaluator für Ausdrucks-Tree-Erstellung die dann mit der DSL ausgewertet werden </summary>
		public CodeEvaluator codeEvaluator = new CodeEvaluator();

		/// <summary>
		/// Erzeugt ein neues MSSQL-Interface für die angegebene Verbindung
		/// </summary>
		/// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public Import_LDAP(string conString, Logger logger = null) : base(conString, logger)
		{
			this.isTransactional = false;
		}

		public override async Task<DataTable> readSelect(string query, ParameterDef[] parameters = null)
		{
			this.logger.logInfo(this.logSubject + "Start Query: '" + query + "'");

			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new ArgumentException("LDAP-classes only supported in windows");

            SqlParseTree parseTree = new SqlParseTree(this.logger, this.logSubject);
            parseTree.parse(query, false, false, new char[] {'.', '/', ':' });
			DataTable resultTable = parseTree.createDataTable();

            // CreateDataTable legt sinnlose Spalten an, die hier nicht benötigt werden - sofort löschen (für Berechnungen innerhalb DataTables)
            int countRemove = resultTable.Columns.Count - parseTree.columns.Count ;
            for ( int i = 0; i < countRemove ; i++)
            {
                resultTable.Columns.RemoveAt(resultTable.Columns.Count-1);
            }

			List<TransferTableColumn> cols = parseTree.createTransferTableColModel(null);

			// Suche ausführen
			bool firstRow = true;
			using (DirectoryEntry entry = new DirectoryEntry(parseTree.tables.Values.FirstOrDefault().alias))
			using (DirectorySearcher search = new DirectorySearcher(entry))
			{
				search.PageSize = 100; // Pagemax auf Server umgehen
				search.SearchScope = SearchScope.Subtree;
				search.PropertiesToLoad.AddRange(parseTree.tables.Values.FirstOrDefault().attributesToLoad.ToArray());
				search.Filter = parseTree.conditions;

				ldapDSL.LdapValueProvider ldapValueProvider = dslDef.valueProvider as ldapDSL.LdapValueProvider;

				// Suche ausführen und in DataTable speichern
				using (SearchResultCollection resultCol = search.FindAll())
				{
					bool splitRows = false;
					string splitCol = "";
					foreach (SearchResult result in resultCol)
					{
						ldapValueProvider.context = result; // Kontext für Evaluierung wechseln

						// Typanpassung der Spalten anhand der tatsächlich erhaltenen Werte - Führt sonst zu Fehlern beim Merge
						if (firstRow)
						{
							foreach (TransferTableColumn att in cols)
							{
								CodeElement el = codeEvaluator.parse(att.sourceCol);
								object value = codeEvaluator.evaluate<object>(el, dslDef);
								if (!(value is DBNull))
									if (value is List<String>)
										resultTable.Columns[att.targetCol].DataType = typeof(object); // weil Anfangs eine Liste drin steht und später ein split-String-Wert - also unklar ob List oder String!
									else
										resultTable.Columns[att.targetCol].DataType = value.GetType();
								else
									resultTable.Columns[att.targetCol].DataType = typeof(string);
							}
							firstRow = false;
						}

						// Erstelle Basiszeile
						DataRow dr = resultTable.NewRow();
						foreach (TransferTableColumn att in cols)
						{
                            CodeElement el = codeEvaluator.parse(att.sourceCol);
                            object value = codeEvaluator.evaluate<object>(el, dslDef);

							if (value is List<String>) // Falls eine String-Liste kommt, wird das Ergebnis gesplittet nach der Spalte
							{
								splitRows = true;
								splitCol = att.targetCol;
							}
							dr[att.targetCol] = value;
							
						}

						// Falls keine Splittung einfach zufügen
						if (!splitRows)
						{
							resultTable.Rows.Add(dr);
						}
						else 
						{   // Sonst versuche anhand der Spalte zu splitten sofern sie nicht null ist - wenn null, einfach zufügen
							if (dr[splitCol] != DBNull.Value)
							{
								
								foreach (string value in (List<String>)dr[splitCol])
								{
									// Pro Wert in Spalte füge Kopie der Spalte mit anderem Wert hinzu
									DataRow drTemp = resultTable.NewRow();
									foreach (TransferTableColumn att in cols)
									{
										if (att.targetCol == splitCol)
										{
											drTemp[att.targetCol] = value;
										}
										else
										{
											drTemp[att.targetCol] = dr[att.targetCol];
										}
									}
									resultTable.Rows.Add(drTemp);
								}
							}
							else
							{
								resultTable.Rows.Add(dr);
							}
							
						}
					}
					resultTable.AcceptChanges();
				}
			}

			this.logger.logInfo(this.logSubject + "Select Result for '" + query + "' are " + resultTable.Rows.Count);

			await Task.Run(() => { });
			return resultTable;
		}

	}
}
