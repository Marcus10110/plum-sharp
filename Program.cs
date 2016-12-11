using System;

namespace Saleae
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            Plum plum = new Plum();

            plum.ScanForDevices("", "");
        }
    }
}
