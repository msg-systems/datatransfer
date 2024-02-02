using msa.Data.Transfer.Model;
using msa.DSL.CodeParser;
using msa.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace msa.Data.Transfer.SQL
{
	/// <summary>
	/// Ein Parsebaum für einen SQL-Ausdruck. Ermöglicht das parsen von SELECT-Expressions
	/// </summary>
	public class SqlParseTree
	{
		/// <summary> CodeEvaluator für die Parse-Tree-Instanz mit Konfiguration</summary>
		protected CodeEvaluator codeEvaluator = new CodeEvaluator();
		/// <summary> Ausgabedefinitionen aus dem SELECT-Teil inklusive Alias-Werten</summary>
		public List<SqlSelectExpression> columns { get; protected set; } = new List<SqlSelectExpression>();
		/// <summary>Liste der Tabellen die im Select involviert sind</summary>
		public Dictionary<String, SqlTableExpression> tables { get; protected set; } = new Dictionary<String, SqlTableExpression>();
		/// <summary>Klartext des WHERE-Teils des geparsten SQL-Ausdrucks</summary>
		public string conditions { get; set; } = "";
		/// <summary> Das geparste conditionsElement, welches dem Where-Teil entspricht - null wenn parseWhereWithEvaluator = false beim parsen war </summary>
		public CodeElement conditionsElement { get; set; }

		/// <summary>Der verwendete Logger für den Parser - defaut oder manuell gesetzt</summary>
		protected Logger p_logger;
		/// <summary>Der logPrefix der vor jeden Logeintrag gesetzt wird</summary>
		public string logSubject { get; set; }

		/// <summary>Der Logger des DBInterfaces - Default "msa.Data.Transfer.Database" - kann aber auch manuell gesetzt werden. Der Standard wird über den Switch msa.Data.Transfer.SqlParser gesteuert </summary>
		public Logger logger
		{
			get
			{
				if (this.p_logger == null)
				{
					this.p_logger = Logger.getLogger("msa.Data.Transfer.SqlParser");
				}
				return this.p_logger;
			}
			set
			{
				this.p_logger = value;
			}
		}

		/// <summary>
		/// Erstellt einen neuen Parser und erlaubt es einen Logger zu übergeben
		/// </summary>
		/// <param name="loggerParam">Ein alternativer Logger der verwendet werden soll</param>
		/// <param name="logSubject">Ein Prefix der vor das Log gehangen werden soll</param>
		public SqlParseTree(Logger loggerParam = null, string logSubject = null)
		{
			if (loggerParam != null) p_logger = loggerParam;
			this.logSubject = (logSubject == null ? "" : logSubject);
		}

		/// <summary>Setzt den geparsten Kontext komplett zurück</summary>
		protected void resetParseContext()
		{
			logger.logVerbose(logSubject + "Reset parse context");
			this.codeEvaluator.additionalIdentifierChars.Clear();
			this.tables.Clear();
			this.columns.Clear();
			this.conditions = "";
			this.conditionsElement = null;
		}

		/// <summary>
		/// Parst einen SQL-Ausdruck in die SQLParseTree-Struktur
		/// </summary>
		/// <param name="query">Die zu parsende Query</param>
		/// <param name="parseWhereWithEvaluator">Gibt an ob der WHERE-Teil der Query mit der DSL-Language geparst werden soll - Dies ist z.B. nicht sinnvoll wenn das Zielsystem eine eigene Anfragesprache besitzt die man nur durchrouten möchte</param>
		/// <param name="parseFrom">Gibt an ob der From-Teil geparst werden soll - Dies ist z.B. nicht sinnvoll wenn das Zielsystem eine eigene Ortsangabe mit eigenen Klassen besitzt und man nur durchrouten möchte</param>
		/// <param name="additionalAllowedIdentifierChars">Gibt zusätzliche Zeichen neben Alphabet, Zahlen und underline an, die in einem Identifier (Spaltennamen) im Zielsystem vorkommen dürfen - beim ParseTree ist '.' immer sinnvoll da in SQL die Tabelle und die Spalte voneinander getrennt sind </param>
		public void parse(string query, bool parseWhereWithEvaluator = true, bool parseFrom = true, char[] additionalAllowedIdentifierChars = null)
		{
			this.resetParseContext();

			logger.logInfo(logSubject + $"Start parsing {query}");
			logger.logVerbose(logSubject + $"parseWhereEval = { parseWhereWithEvaluator}, parseFrom = { parseFrom}, additionalChars = {new String(additionalAllowedIdentifierChars)}");

			if (additionalAllowedIdentifierChars != null)
				this.codeEvaluator.additionalIdentifierChars.AddRange(additionalAllowedIdentifierChars);

			query = query.TrimEnd(';');
			// Parsen des Ausdrucks
			Regex pattern = new Regex(@"SELECT (.+?)(?=FROM)FROM\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			Match m = pattern.Match(query);
			if (!m.Success) throw new ArgumentException(@"query not in Format ' SELECT (.*) FROM([^\s]+) '");
			this.conditions = "";

			// Parse From
			string from = m.Groups[2].Value.Replace('\t', ' ').Trim();
			if (from.ToUpper().Contains(" WHERE ")){
				// Get Where
				this.conditions = from.Substring(from.ToUpper().IndexOf("WHERE") + 6).Trim();
				logger.logVerbose(logSubject + " from-part contains where " + this.conditions);
				from = from.Substring(0, from.ToUpper().IndexOf("WHERE")).Trim();
			}
			logger.logVerbose(logSubject + "start parsing from " + from);
			if (parseFrom)
			{
				while (from.Length > 0)
				{
                    int tabEndIndex = from.IndexOf(' ');
                    string tabRealName = null;
                    if (tabEndIndex == -1)
                    {
                        tabRealName = from;
                        from = "";
                    }
                    else
                    {
                        tabRealName = from.Substring(0, tabEndIndex).Trim();
                        from = from.Substring(tabEndIndex).Trim();
                    }
                    //CodeElement fromPart = codeEvaluator.parse(from, true); // parse n-te Tabelle
                    //from = codeEvaluator.unparsedExpression.Trim();
                    //logger.logVerbose(logSubject + "parsed subexpression from: '" + codeEvaluator.parsedExpression + "' - remainer " + codeEvaluator.unparsedExpression );
                    logger.logVerbose(logSubject + "parsed subexpression from: '" + tabRealName + "' - remainer " + from);

                    if (from.Length > 0) // prüfe auf AS oder , für nächste Spalte
					{
						int commaIndex = from.IndexOf(",");
						string subExpression = "";
						if (commaIndex == -1)
						{
							// kein Komma gefunden - potentiell steht 'as ' dahinter ODER ein join-Ausdruck, es gibt aber keinen subparse-Ausdruck mehr deswegen = ""
							subExpression = from;
							from = "";
						}
						else
						{
							// Hole den Teil bis zum Komma und prüfe ihn auf 'as '. Da Komma vorhanden, muss ein weiteres Subparse durchgeführt werden für die nächste Spalte
							subExpression = from.Substring(0, commaIndex).TrimStart();
							from = from.Substring(commaIndex + 1).TrimStart();
						}
						if (subExpression == "")
						{
                            // Einfach nur ein Kommma - nächste Spalte folgt
                            /*this.tables.Add(codeEvaluator.parsedExpression.Trim(), new SqlTableExpression()
							{
								expression = codeEvaluator.parsedExpression.Trim(),
								alias = codeEvaluator.parsedExpression.Trim(),
								parent = this
							});*/
                            this.tables.Add(tabRealName, new SqlTableExpression()
                            {
                                expression = tabRealName,
                                alias = tabRealName,
                                parent = this
                            });
                        }
						else if (subExpression.ToLower().StartsWith("as "))
						{
							logger.logVerbose(logSubject + "found 'as' Alias");
							subExpression = subExpression.Substring(3).Trim(); // kürze 'as ' weg
																			   // Prüfe auf gültigen Identifier
							Match tabMatch = Regex.Match(subExpression, @"^[a-zA-Z0-9_]*");
							if (tabMatch.Success)
							{
                                // Gültiger As-Ausdruck
                                /*SqlTableExpression tabEx = new SqlTableExpression()
								{
									expression = codeEvaluator.parsedExpression.Trim(),
									alias = tabMatch.Groups[0].Value,
									parent = this
								};*/
                                SqlTableExpression tabEx = new SqlTableExpression()
                                {
                                    expression = tabRealName,
                                    alias = tabMatch.Groups[0].Value,
                                    parent = this
                                };
                                this.tables.Add(tabEx.alias, tabEx);
								subExpression = subExpression.Substring(tabMatch.Groups[0].Value.Length).Trim();

								if (subExpression.Length > 0) this.parseJoin(subExpression);
							}
							else
							{
                                //throw new Exception("Unexpected table-Identifier for " + codeEvaluator.parsedExpression + " of " + codeEvaluator.unparsedExpression);
                                throw new Exception("Unexpected table-Identifier for " + tabRealName + " of " + from);
                            }
						}
						else if (subExpression.ToLower().Contains("join "))
						{
							logger.logVerbose(logSubject + "found 'join' Alias");
							/*SqlTableExpression tabEx = new SqlTableExpression()
							{
								expression = codeEvaluator.parsedExpression.Trim(),
								alias = codeEvaluator.parsedExpression.Trim(),
								parent = this
							};*/
                            SqlTableExpression tabEx = new SqlTableExpression()
                            {
                                expression = tabRealName,
                                alias = tabRealName,
                                parent = this
                            };
                            this.tables.Add(tabEx.alias, tabEx);
							this.parseJoin(subExpression);
						}
						else
						{
                            //throw new Exception("Unexpected table-Identifier for " + codeEvaluator.parsedExpression + " of " + codeEvaluator.unparsedExpression);
                            throw new Exception("Unexpected table-Identifier for " + tabRealName + " of " + from);
                        }
					}
					else
					{
                        // letzter Ausdruck des Parse-Ausdrucks ohne as
                        /*this.tables.Add(codeEvaluator.parsedExpression.Trim(), new SqlTableExpression()
						{
							expression = codeEvaluator.parsedExpression.Trim(),
							alias = codeEvaluator.parsedExpression.Trim(),
							parent = this
						});*/
                        this.tables.Add(tabRealName, new SqlTableExpression()
                        {
                            expression = tabRealName,
                            alias = tabRealName,
                            parent = this
                        });
                    }
				}
			}
			else // Wenn nicht parseFrom, stur übernehmen ohne parsing
			{
				this.tables.Add(from, new SqlTableExpression()
				{
					expression = from,
					alias = from,
					parent = this
				});
			}

			DataTable resultTable = new DataTable();
			string selectExpression = m.Groups[1].Value.Trim();

			// parse where
			logger.logVerbose(logSubject + "start parsing where " + from);
			if (!String.IsNullOrWhiteSpace(this.conditions) && parseWhereWithEvaluator)
			{
				logger.logVerbose(logSubject + "where = " + this.conditions);
				CodeElement whereElement = codeEvaluator.parse(this.conditions);
				List<CodeElement> whereReferences = whereElement.childElementsOf<CodeReference>();
				foreach (CodeReference whereReference in whereReferences)
				{
					SqlSelectExpression s = new SqlSelectExpression(whereReference, whereReference.content);
					if (s.baseTable != null)
						this.tables[s.baseTable].attributesToLoad.Add(whereReference.content);
					else
						this.tables.Values.FirstOrDefault().attributesToLoad.Add(whereReference.content);
				}
				this.conditionsElement = whereElement;
			}

			// Parse Select
			logger.logVerbose(logSubject + "start parsing select " + from);
			while (selectExpression.Length > 0)
			{
				if (selectExpression.Trim() == "*") throw new ArgumentException("SELECT * is not allowed in noSQL queries");
				CodeElement selectPart = codeEvaluator.parse(selectExpression, true); // parse n-te Spalte
				logger.logVerbose(logSubject + "parsed subexpression select: '" + codeEvaluator.parsedExpression + "' - remainer " + codeEvaluator.unparsedExpression);

				foreach (CodeReference codeRef in selectPart.childElementsOf<CodeReference>())
				{
					SqlSelectExpression s = new SqlSelectExpression(codeRef, selectPart.content);
					SqlTableExpression tempTab;
					if (s.baseTable != null)
						tempTab = this.tables[s.baseTable];
					else
						tempTab = this.tables.Values.FirstOrDefault();

					if (!tempTab.attributesToLoad.Contains(codeRef.content))
					{
						tempTab.attributesToLoad.Add(codeRef.content);
					}
				}

				selectExpression = codeEvaluator.unparsedExpression.Trim();
				if (selectExpression.Length > 0) // prüfe auf AS oder , für nächste Spalte
				{
					int commaIndex = selectExpression.IndexOf(",");
					string subExpression = "";
					if (commaIndex == -1)
					{
						// kein Komma gefunden - potentiell steht 'as ' dahinter, es gibt aber keinen subparse-Ausdruck mehr deswegen = ""
						subExpression = selectExpression;
						selectExpression = "";
					}
					else
					{
						// Hole den Teil bis zum Komma und prüfe ihn auf 'as '. Da Komma vorhanden, muss ein weiteres Subparse durchgeführt werden für die nächste Spalte
						subExpression = selectExpression.Substring(0, commaIndex).Trim();
						selectExpression = selectExpression.Substring(commaIndex + 1);
					}
					if (subExpression == "")
					{
						// Einfach nur ein Kommma - nächste Spalte folgt
						this.columns.Add(new SqlSelectExpression(selectPart, codeEvaluator.parsedExpression.Trim()));
					}
					else if (subExpression.ToLower().StartsWith("as "))
					{
						logger.logVerbose(logSubject + "found 'as' Alias");
						subExpression = subExpression.Substring(3).Trim(); // kürze 'as ' weg
																		   // Prüfe auf gültigen Identifier
						if (Regex.IsMatch(subExpression, @"^[a-zA-Z0-9_]*$"))
						{
							// Gültiger As-Ausdruck
							this.columns.Add(new SqlSelectExpression(selectPart, subExpression));
						}
						else
						{
							throw new Exception("Unexpected col-Identifier for " + codeEvaluator.parsedExpression + " of " + codeEvaluator.unparsedExpression);
						}
					}
					else
					{
						throw new Exception("Unexpected col-Identifier for " + codeEvaluator.parsedExpression + " of " + codeEvaluator.unparsedExpression);
					}
				}
				else
				{
					// letzter Ausdruck des Parse-Ausdrucks ohne as
					this.columns.Add(new SqlSelectExpression(selectPart, codeEvaluator.parsedExpression.Trim()));
				}
			}

			// Ordne Select-Spalten den Basistabellen zu inklusive Maps
			logger.logVerbose(logSubject + "Assign detected base columns to tables");
			foreach (SqlTableExpression tab in this.tables.Values)
			{
				List<SqlSelectExpression> selExps = this.columns;
				if (this.tables.Count > 1)
					{ selExps = selExps.Where((c) => c.baseTable == tab.alias).ToList(); }

				int uniqueCounter = 0;
				foreach (SqlSelectExpression se in selExps)
				{
					if (se.expressionElement.childElementsOf<CodeReference>().Count > 0)
					{
						tab.attributeMap.Add(se.alias, se);
						if (se.hasAlias)
                        {
							// Ausnahme falls Spalte mehrfach auf verschiedene Ziel-Attribute gemappt wird
							if (tab.attributeMap.ContainsKey(se.expression)){
								uniqueCounter += 1;
								tab.attributeMap.Add(se.expression + new string(' ', uniqueCounter) , se);
							} 
							else
							{
								tab.attributeMap.Add(se.expression, se);
							}
						}
							
					}
				}
			}
		}

		private void parseJoin(string expression)
		{
			string addChars = "";
			foreach (char c in this.codeEvaluator.additionalIdentifierChars)
			{
				addChars += c;
				if (c == '\\') addChars += "\\";
			}
			if (expression.Trim().ToUpper().StartsWith("INNER JOIN"))
			{
				logger.logVerbose(logSubject + "parse INNER JOIN: '" + expression);

				// Prüfe Join-Condition
				string subExpression = expression.Trim().Substring(10).Trim();

				//Match tabMatch = Regex.Match(subExpression, @"^[a-zA-Z0-9_" + addChars + "]*");
				Match tabMatch = Regex.Match(subExpression, @"^[^\s]*"); // Da könnten auch :::Param=Wert -Ausdrücke drin sein mit beliebigem Text
				string tabName = tabMatch.Groups[0].Value;
				string asName = tabName;
				subExpression = subExpression.Substring(tabMatch.Groups[0].Value.Length).Trim();

				if (subExpression.ToLower().StartsWith("as "))
				{
					logger.logVerbose(logSubject + "alias for table " + tabName + " detected");
					subExpression = subExpression.Substring(3).Trim(); // kürze 'as ' weg
																	   // Prüfe auf gültigen Identifier
					tabMatch = Regex.Match(subExpression, @"^[a-zA-Z0-9_" + addChars + "]*");
					if (tabMatch.Success)
					{
						// alles nach dem as XXX
						subExpression = subExpression.Substring(tabMatch.Groups[0].Value.Length).Trim();
						asName = tabMatch.Groups[0].Value;
					}
					else
					{
						throw new Exception("Unexpected table-Identifier for " + codeEvaluator.parsedExpression + " of " + codeEvaluator.unparsedExpression);
					}
				}

				if (!subExpression.ToUpper().StartsWith("ON ")) throw new ArgumentException("Inner join not followed by ON ");
				subExpression = subExpression.Substring(2).Trim();
				string condition = subExpression;
				if (subExpression.ToUpper().Contains("INNER JOIN"))
				{
					condition = subExpression.Substring(0, subExpression.ToUpper().IndexOf("INNER JOIN"));
				}

				logger.logVerbose(logSubject + "parse INNER JOIN condition: '" + condition);
				CodeElement parseElement = codeEvaluator.parse(condition);
				subExpression = subExpression.Substring(condition.Length);

				string[] baseTables = parseElement.childElementsOf<CodeReference>().Select(
					(cr) =>
					{
						int index = cr.content.IndexOf(".");
						if (index == -1) throw new ArgumentException("Join-Condition " + codeEvaluator.parsedExpression + " does not use aliases for columns");
						return cr.content.Substring(0, index);
					}
				  ).ToArray();
				string otherTableAlias = baseTables.FirstOrDefault((bt) => bt != asName);
				if (!this.tables.ContainsKey(otherTableAlias)) throw new ArgumentException("Basetable alias " + otherTableAlias + " from join condition " + codeEvaluator.parsedExpression + " is unknown");
				SqlTableExpression otherTable = this.tables[otherTableAlias];

				// Gültiger As-Ausdruck
				SqlTableExpression tabEx = new SqlTableExpression()
				{
					expression = tabName,
					alias = asName,
					parent = this,
					join = new SqlJoinExpression()
					{
						joinType = SqlJoinType.INNERJOIN,
						joinCondition = condition,
						joinElement = parseElement,
						baseTable = otherTable
					}
				};
				tabEx.join.joinTable = tabEx;
				this.tables.Add(tabEx.alias, tabEx);

				if (String.IsNullOrWhiteSpace(subExpression)) return;
				if (subExpression.StartsWith(",")) return; // weiter regulär parsen

				// sonst Restausdruck wieder als Join behandeln
				parseJoin(subExpression);
			}
			else
			{
				throw new ArgumentException("Unknown or not a Join Type " + expression);
			}
		}

		/// <summary>
		/// Erzeugt eine leere DataTable mit den korrekten Spalten für die aktuelle Parse-Instanz. Dabei kann man eine Tablle der Quelltabellen 
		/// angeben womit nur Spalten erzeugt werden die aus genannter Tabelle stammen
		/// </summary>
		/// <param name="forOrigin">Angabe der Tabelle die als Filter dient um die erzeugten Spalten der DataTable einzuschränken</param>
		/// <returns>Eine DataTable mit den Spalten für die Abbildung der Teildaten der angegbeenen Origin-Tabelle</returns>
		public DataTable createDataTable(string forOrigin = null)
		{
			DataTable dt = new DataTable();
			// BasisAttribute
			foreach (SqlSelectExpression col in this.columns.Where( (c) => c.baseTable == forOrigin && c.expressionElement is CodeReference) )
			{
				if (col.alias == col.expression)
					dt.Columns.Add(col.colName, typeof(object));
				else
					dt.Columns.Add(col.alias, typeof(object));
			}

			// berechnete Attribute
			foreach (SqlSelectExpression col in this.columns.Where( (c => !(c.expressionElement is CodeReference) ) ))
			{
				// Mehrfachspaltenberechnungen weglassen
				if (col.baseTables != null && col.baseTables.Count > 1) continue;

                List<CodeElement> subCodeRefs = col.expressionElement.childElementsOf<CodeReference>();
                if (subCodeRefs.Count == 0)
                {
                    dt.Columns.Add(col.alias, typeof(object));
                }
                else
                {
                    foreach (CodeReference codeRef in subCodeRefs)
                    {
                        if (forOrigin == null)
                        {
                            if (!dt.Columns.Contains(col.alias))
                                dt.Columns.Add(col.alias, typeof(object));
                        }
                        else if (codeRef.content.Contains("."))
                        {
                            string baseTable = codeRef.content.Substring(0, codeRef.content.IndexOf('.'));
                            if (baseTable == forOrigin)
                            {
                                // berechnete Spalten haben immer einen Alias
                                if (!dt.Columns.Contains(col.alias))
                                    dt.Columns.Add(col.alias, typeof(object));
                            }
                        }
                    }
                }
			}

			// Nötige Attribute für Berechnungen
			// Füge CodeRefs zu, die nicht in den Spalten enthalten sind
			SqlTableExpression tab = (forOrigin == null? this.tables.Values.FirstOrDefault() : this.tables[forOrigin]);
			foreach (string colName in tab.attributesToLoad)
			{
				if (!tab.attributeMap.Keys.Contains(colName))
				{
					CodeElement expression = codeEvaluator.parse(colName);
					dt.Columns.Add(colName.Replace(".", "_"), typeof(object));

					tab.attributeMap.Add(colName, new SqlSelectExpression(expression, codeEvaluator.parsedExpression.Replace(".", "_")));
				}
			}

			return dt;
		}

		/// <summary>
		/// Erzeugt ein Spaltenmapping mit den korrekten Spalten für die aktuelle Parse-Instanz. Dabei kann man eine Tablle der Quelltabellen 
		/// angeben womit nur Mappings erzeugt werden die aus genannter Tabelle stammen
		/// </summary>
		/// <param name="forOrigin">Angabe der Tabelle die als Filter dient um die erzeugten Mappings einzuschränken</param>
		/// <returns>Mappings für die Spalten für die Abbildung der Teildaten der angegbeenen Origin-Tabelle</returns>
		public List<TransferTableColumn> createTransferTableColModel(string forOrigin)
		{
			List<TransferTableColumn> cols = new List<TransferTableColumn>();
			foreach (SqlSelectExpression col in this.columns.Where((c) => c.baseTable == forOrigin))
			{
				cols.Add(new TransferTableColumn() { sourceCol = col.expression, targetCol = col.alias });
			}
			
			return cols;
		}

		/// <summary>
		/// Tätigt die Rückumwandlung des Parse-Baums in ein SQL-Statement 
		/// </summary>
		/// <returns> Das SQL-Statement welches durch den Parse-Tree abgebildet ist </returns>
		public override string ToString()
		{
			string result = "SELECT " + String.Join(", ", columns.Select((c) => c.expression + " AS " + c.alias)) + " FROM ";

			// alle non Join-Tabellen
			result += String.Join(", ", tables.Values.Where((t) => t.join == null).Select((t) => t.expression + " AS " + t.alias));

			// alle Join Tabellen
			result += String.Join(" ", tables.Values.Where((t) => t.join != null).Select((t) => t.join.getJoinType() + t.expression + " AS " + t.alias + " ON " + t.join.joinCondition));

			if (!String.IsNullOrWhiteSpace(conditions)) result += " WHERE " + conditions; 

			return result;
		}
	}
}
