using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using msa.Data.Transfer.SQL;
using msa.DSL.CodeParser;
using msa.Logging;

namespace UnitTest
{
    [TestClass]
    public class SqlParserTest
    {

        /// <summary> Initialisiert die Logger für die Tests </summary>
		[TestInitialize]
        public void init()
        {
            Logger logger = Logger.getLogger("msa.Data.Transfer.SqlParser");
            logger.autoFlush = true;
            logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(System.Console.Out));
            logger.trace.Switch.Level = System.Diagnostics.SourceLevels.Verbose;
        }

        [TestMethod]
        public void TestProgrammaticCreateAndConvertBack()
        {
            SqlParseTree parseTree = new SqlParseTree();
            CodeEvaluator eval = new CodeEvaluator();
            parseTree.columns.AddRange(new SqlSelectExpression[] {
                new SqlSelectExpression( eval.parse("col1"), "col1"),
                new SqlSelectExpression( eval.parse("7"), "col2"),
                new SqlSelectExpression( eval.parse("'test'"), "col3"),
                new SqlSelectExpression( eval.parse("col1 + 3"), "col4")
            });

            parseTree.tables.Add("MyTab", new SqlTableExpression() { alias="MyTab", expression = "Table1" });

            parseTree.conditions = "col1 > 5";

            Console.WriteLine(parseTree);

        }

        [TestMethod]
        public void TestSimpleSQL()
        {
            SqlParseTree parseTree = new SqlParseTree();
            parseTree.parse("SELECT a,b,c FROM test");
            Console.WriteLine(parseTree);
        }

        [TestMethod]
        public void TestSQLWithAs()
        {
            SqlParseTree parseTree = new SqlParseTree();
            parseTree.parse("SELECT a as col1,b as col2,c FROM test as t");
            Console.WriteLine(parseTree);
        }

        [TestMethod]
        public void TestSQLWithMultipleTablesWithoutJoin()
        {
            SqlParseTree parseTree = new SqlParseTree();
            parseTree.parse("SELECT a as col1,b as col2,c FROM test as t, tab2 as t2");
            Console.WriteLine(parseTree);
        }

        [TestMethod]
        public void TestSQLWithMultipleTablesWithJoin()
        {
            SqlParseTree parseTree = new SqlParseTree();
            parseTree.parse("SELECT t.a as col1, t.b as col2, t.c FROM test as t, tab2 as t2 inner join tab3 as t3 on t2.a = t3.b inner join tab4 as t4 on t3.c = t4.d", true, true, new char[] { '.' });
            Console.WriteLine(parseTree);
        }

        [TestMethod]
        public void TestSqlWithDotNotation()
        {
            SqlParseTree parseTree = new SqlParseTree();
            parseTree.parse("SELECT t.a, t.b FROM test as t", true, true, new char[] { '.' });
            Console.WriteLine(parseTree);
        }

        [TestMethod]
        public void TestSQLWithMultipleTablesWithoutJoinAndWhere()
        {
            SqlParseTree parseTree = new SqlParseTree();
            parseTree.parse("SELECT a as col1,b as col2,c FROM test as t, tab2 as t2 where a = 4");
            Console.WriteLine(parseTree);
        }

        [TestMethod]
        public void TestCalculatedColumn()
        {
            SqlParseTree parseTree = new SqlParseTree();
            parseTree.parse("SELECT a + ' ' + b FROM test");
            Console.WriteLine(parseTree);
        }
    }
}
