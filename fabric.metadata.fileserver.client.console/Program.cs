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
            try
            {
                (new UploadRunner().RunAsync()).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine("Done. Waiting for key to exit...");
            Console.ReadKey();
        }

    }
}
