using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using msa.DSL;
using msa.DSL.CodeParser;

namespace UnitTest
{
    class MyDSLValueProvider : DSLValueProvider
    {
        public override object getValue(string refName)
        {
            switch (refName.ToLower())
            {
                case "x": return 3;
                case "y": return 6; 
                default: throw new Exception("Unknown Identifier " + refName);
            }
        }
    }

    class MyFunctionHandler : DSLFunctionHandler
    {
        public MyFunctionHandler()
        {
            this.addConversions();
            this.addMathFunctions();
            this.addStringFunctions();
            this.addLogic();
            this.addDateFunctions();
            
            functionHandler.Add("nothing", (args) => args[0]);
        }
    }

    [TestClass]
    public class UnitTest_DSL
    {
        public DSLDef myDef;
        CodeEvaluator codeEval;

        [TestInitialize]
        public void init()
        {
            myDef = new DSLDef(new MyDSLValueProvider(), new MyFunctionHandler());
            codeEval = new CodeEvaluator();
        }

        [TestMethod]
        public void testExpressionWithDot()
        {
            string expression = "a.x + b";
            CodeEvaluator test = new CodeEvaluator();
            test.additionalIdentifierChars.Add('.');

            CodeElement parsedExp = test.parse(expression);
            Console.WriteLine(test.parsedExpression);
        }

        [TestMethod]
        public void testExpression()
        {
            string expression = "(rnd(sin(3 - 5 + 7), 2) + 2)";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine(parsedExp.ToString() + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));
        }

        [TestMethod]
        public void testRelationExpression()
        {
            string expression = "(rnd(sin(3 - 5 + 7), 2) + 2) < 2";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(rnd(sin(3 - 5 + 7), 2) + 2) >= 2";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "3 != 4";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));
        }

