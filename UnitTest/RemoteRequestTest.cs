using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using msa.Data.Transfer.Model;
using System.Threading.Tasks;

namespace UnitTest
{
    [TestClass]
    public class RemoteRequestTest
    {
        [TestMethod]
        public async Task testRemoteLocation()
        {
            RemoteRequest request = new RemoteRequest("http://www.google.de");
            Console.WriteLine(request + "\n");

            request = new RemoteRequest("http-METHOD=GET:::http://www.google.de");
            Console.WriteLine(request + "\n");

            request = new RemoteRequest("http-METHOD=GET:::http-Header=abc:123:::http-header=def:456:::http://www.google.de");
            Console.WriteLine(request + "\n");
            Console.WriteLine(await request.resolveRequest());

            request = new RemoteRequest(@"file-server=msafs1i3v:::file://Projects2:\blablub\Komponenten-Kosten.csv");
            Console.WriteLine(request + "\n");
            Console.WriteLine(await request.resolveRequest());

        }
    }
}
