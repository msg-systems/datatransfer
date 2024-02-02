using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using msa.Data.Transfer.SQL;
using msa.DSL.CodeParser;
using msa.Logging;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;

namespace UnitTest
{
    [TestClass]
    public class MiscTest
    {

        /// <summary> Initialisiert die Logger für die Tests </summary>
		[TestInitialize]
        public void init()
        {
            
        }

        [TestMethod]
        public void testJsonParsing()
        {
            JArray parsedContent = JArray.Parse( System.IO.File.ReadAllText(@"..\..\..\..\TestData\json1.json") );

            JObject jObj = (JObject)parsedContent[0];
            IEnumerable<JProperty> props = jObj.Properties();
            JToken t = jObj["test"];
            JToken t2 = jObj["number"];
            JToken t3 = jObj["date"];

            Console.WriteLine();
        }

    }
}
