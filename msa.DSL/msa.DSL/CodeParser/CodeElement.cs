using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.DSL.CodeParser
{
    /// <summary> Basisklasse für ein CodeElement eines ParseTrees des CodeEvaluators </summary>
    public abstract class CodeElement
    {
        /// <summary>Inhalt des Elements - Entweder ein konkreter Wert oder der Ausdruck der versucht wurde auszuwerten </summary>
        public string content { get; set; }
        /// <summary> Vaterknoten im ParseTree des Ausdrucks </summary>
        public CodeElement parent { get; set; }
        /// <summary> Rückgabewert des Elementteilausdrucks </summary>
        public Type elementType { get; set; }

        /// <summary>
        /// Evaluierungsfunktion um aus dem Knoten einen Wert zu erhalten
        /// </summary>
        /// <param name="dslDefinition">DSL-Definition die bei der Evaluierung verwendet werden soll</param>
        /// <returns>Element vom Typ elementType</returns>
        public abstract object evaluate(DSLDef dslDefinition = null);

        /// <summary>Liste der untergeordneten Ausdrücke, sofern vorhanden</summary>
        /// <returns>Liste der untergeordneten Ausdrücke, sofern vorhanden</returns>
        public abstract List<CodeElement> childElements();

        /// <summary> Ermittelt für das Element alle Unterelemente eines bestimmten Elementtyps, etwa Operation, Literal, usw. </summary>
        /// <typeparam name="T">Der gewünschte Rückgabetyp</typeparam>
        /// <returns>Liste der emittelten Elemente</returns>
        public List<CodeElement> childElementsOf<T>()
        {
            List<CodeElement> resultList = new List<CodeElement>();

            // Eigenprüfung für Root
            if (this.parent == null)
            {
                if (this is T) resultList.Add(this);
            }
            
            // Childprüfung
            List<CodeElement> childs = childElements();
            foreach (CodeElement child in childs)
            {
                if (child is T) resultList.Add(child);
                resultList.AddRange(child.childElementsOf<T>());
            }
            return resultList;
        }
    }

    
    /// <summary>
    /// Binäroperationsknoten aus einem ParseTree (+, -, *, usw.)
    /// </summary>
    public class CodeBinaryOp : CodeElement
    {
        /// <summary> Liste von Berechnungsoperatoren </summary>
        public static List<String> codeOperatorType = new List<string>() { "+", "-", "*", "/", "%", "&", "|" };
        /// <summary> Liste von Vergleichsoperatoren </summary>
        public static List<String> codeRelationType = new List<string>() { "<", "<=", ">", ">=", "=", "<>", "!=" };
        /// <summary> Liste von Logikoperatprem </summary>
        public static List<String> logicRelationType = new List<string>() { "&&", "||", "and", "or" };
        /// <summary> Liste aller bekannten binären Operatoren </summary>
        public static List<String> allTypes = codeOperatorType.Union(codeRelationType).Union(logicRelationType).ToList();

        /// <summary> Operatortyp , z.B. plus '+' </summary>
        public string operatorType { get; set; }
        /// <summary> Linker Operand der Binäroperation </summary>
        public CodeElement operand1 { get; set; }
        /// <summary> Rechter Operand der Binäroperation </summary>
        public CodeElement operand2 { get; set; }

        /// <summary> Wertet die Binäroperation aus </summary>
        /// <param name="dslDefinition">DSLDef die für die Evaluierung genutzt wird</param>
        /// <returns>Der ausgewertete Ausdruck</returns>
        public override object evaluate(DSLDef dslDefinition = null)
        {
            var op1 = operand1.evaluate(dslDefinition);
            var op2 = operand2.evaluate(dslDefinition);

            if (op1 == null || op2 == null) return false;
            if (op1 == DBNull.Value || op2 == DBNull.Value) return false;

            switch (operatorType)
            {
                case "+":
                    // Prüfung ob StringCat oder ZahlenAdd
                    double outOp1, outOp2;
                    if (Double.TryParse(op1.ToString(), out outOp1) && Double.TryParse(op2.ToString(), out outOp2))
                        return Convert.ToDouble(op1) + Convert.ToDouble(op2);
                    else
                        return op1.ToString() + op2.ToString();

                case "-": return Convert.ToDouble(op1) - Convert.ToDouble(op2);
                case "*": return Convert.ToDouble(op1) * Convert.ToDouble(op2);
                case "/": return Convert.ToDouble(op1) / Convert.ToDouble(op2);
                case "%": return Convert.ToInt32(op1) % Convert.ToInt32(op2);
                case "&": return Convert.ToInt32(op1) & Convert.ToInt32(op2);
                case "|": return Convert.ToInt32(op1) | Convert.ToInt32(op2);

                case "<": return Convert.ToDouble(op1) < Convert.ToDouble(op2);
                case "<=": return Convert.ToDouble(op1) <= Convert.ToDouble(op2);
                case ">": return Convert.ToDouble(op1) > Convert.ToDouble(op2);
                case ">=": return Convert.ToDouble(op1) >= Convert.ToDouble(op2);
                case "=": return op1.ToString() == op2.ToString();
                case "<>": case "!=": return op1.ToString() != op2.ToString();

                case "&&": case "and": return Convert.ToBoolean(op1) && Convert.ToBoolean(op2);
                case "||": case "or": return Convert.ToBoolean(op1) || Convert.ToBoolean(op2);
                default: throw new Exception("Unknown Operator " + this.operatorType);
            }
        }

        /// <summary>Liefert die Liste der beteiligten Argumente am Operator (2)</summary>
        /// <returns>Liefert die Liste der beteiligten Argumente am Operator (2)</returns>
        public override List<CodeElement> childElements()
        {
            return new List<CodeElement>() { this.operand1, this.operand2 };
        }

        /// <summary> Wandelt die Operation in die lesbare Schreibform um </summary>
        /// <returns>Textversion der Operation</returns>
        public override string ToString()
        {
            return this.operand1.ToString() + this.operatorType + this.operand2.ToString();
        }

    }

    /// <summary>Ein FunctionCall-Knoten im ParseTree</summary>
    public class CodeFunctionCall : CodeElement
    {
        /// <summary> Name der aufzurufenden Funktion </summary>
        public string functionName { get; set; }
        
        /// <summary> Parameter der Funktion </summary>
        public List<CodeElement> functionParameters { get; set; }

        /// <summary> Erstellt einen neuen FunctionCall-Knoten </summary>
        public CodeFunctionCall()
        {
            this.functionParameters = new List<CodeElement>();
        }

        /// <summary> Wertet den Funktionsaufruf aus </summary>
        /// <param name="dslDefinition">DSLDef die für die Evaluierung genutzt wird</param>
        /// <returns>Der ausgewertete Ausdruck</returns>
        public override object evaluate(DSLDef dslDefinition = null)
        {
            if (dslDefinition == null)
            {
                return "";
            }
            else
            {
                return dslDefinition.functionHandler.handleFunction(this.functionName, this.functionParameters);
            }
        }

        /// <summary> Parameter des Funktionsaufrufs </summary>
        /// <returns> Parameter des Funktionaufrufs</returns>
        public override List<CodeElement> childElements()
        {
            return this.functionParameters;
        }

        /// <summary> Wandelt den Funktionsaufruf in die lesbare Schreibform um </summary>
        /// <returns>Textversion des Funktionsaufrufs</returns>
        public override string ToString()
        {
            return this.functionName + "(" + String.Join( ", ", this.functionParameters.Select((c) => c.ToString())) + ")";
        }
    }

    /// <summary> Ein Literal-Ausdruck aus einem ParseTree - steht für einen konstanten Wert der in content steht </summary>
    public class CodeLiteral : CodeElement
    {
        /// <summary> Wertet das Literal aus ( da gibts nicht wirklich was auzuwerten außer Typ-Konvertierung) </summary>
        /// <param name="dslDefinition">DSLDef die für die Evaluierung genutzt wird</param>
        /// <returns>Der ausgewertete Ausdruck</returns>
        public override object evaluate(DSLDef dslDefinition = null)
        {
            return Convert.ChangeType(this.content, this.elementType, System.Globalization.CultureInfo.GetCultureInfo("en-us"));
        }

        /// <summary> Inhaltselemente des Literals (Leer) </summary>
        /// <returns> Inhalt des Literals (Leere Liste)</returns>
        public override List<CodeElement> childElements()
        {
            return new List<CodeElement>();
        }

        /// <summary> Wandelt das Literal in die lesbare Schreibform um </summary>
        /// <returns>Textversion des Literals</returns>
        public override string ToString()
        {
            if (this.elementType == typeof(string))
                return "'" + this.content + "'";
            else
                return this.content;
        }
    }

    /// <summary> Ein Boolean Literal-Ausdruck aus einem ParseTree - steht für einen konstanten boolean Wert der in content steht </summary>
    public class CodeBooleanLiteral : CodeElement
    {
        /// <summary>Varianten für Boolean Ausdrücke</summary>
        public static List<String> boolType = new List<string>() { "true", "false" };

        /// <summary> Wertet das Boolean Literal aus (da gibts nicht wirklich was auzuwerten außer Typ-Konvertierung) </summary>
        /// <param name="dslDefinition">DSLDef die für die Evaluierung genutzt wird</param>
        /// <returns>Der ausgewertete Ausdruck</returns>
        public override object evaluate(DSLDef dslDefinition = null)
        {
            return Convert.ToBoolean(this.content);
        }

        /// <summary> Inhaltselemente des Bool-Literals (Leer) </summary>
        /// <returns> Inhalt des Bool-Literals (Leere Liste)</returns>
        public override List<CodeElement> childElements()
        {
            return new List<CodeElement>();
        }

        /// <summary> Wandelt das Bool-Literal in die lesbare Schreibform um </summary>
        /// <returns>Textversion des Bool-Literals</returns>
        public override string ToString()
        {
            return this.content;
        }
    }

    /// <summary> Ein Null Literal-Ausdruck aus einem ParseTree - steht für einen konstanten null Wert der in content steht </summary>
    public class CodeNullLiteral : CodeElement
    {
        /// <summary>Varianten für Boolean Ausdrücke</summary>
        public static List<String> nullType = new List<string>() { "null" };

        /// <summary> Wertet das null Literal aus (immer null) </summary>
        /// <param name="dslDefinition">DSLDef die für die Evaluierung genutzt wird</param>
        /// <returns>null-Wert</returns>
        public override object evaluate(DSLDef dslDefinition = null)
        {
            return null;
        }

        /// <summary> Inhaltselemente des Null-Literals (Leer) </summary>
        /// <returns> Inhalt des Null-Literals (Leere Liste)</returns>
        public override List<CodeElement> childElements()
        {
            return new List<CodeElement>();
        }

        /// <summary> Wandelt das Null-Literal in die lesbare Schreibform um </summary>
        /// <returns>Textversion des Null-Literals</returns>
        public override string ToString()
        {
            return this.content;
        }
    }

    /// <summary> Ein Variablenverweis in einem ParseTree - in content steht der Variablenname </summary>
    public class CodeReference : CodeElement
    {
        /// <summary> Wertet den Variablenverweis zu einem tatsächlichen Wert aus, mit Hilfe des ValueProviders der DSL-Definition </summary>
        /// <param name="dslDefinition">DSLDef die für die Evaluierung genutzt wird</param>
        /// <returns>Der ausgewertete Ausdruck</returns>
        public override object evaluate(DSLDef dslDefinition = null)
        {
            if (dslDefinition == null)
            {
                return "";
            }
            else
            {
                return dslDefinition.valueProvider.getValue(this.content);
            }
        }

        /// <summary> Inhaltselemente der Referenz (Leer) </summary>
        /// <returns> Inhalt der Referenz (Leere Liste)</returns>
        public override List<CodeElement> childElements()
        {
            return new List<CodeElement>();
        }

        /// <summary> Wandelt die Referenz in die lesbare Schreibform um </summary>
        /// <returns>Textversion der Referenz</returns>
        public override string ToString()
        {
            return this.content;
        }
    }

    /// <summary>
    /// Eine Klammerung in einem ParseTree
    /// </summary>
    public class CodeBracing : CodeElement
    {
        /// <summary> Inhalt der Klammerung </summary>
        public CodeElement braceContent { get; set; }

        /// <summary> Wertet dden Wert innerhalb der Klammerung aus und gibt den Wert zurück </summary>
        /// <param name="dslDefinition">DSLDef die für die Evaluierung genutzt wird</param>
        /// <returns>Der ausgewertete Ausdruck</returns>
        public override object evaluate(DSLDef dslDefinition = null)
        {
            return braceContent.evaluate(dslDefinition);
        }

        /// <summary> Inhaltselemente des Klammerausdrucks </summary>
        /// <returns> Inhalt der Klammer</returns>
        public override List<CodeElement> childElements()
        {
            return new List<CodeElement>() { this.braceContent };
        }

        /// <summary> Wandelt den Klammerausdruck in die lesbare Schreibform um </summary>
        /// <returns>Textversion des Klammerausdrucks</returns>
        public override string ToString()
        {
            return "(" + this.braceContent.ToString() + ")";
        }


    }
}
