using msa.DSL.CodeParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.SQL
{
    /// <summary> Erlaubte JoinTypen für Custom-SQLs </summary>
    public enum SqlJoinType {
        /// <summary> INNER Join der Fehltreffer ausfiltert </summary>
        INNERJOIN
    }

    /// <summary> Stellt einen Join-Ausdruck auf einer Tabelle dar</summary>
    public class SqlJoinExpression
    {
        /// <summary>Gibt den Typ des Joins an</summary>
        public SqlJoinType joinType { get; set; }

        /// <summary>Join-Bedingung</summary>
        public string joinCondition { get; set; }

        /// <summary> Gibt die Tabelle auf der linken Seite des Joins an </summary>
        public SqlTableExpression baseTable { get; set; }

        /// <summary> Gibt die Tabelle auf der rechten Seite des Joins an </summary>
        public SqlTableExpression joinTable { get; set; }

        /// <summary> Der geparste Ausdruck der ON-Bedingung des Joins </summary>
        public CodeElement joinElement { get; set; }


        /// <summary> Der Teilausdruck für die ON-Bedingung des Joins die die rechte Seite (Tabelle) des Joins betrifft </summary>
        private CodeElement _joinTableEvaluation = null;
        /// <summary> Der Teilausdruck für die ON-Bedingung des Joins die die rechte Seite (Tabelle) des Joins betrifft </summary>
        public CodeElement joinTableEvaluation { get
            {
                if (_joinTableEvaluation == null)
                {
                    foreach (CodeReference codeRef in joinElement.childElementsOf<CodeReference>())
                    {
                        if (codeRef.content.Substring(0, codeRef.content.IndexOf(".")) == this.joinTable.alias)
                        {
                            _joinTableEvaluation = codeRef;
                            return _joinTableEvaluation;
                        }
                    }
                }

                return _joinTableEvaluation;
            }
        }

        /// <summary> Der Teilausdruck für die ON-Bedingung des Joins die die linke Seite (Tabelle) des Joins betrifft </summary>
        private CodeElement _baseTableEvaluation = null;
        /// <summary> Der Teilausdruck für die ON-Bedingung des Joins die die linke Seite (Tabelle) des Joins betrifft </summary>
        public CodeElement baseTableEvaluation
        {
            get
            {
                if (_baseTableEvaluation == null)
                {
                    foreach (CodeReference codeRef in joinElement.childElementsOf<CodeReference>())
                    {
                        if (codeRef.content.Substring(0, codeRef.content.IndexOf(".")) == this.baseTable.alias)
                        {
                            _baseTableEvaluation = codeRef;
                            return _baseTableEvaluation;
                        }
                    }
                }

                return _baseTableEvaluation;
            }
        }

        /// <summary>Rückübersetzung des JOIN-Typs in SQL-Syntax</summary>
        /// <returns>Die SQL-konforme Syntax für den JoinTyp der JoinExpression</returns>
        public string getJoinType()
        {
            switch (this.joinType)
            {
                case SqlJoinType.INNERJOIN: return " INNER JOIN ";
            }
            return "";
        }
    }
}
