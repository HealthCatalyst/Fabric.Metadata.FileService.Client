using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fabric.Metadata.FileService.Client;

namespace fabric.metadata.fileserver.client.console
{
    class Program
    {
        static void Main(string[] args)
        {
            (new UploadRunner().RunAsync()).GetAwaiter().GetResult();

            Console.WriteLine("Done. Waiting for key to exit...");
            Console.ReadKey();
        }

    }
}
