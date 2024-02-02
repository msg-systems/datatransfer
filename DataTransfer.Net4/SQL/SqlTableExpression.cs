using msa.Data.Transfer.Database.Custom;
using msa.Data.Transfer.Model;
using msa.DSL.CodeParser;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.SQL
{
    /// <summary> Ein Tabellenausdruck in einem geparsten SQL-Tree (FROM-Teil) </summary>
    public class SqlTableExpression
    {
        /// <summary> Verweis zum ParseTree zu dem diese Tabelle gehört</summary>
        public SqlParseTree parent { get; internal protected set; }

        /// <summary> Der Ausdruck der für diese Tabelle erfasst wurde</summary>
        public string expression { get; set; }
        /// <summary> Der Alias Wert der Tabelle - im Zweifel identisch wie die expression </summary>
        public string alias { get; set; }

        /// <summary>Gibt an ob ein echter Alias vorliegt, also ob expression ungleich alias</summary>
        public bool hasAlias { get
            {
                return alias != expression;
            }
        }

        /// <summary>Menge der Einzelattribute aus der Tabelle die geladen werden sollten, um den SELECT-ParseTree zu genügen. Dies umfasst berechnete Spalten, 
        /// Where-Bedingungen und Join-Bedingungen </summary>
        public List<string> attributesToLoad { get; internal protected set; } = new List<string>();

        /// <summary>Das AttributeMap enthält Infos dazu mit welchem Ausdruck die Spalten/Aliase in der Originaltabelle wirklich gefunden werden, in dem auf den entsprechenden SQLSelect-Part verwiesen wird</summary>
        public Dictionary<string, SqlSelectExpression> attributeMap { get; internal protected set; } = new Dictionary<string, SqlSelectExpression>();

        /// <summary>Gibt einen möglichen Join dieser Tabelle (Basis) mit einer Join-Tabelle an</summary>
        public SqlJoinExpression join { get; set; }

        /// <summary> Erstellt eine DataTable mit allen nötigen Spalten die nur diese Tabelle betreffen - komplexere Spalten aus berechneten verschiedenen Tabellen herkommend werden nicht zugefügt</summary>
        /// <returns>Eine leere DataTable mit den entsprechenden Spalten</returns>
        public DataTable createDataTable()
        {
            if (this.hasAlias)
                return parent.createDataTable(alias);
            else
                return parent.createDataTable(null);
        }

        /// <summary> Erstellt eine TransferTableColumnList mit allen nötigen SELECT-Spalten. Primär benötigt wenn man eine Read-Implementierung durchführt die eigene Anfragesprachen nutzt z.B. LDAP </summary>
        /// <returns>Eine TransferTableColumnList mit dem entsprechenden Spalten-Mapping</returns>
        public List<TransferTableColumn> createTransferTableColModel()
        {
            if (this.hasAlias)
                return parent.createTransferTableColModel(alias);
            else
                return parent.createTransferTableColModel(null);
        }

        /// <summary>Ermittelt alle direkt auflösbaren Attribute für diese Tabelle, die bei einer Implementierung von <see cref="CustomInterfaceBase.fillFromSQLParseTree"/> befüllt werden sollten </summary>
        /// <returns>Liste vpn SqlSelectExpressions die befüllt werden müssen</returns>
        public List<SqlSelectExpression> getDirectColReferences()
        {
            List<SqlSelectExpression> codeEl = new List<SqlSelectExpression>();
            foreach (SqlSelectExpression selEx in this.attributeMap.Values)
            {
                if (selEx.expressionElement is CodeReference)
                {
                    codeEl.Add(selEx);
                }
            }
            return codeEl;
        }
    }
}