        [TestMethod]
        public void testIdentifierSimple()
        {
            string expression = "x";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));
        }

        [TestMethod]
        public void testIdentifierComplex()
        {
            string expression = "(rnd( sin( 3 - x + y ), 2 ) + 2) < 2";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));
        }

        [TestMethod]
        public void testGetNodeType()
        {
            string expression = "(rnd(sin(3 - 5 + 7), 2) + 2) < 2";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine("Found Literals: " + parsedExp.childElementsOf<CodeLiteral>().Count );
        }

        [TestMethod]
        public void testStringLiteral()
        {
            string expression = "( x + y )";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = " x + ' + ' + y + ' = ' + cstr(x+y)";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));

            expression = "'test backslash \\\\ und newline \\n und die Sonderzeichen \\'inline\\''";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));
        }

        [TestMethod]
        public void testNumeric()
        {
            string expression = "14.123";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));
        }

        [TestMethod]
        public void testPartialParse()
        {
            string expression = "(3+4) , sin(15), rnd(24.123, 2)";
            CodeElement parsedExp = codeEval.parse(expression, true);
            Console.WriteLine("Parsed " + codeEval.parsedExpression);
            Console.WriteLine("Remains " + codeEval.unparsedExpression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));
            Console.WriteLine("");

            expression = codeEval.unparsedExpression.Substring(1); // Rest nehmen und , entfernen
            parsedExp = codeEval.parse(expression, true);
            Console.WriteLine("Parsed " + codeEval.parsedExpression);
            Console.WriteLine("Remains " + codeEval.unparsedExpression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));
            Console.WriteLine("");

            expression = codeEval.unparsedExpression.Substring(1); // Rest nehmen und , entfernen
            parsedExp = codeEval.parse(expression, true);
            Console.WriteLine("Parsed " + codeEval.parsedExpression);
            Console.WriteLine("Remains " + codeEval.unparsedExpression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));
            Console.WriteLine("");
        }

        [TestMethod]
        public void testLogic()
        {
            string expression = "(3 == 3) && (4 == 4) ";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(3 == 3) && (4 == 3) ";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(3 == 3) || (4 == 3) ";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(3 == 3) and (4 == 4) ";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(3 == 3) and (4 == 3) ";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(3 == 3) or (4 == 3) ";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(3 != 4) != ( 2==3 )";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));
        }

        [TestMethod]
        public void testBooleanLiteral()
        {
            string expression = "(3>4) = true";
            CodeElement parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(3>4) = false";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "true = false";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "(true = false) or (true = true)";
            parsedExp = codeEval.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));
        }

        [TestMethod]
        public void testFunctionLibString()
        {
            CodeEvaluator test = new CodeEvaluator();

            string expression = "left('test 123 hallo', '123')";
            CodeElement parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));

            expression = "right('test 123 hallo', '123')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));

            expression = "mid('test 123 hallo', ' ', ' ')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));

            expression = "upper('test 123 HALLO')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));

            expression = "lower('test 123 HALLO')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));

            expression = "replace('test 123 HALLO', '123', 'neu')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));

            expression = "contains('test 123 HALLO', '123')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "indexOf('test 123 HALLO', '123')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));

            expression = "substring('test 123 HALLO', 3)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<string>(parsedExp, myDef));

            expression = "startsWith('test 123 HALLO', 'test')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));

            expression = "endsWith('test 123 HALLO', 'test')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<bool>(parsedExp, myDef));
        }

        [TestMethod]
        public void testFunctionLibDate()
        {
            CodeEvaluator test = new CodeEvaluator();

            string expression = "Date(2018, 10, 3)";
            CodeElement parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<DateTime>(parsedExp, myDef));

            expression = "Date(2018, 10, 3, 14, 24, 48)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<DateTime>(parsedExp, myDef));

            expression = "Second(Date(2018, 10, 3, 14, 24, 48))";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));

            expression = "Minute(Date(2018, 10, 3, 14, 24, 48))";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));

            expression = "Hour(Date(2018, 10, 3, 14, 24, 48))";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));

            expression = "Day(Date(2018, 10, 3, 14, 24, 48))";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));

            expression = "Month(Date(2018, 10, 3, 14, 24, 48))";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));

            expression = "Year(Date(2018, 10, 3, 14, 24, 48))";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));

            expression = "Addseconds( Date(2018, 10, 3), 10)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<DateTime>(parsedExp, myDef));

            expression = "AddMinutes( Date(2018, 10, 3), 10)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<DateTime>(parsedExp, myDef));

            expression = "AddHours( Date(2018, 10, 3), 10)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<DateTime>(parsedExp, myDef));

            expression = "AddDays( Date(2018, 10, 3), 10)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<DateTime>(parsedExp, myDef));

            expression = "AddMonths( Date(2018, 10, 3), 10)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<DateTime>(parsedExp, myDef));

            expression = "AddYears( Date(2018, 10, 3), 10)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<DateTime>(parsedExp, myDef));
        }

        [TestMethod]
        public void testFunctionLibLogic()
        {
            CodeEvaluator test = new CodeEvaluator();

            string expression = "if( 1==2, 1, 1==3, 2, 3)";
            CodeElement parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "if( 1==2, 1, 1==1, 2, 3)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "if( 1==1, 1, 1==3, 2, 3)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "nvl(null, 'test')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<object>(parsedExp, myDef));

            expression = "nvl('start', 'test')";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<object>(parsedExp, myDef));
        }

        [TestMethod]
        public void testFunctionLibMath()
        {
            CodeEvaluator test = new CodeEvaluator();

            string expression = "abs(-3)";
            CodeElement parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "max(3,5)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "min(3,5)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "sin(3)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "cos(3)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "tan(3)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "pi()";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "ceiling(3.5)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "floor(3.5)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "round(3.5123)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));

            expression = "round(3.5123 , 2)";
            parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<double>(parsedExp, myDef));
        }

        [TestMethod]
        public void testPlay()
        {
            CodeEvaluator test = new CodeEvaluator();
            test.additionalIdentifierChars.Add('.');

            string expression = "t1.KEY = t2.KEY";
            CodeElement parsedExp = test.parse(expression);
            Console.WriteLine(expression + " evaluates to " + codeEval.evaluate<int>(parsedExp, myDef));
        }
    }
}
