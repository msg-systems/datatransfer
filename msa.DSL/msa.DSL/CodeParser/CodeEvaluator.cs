using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.DSL.CodeParser
{
    /// <summary>
    /// Stellt Funktionen bereit um DSL-Ausdrücke zu parsen und zu evaluieren
    /// </summary>
    public class CodeEvaluator
    {
        /// <summary> Gibt an welcher Ausdruck bei parse zuletzt eingelesen wurde </summary>
        public string parsedExpression { get; protected set; }
        /// <summary> Gibt an welcher Restausdruck bei parse zuletzt übrig geblieben ist ( nur wenn parsePartial = true angegeben) </summary>
        public string unparsedExpression { get; protected set; }

        /// <summary>Erlaubt einer Sprache weitere Identifizierungszeichen für Ausdrücke zu erlauben - Im Standard sind nur Buchstaben und Zahlen erlaubt</summary>
        public List<char> additionalIdentifierChars { get; set; } = new List<char>();

        /// <summary> Parst einen Ausdruck in einen Parse-Tree - es findet nur eine Basissyntaxprüfung wie Klammerungsprüfung statt - keine Typprüfung </summary>
        /// <param name="expression"> Der Ausdruck der geparst werden soll</param>
        /// <param name="parsePartial"> Der Ausdruck der geparst werden soll</param>
        /// <returns>Ein Parse-Tree des übergebenen Ausdrucks</returns>
        public CodeElement parse(string expression, bool parsePartial = false)
        {
            // Parsen
            this.parsedExpression = "";
            this.unparsedExpression = "";
            CodeElement elem = null;
            expression = expression.Trim();
            int output = 0;
            int offset = 0;
            int dummyIndex = 0;
            elem = this.parse(expression, null, out output);
            offset += output;

            // Parse bis kein Operator mehr gefunden bzw. Ende des Ausdrucks gefunden
            string partialExpression = this.shortenString(expression.Substring(output), ref offset);
            while (partialExpression.Length > 0)
            {
                char c = partialExpression[0];
                char c2 = ' ';
                if (partialExpression.Length > 1) c2 = partialExpression[1];
                string lookAhead = this.readText(partialExpression, 0, out dummyIndex);

                if (CodeBinaryOp.allTypes.Contains(c.ToString()) || CodeBinaryOp.allTypes.Contains(c.ToString() + c2.ToString()) || CodeBinaryOp.logicRelationType.Contains(lookAhead.ToLower()) || CodeBinaryOp.logicRelationType.Contains(c.ToString() + c2.ToString())) // Operator und Referenz
                {
                    int outIndex = 0;
                    elem = this.parseCodeBinaryOp(partialExpression, null, elem, out outIndex);
                    offset += outIndex;
                    partialExpression = this.shortenString(partialExpression.Substring(outIndex), ref offset);
                }
                else
                {
                    if (parsePartial)
                    {
                        this.parsedExpression = expression.Substring(0, offset);
                        this.unparsedExpression = expression.Substring(offset);
                        return elem;
                    }
                    else
                    {
                        throw new ArgumentException("Non-Operator on root-level found");
                    }
                }
            }
            this.parsedExpression = expression;
            return elem;
        }

        /// <summary> parst einen Teilausdruck für ein vorhandenes Elternteil und gibt die menge der Zeichen zurück die ausgewertet wurden </summary>
        /// <param name="expression">Der auszuwertende Ausdruck</param>
        /// <param name="parent">Knoten die bei der Auswertung entstehen sollen diesem Knoten als Child zugeordnet werden </param>
        /// <param name="outPosition">Die Menge an Zeichen die im Ausdruck abgearbeitet worden und die Stelle an der die übergeordnete Auswertung weiterarbeiten muss</param>
        /// <returns></returns>
        protected CodeElement parse( string expression, CodeElement parent, out int outPosition)
        {
            int len = expression.Length;
            int outIndex = 0;
            int dummyIndex = 0;
            int trimmedOffsetSum = 0;
            string cache = "";

            for( int i = 0; i < len; i++)
            {
                char c = expression[i];
                if (Char.IsLetter(c) || additionalIdentifierChars.Contains(c)) // Function oder Reference oder KeyWord
                {
                    cache = this.readText(expression, i, out outIndex);
                    i = outIndex; // Index nach vorne verschieben
                    string partialExpression = this.shortenString(expression.Substring(i), ref trimmedOffsetSum);

                    if (partialExpression.Length == 0)
                    {
                        // Könnte statt Code Reference auch ein BooleanLiteral sein
                        CodeElement referenceElement = this.createKeyWordOrReference(cache, parent);
                        /*CodeReference referenceElement = new CodeReference()
                        {
                            content = cache,
                            elementType = typeof(object),
                            parent = parent
                        };*/
                        outPosition = i;
                        return referenceElement;
                    }

                    c = partialExpression[0];
                    char c2 = ' ';
                    if (partialExpression.Length > 1) c2 = partialExpression[1];
                    string lookAhead = this.readText(partialExpression, 0, out dummyIndex);
                    if (c == '(') // FunctionCall
                    {

                        CodeElement funcCall = parseCodeFunctionCall(partialExpression, parent, cache, out outIndex);
                        outPosition = i + outIndex + trimmedOffsetSum;
                        return funcCall;
                    } 
                    else if (CodeBinaryOp.allTypes.Contains( c.ToString() ) || CodeBinaryOp.allTypes.Contains(c.ToString() + c2.ToString()) || CodeBinaryOp.logicRelationType.Contains(lookAhead.ToLower())) // Operator und Referenz
                    {
                        CodeElement referenceElement = this.createKeyWordOrReference(cache, null);
                        /*CodeReference referenceElement = new CodeReference()
                        {
                            content = cache,
                            elementType = typeof(object),
                            parent = null
                        };*/
                        if (i == expression.Length)
                        {
                            outPosition = i;
                            return referenceElement;
                        }

                        CodeBinaryOp binaryOp = this.parseCodeBinaryOp(partialExpression, parent, referenceElement, out outIndex);
                        outPosition = i + outIndex + trimmedOffsetSum;
                        return binaryOp;
                    }
                    else
                    {
                        // Evt. Identifier
                        CodeElement referenceElement = this.createKeyWordOrReference(cache, parent);
                        /*CodeReference referenceElement = new CodeReference()
                        {
                            content = cache,
                            elementType = typeof(object),
                            parent = parent
                        };*/
                        outPosition = i;
                        return referenceElement;

                        throw new ArgumentException("Parsing - Expected FunctionCall or Operator -> " + expression );
                    }

                }
                else if (Char.IsNumber(c)) // Zahlenliteral
                {
                    cache = this.readNumber(expression, i, out outIndex);
                    i = outIndex; // Index nach vorne verschieben

                    CodeLiteral literalElement = new CodeLiteral()
                    {
                        content = cache,
                        elementType = typeof(double),
                        parent = parent
                    };

                    CodeElement elem = this.parseAdditionalOperands(expression, parent, literalElement, i, ref outIndex);
                    outPosition = outIndex;
                    return elem;
                }
                else if (c == '(') // Öffnende Klammer für Klammerungsausdruck
                {
                    CodeBracing braceElement = new CodeBracing()
                    {
                        content = expression,
                        elementType = typeof(object),
                        parent = parent
                    };
                    // Öffnende Klammer
                    i += 1; 
                    string partialExpression = this.shortenString(expression.Substring(1), ref trimmedOffsetSum);

                    // Innerhalb der Klammer
                    braceElement.braceContent = this.parse(partialExpression, braceElement, out outIndex);
                    i += outIndex;
                    partialExpression = this.shortenString(partialExpression.Substring(outIndex), ref trimmedOffsetSum);
                    i += trimmedOffsetSum; // Entfernte Leerzeichen dem Startpunkt zufügen, sonst Probleme

                    CodeElement elem = this.parseAdditionalOperands(expression, parent, braceElement, i, ref outIndex);
                    c = partialExpression[0];
                    if (c == ')')
                    {
                        outPosition = outIndex + 1; // +1 für Klammer )
                        return elem;
                    }
                    else
                    {
                        throw new Exception("Expected closing brace " + expression + " at " + partialExpression);
                    }
                }
                else if (c == '\'')
                {
                    cache = readUntilDelmiterWithEscaping(expression, i + 1, '\'', out outIndex);
                    i = outIndex; // Index nach vorne verschieben

                    CodeLiteral literalElement = new CodeLiteral()
                    {
                        content = cache,
                        elementType = typeof(string),
                        parent = parent
                    };

                    CodeElement elem = this.parseAdditionalOperands(expression, parent, literalElement, i, ref outIndex);
                    outPosition = outIndex + 1; // +1 für schließendes '
                    return elem;
                }

            }

            outPosition = len;
            return null;
        }

        /// <summary> Versucht den übergebenen Teilausdruck als Binäroperation zu parsen. Daher wird der bereits linksseitige Ausdruck als 1. Operand übergeben - Führt zu einem Parent-Change im Baum </summary>
        /// <param name="expression"> Ausdruck der als Binäroperation interpretiert werden soll </param>
        /// <param name="parent">Parent-Knoten für die Binäroperation</param>
        /// <param name="firstOperand">Der erste Operand (linksseitig von expression) für die Binäroperation </param>
        /// <param name="outPosition">Die Menge an Zeichen die im Ausdruck abgearbeitet worden und die Stelle an der die übergeordnete Auswertung weiterarbeiten muss</param>
        /// <returns> Eine gültige Binäroperation </returns>
        protected CodeBinaryOp parseCodeBinaryOp(string expression, CodeElement parent, CodeElement firstOperand, out int outPosition)
        {
            // Auswerten des Operanden
            string op = "";
            int trimmedOffsetSum = 0;
            int outIndex = 0;
            int offset = 0;

            // Suche Operator mit Maximallänge 3
            int maxSearchLength = Math.Min(3, expression.Length);
            op = expression.Substring(0, maxSearchLength).ToLower();
            for ( int i = maxSearchLength; i>-1; i--)
            {
                if (i == 0) throw new ArgumentException("Unknown binary operator '" + op + "'");
                if (CodeBinaryOp.allTypes.Contains(op.Substring(0, i)))
                {
                    op = op.Substring(0, i);
                    break;
                }
            }
            
            // Binäroperationen mit Relation ergeben im bool, sonst object
            CodeBinaryOp operatorElement = new CodeBinaryOp()
            {
                content = expression,
                elementType = (CodeBinaryOp.codeRelationType.Contains(op) ? typeof(bool) : typeof(object)),
                parent = parent,
                operatorType = op
            };

            // Operand im Baum umhängen und an operatorElement anhängen
            firstOperand.parent = operatorElement;
            operatorElement.operand1 = firstOperand;

            // Auswerten des 2. Operanden
            string partialExpression = this.shortenString(expression.Substring(op.Length), ref trimmedOffsetSum);
            offset += op.Length;
            operatorElement.operand2 = this.parse(partialExpression, operatorElement, out outIndex);
            offset += outIndex;
            outPosition = offset + trimmedOffsetSum;
            return operatorElement;
        }

        /// <summary>Versucht den übergebenen Ausdruck als Funktionsknoten auszuwerten</summary>
        /// <param name="expression">Ausdruck der ab der '(' auf des Funktionsaufrufs beginnt </param>
        /// <param name="parent">Parent-Knoten des Function-Calls</param>
        /// <param name="funcName">Funktionsname des Function-Calls</param>
        /// <param name="outPosition">Die Menge an Zeichen die im Ausdruck abgearbeitet worden und die Stelle an der die übergeordnete Auswertung weiterarbeiten muss</param>
        /// <returns>Eine gültiger Funktionsaufrufs-Knoten </returns>
        protected CodeElement parseCodeFunctionCall(string expression, CodeElement parent, string funcName, out int outPosition)
        {
            int outIndex = 0;
            int trimmedOffsetSum = 0;
            CodeFunctionCall funcCallElement = new CodeFunctionCall()
            {
                content = expression,
                elementType = typeof(object),
                functionName = funcName,
                parent = parent
            };
            int offset = 1; // Klammerauf
            string partialExpression = this.shortenString(expression.Substring(1), ref trimmedOffsetSum);

            if (partialExpression[0] != ')')
            {
                funcCallElement.functionParameters.Add(this.parse(partialExpression, funcCallElement, out outIndex));
                offset += outIndex;

                // Parameter prüfen, bis kein ',' mehr kommt
                partialExpression = this.shortenString(partialExpression.Substring(outIndex), ref trimmedOffsetSum);
            }

            Char c = partialExpression[0];
            while( c == ',' )
            {
                // Komma entfernen
                offset++;
                partialExpression = this.shortenString(partialExpression.Substring(1), ref trimmedOffsetSum);

                funcCallElement.functionParameters.Add(this.parse(partialExpression, funcCallElement, out outIndex));
                offset += outIndex;
                partialExpression = this.shortenString(partialExpression.Substring(outIndex).Trim(), ref trimmedOffsetSum);

                c = partialExpression[0];
            }
                        
            // Ende wenn ) - Sonst Fehler
            if (c == ')')
            {
                offset++; // für schließende Klammer
                partialExpression = this.shortenString(partialExpression.Substring(1), ref trimmedOffsetSum); // für weitere Freizeichen

                CodeElement elem = this.parseAdditionalOperands(partialExpression, parent, funcCallElement, 0, ref outIndex);
                offset += outIndex + trimmedOffsetSum;
                outPosition = offset;
                return elem;
            }
            else
            {
                throw new Exception("Expected ) to end function, got " + c.ToString() + " - Expression is " + expression);
            }
        }

        /// <summary>Rekursionsaufruf für Operanden-Parsing.
        /// Ausgehend von einem Ausdruck, etwa CodeReferenz, Literal, Klammerausdruck usw, wird geprüft ob ein weiterer Operand folgt und ggf. rekursiv dieser Operand weiter geparst</summary>
        /// <param name="expression">Restausdruck eines Operator-Ausdrucks</param>
        /// <param name="parent">parent-Element des Parse-Baums an dem dieser Teilausdruck zugehörig ist</param>
        /// <param name="baseElement"> Der erste Operand/Funktionsaufrufparameter, Klammerausdruck, Literal was auch immer, welcher geparst wurde und für den nun geprüft werden soll ob ein Operand folgt</param>
        /// <param name="offset">Position ab der geparst werden soll</param>
        /// <param name="outPosition">Position an der der Parsevorgang fertig war</param>
        /// <returns></returns>
        protected CodeElement parseAdditionalOperands(string expression, CodeElement parent, CodeElement baseElement, int offset, ref int outPosition)
        {
            int trimmedOffsetSum = 0;
            int outIndex = 0;
            if (offset == expression.Length)
            {
                outPosition = offset;
                return baseElement;
            }
            string partialExpression = this.shortenString(expression.Substring(offset), ref trimmedOffsetSum);

            char c = partialExpression[0];
            char c2 = ' ';
            if (partialExpression.Length > 1) c2 = partialExpression[1];
            if (CodeBinaryOp.allTypes.Contains(c.ToString()) || CodeBinaryOp.allTypes.Contains(c.ToString() + c2.ToString())) // Operator und Irgendein Element, etwa Function, Literal etc.
            {
                CodeBinaryOp binaryOp = this.parseCodeBinaryOp(partialExpression, parent, baseElement, out outIndex);
                outPosition = offset + outIndex + trimmedOffsetSum;
                return binaryOp;
            }
            else // Literal Ende
            {
                outPosition = offset + trimmedOffsetSum;
                return baseElement;
            }
        }

        /// <summary> Erstellt ein Keyword-Element (z.B. true/false Literal) oder eine Variablenreferenz mit dem angegebenen Namen und hängt sie an das parent-Element </summary>
        /// <param name="word">Key-Word/Referenz die zugefügt werden soll</param>
        /// <param name="parent">Parent-Ausdruck der die Referenz enthalten soll</param>
        /// <returns>Das erstellte Element</returns>
        protected CodeElement createKeyWordOrReference(string word, CodeElement parent)
        {
            if (CodeBooleanLiteral.boolType.Contains(word.ToLower()))
            {
                return new CodeBooleanLiteral()
                {
                    content = word,
                    elementType = typeof(bool),
                    parent = parent
                };
            }
            else if( CodeNullLiteral.nullType.Contains(word.ToLower()))
            {
                return new CodeNullLiteral()
                {
                    content = word,
                    elementType = typeof(object),
                    parent = parent
                };
            }
            else
            {
                return new CodeReference()
                {
                    content = word,
                    elementType = typeof(object),
                    parent = parent
                };
            }
        }

        /// <summary>Liest einen Text_Identifier (beginnt mit Text und kann dann _, Buchstaben und Zahlen beinhalten</summary>
        /// <param name="expression">Der Asudruck aus dem der Wert gelesen werden soll</param>
        /// <param name="startPos">Startposition an der der Text-Ausdruck gesucht/ermittelt wird </param>
        /// <param name="returnpos">Die Position an der die Auswertung endete</param>
        /// <returns>Der ausgelesene Text-Identifier</returns>
        protected string readText(string expression, int startPos, out int returnpos)
        {
            StringBuilder result = new StringBuilder();
            int len = expression.Length;
            for (int i = startPos; i<len; i++)
            {
                char c = expression[i];
                if (Char.IsLetter(c) || c == '_' || Char.IsDigit(c) || additionalIdentifierChars.Contains(c)) // Kennung
                {
                    result.Append(c);
                }
                else
                {
                    returnpos = i;
                    return result.ToString();
                }
            }
            returnpos = len;
            return result.ToString();
        }

        /// <summary>Liest einen Ausdruck bis zum Auftreten eines definierten Zeichens ein und gibt das Teilergebnis zurück und den gefundenen Index</summary>
        /// <param name="expression">Ausdruck der durchsucht wird</param>
        /// <param name="startPos">Startposition der Suche</param>
        /// <param name="delmiter">Gesuchtes Zeichen</param>
        /// <param name="returnpos">Position die gefunden wurde</param>
        /// <returns>Gefundener Teilausdruck</returns>
        protected string readUntilDelmiter(string expression, int startPos, char delmiter, out int returnpos)
        {
            StringBuilder result = new StringBuilder();
            int len = expression.Length;
            for (int i = startPos; i < len; i++)
            {
                char c = expression[i];
                if (c != delmiter) // Kennung
                {
                    result.Append(c);
                }
                else
                {
                    returnpos = i;
                    return result.ToString();
                }
            }
            throw new Exception("Unterminated String literal " + expression);
        }

        /// <summary>Liest einen Ausdruck bis zum Auftreten eines definierten Zeichens ein, wobei Maskierungen mit \ erlaubt sind, und gibt das Teilergebnis zurück und den gefundenen Index
        /// Maskierungen mit \ sind erlaubt </summary>
        /// <param name="expression">Ausdruck der durchsucht wird</param>
        /// <param name="startPos">Startposition der Suche</param>
        /// <param name="delmiter">Gesuchtes Zeichen</param>
        /// <param name="returnpos">Position die gefunden wurde</param>
        /// <returns>Gefundener Teilausdruck</returns>
        protected string readUntilDelmiterWithEscaping(string expression, int startPos, char delmiter, out int returnpos)
        {
            StringBuilder result = new StringBuilder();
            int len = expression.Length;
            for (int i = startPos; i < len; i++)
            {
                char c = expression[i];
                if ( c == '\\')
                {
                    if (!(i + 1 < len)) throw new ArgumentException($"Unterminated String literal in expresion {expression}");
                    char c2 = expression[i + 1];
                    i++;

                    if (c2 == delmiter || c2 == '\\')
                    {
                        result.Append(c2);
                    }
                    else
                    {
                        switch(c2){
                            case 'n': result.Append("\n");
                                break;
                            case 't': result.Append("\t");
                                break;
                            case 'r': result.Append("\r");
                                break;
                            default:
                                throw new ArgumentException($"Unknown escape sequence {c2} in expression {expression}");
                        }
                    }
                }
                else if (c != delmiter) // Kennung
                {
                    result.Append(c);
                }
                else
                {
                    returnpos = i;
                    return result.ToString();
                }
            }
            throw new Exception($"Unterminated String literal " + expression);
        }

        /// <summary>Liest eine Zahl (beginnt mit Zahl und kann dann einmalig . beinhalten) </summary>
        /// <param name="expression">Der Asudruck aus dem der Wert gelesen werden soll</param>
        /// <param name="startPos">Startposition an der der Zahl-Ausdruck gesucht/ermittelt wird </param>
        /// <param name="returnpos">Die Position an der die Auswertung endete</param>
        /// <returns>Der ausgelesene Zahlenwert</returns>
        protected string readNumber(string expression, int startPos, out int returnpos)
        {
            StringBuilder result = new StringBuilder();
            int len = expression.Length;
            bool commaFound = false;

            for (int i = startPos; i < len; i++)
            {
                char c = expression[i];
                if (Char.IsDigit(c)) // Zahl
                {
                    result.Append(c);
                }
                else if (c == '.'){
                    if (commaFound)
                       throw new Exception("Comma already found in number " + expression);
                    else
                    {
                        result.Append(c);
                        commaFound = true;
                    }
                }
                else 
                {
                    returnpos = i;
                    return result.ToString();
                }
            }
            returnpos = len;
            return result.ToString();
        }

        /// <summary> Verkürzt einen Text von führenden Freizeichen und gibt als Rückgabe den verkürzten Text sowie die Anzahl der gekürzten Zeichen zurück </summary>
        /// <param name="expression">Der Ausdruck dem führende Freizeichen entfernt werden</param>
        /// <param name="outPos">Die Menge der entfernten Freizeichen</param>
        /// <returns>Der gekürzte Ausdruck</returns>
        private string shortenString(string expression, ref int outPos)
        {
            string newExp = expression.TrimStart();
            outPos += expression.Length - newExp.Length;
            return newExp;
        }

        /// <summary>Evaluiert einen Parse-Tree eines Ausdrucks </summary>
        /// <typeparam name="T">Der Rückgabetyp der erwartet wird</typeparam>
        /// <param name="elem">Der Ausdrucksbaum der evaluiert werden soll</param>
        /// <returns>Der evaluierte Wert</returns>
        public T evaluate<T>(CodeElement elem)
        {
            return (T)elem.evaluate();
        }

        /// <summary>Evaluiert einen Parse-Tree eines Ausdrucks </summary>
        /// <typeparam name="T">Der Rückgabetyp der erwartet wird</typeparam>
        /// <param name="elem">Der Ausdrucksbaum der evaluiert werden soll</param>
        /// <param name="dslDefinition"> Die DSL-Definition die zur Evaluierung herangezogen werden soll </param>
        /// <returns> Der evaluierte Wert </returns>
        public T evaluate<T>(CodeElement elem, DSLDef dslDefinition)
        {
            return (T)elem.evaluate(dslDefinition);
        }
    }
}
